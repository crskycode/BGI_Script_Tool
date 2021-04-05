using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BGI_Script_Tool
{
    class Script
    {
        string _version;
        byte[] _sectionImport;
        byte[] _sectionCode;
        byte[] _sectionString;

        public Script()
        {
        }

        public void Load(string filePath)
        {
            using (Stream stream = File.OpenRead(filePath))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                Parse(reader);
            }
        }

        void Parse(BinaryReader reader)
        {
            // Read version
            string version = reader.ReadNullTerminatedString(Encoding.ASCII);
            if (version != "BurikoCompiledScriptVer1.00")
                throw new Exception("Unsupported script version.");
            _version = version;

            // Copy import section
            int importSize = reader.ReadInt32();
            importSize -= sizeof(int);
            _sectionImport = reader.ReadBytes(importSize);

            // Copy remaining sections
            int remainingBytes = Convert.ToInt32(reader.BaseStream.Length - reader.BaseStream.Position);
            byte[] block = reader.ReadBytes(remainingBytes);

            // Guess code section range
            int codeSize = GuessCodeSize(block);
            if (codeSize == -1)
                throw new Exception("Unable to guess the section range.");

            // Copy code section
            _sectionCode = new byte[codeSize];
            Buffer.BlockCopy(block, 0, _sectionCode, 0, _sectionCode.Length);

            // Copy string section
            int stringStart = codeSize;
            int stringSize = block.Length - stringStart;
            _sectionString = new byte[stringSize];
            Buffer.BlockCopy(block, stringStart, _sectionString, 0, _sectionString.Length);
        }

        static int GuessCodeSize(byte[] block)
        {
            byte[] pattern = { 0xF4, 0x00, 0x00, 0x00 };
            var searcher = new Searcher(block);
            var result = searcher.FindAll(pattern);

            if (result.Count == 0)
                return -1;

            return result.Last() + pattern.Length;
        }

        public void Save(string filePath)
        {
            CheckLoaded();

            using (Stream stream = File.Create(filePath))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                // Write version
                writer.Write(Encoding.ASCII.GetBytes(_version));
                writer.Write((byte)0);

                // Write import section
                writer.Write(_sectionImport.Length + sizeof(int));
                writer.Write(_sectionImport);

                // Write code section
                writer.Write(_sectionCode);

                //Write string section
                writer.Write(_sectionString);

                // Finished
                writer.Flush();
            }
        }

        IReadOnlyList<Tuple<int, string>> FindAllStrings()
        {
            var pattern = new byte[] { 0x03, 0x00, 0x00, 0x00 };
            var searcher = new Searcher(_sectionCode);
            var result = searcher.FindAll(pattern);

            var strings = new List<Tuple<int, string>>();

            var encoding = Encoding.GetEncoding("shift_jis");

            foreach (var offset in result)
            {
                // 03 00 00 00 ?? ?? ?? ??

                // Ensure 8 bytes
                if (offset + 8 > _sectionCode.Length)
                    continue;

                // Get string offset
                int temp = BitConverter.ToInt32(_sectionCode, offset + 4);
                temp -= _sectionCode.Length;

                // Check placement
                if (temp < 0 || temp >= _sectionString.Length)
                    continue;

                // strlen
                int temp2 = temp;
                while (_sectionString[temp2] != 0)
                    temp2++;
                int length = temp2 - temp;

                // Copy string
                var @string = encoding.GetString(_sectionString, temp, length);

                // Store
                strings.Add(new Tuple<int, string>(offset + 4, @string));
            }

            return strings;
        }

        public void ExportStrings(string filePath, bool exportAll)
        {
            CheckLoaded();

            var strings = FindAllStrings();

            using (StreamWriter writer = File.CreateText(filePath))
            {
                foreach (var item in strings)
                {
                    if (!exportAll)
                    {
                        if (item.Item2.Length <= 0 || item.Item2[0] <= 0x80)
                            continue;
                    }

                    writer.WriteLine($"◇{item.Item1:X8}◇{item.Item2}");
                    writer.WriteLine($"◆{item.Item1:X8}◆{item.Item2}");
                    writer.WriteLine(string.Empty);
                }

                writer.Flush();
            }
        }

        public void ImportStrings(string filePath)
        {
            CheckLoaded();

            var translated = new List<Tuple<int, string>>();

            // Read translation file
            using (StreamReader reader = File.OpenText(filePath))
            {
                int lineNo = 0;

                while (!reader.EndOfStream)
                {
                    int ln = lineNo;
                    var line = reader.ReadLine();
                    lineNo++;

                    if (line.Length == 0 || line[0] != '◆')
                        continue;

                    translated.Add(new Tuple<int, string>(ln, line));
                }
            }

            // Convert to dictionary for fast match
            var strings = FindAllStrings().ToDictionary(a => a.Item1, b => b.Item2);

            // Import translated string
            for (int i = 0; i < translated.Count; i++)
            {
                // Parse line
                string line = translated[i].Item2;
                var m = Regex.Match(line, @"◆(\w+)◆(.+$)");

                // Check match result
                if (!m.Success || m.Groups.Count != 3)
                    throw new Exception($"Bad format at line: {translated[i].Item1}");

                // in code section
                int offset = int.Parse(m.Groups[1].Value, NumberStyles.HexNumber);
                // translated string
                var @string = m.Groups[2].Value;

                // Check offset valid
                if (!strings.ContainsKey(offset))
                    throw new Exception($"The offset {offset:X8} is not contained in the script.");

                // Update string
                strings[offset] = @string;
            }

            // Build string section

            var stringMap = new Dictionary<string, int>();
            var encoding = Encoding.GetEncoding("utf-8");
            int capacity = 4 * 1024 * 1024;

            using (MemoryStream stream = new MemoryStream(capacity))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                foreach (var item in strings)
                {
                    if (stringMap.ContainsKey(item.Value))
                        continue;

                    int offset = Convert.ToInt32(stream.Position);
                    var bytes = encoding.GetBytes(item.Value);
                    writer.Write(bytes);
                    writer.Write((byte)0);

                    stringMap.Add(item.Value, offset);
                }

                writer.Flush();
                _sectionString = stream.ToArray();
            }

            // Update code section

            foreach (var item in strings)
            {
                // Find string offset
                int offset = stringMap[item.Value];
                offset += _sectionCode.Length;

                // Update code
                var bytes = BitConverter.GetBytes(offset);
                Buffer.BlockCopy(bytes, 0, _sectionCode, item.Key, bytes.Length);
            }
        }

        void CheckLoaded()
        {
            if (_version == null)
                throw new Exception("The script has not been loaded yet.");
        }
    }

    class Searcher
    {
        byte[] _buffer;
        byte[] _pattern;
        int _current;

        public Searcher(byte[] buffer)
        {
            _buffer = buffer;
            _current = -1;
        }

        public int FindFirst(byte[] pattern)
        {
            _pattern = pattern;
            var span = _buffer.AsSpan();
            _current = span.IndexOf(pattern);
            return _current;
        }

        public int FindNext()
        {
            if (_pattern == null)
                throw new Exception();
            if (_current == -1)
                return -1;
            int start = _current + _pattern.Length;
            var span = _buffer.AsSpan(start);
            int offset = span.IndexOf(_pattern);
            _current = (offset != -1) ? (start + offset) : -1;
            return _current;
        }

        public IReadOnlyList<int> FindAll(byte[] pattern)
        {
            var list = new List<int>();
            int offset = FindFirst(pattern);
            while (offset != -1)
            {
                list.Add(offset);
                offset = FindNext();
            }
            return list;
        }
    }
}

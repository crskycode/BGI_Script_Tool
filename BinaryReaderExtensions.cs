using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BGI_Script_Tool
{
    static class BinaryReaderExtensions
    {
        public static string ReadNullTerminatedString(this BinaryReader @this, Encoding encoding)
        {
            List<byte> buffer = new List<byte>();

            for (byte value = @this.ReadByte(); value != 0; value = @this.ReadByte())
            {
                buffer.Add(value);
            }

            if (buffer.Count == 0)
            {
                return string.Empty;
            }

            return encoding.GetString(buffer.ToArray());
        }
    }
}

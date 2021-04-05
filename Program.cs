using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BGI_Script_Tool
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Usage:");
                Console.WriteLine("  Export text     : ScriptTool -e [file|folder]");
                Console.WriteLine("  Export all text : ScriptTool -a [file|folder]");
                Console.WriteLine("  Rebuild script  : ScriptTool -b [file|folder]");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return;
            }

            string mode = args[0];
            string path = args[1];

            switch (mode)
            {
                case "-e":
                case "-a":
                {
                    bool exportAll = (mode == "-a");

                    void ExportString(string filePath)
                    {
                        Console.WriteLine($"Exporting text from {Path.GetFileName(filePath)}");

                        try
                        {
                            Script script = new Script();
                            script.Load(filePath);
                            script.ExportStrings(Path.ChangeExtension(filePath, "txt"), exportAll);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }



                    if (Util.PathIsFolder(path))
                    {
                        foreach (var item in Directory.EnumerateFiles(path, "*."))
                        {
                            ExportString(item);
                        }
                    }
                    else
                    {
                        ExportString(path);
                    }

                    break;
                }
                case "-b":
                {
                    void RebuildScript(string filePath)
                    {
                        Console.WriteLine($"Rebuilding script {Path.GetFileName(filePath)}");

                        try
                        {
                            string textFilePath = Path.ChangeExtension(filePath, "txt");
                            string newFilePath = Path.GetDirectoryName(filePath) + @"\rebuild\" + Path.GetFileName(filePath);
                            Directory.CreateDirectory(Path.GetDirectoryName(newFilePath));
                            Script script = new Script();
                            script.Load(filePath);
                            script.ImportStrings(textFilePath);
                            script.Save(newFilePath);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }

                    if (Util.PathIsFolder(path))
                    {
                        foreach (var item in Directory.EnumerateFiles(path, "*."))
                        {
                            RebuildScript(item);
                        }
                    }
                    else
                    {
                        RebuildScript(path);
                    }

                    break;
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BGI_Script_Tool
{
    static class Util
    {
        public static bool PathIsFolder(string path)
        {
            return new FileInfo(path).Attributes.HasFlag(FileAttributes.Directory);
        }
    }
}

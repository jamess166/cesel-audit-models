using System.Collections.Generic;
using System.IO;

namespace BimManagement
{
    /// <summary>
    /// Parámetros compartidos entre la vista y el handler de exportación de datos CP.
    /// </summary>
    public static class ExportDataCPTools
    {
        public static bool           UseCurrentModel { get; set; }
        public static string         FolderPath      { get; set; }
        public static List<FileInfo> Files           { get; set; }
        public static bool           CombineIntoSingleFile { get; set; }
    }
}

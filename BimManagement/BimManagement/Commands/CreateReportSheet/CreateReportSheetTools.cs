using System.Collections.Generic;
using System.IO;

namespace BimManagement
{
    /// <summary>
    /// Parámetros compartidos entre la vista y el handler de creación de planos.
    /// </summary>
    public static class CreateReportSheetTools
    {
        public static string          Week              { get; set; }
        public static string          Month             { get; set; }
        public static string          Period            { get; set; }
        public static bool            IsMonthly         { get; set; }
        public static string          FechaPresentacion { get; set; }
        public static bool            UseCurrentModel   { get; set; }
        public static List<FileInfo>  Files             { get; set; }
    }
}

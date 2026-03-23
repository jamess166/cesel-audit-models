using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;

namespace BimManagement
{
    /// <summary>
    /// Lee el archivo Excel de partidas y devuelve un mapa:
    ///   clave  = código PO-WBS  (columna 1)
    ///   valor  = (Unidad columna 3, Factor columna 4)
    /// </summary>
    public class ReportExcelReader
    {
        public class PartidaData
        {
            public string Unidad { get; set; }
            public double Factor { get; set; }
        }

        /// <summary>
        /// Carga el Excel y devuelve el diccionario. Lanza excepción si el archivo no existe.
        /// </summary>
        public static Dictionary<string, PartidaData> Load(string excelPath)
        {
            if (!File.Exists(excelPath))
                throw new FileNotFoundException(
                    $"No se encontró el archivo de partidas:\n{excelPath}");

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            var result = new Dictionary<string, PartidaData>(StringComparer.OrdinalIgnoreCase);

            using (var package = new ExcelPackage(new FileInfo(excelPath)))
            {
                ExcelWorksheet sheet = package.Workbook.Worksheets[0];

                if (sheet.Dimension == null) return result;

                int lastRow = sheet.Dimension.End.Row;

                // Fila 1 = encabezado, datos desde fila 2
                for (int r = 2; r <= lastRow; r++)
                {
                    string code   = sheet.Cells[r, 1].GetValue<string>()?.Trim();
                    string unidad = sheet.Cells[r, 3].GetValue<string>()?.Trim();
                    double factor = sheet.Cells[r, 4].GetValue<double>();

                    if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(unidad))
                        continue;

                    // Factor 0 o vacío → se trata como 1
                    if (factor == 0) factor = 1.0;

                    result[code] = new PartidaData { Unidad = unidad, Factor = factor };
                }
            }

            return result;
        }
    }
}

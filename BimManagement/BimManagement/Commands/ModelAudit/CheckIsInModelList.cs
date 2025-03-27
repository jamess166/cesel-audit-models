using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BimManagement.Commands.ModelAudit
{
    public class CheckIsInModelList
    {
        public ValidationResult ValidateFileNameInExcel(string fileName)
        {
            Debug.WriteLine("Iniciando validación del nombre en Excel...");

            try
            {
                // Configuración para habilitar el uso de EPPlus (obligatorio en versiones recientes)
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                // Obtener la ruta del archivo de configuración
                string dllPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string dllDirectory = Path.GetDirectoryName(dllPath);
                string excelPath = Path.Combine(dllDirectory, "Resources", "2235055-CPA-XX-XX-XXX-OD-ZZ-ModelosBIM.xlsx");

                Debug.WriteLine($"Buscando archivo en: {excelPath}");

                if (!File.Exists(excelPath))
                {
                    Debug.WriteLine("Archivo Excel no encontrado.");
                    return new ValidationResult
                    {
                        IsValid = false,
                        Message = "El archivo de lista de modelos no fue encontrado.",
                        IsRelevant = false
                    };
                }

                Debug.WriteLine("Abriendo archivo Excel...");
                using (var package = new ExcelPackage(new FileInfo(excelPath)))
                {
                    ExcelWorksheet worksheet = package.Workbook.Worksheets.FirstOrDefault();
                    if (worksheet == null)
                    {
                        Debug.WriteLine("No se encontraron hojas en el archivo Excel.");
                        return new ValidationResult
                        {
                            IsValid = false,
                            Message = "El archivo Excel no contiene hojas válidas.",
                            IsRelevant = false
                        };
                    }

                    Debug.WriteLine("Buscando el nombre en la columna J...");

                    int rowCount = worksheet.Dimension.Rows;
                    bool found = false;

                    for (int row = 1; row <= rowCount; row++)
                    {
                        object cellValue = worksheet.Cells[row, 10].Value; // Columna J (10)
                        if (cellValue != null && cellValue.ToString().Trim().Equals(fileName, StringComparison.OrdinalIgnoreCase))
                        {
                            Debug.WriteLine($"Nombre encontrado en la fila {row}.");
                            found = true;
                            break;
                        }
                    }

                    if (found)
                    {
                        return new ValidationResult
                        {
                            IsValid = true,
                            Message = "",
                            IsRelevant = true
                        };
                    }
                    else
                    {
                        return new ValidationResult
                        {
                            IsValid = false,
                            Message = $"El archivo '{fileName}' NO se encuentra en la lista de modelos BIM.",
                            IsRelevant = true
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error en la validación: {ex.Message}");
                return new ValidationResult
                {
                    IsValid = false,
                    Message = $"Error durante la validación: {ex.Message}",
                    IsRelevant = false
                };
            }
        }

    }
}

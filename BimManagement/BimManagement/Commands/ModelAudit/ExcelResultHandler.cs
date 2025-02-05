using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BimManagement.Commands.ModelAudit
{
    public class ExcelResultHandler
    {
        private const int MAX_LINES = 15;
        private const int MAX_CHARS = 300;

        internal void SetResultInCell(ValidationResult validationResult, ExcelWorksheet worksheet, string cellValid, string cellComment)
        {
            if (validationResult == null || !validationResult.IsRelevant) return;

            // Establecer el resultado SI/NO
            worksheet.Cells[cellValid].Value = validationResult.IsValid ? "SI" : "NO";

            if (string.IsNullOrEmpty(validationResult.Message))
            {
                worksheet.Cells[cellComment].Value = "";
                return;
            }

            // Contar líneas y caracteres
            var lines = validationResult.Message.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            bool isLongMessage = lines.Length > MAX_LINES || validationResult.Message.Length > MAX_CHARS;

            if (!isLongMessage)
            {
                worksheet.Cells[cellComment].Value = validationResult.Message;
                return;
            }

            // Crear nueva hoja para mensaje largo
            string cellReference = new ExcelAddress(cellComment).Start.Address;
            string columnName = GetColumnName(worksheet, cellReference);
            //string sheetName = $"{columnName}{GetRowNumber(cellReference)}";
            string sheetName = $"{columnName}";


            CreateDetailSheet(worksheet.Workbook, sheetName, validationResult.Message);
            worksheet.Cells[cellComment].Value = $"Ver hoja '{sheetName}'";
        }

        private string GetColumnName(ExcelWorksheet worksheet, string cellReference)
        {
            try
            {
                // Obtener el valor de la columna C en la misma fila que la celda de referencia
                int row = new ExcelAddress(cellReference).Start.Row;
                var columnCValue = worksheet.Cells[row, 3].Text; // Columna C es índice 3

                // Limpiar el nombre para usarlo como nombre de hoja
                return CleanSheetName(columnCValue);
            }
            catch
            {
                // Si hay algún error, usar un nombre genérico basado en la fecha y hora
                return $"Detalles_{DateTime.Now:yyyyMMdd_HHmmss}";
            }
        }

        private string CleanSheetName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Detalles";

            // Eliminar caracteres inválidos para nombres de hojas de Excel
            var invalidChars = new[] { '\\', '/', '?', '*', '[', ']', ':', ' ' };
            string cleanName = invalidChars.Aggregate(name, (current, c) => current.Replace(c, '_'));

            // Limitar longitud del nombre
            if (cleanName.Length > 31) // Excel tiene un límite de 31 caracteres para nombres de hojas
            {
                cleanName = cleanName.Substring(0, 28) + "...";
            }

            return cleanName;
        }

        private int GetRowNumber(string cellReference)
        {
            return new ExcelAddress(cellReference).Start.Row;
        }

        private void CreateDetailSheet(ExcelWorkbook workbook, string sheetName, string content)
        {
            // Verificar si ya existe una hoja con ese nombre y modificar si es necesario
            string finalSheetName = sheetName;
            int counter = 1;
            while (workbook.Worksheets.Any(ws => ws.Name.Equals(finalSheetName, StringComparison.OrdinalIgnoreCase)))
            {
                finalSheetName = $"{sheetName}_{counter++}";
            }

            // Crear nueva hoja
            var detailSheet = workbook.Worksheets.Add(finalSheetName);

            // Configurar el formato de la hoja
            detailSheet.Cells["A1"].Value = "No se cumple el criterio de Evaluación";
            detailSheet.Cells["A1"].Style.Font.Bold = true;
            detailSheet.Cells["A1"].Style.Font.Size = 14;

            // Separar el contenido en bloques usando \n\n como delimitador
            var blocks = content.Split(new[] { "\n\n" }, StringSplitOptions.None);

            int row = 3; // Empezamos a escribir en la fila 3 para dejar espacio por encima

            foreach (var block in blocks)
            {
                // Separar el bloque en líneas usando \n
                var lines = block.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                // Si es el primer bloque (el título), lo formateamos
                detailSheet.Cells[row, 1].Value = lines[0]; // Título
                detailSheet.Cells[row, 1].Style.Font.Bold = true;
                detailSheet.Cells[row, 1].Style.Font.Size = 14;
                row++; // Avanzamos una fila después del título

                // Colocamos el contenido restante en celdas separadas
                for (int i = 1; i < lines.Length; i++)
                {
                    detailSheet.Cells[row, 1].Value = lines[i]; // Contenido
                    row++; // Avanzamos a la siguiente fila
                }

                // Añadir una fila en blanco entre bloques
                row++;
            }

            // Ajustar el ancho de la columna
            detailSheet.Column(1).AutoFit();

            // Asegurarse de que el texto tenga salto de línea
            detailSheet.Cells[3, 1, row - 1, 1].Style.WrapText = true;

            // Ajustar la altura de las filas según sea necesario
            for (int i = 3; i < row; i++)
            {
                detailSheet.Row(i).Height = 18; // Ajustar altura (ajústalo según el contenido)
            }

            //// Verificar si ya existe una hoja con ese nombre y modificar si es necesario
            //string finalSheetName = sheetName;
            //int counter = 1;
            //while (workbook.Worksheets.Any(ws => ws.Name.Equals(finalSheetName, StringComparison.OrdinalIgnoreCase)))
            //{
            //    finalSheetName = $"{sheetName}_{counter++}";
            //}

            //// Crear nueva hoja
            //var detailSheet = workbook.Worksheets.Add(finalSheetName);

            //// Configurar el formato de la hoja
            //detailSheet.Cells["A1"].Value = "No se cumple el criterio de Evaluación";
            //detailSheet.Cells["A1"].Style.Font.Bold = true;
            //detailSheet.Cells["A1"].Style.Font.Size = 14;

            //// Separar el contenido en líneas y escribirlas
            //var lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            //for (int i = 0; i < lines.Length; i++)
            //{
            //    detailSheet.Cells[i + 3, 1].Value = lines[i];
            //}

            //// Ajustar el ancho de la columna
            //detailSheet.Column(1).AutoFit();
        }
    }
}

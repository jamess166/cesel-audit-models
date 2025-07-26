using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms;

namespace BimManagement.Commands.ModelAudit
{
    public class CheckGridsWidhArchitecture
    {
        public static ValidationResult CompareGridsLocation()
        {
            try
            {

                // Obtener la ruta del modelo actual
                string currentPath = RevitTools.doc.PathName;
                string currentDirectory = Path.GetDirectoryName(currentPath);
                string currentFileName = Path.GetFileNameWithoutExtension(currentPath);

                string filePath = BuscarModeloArquitectura(currentPath);
                
                if (filePath == null)
                {
                    OpenFileDialog openFileDialog = new OpenFileDialog
                    {
                        Filter = "Archivos de Revit (*.rvt)|*.rvt",
                        Title = "Seleccionar modelo de Arquitectura"
                    };

                    if (openFileDialog.ShowDialog() != DialogResult.OK)
                    {
                        return new ValidationResult
                        {
                            IsValid = false,
                            IsRelevant = false,
                            Message = "Operación cancelada por el usuario."
                        };
                    }

                    filePath = openFileDialog.FileName;
                }

                ModelPath modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(filePath);
                OpenOptions openOpt = new OpenOptions
                {
                    DetachFromCentralOption = DetachFromCentralOption.DetachAndPreserveWorksets
                };

                using (Document archDoc = RevitTools.app.OpenDocumentFile(modelPath, openOpt))
                {
                    // Obtener todos los ejes de ambos documentos
                    var currentGrids = new FilteredElementCollector(RevitTools.doc)
                        .OfClass(typeof(Autodesk.Revit.DB.Grid))
                        .Cast<Autodesk.Revit.DB.Grid>()
                        .ToList();

                    var archGrids = new FilteredElementCollector(archDoc)
                        .OfClass(typeof(Autodesk.Revit.DB.Grid))
                        .Cast<Autodesk.Revit.DB.Grid>()
                        .ToList();

                    // Listas para almacenar resultados
                    var misalignedGrids = new List<string>();
                    var missingInCurrentModel = new List<string>();
                    var missingInArchModel = new List<string>();

                    // Comparar ejes del modelo actual con arquitectura
                    //foreach (var currentGrid in currentGrids)
                    //{
                    //    var matchingGrid = archGrids.FirstOrDefault(g => g.Name == currentGrid.Name);

                    //    if (matchingGrid == null)
                    //    {
                    //        missingInArchModel.Add(currentGrid.Name);
                    //        continue;
                    //    }

                    //    Line currentLine = currentGrid.Curve as Line;
                    //    Line archLine = matchingGrid.Curve as Line;

                    //    if (currentLine == null || archLine == null)
                    //        continue;

                    //    if (!CompareGridLines(currentLine, archLine))
                    //    {
                    //        misalignedGrids.Add(currentGrid.Name);
                    //    }
                    //}

                    // Verificar ejes que existen en arquitectura pero no en el modelo actual
                    foreach (var archGrid in archGrids)
                    {
                        if (!currentGrids.Any(g => g.Name == archGrid.Name))
                        {
                            missingInCurrentModel.Add(archGrid.Name);
                        }
                    }

                    // Preparar mensaje de resultado
                    if (!misalignedGrids.Any() && !missingInCurrentModel.Any() && !missingInArchModel.Any())
                    {
                        return new ValidationResult
                        {
                            IsValid = true,
                            IsRelevant = true,
                            Message = ""
                        };
                    }

                    var message = new System.Text.StringBuilder();

                    //if (misalignedGrids.Any())
                    //{
                    //    message.AppendLine($"Ejes no alineados: {string.Join(", ", misalignedGrids)}");
                    //}

                    if (missingInCurrentModel.Any())
                    {
                        message.AppendLine($"Ejes faltantes: {string.Join(", ", missingInCurrentModel)}");
                    }

                    if (missingInArchModel.Any())
                    {
                        message.AppendLine($"Ejes no están en arquitectura: {string.Join(", ", missingInArchModel)}");
                    }

                    return new ValidationResult
                    {
                        IsValid = false,
                        IsRelevant = true,
                        Message = message.ToString().Trim()
                    };
                }
            }
            catch (Exception ex)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    IsRelevant = false,
                    Message = $"Error al comparar ejes: {ex.Message}"
                };
            }
        }

        private static string BuscarModeloArquitectura(string currentPath)
        {
            string currentDirectory = Path.GetDirectoryName(currentPath);
            string currentFileName = Path.GetFileNameWithoutExtension(currentPath);

            string[] parts = currentFileName.Split('-');
            if (parts.Length < 8)
                return null;

            parts[6] = "ARQ"; // Reemplazar especialidad por ARQ
            string targetFileName = string.Join("-", parts) + ".rvt";

            // 1. Buscar en la misma carpeta
            string directPath = Path.Combine(currentDirectory, targetFileName);
            if (File.Exists(directPath))
                return directPath;

            // 2. Subir un nivel
            string parentDir = Directory.GetParent(currentDirectory)?.FullName;
            if (parentDir == null)
                return null;

            // 3. Buscar UNA carpeta con "ARQUITECTURA" en el nombre
            string arquitecturaDir = Directory.GetDirectories(parentDir)
                .FirstOrDefault(d =>
                    Path.GetFileName(d).IndexOf("ARQUITECTURA", StringComparison.OrdinalIgnoreCase) >= 0);

            if (arquitecturaDir == null)
                return null;

            // 4. Buscar el archivo en esa única carpeta
            string possiblePath = Path.Combine(arquitecturaDir, targetFileName);
            return File.Exists(possiblePath) ? possiblePath : null;
        }


        private static bool CompareGridLines(Line line1, Line line2)
        {
            // Tolerancia para comparaciones
            const double tolerance = 0.001;

            // Comparar direcciones de las líneas
            XYZ direction1 = line1.Direction.Normalize();
            XYZ direction2 = line2.Direction.Normalize();

            // Verificar si las direcciones son paralelas (misma dirección o dirección opuesta)
            bool isParallel = direction1.IsAlmostEqualTo(direction2, tolerance) ||
                              direction1.IsAlmostEqualTo(direction2.Negate(), tolerance);

            if (!isParallel)
                return false;

            // Verificar si las líneas están en el mismo plano
            // Proyectar un punto de la línea 1 sobre la línea 2
            XYZ point1 = line1.GetEndPoint(0);
            XYZ vector = point1 - line2.GetEndPoint(0);
            double distance = vector.DotProduct(direction2.CrossProduct(XYZ.BasisZ));

            // Si la distancia es cercana a cero, las líneas están alineadas
            return Math.Abs(distance) < tolerance;
        }
    }
}

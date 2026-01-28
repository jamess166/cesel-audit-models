using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;

namespace BimManagement
{
    [Transaction(TransactionMode.Manual)]
    public class RepairLinksCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // Obtener la ruta del documento actual
                string docPath = doc.PathName;
                if (string.IsNullOrEmpty(docPath))
                {
                    TaskDialog.Show("Error", "El documento debe estar guardado antes de ejecutar este comando.");
                    return Result.Failed;
                }

                // Obtener la carpeta donde está el documento actual
                string docFolder = Path.GetDirectoryName(docPath);

                // Buscar la carpeta "Modelos" subiendo en la jerarquía
                string modelsFolder = FindModelsFolder(docFolder);

                if (string.IsNullOrEmpty(modelsFolder))
                {
                    TaskDialog.Show("Error", "No se encontró la carpeta 'Modelos' en la jerarquía de carpetas.");
                    return Result.Failed;
                }

                // Entrar en la carpeta "Contratista" dentro de "Modelos"
                string contractorFolder = Path.Combine(modelsFolder, "Contratista");

                if (!Directory.Exists(contractorFolder))
                {
                    TaskDialog.Show("Error", $"No se encontró la carpeta 'Contratista' en: {modelsFolder}");
                    return Result.Failed;
                }

                // Recopilar todos los vínculos rotos
                List<RevitLinkType> brokenLinks = CollectBrokenLinks(doc);

                if (brokenLinks.Count == 0)
                {
                    TaskDialog.Show("Información", "No se encontraron vínculos rotos.");
                    return Result.Succeeded;
                }

                // Reparar vínculos
                RepairResult result = RepairLinks(doc, brokenLinks, contractorFolder);

                // Mostrar resumen
                ShowRepairSummary(result);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Error", ex.Message + "\n\n" + ex.StackTrace);
                return Result.Failed;
            }
        }

        /// <summary>
        /// Busca la carpeta "Modelos" subiendo en la jerarquía de carpetas
        /// </summary>
        private string FindModelsFolder(string startFolder)
        {
            DirectoryInfo currentDir = new DirectoryInfo(startFolder);

            // Subir hasta encontrar la carpeta "Modelos" o llegar a la raíz
            while (currentDir != null)
            {
                // Verificar si la carpeta actual se llama "Modelos"
                if (currentDir.Name.Equals("Modelos", StringComparison.OrdinalIgnoreCase))
                {
                    return currentDir.FullName;
                }

                // Subir a la carpeta padre
                currentDir = currentDir.Parent;
            }

            // No se encontró la carpeta "Modelos"
            return null;
        }

        /// <summary>
        /// Recopila todos los vínculos rotos del documento
        /// </summary>
        private List<RevitLinkType> CollectBrokenLinks(Document doc)
        {
            List<RevitLinkType> brokenLinks = new List<RevitLinkType>();
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            ICollection<Element> linkTypes = collector.OfClass(typeof(RevitLinkType)).ToElements();

            foreach (Element elem in linkTypes)
            {
                RevitLinkType linkType = elem as RevitLinkType;
                if (linkType != null)
                {
                    ExternalFileReference extRef = linkType.GetExternalFileReference();
                    if (extRef != null && extRef.GetLinkedFileStatus() != LinkedFileStatus.Loaded)
                    {
                        brokenLinks.Add(linkType);
                    }
                }
            }

            return brokenLinks;
        }

        /// <summary>
        /// Repara los vínculos rotos buscando los archivos en la carpeta especificada
        /// </summary>
        private RepairResult RepairLinks(Document doc, List<RevitLinkType> brokenLinks, string searchFolder)
        {
            RepairResult result = new RepairResult();
            result.TotalBrokenLinks = brokenLinks.Count;

            // Crear un diccionario con los vínculos rotos y sus nuevas rutas
            Dictionary<RevitLinkType, string> linksToRepair = new Dictionary<RevitLinkType, string>();

            // Primero buscar todos los archivos
            foreach (RevitLinkType linkType in brokenLinks)
            {
                string fileName = linkType.Name;
                string foundFile = SearchFileRecursively(searchFolder, fileName);

                if (!string.IsNullOrEmpty(foundFile))
                {
                    linksToRepair.Add(linkType, foundFile);
                }
                else
                {
                    result.NotFoundCount++;
                    result.Details += $"✗ No encontrado: {fileName}\n\n";
                }
            }

            // Ahora reparar los vínculos FUERA de una transacción
            foreach (var kvp in linksToRepair)
            {
                RevitLinkType linkType = kvp.Key;
                string foundFile = kvp.Value;
                string fileName = linkType.Name;

                try
                {
                    // Crear la ruta del modelo
                    ModelPath newPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(foundFile);

                    // Recargar el vínculo (esto NO debe estar en una transacción)
                    linkType.LoadFrom(newPath, new WorksetConfiguration());

                    result.RepairedCount++;
                    result.Details += $"✓ Reparado: {fileName}\n  Ubicación: {foundFile}\n\n";
                }
                catch (Exception ex)
                {
                    result.Details += $"✗ Error al cargar: {fileName}\n  Razón: {ex.Message}\n\n";
                }
            }

            return result;
        }

        /// <summary>
        /// Busca un archivo de forma recursiva en una carpeta y sus subcarpetas
        /// </summary>
        private string SearchFileRecursively(string rootFolder, string fileName)
        {
            try
            {
                // Primero buscar en la carpeta raíz
                string[] files = Directory.GetFiles(rootFolder, fileName, SearchOption.TopDirectoryOnly);
                if (files.Length > 0)
                {
                    return files[0];
                }

                // Buscar en subcarpetas de forma recursiva
                files = Directory.GetFiles(rootFolder, fileName, SearchOption.AllDirectories);
                if (files.Length > 0)
                {
                    return files[0];
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Ignorar carpetas sin permisos
            }
            catch (Exception)
            {
                // Ignorar otros errores de búsqueda
            }

            return null;
        }

        /// <summary>
        /// Muestra el resumen de la operación de reparación
        /// </summary>
        private void ShowRepairSummary(RepairResult result)
        {
            string summary = $"RESUMEN DE REPARACIÓN\n" +
                            $"═══════════════════════\n" +
                            $"Total vínculos rotos: {result.TotalBrokenLinks}\n" +
                            $"Reparados: {result.RepairedCount}\n" +
                            $"No encontrados: {result.NotFoundCount}\n\n" +
                            $"DETALLES:\n" +
                            $"═══════════════════════\n" +
                            result.Details;

            TaskDialog dialog = new TaskDialog("Resultado");
            dialog.MainContent = summary;
            dialog.Show();
        }
    }

    /// <summary>
    /// Clase que almacena el resultado de la operación de reparación
    /// </summary>
    public class RepairResult
    {
        public int TotalBrokenLinks { get; set; }
        public int RepairedCount { get; set; }
        public int NotFoundCount { get; set; }
        public string Details { get; set; }

        public RepairResult()
        {
            TotalBrokenLinks = 0;
            RepairedCount = 0;
            NotFoundCount = 0;
            Details = "";
        }
    }
}
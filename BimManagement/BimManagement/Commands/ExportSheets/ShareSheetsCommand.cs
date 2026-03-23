using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BimManagement
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ShareSheetsCommand : IExternalCommand
    {
        private const string SheetPrefix = "-CSL-";

        public Result Execute(ExternalCommandData commandData, ref string message,
            ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            Document      doc   = uiApp.ActiveUIDocument.Document;

            try
            {
                // 1. Recopilar planos del modelo activo para mostrar en la vista
                List<SheetItem> sheets = CollectSheets(doc);

                // 2. Determinar carpeta de destino sugerida desde el modelo activo
                string suggestedOutput = ResolveOutputPath(doc.PathName);

                // 3. Mostrar diálogo
                var view = new ShareSheetsView(suggestedOutput, sheets);
                if (view.ShowDialog() != true)
                    return Result.Cancelled;

                string outputDir = view.OutputPath;
                Directory.CreateDirectory(outputDir);

                if (view.UseCurrentModel)
                {
                    // ── Modelo activo ──────────────────────────────────────────
                    var selectedSheets = view.SelectedSheets;
                    List<ElementId> ids = selectedSheets.Select(s => s.Id).ToList();
                    doc.Export(outputDir, ids, BuildPDFOptions());

                    string suffix = GetDocSuffix(doc.PathName);
                    RenameExportedPDFs(outputDir, selectedSheets, suffix);

                    TaskDialog.Show("CSL – Exportar PDF",
                        $"Se exportaron {ids.Count} plano(s) a:\n{outputDir}");
                }
                else
                {
                    // ── Carpeta de modelos ─────────────────────────────────────
                    var files   = view.SelectedFiles;
                    var log     = new StringBuilder();
                    int success = 0;

                    foreach (FileInfo fileInfo in files)
                    {
                        Document fileDoc = null;
                        try
                        {
                            var modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(
                                fileInfo.FullName);
                            fileDoc = uiApp.Application.OpenDocumentFile(
                                modelPath, new OpenOptions { Audit = false });

                            var fileSheets = CollectSheets(fileDoc);

                            if (fileSheets.Count > 0)
                            {
                                Directory.CreateDirectory(outputDir);
                                fileDoc.Export(outputDir, fileSheets.Select(s => s.Id).ToList(),
                                    BuildPDFOptions());

                                string suffix = GetDocSuffix(fileInfo.FullName);
                                RenameExportedPDFs(outputDir, fileSheets, suffix);

                                log.AppendLine($"✔ {fileInfo.Name} → {fileSheets.Count} plano(s)");
                                success++;
                            }
                            else
                            {
                                log.AppendLine($"– {fileInfo.Name}: sin planos CSL");
                            }

                            fileDoc.Close(false);
                        }
                        catch (Exception ex)
                        {
                            if (fileDoc != null)
                                try { fileDoc.Close(false); } catch { }
                            log.AppendLine($"✖ {fileInfo.Name}: {ex.Message}");
                        }
                    }

                    TaskDialog.Show("CSL – Exportar PDF",
                        $"Procesados: {files.Count} archivo(s), {success} con planos exportados.\n\n" +
                        $"Destino: {outputDir}\n\n{log}");
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("CSL – Error", $"Error al exportar:\n\n{ex.Message}");
                return Result.Failed;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static List<SheetItem> CollectSheets(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(s => s.SheetNumber.IndexOf(SheetPrefix,
                                StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(s => s.SheetNumber)
                .Select(s => new SheetItem(s))
                .ToList();
        }

        /// <summary>
        /// Sube 3 niveles desde el directorio del modelo y agrega "ANEXOS".
        /// .../SEMANA 123/CP/MODELOS CP/UT7/model.rvt → .../SEMANA 123/ANEXOS
        /// </summary>
        private static string ResolveOutputPath(string modelPathName)
        {
            if (string.IsNullOrEmpty(modelPathName)) return string.Empty;

            string modelDir = Path.GetDirectoryName(modelPathName);
            string root     = GetAncestor(modelDir, levels: 3);
            return Path.Combine(root, "ANEXOS");
        }

        private static string GetAncestor(string path, int levels)
        {
            string current = path;
            for (int i = 0; i < levels; i++)
            {
                string parent = Path.GetDirectoryName(current);
                if (string.IsNullOrEmpty(parent)) break;
                current = parent;
            }
            return current;
        }

        /// <summary>
        /// Extrae la última parte del nombre del archivo (después del último guión).
        /// "2235055-CPA-UT7-B08-ZZZ-M3D-EST-CampoFutbol.rvt" → "CampoFutbol"
        /// </summary>
        private static string GetDocSuffix(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return string.Empty;
            string name  = Path.GetFileNameWithoutExtension(filePath);
            int    index = name.LastIndexOf('-');
            return index >= 0 ? name.Substring(index + 1) : string.Empty;
        }

        /// <summary>
        /// Renombra los PDFs exportados de "{SheetNumber}.pdf"
        /// a "{SheetNumber}-{suffix}.pdf".
        /// </summary>
        private static void RenameExportedPDFs(string folder,
            IEnumerable<SheetItem> sheets, string suffix)
        {
            if (string.IsNullOrEmpty(suffix)) return;

            foreach (SheetItem sheet in sheets)
            {
                string oldPath = Path.Combine(folder, $"{sheet.Number}.pdf");
                string newPath = Path.Combine(folder, $"{sheet.Number}-{suffix}.pdf");

                if (!File.Exists(oldPath)) continue;

                // Si ya existe el destino, eliminarlo para sobrescribir
                if (File.Exists(newPath))
                    File.Delete(newPath);

                File.Move(oldPath, newPath);
            }
        }

        private static PDFExportOptions BuildPDFOptions()
        {
            var options = new PDFExportOptions
            {
                ExportQuality            = PDFExportQualityType.DPI300,
                RasterQuality            = RasterQualityType.Presentation,
                AlwaysUseRaster          = true,
                PaperFormat              = ExportPaperFormat.Default,
                ZoomType                 = ZoomType.Zoom,
                ZoomPercentage           = 100,
                HideCropBoundaries       = true,
                HideReferencePlane       = true,
                HideScopeBoxes           = true,
                HideUnreferencedViewTags = true,
                MaskCoincidentLines      = true,
                ViewLinksInBlue          = false,
                ColorDepth               = ColorDepthType.Color,
                StopOnError              = false,
                Combine                  = false,
            };

            // Nombre del PDF = número de plano
            var rule = TableCellCombinedParameterData.Create();
            rule.ParamId = new ElementId(BuiltInParameter.SHEET_NUMBER);
            options.SetNamingRule(new List<TableCellCombinedParameterData> { rule });

            return options;
        }
    }
}

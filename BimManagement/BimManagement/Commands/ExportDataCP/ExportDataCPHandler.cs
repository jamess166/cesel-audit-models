using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace BimManagement
{
    /// <summary>
    /// Manejador de evento externo para exportar datos CP a Excel.
    /// Procesa el modelo activo o itera una lista de archivos .rvt,
    /// generando un Excel por modelo o un único Excel combinado.
    /// </summary>
    public class ExportDataCPHandler : IExternalEventHandler
    {
        private static string TemplatePath => Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Resources", "TemplateReport.xlsx");

        public void Execute(UIApplication uiApp)
        {
            try
            {
                if (!File.Exists(TemplatePath))
                    throw new FileNotFoundException("No se encontró TemplateReport.xlsx", TemplatePath);

                bool combine = ExportDataCPTools.CombineIntoSingleFile;

                if (ExportDataCPTools.UseCurrentModel)
                {
                    Document doc = uiApp.ActiveUIDocument.Document;
                    Log($"Procesando: {doc.Title}");

                    List<ExportRow> rows = CollectRows(doc);
                    if (rows.Count == 0)
                    {
                        Log("✖ No se encontraron elementos con PO-WBS o parámetros de control.");
                    }
                    else
                    {
                        string directory = Path.GetDirectoryName(doc.PathName);
                        if (string.IsNullOrEmpty(directory)) directory = Path.GetDirectoryName(TemplatePath);
                        string fileName = combine ? BuildCombinedFileName() : Path.GetFileNameWithoutExtension(doc.Title) + ".xlsx";
                        SaveWorkbook(rows, Path.Combine(directory, fileName));
                        Log($"✔ Se exportaron {rows.Count} elementos.");
                    }
                }
                else
                {
                    var files = ExportDataCPTools.Files ?? new List<FileInfo>();
                    int total = files.Count;
                    int current = 0;
                    int exported = 0;
                    var errors = new List<string>();
                    var combinedRows = new List<ExportRow>();

                    foreach (FileInfo fileInfo in files)
                    {
                        current++;
                        UpdateProgress(current, total);
                        Document fileDoc = null;

                        try
                        {
                            Log($"Procesando: {fileInfo.Name}");
                            ModelPath modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(fileInfo.FullName);
                            fileDoc = uiApp.Application.OpenDocumentFile(modelPath, new OpenOptions { Audit = false });

                            List<ExportRow> rows = CollectRows(fileDoc);
                            if (combine)
                            {
                                combinedRows.AddRange(rows);
                            }
                            else
                            {
                                string output = Path.Combine(fileInfo.DirectoryName, Path.GetFileNameWithoutExtension(fileDoc.Title) + ".xlsx");
                                SaveWorkbook(rows, output);
                            }

                            exported++;
                            Log($"✔ {fileInfo.Name}: {rows.Count} elementos.");
                        }
                        catch (Exception ex)
                        {
                            errors.Add(fileInfo.Name + ": " + ex.Message);
                            Log($"✖ {fileInfo.Name}: {ex.Message}");
                        }
                        finally
                        {
                            if (fileDoc != null) try { fileDoc.Close(false); } catch { }
                        }
                    }

                    if (combine)
                    {
                        if (combinedRows.Count == 0)
                        {
                            Log("✖ No se encontraron elementos para exportar en ningún modelo.");
                        }
                        else
                        {
                            string folder = !string.IsNullOrEmpty(ExportDataCPTools.FolderPath)
                                ? ExportDataCPTools.FolderPath
                                : Path.GetDirectoryName(TemplatePath);
                            string output = Path.Combine(folder, BuildCombinedFileName());
                            SaveWorkbook(combinedRows, output);
                            Log($"✔ Excel combinado generado: {Path.GetFileName(output)} ({combinedRows.Count} elementos).");
                        }
                    }

                    UpdateProgress(0, 0);
                    string summary = $"Modelos exportados: {exported} de {total}.";
                    if (errors.Count > 0) summary += " Errores: " + errors.Count;
                    Log(summary);
                }
            }
            catch (Exception ex)
            {
                Log($"✖ Error: {ex.Message}");
            }
        }

        public string GetName() => "Exportar Datos CP";

        private static string BuildCombinedFileName() => $"ExportData_{DateTime.Now:yyMMdd_HHmm}.xlsx";

        private static void SaveWorkbook(List<ExportRow> rows, string outputPath)
        {
            File.Copy(TemplatePath, outputPath, true);

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var package = new ExcelPackage(new FileInfo(outputPath)))
            {
                ExcelWorksheet sheet = package.Workbook.Worksheets["Contenido"];
                if (sheet == null) throw new InvalidOperationException("El template no contiene la hoja Contenido.");
                if (sheet.Dimension != null && sheet.Dimension.End.Row >= 2)
                    sheet.Cells[2, 1, sheet.Dimension.End.Row, 14].Clear();

                object[,] values = new object[rows.Count, 14];
                for (int i = 0; i < rows.Count; i++)
                {
                    ExportRow row = rows[i];
                    values[i, 0] = row.Model;
                    values[i, 1] = row.Id;
                    values[i, 2] = row.Category;
                    values[i, 3] = row.FamilyType;
                    values[i, 4] = row.Wbs;
                    values[i, 5] = row.Element;
                    values[i, 6] = row.Level;
                    values[i, 7] = row.Sector;
                    values[i, 8] = row.State;
                    values[i, 9] = row.Date;
                    values[i, 10] = row.Week;
                    values[i, 11] = row.Cars;
                    values[i, 12] = row.Unit;
                    values[i, 13] = row.Quantity.HasValue ? (object)row.Quantity.Value : "";
                }
                sheet.Cells[2, 1, rows.Count + 1, 14].Value = values;
                package.Save();
            }
        }

        private static List<ExportRow> CollectRows(Document doc)
        {
            Dictionary<string, ReportExcelReader.PartidaData> partidas = LoadPartidas(doc);
            var rows = new List<ExportRow>();
            var collector = new FilteredElementCollector(doc).WhereElementIsNotElementType();

            foreach (Element element in collector)
            {
                if (element is Part || element.Category == null || !HasControlData(element)) continue;
                if (string.Equals(element.Category.Name, "Center Line", StringComparison.OrdinalIgnoreCase)) continue;
                string wbs = GetValue(element, "PO-WBS");
                partidas.TryGetValue(wbs, out ReportExcelReader.PartidaData partida);
                string unit = GetValue(element, "CSL-Unidad");
                if (string.IsNullOrWhiteSpace(unit) && partida != null) unit = partida.Unidad;
                bool hasStoredQuantity = GetValue(element, "CSL-Cantidad", out double stored);
                double? quantity = hasStoredQuantity ? stored : ResolveQuantity(element, unit);
                if (!hasStoredQuantity && quantity.HasValue && partida != null)
                    quantity *= partida.Factor == 0 ? 1 : partida.Factor;

                rows.Add(new ExportRow
                {
                    Model = Path.GetFileNameWithoutExtension(doc.Title), Id = element.Id.IntegerValue.ToString(),
                    Category = element.Category.Name, FamilyType = GetFamilyType(element), Wbs = wbs,
                    Element = GetValue(element, "PO-ELEMENTO"), Level = GetValue(element, "PR-NIVEL"),
                    Sector = GetValue(element, "PO-SECTOR"), State = GetValue(element, "PO-ESTADO CONSTRUCCION"),
                    Date = GetValue(element, "PO-FECHA CONSTRUIDA"), Week = GetValue(element, "PO-SEMANA PROYECTO"),
                    Cars = GetValue(element, "PO-CARS"), Unit = unit, Quantity = quantity
                });
            }
            return rows;
        }

        private static Dictionary<string, ReportExcelReader.PartidaData> LoadPartidas(Document doc)
        {
            string[] parts = Path.GetFileNameWithoutExtension(doc.Title).Split('-');
            if (parts.Length < 7) return new Dictionary<string, ReportExcelReader.PartidaData>(StringComparer.OrdinalIgnoreCase);
            string ut = parts[2].Trim();
            string specialty = parts[6].Trim().ToUpperInvariant();
            if (new[] { "ACI", "AGU", "DES", "DRE" }.Contains(specialty)) specialty = "ISS";
            string folder = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Resources", "Partidas", ut);
            string path = Path.Combine(folder, ut + "-" + specialty + ".xlsx");
            return File.Exists(path) ? ReportExcelReader.Load(path) : new Dictionary<string, ReportExcelReader.PartidaData>(StringComparer.OrdinalIgnoreCase);
        }

        private static bool HasControlData(Element e) => !string.IsNullOrWhiteSpace(GetValue(e, "PO-WBS")) ||
            !string.IsNullOrWhiteSpace(GetValue(e, "PO-FECHA CONSTRUIDA")) ||
            !string.IsNullOrWhiteSpace(GetValue(e, "PO-SEMANA PROYECTO")) ||
            !string.IsNullOrWhiteSpace(GetValue(e, "PO-ESTADO CONSTRUCCION"));

        private static double? ResolveQuantity(Element e, string unit)
        {
            unit = (unit ?? "").Trim().ToLowerInvariant();
            if (unit == "und") return 1;
            if (unit == "m2") return GetAreaMeters(e);
            if (unit == "m3") return GetVolumeMeters(e);
            if (unit == "m") return GetLengthMeters(e);
            return null;
        }

        // ── Área (m2): parámetros nativos, con respaldo por geometría (p.ej. Bordes de Losa) ──

        private static double? GetAreaMeters(Element e)
        {
            double? value = TryBuiltIn(e, BuiltInParameter.HOST_AREA_COMPUTED)
                          ?? TryBuiltIn(e, BuiltInParameter.STRUCTURAL_SECTION_AREA)
                          ?? TryBuiltIn(e, BuiltInParameter.SURFACE_AREA)
                          ?? TryBuiltIn(e, BuiltInParameter.ROOM_AREA);
            if (value.HasValue) return ConvertArea(value);

            return GetAreaFromGeometry(e);
        }

        private static double? GetAreaFromGeometry(Element e)
        {
            try
            {
                var options = new Options { DetailLevel = ViewDetailLevel.Fine, IncludeNonVisibleObjects = false };
                GeometryElement geomElem = e.get_Geometry(options);
                if (geomElem == null) return null;

                double totalAreaSqFt = SumSolidsArea(geomElem);
                return totalAreaSqFt > 0 ? ConvertArea(totalAreaSqFt) : (double?)null;
            }
            catch { return null; }
        }

        private static double SumSolidsArea(GeometryElement geomElem)
        {
            double total = 0.0;
            foreach (GeometryObject obj in geomElem)
            {
                if (obj is Solid solid && solid.Volume > 0)
                {
                    foreach (Face face in solid.Faces) total += face.Area;
                }
                else if (obj is GeometryInstance instance)
                {
                    total += SumSolidsArea(instance.GetInstanceGeometry());
                }
            }
            return total;
        }

        // ── Volumen (m3): parámetro nativo, con respaldo por geometría (p.ej. Escaleras) ──

        private static double? GetVolumeMeters(Element e)
        {
            double? value = TryBuiltIn(e, BuiltInParameter.HOST_VOLUME_COMPUTED);
            if (value.HasValue) return UnitUtils.ConvertFromInternalUnits(value.Value, UnitTypeId.CubicMeters);

            return GetVolumeFromGeometry(e);
        }

        private static double? GetVolumeFromGeometry(Element e)
        {
            try
            {
                var options = new Options { DetailLevel = ViewDetailLevel.Fine, IncludeNonVisibleObjects = false };
                GeometryElement geomElem = e.get_Geometry(options);
                if (geomElem == null) return null;

                double totalVolumeCubicFt = SumSolidsVolume(geomElem);
                return totalVolumeCubicFt > 0 ? UnitUtils.ConvertFromInternalUnits(totalVolumeCubicFt, UnitTypeId.CubicMeters) : (double?)null;
            }
            catch { return null; }
        }

        private static double SumSolidsVolume(GeometryElement geomElem)
        {
            double total = 0.0;
            foreach (GeometryObject obj in geomElem)
            {
                if (obj is Solid solid && solid.Volume > 0)
                {
                    total += solid.Volume;
                }
                else if (obj is GeometryInstance instance)
                {
                    total += SumSolidsVolume(instance.GetInstanceGeometry());
                }
            }
            return total;
        }

        // ── Longitud (m): curva de ubicación, con respaldo de ruta para Barandas ──

        private static double? GetLengthMeters(Element e)
        {
            IList<Curve> path = null;
            if (e is Railing railing) path = railing.GetPath();
            else if (e is ContinuousRail contRail) path = contRail.GetPath();

            if (path != null && path.Count > 0)
            {
                double total = path.Sum(c => c.Length);
                if (total > 0) return UnitUtils.ConvertFromInternalUnits(total, UnitTypeId.Meters);
            }

            LocationCurve curve = e.Location as LocationCurve;
            if (curve != null && curve.Curve != null)
                return UnitUtils.ConvertFromInternalUnits(curve.Curve.Length, UnitTypeId.Meters);

            return null;
        }

        private static double? ConvertArea(double? value)
        {
            if (!value.HasValue) return null;
            return UnitUtils.ConvertFromInternalUnits(value.Value, UnitTypeId.SquareMeters);
        }

        private static double ConvertArea(double value)
            => UnitUtils.ConvertFromInternalUnits(value, UnitTypeId.SquareMeters);

        private static double? TryBuiltIn(Element e, BuiltInParameter id)
        {
            Parameter p = e.get_Parameter(id);
            if (p == null || !p.HasValue || p.StorageType != StorageType.Double) return null;
            return p.AsDouble();
        }
        private static bool GetValue(Element e, string name, out double value)
        {
            Parameter p = e.LookupParameter(name); value = 0;
            if (p == null || !p.HasValue || p.StorageType != StorageType.Double) return false;
            value = p.AsDouble(); return true;
        }
        private static string GetValue(Element e, string name)
        {
            Parameter p = e.LookupParameter(name);
            if (p == null || !p.HasValue) return "";
            return p.StorageType == StorageType.String ? p.AsString() ?? "" : p.AsValueString() ?? "";
        }
        private static string GetFamilyType(Element e)
        {
            ElementType type = e.Document.GetElement(e.GetTypeId()) as ElementType;
            return type == null ? e.Name : type.FamilyName + " - " + type.Name;
        }

        private class ExportRow
        {
            public string Model, Id, Category, FamilyType, Wbs, Element, Level, Sector, State, Date, Week, Cars, Unit;
            public double? Quantity;
        }

        // ── Helpers de UI ──────────────────────────────────────────────────────

        private static void Log(string message)
        {
            var view = App.m_ExportDataCPView;
            if (view == null) return;
            view.Dispatcher.Invoke(() => view.Log(message));
        }

        private static void UpdateProgress(int current, int total)
        {
            var view = App.m_ExportDataCPView;
            if (view == null) return;
            view.Dispatcher.Invoke(() => view.SetProgress(current, total));
        }
    }
}

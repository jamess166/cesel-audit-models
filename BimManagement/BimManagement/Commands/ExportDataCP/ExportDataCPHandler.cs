using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Globalization;
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

        private static readonly DateTime AnchorSemanaProyecto = new DateTime(2023, 11, 13);
        private static readonly DateTime AnchorCars = new DateTime(2023, 11, 9);
        private static readonly string[] ValidStates = { "Ejecutado", "En Proceso", "En Ejecución" };
        private static readonly HashSet<BuiltInCategory> ExcludedCategories = new HashSet<BuiltInCategory>
        {
            BuiltInCategory.OST_PipingSystem, BuiltInCategory.OST_DuctSystem
        };
        private static readonly string[] ValidationHeaders =
        {
            "VAL-ESTADO VALIDO", "VAL-GRUPO COMPLETO", "VAL-CAMPOS FALTANTES", "VAL-CARS SIN EJECUTAR",
            "VAL-SEMANA OK", "VAL-SEMANA ESPERADA", "VAL-CARS OK", "VAL-CARS ESPERADO",
            "VAL-SEMANA PROYECTO OK", "VAL-SEMANA PROYECTO ESPERADO",
            "VAL-TIENE ERRORES", "VAL-OBSERVACIONES"
        };

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
                const int columnCount = 26;
                if (sheet.Dimension != null && sheet.Dimension.End.Row >= 2)
                    sheet.Cells[2, 1, sheet.Dimension.End.Row, columnCount].Clear();

                for (int h = 0; h < ValidationHeaders.Length; h++)
                    sheet.Cells[1, 15 + h].Value = ValidationHeaders[h];

                object[,] values = new object[rows.Count, columnCount];
                for (int i = 0; i < rows.Count; i++)
                {
                    ExportRow row = rows[i];
                    Validate(row);

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
                    values[i, 14] = row.EstadoValido ? "Sí" : "No";
                    values[i, 15] = row.GrupoCompleto ? "Sí" : "No";
                    values[i, 16] = row.CamposFaltantes;
                    values[i, 17] = row.CarsSinEjecutar ? "Sí" : "No";
                    values[i, 18] = row.SemanaOk;
                    values[i, 19] = row.SemanaEsperada.HasValue ? (object)row.SemanaEsperada.Value : "";
                    values[i, 20] = row.CarsOk;
                    values[i, 21] = row.CarsEsperado.HasValue ? (object)row.CarsEsperado.Value : "";
                    values[i, 22] = row.SemanaOk;
                    values[i, 23] = row.SemanaEsperada.HasValue ? (object)row.SemanaEsperada.Value : "";
                    values[i, 24] = row.TieneErrores ? "Sí" : "No";
                    values[i, 25] = row.Observaciones;
                }
                sheet.Cells[2, 1, rows.Count + 1, columnCount].Value = values;
                ResizePivotTables(package, rows.Count);
                package.Save();
            }
        }

        private static void ResizePivotTables(ExcelPackage package, int rowCount)
        {
            int lastRow = Math.Max(rowCount + 1, 2);
            foreach (ExcelWorksheet ws in package.Workbook.Worksheets)
            {
                foreach (var pivotTable in ws.PivotTables)
                {
                    ExcelRangeBase src = pivotTable.CacheDefinition.SourceRange;
                    ExcelWorksheet contenidoSheet = src.Worksheet;
                    int lastCol = src.End.Column;
                    pivotTable.CacheDefinition.SourceRange = contenidoSheet.Cells[1, 1, lastRow, lastCol];
                    pivotTable.CacheDefinition.Refresh();
                }
            }
        }

        private static List<ExportRow> CollectRows(Document doc)
        {
            Dictionary<string, ReportExcelReader.PartidaData> partidas = LoadPartidas(doc);
            var rows = new List<ExportRow>();
            var collector = new FilteredElementCollector(doc).WhereElementIsNotElementType();

            foreach (Element element in collector)
            {
                if (IsNonExportableElement(element)) continue;
                if (element.Category == null) continue;
                if (IsExcludedCategory(element.Category)) continue;

                List<Element> targets = TryGetPartsWithWbs(doc, element, out List<Element> parts)
                    ? parts
                    : new List<Element> { element };

                foreach (Element target in targets)
                {
                    if (!HasControlData(target)) continue;
                    rows.Add(BuildRow(doc, partidas, target));
                }
            }
            return rows;
        }

        // ── Elementos que nunca deben exportarse: partes, vistas (incluye planillas/──
        // ── schedules, que son un tipo de View), viewports y vínculos RVT. ─────────

        private static bool IsNonExportableElement(Element e) =>
            e is Part || e is View || e is Viewport || e is RevitLinkInstance;

        private static bool IsExcludedCategory(Category category)
        {
            if (string.Equals(category.Name, "Center Line", StringComparison.OrdinalIgnoreCase)) return true;
            return ExcludedCategories.Contains((BuiltInCategory)category.Id.IntegerValue);
        }

        // ── Partes: si el elemento tiene partes asociadas y alguna tiene PO-WBS, ──
        // ── se exportan esas partes en lugar del elemento padre. ──────────────────

        private static bool TryGetPartsWithWbs(Document doc, Element host, out List<Element> parts)
        {
            parts = new List<Element>();
            if (!PartUtils.HasAssociatedParts(doc, host.Id)) return false;

            ICollection<ElementId> partIds = PartUtils.GetAssociatedParts(
                doc, host.Id, includePartsWithAssociatedParts: true, includeAllChildren: false);

            foreach (ElementId id in partIds)
            {
                Element part = doc.GetElement(id);
                if (part != null && !string.IsNullOrWhiteSpace(GetValue(part, "PO-WBS")))
                    parts.Add(part);
            }
            return parts.Count > 0;
        }

        private static ExportRow BuildRow(Document doc, Dictionary<string, ReportExcelReader.PartidaData> partidas, Element element)
        {
            string wbs = GetValue(element, "PO-WBS");
            partidas.TryGetValue(wbs, out ReportExcelReader.PartidaData partida);
            string unit = GetValue(element, "CSL-Unidad");
            if (string.IsNullOrWhiteSpace(unit) && partida != null) unit = partida.Unidad;
            bool hasStoredQuantity = GetValue(element, "CSL-Cantidad", out double stored);
            double? quantity = hasStoredQuantity ? stored : ResolveQuantity(element, unit);
            if (!hasStoredQuantity && quantity.HasValue && partida != null)
                quantity *= partida.Factor == 0 ? 1 : partida.Factor;

            return new ExportRow
            {
                Model = Path.GetFileNameWithoutExtension(doc.Title), Id = element.Id.IntegerValue.ToString(),
                Category = element.Category?.Name ?? "", FamilyType = GetFamilyType(element), Wbs = wbs,
                Element = GetValue(element, "PO-ELEMENTO"), Level = GetValue(element, "PR-NIVEL"),
                Sector = GetValue(element, "PO-SECTOR"), State = GetValue(element, "PO-ESTADO CONSTRUCCION"),
                Date = GetValue(element, "PO-FECHA CONSTRUIDA"), Week = GetValue(element, "PO-SEMANA PROYECTO"),
                Cars = GetValue(element, "PO-CARS"), Unit = unit, Quantity = quantity
            };
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

            public bool EstadoValido, GrupoCompleto, CarsSinEjecutar, TieneErrores;
            public string CamposFaltantes, SemanaOk, CarsOk, Observaciones;
            public int? SemanaEsperada, CarsEsperado;
        }

        // ── Validación de consistencia ─────────────────────────────────────────

        private static void Validate(ExportRow row)
        {
            string state = (row.State ?? "").Trim();
            var observaciones = new List<string>();

            row.EstadoValido = state.Length == 0 || ValidStates.Contains(state, StringComparer.Ordinal);
            if (!row.EstadoValido) observaciones.Add($"Estado inválido: '{state}'");

            bool ejecutado = string.Equals(state, "Ejecutado", StringComparison.Ordinal);
            if (ejecutado)
            {
                var grupo = new[]
                {
                    ("PO-WBS", row.Wbs), ("PO-ELEMENTO", row.Element), ("PR-NIVEL", row.Level),
                    ("PO-SECTOR", row.Sector), ("PO-FECHA CONSTRUIDA", row.Date), ("PO-SEMANA PROYECTO", row.Week)
                };
                var faltantes = grupo.Where(g => string.IsNullOrWhiteSpace(g.Item2)).Select(g => g.Item1).ToList();
                row.GrupoCompleto = faltantes.Count == 0;
                row.CamposFaltantes = string.Join("; ", faltantes);
                if (!row.GrupoCompleto) observaciones.Add($"Ejecutado incompleto, faltan: {row.CamposFaltantes}");
            }
            else
            {
                row.GrupoCompleto = true;
                row.CamposFaltantes = "";
            }

            row.CarsSinEjecutar = !string.IsNullOrWhiteSpace(row.Cars) && !ejecutado;
            if (row.CarsSinEjecutar) observaciones.Add("Tiene PO-CARS pero el estado no es 'Ejecutado'");

            bool dateBlank = string.IsNullOrWhiteSpace(row.Date);
            bool dateValid = dateBlank || DateTime.TryParseExact(row.Date, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
            if (!dateBlank && !dateValid) observaciones.Add("PO-FECHA CONSTRUIDA con formato inválido (esperado dd/MM/yyyy)");
            DateTime? fecha = dateValid && !dateBlank
                ? DateTime.ParseExact(row.Date, "dd/MM/yyyy", CultureInfo.InvariantCulture)
                : (DateTime?)null;

            (row.SemanaOk, row.SemanaEsperada) = ValidateWeek(fecha, row.Week, AnchorSemanaProyecto, AnchorSemanaProyecto);
            if (row.SemanaOk == "No")
                observaciones.Add($"PO-SEMANA PROYECTO inconsistente (esperado: {(row.SemanaEsperada.HasValue ? row.SemanaEsperada.Value.ToString() : "vacío")})");

            (row.CarsOk, row.CarsEsperado) = ValidateWeek(fecha, row.Cars, AnchorCars, AnchorSemanaProyecto);
            if (row.CarsOk == "No")
                observaciones.Add($"PO-CARS inconsistente (esperado: {(row.CarsEsperado.HasValue ? row.CarsEsperado.Value.ToString() : "vacío")})");

            row.Observaciones = string.Join("; ", observaciones);
            row.TieneErrores = observaciones.Count > 0;
        }

        private static (string Estado, int? Esperado) ValidateWeek(DateTime? fecha, string actualText, DateTime formulaAnchor, DateTime gateAnchor)
        {
            bool actualPresent = !string.IsNullOrWhiteSpace(actualText);
            int actualValue = 0;
            bool actualNumeric = actualPresent && int.TryParse(actualText, out actualValue);

            if (!fecha.HasValue)
                return (actualPresent ? "No" : "N/A", null);

            if (fecha.Value < gateAnchor)
                return (actualPresent ? "No" : "N/A", null);

            int esperado = (int)(fecha.Value - formulaAnchor).TotalDays / 7 + 1;
            bool ok = actualNumeric && actualValue == esperado;
            return (ok ? "Sí" : "No", esperado);
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

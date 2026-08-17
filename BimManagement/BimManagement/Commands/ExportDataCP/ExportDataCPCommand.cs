using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace BimManagement
{
    [Transaction(TransactionMode.Manual)]
    public class ExportDataCPCommand : IExternalCommand
    {
        private const string TemplatePath = @"D:\Developer\_test\ExportRevitToExcel\ExportRevitToExcel\Resources\TemplateReport.xlsx";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (!File.Exists(TemplatePath))
                    throw new FileNotFoundException("No se encontró TemplateReport.xlsx", TemplatePath);

                TaskDialogResult scope = new TaskDialog("Exportar datos CP a Excel")
                {
                    MainInstruction = "Seleccione el alcance de la exportación",
                    MainContent = "No: modelo activo.\nSí: seleccionar una carpeta y exportar todos los modelos .rvt.",
                    CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No | TaskDialogCommonButtons.Cancel
                }.Show();

                if (scope == TaskDialogResult.Cancel) return Result.Cancelled;
                if (scope == TaskDialogResult.Yes)
                    return ExportFolder(commandData.Application, ref message);

                int count = ExportDocument(commandData.Application.ActiveUIDocument.Document);
                TaskDialog.Show("Exportar datos CP", $"Se exportaron {count} elementos.");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Exportar datos CP", ex.Message);
                return Result.Failed;
            }
        }

        private static Result ExportFolder(UIApplication uiApp, ref string message)
        {
            using (var dialog = new FolderBrowserDialog { Description = "Seleccione la carpeta de modelos Revit" })
            {
                if (dialog.ShowDialog() != DialogResult.OK) return Result.Cancelled;

                string[] paths = Directory.GetFiles(dialog.SelectedPath, "*.rvt", SearchOption.TopDirectoryOnly)
                    .Where(p => !p.EndsWith(".0001.rvt", StringComparison.OrdinalIgnoreCase)).ToArray();
                int exported = 0;
                var errors = new List<string>();

                foreach (string path in paths)
                {
                    Document doc = null;
                    try
                    {
                        doc = uiApp.Application.OpenDocumentFile(path, new OpenOptions { Audit = false });
                        ExportDocument(doc);
                        exported++;
                    }
                    catch (Exception ex) { errors.Add(Path.GetFileName(path) + ": " + ex.Message); }
                    finally { if (doc != null) try { doc.Close(false); } catch { } }
                }

                string summary = $"Modelos exportados: {exported} de {paths.Length}.";
                if (errors.Count > 0) summary += "\n\nErrores:\n" + string.Join("\n", errors);
                TaskDialog.Show("Exportar datos CP", summary);
                if (errors.Count == paths.Length && paths.Length > 0) { message = summary; return Result.Failed; }
                return Result.Succeeded;
            }
        }

        private static int ExportDocument(Document doc)
        {
            List<ExportRow> rows = CollectRows(doc);
            if (rows.Count == 0) throw new InvalidOperationException("No se encontraron elementos con PO-WBS o parámetros de control.");

            string directory = Path.GetDirectoryName(doc.PathName);
            if (string.IsNullOrEmpty(directory)) directory = Path.GetDirectoryName(TemplatePath);
            string output = Path.Combine(directory, Path.GetFileNameWithoutExtension(doc.Title) + ".xlsx");
            File.Copy(TemplatePath, output, true);

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var package = new ExcelPackage(new FileInfo(output)))
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
            return rows.Count;
        }

        private static List<ExportRow> CollectRows(Document doc)
        {
            Dictionary<string, ReportExcelReader.PartidaData> partidas = LoadPartidas(doc);
            var rows = new List<ExportRow>();
            var collector = new FilteredElementCollector(doc).WhereElementIsNotElementType();

            foreach (Element element in collector)
            {
                if (element is Part || element.Category == null || !HasControlData(element)) continue;
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
            if (unit == "m2") return ConvertArea(TryBuiltIn(e, BuiltInParameter.HOST_AREA_COMPUTED));
            if (unit == "m3")
            {
                double? value = TryBuiltIn(e, BuiltInParameter.HOST_VOLUME_COMPUTED);
                return value.HasValue ? UnitUtils.ConvertFromInternalUnits(value.Value, UnitTypeId.CubicMeters) : null;
            }
            if (unit == "m")
            {
                LocationCurve curve = e.Location as LocationCurve;
                return curve?.Curve == null ? null : UnitUtils.ConvertFromInternalUnits(curve.Curve.Length, UnitTypeId.Meters);
            }
            return null;
        }

        private static double? ConvertArea(double? value) => value.HasValue ? UnitUtils.ConvertFromInternalUnits(value.Value, UnitTypeId.SquareMeters) : (double?)null;
        private static double? TryBuiltIn(Element e, BuiltInParameter id)
        {
            Parameter p = e.get_Parameter(id);
            return p != null && p.HasValue && p.StorageType == StorageType.Double ? (double?)p.AsDouble() : null;
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
    }
}

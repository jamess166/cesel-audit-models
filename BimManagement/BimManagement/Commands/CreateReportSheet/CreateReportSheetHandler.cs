using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BimManagement
{
    /// <summary>
    /// Manejador de evento externo para crear planos de avance.
    /// Procesa el modelo activo o itera una lista de archivos .rvt.
    /// </summary>
    public class CreateReportSheetHandler : IExternalEventHandler
    {
        public void Execute(UIApplication uiApp)
        {
            string configPath = GetConfigPath();
            AppConfig config  = ConfigManager.Load(configPath);

            if (CreateReportSheetTools.UseCurrentModel)
            {
                // ── Modelo activo ─────────────────────────────────────────────
                Document doc = uiApp.ActiveUIDocument.Document;
                Log($"Procesando: {doc.Title}");
                try
                {
                    ApplyToDocument(doc, config);
                    Log("✔ Completado exitosamente.");
                }
                catch (Exception ex)
                {
                    Log($"✖ Error: {ex.Message}");
                }
            }
            else
            {
                // ── Carpeta de archivos ───────────────────────────────────────
                var files = CreateReportSheetTools.Files ?? new List<FileInfo>();
                int total   = files.Count;
                int current = 0;

                foreach (FileInfo fileInfo in files)
                {
                    current++;
                    UpdateProgress(current, total);
                    Document fileDoc = null;

                    try
                    {
                        Log($"Procesando: {fileInfo.Name}");
                        var modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(
                            fileInfo.FullName);
                        fileDoc = uiApp.Application.OpenDocumentFile(
                            modelPath, new OpenOptions { Audit = false });

                        ApplyToDocument(fileDoc, config);

                        fileDoc.Save();
                        fileDoc.Close(false);
                        Log($"✔ {fileInfo.Name}");
                    }
                    catch (Exception ex)
                    {
                        if (fileDoc != null)
                            try { fileDoc.Close(false); } catch { }
                        Log($"✖ {fileInfo.Name}: {ex.Message}");
                    }
                }

                Log("✔ Proceso completado.");
                UpdateProgress(0, 0);
            }
        }

        public string GetName() => "Crear Planos de Reporte";

        // ── Procesamiento de un documento ─────────────────────────────────────

        private void ApplyToDocument(Document doc, AppConfig config)
        {
            string week    = CreateReportSheetTools.Week;
            string period  = CreateReportSheetTools.Period;
            string month   = CreateReportSheetTools.Month;
            bool isMonthly = CreateReportSheetTools.IsMonthly;
            string issueDate = CreateReportSheetTools.FechaPresentacion;

            // 1. Garantizar parámetros compartidos
            using (Transaction tx = new Transaction(doc, "CSL - Crear Parámetros Compartidos"))
            {
                tx.Start();
                new SharedParameterCreator(doc, config).EnsureParametersExist();
                tx.Commit();
            }

            // 2. Recopilar elementos
            var filter   = new ReportElementFilter(doc, isMonthly, week, month);
            List<Element> elements = filter.Collect();

            if (elements.Count == 0)
                throw new InvalidOperationException(
                    "No se encontraron elementos que cumplan el criterio de filtrado.");

            // 3. Llenar CSL-Unidad y CSL-Cantidad
            using (Transaction tx = new Transaction(doc, "CSL - Llenar Parámetros de Reporte"))
            {
                tx.Start();
                new ReportParameterFiller(doc).Fill(elements);
                tx.Commit();
            }

            // 4. Crear tabla multi-categoría
            ViewSchedule reportSchedule;
            using (Transaction tx = new Transaction(doc, "CSL - Crear Tabla de Reporte"))
            {
                tx.Start();
                reportSchedule = new ReportScheduleCreator(doc, isMonthly, week, month).Create();
                tx.Commit();
            }

            // 5. Crear vistas 3D isométricas con filtros
            using (Transaction tx = new Transaction(doc, "CSL - Crear Vistas 3D de Reporte"))
            {
                tx.Start();
                new Report3DViewCreator(doc, isMonthly, week, month).Create();
                tx.Commit();
            }

            // 6. Crear plano con titleblock, vistas y tabla
            using (Transaction tx = new Transaction(doc, "CSL - Crear Plano"))
            {
                tx.Start();
                CreateSheet(doc, config.TitleBlockFamilyPath,
                    week, month, isMonthly, issueDate, reportSchedule);
                tx.Commit();
            }
        }

        // ── Plano ─────────────────────────────────────────────────────────────

        private void CreateSheet(Document doc, string titleBlockPath,
            string week, string month, bool isMonthly, string issueDate,
            ViewSchedule reportSchedule)
        {
            string docName     = Path.GetFileNameWithoutExtension(doc.Title);
            string sheetNumber = BuildSheetNumber(doc, docName);
            string sheetName   = BuildSheetName(week, month, isMonthly);
            string especialidad = ResolveEspecialidad(docName);

            Family tbFamily = GetOrLoadTitleBlock(doc, titleBlockPath);

            ElementId symbolId = tbFamily.GetFamilySymbolIds().FirstOrDefault()
                ?? ElementId.InvalidElementId;

            if (symbolId == ElementId.InvalidElementId)
                throw new InvalidOperationException(
                    "No se pudo obtener el símbolo del titleblock.");

            ViewSheet sheet  = ViewSheet.Create(doc, symbolId);
            sheet.SheetNumber = sheetNumber;
            sheet.Name        = sheetName;

            // Fecha de emisión
            if (!string.IsNullOrEmpty(issueDate))
            {
                Parameter dateParam = sheet.get_Parameter(BuiltInParameter.SHEET_ISSUE_DATE);
                if (dateParam != null && !dateParam.IsReadOnly)
                    dateParam.Set(issueDate);
            }

            // PR-ESPECIALIDAD
            if (!string.IsNullOrEmpty(especialidad))
            {
                Parameter p = sheet.LookupParameter("PR-ESPECIALIDAD");
                if (p != null && !p.IsReadOnly && p.StorageType == StorageType.String)
                    p.Set(especialidad);
            }

            // Títulos del cajetín
            SetSheetParam(sheet, "PR-TITULO DE PLANO 01", "CSL-SUPERVISION");
            SetSheetParam(sheet, "PR-TITULO DE PLANO 02",
                isMonthly ? "AVANCE MENSUAL" : "AVANCE SEMANAL");

            // Vistas 3D
            string tag           = isMonthly ? $"MENSUAL {month}" : $"SEMANA {week}";
            string nameActual    = $"CSL-V3D-AVANCE {tag}";
            string nameAcumulado = $"CSL-V3D-AVANCE {tag} Acumulado";

            View3D v3dActual    = FindView3DByName(doc, nameActual);
            View3D v3dAcumulado = FindView3DByName(doc, nameAcumulado);

            if (v3dActual != null && v3dAcumulado != null)
                PlaceViewsOnSheet(doc, sheet, v3dActual, v3dAcumulado);

            // Tabla
            if (reportSchedule != null)
                PlaceScheduleOnSheet(doc, sheet, reportSchedule);
        }

        private string ResolveEspecialidad(string docName)
        {
            string[] parts = docName.Split('-');
            if (parts.Length < 7) return string.Empty;

            string code = parts[6].Trim().ToUpperInvariant();
            switch (code)
            {
                case "EST":                      return "ESTRUCTURAS";
                case "ARQ":                      return "ARQUITECTURA";
                case "ISS":
                case "AGU":
                case "DES":
                case "DRE":
                case "ACI":                      return "INSTALACIONES SANITARIAS";
                case "IEE":                      return "INSTALACIONES ELECTRICAS";
                case "COM":                      return "COMUNICACIONES";
                case "IMM":                      return "MECANICAS";
                case "SSA":                      return "SEGURIDAD";
                default:                         return string.Empty;
            }
        }

        private Family GetOrLoadTitleBlock(Document doc, string path)
        {
            string familyName = Path.GetFileNameWithoutExtension(path);

            Family existing = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .FirstOrDefault(f => string.Equals(f.Name, familyName,
                                                    StringComparison.OrdinalIgnoreCase));
            if (existing != null) return existing;

            if (!File.Exists(path))
                throw new FileNotFoundException(
                    $"No se encontró el archivo de titleblock:\n{path}");

            if (!doc.LoadFamily(path, out Family loaded))
                throw new InvalidOperationException(
                    $"No se pudo cargar el titleblock:\n{path}");

            return loaded;
        }

        private static void ApplyViewportLabelFamily(Document doc, string familyPath,
            params Viewport[] viewports)
        {
            string familyName = Path.GetFileNameWithoutExtension(familyPath);

            Family labelFamily = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .FirstOrDefault(f => string.Equals(f.Name, familyName,
                                                    StringComparison.OrdinalIgnoreCase));
            if (labelFamily == null)
            {
                if (!File.Exists(familyPath)) return;
                if (!doc.LoadFamily(familyPath, out labelFamily)) return;
            }

            ElementId symbolId = labelFamily.GetFamilySymbolIds().FirstOrDefault()
                ?? ElementId.InvalidElementId;
            if (symbolId == ElementId.InvalidElementId) return;

            FamilySymbol symbol = doc.GetElement(symbolId) as FamilySymbol;
            if (symbol != null && !symbol.IsActive) symbol.Activate();

            foreach (Viewport vp in viewports)
            {
                ElementType vt = doc.GetElement(vp.GetTypeId()) as ElementType;
                if (vt == null) continue;

                var titleParam = vt.get_Parameter(BuiltInParameter.VIEWPORT_ATTR_LABEL_TAG);
                if (titleParam != null && !titleParam.IsReadOnly)
                    titleParam.Set(symbolId);

                var lineParam = vt.get_Parameter(BuiltInParameter.VIEWPORT_ATTR_SHOW_EXTENSION_LINE);
                if (lineParam != null && !lineParam.IsReadOnly)
                    lineParam.Set(0);
            }
        }

        private static void PlaceScheduleOnSheet(Document doc, ViewSheet sheet,
            ViewSchedule schedule)
        {
            const double mmToFt = 1.0 / 304.8;
            double xMm = 420.0 / 2.0 + 4.0 + 20.0; // 234 mm
            double yMm = 297.0 - 4.0        - 10.0; // 283 mm
            XYZ insertionPoint = new XYZ(xMm * mmToFt, yMm * mmToFt, 0);
            ScheduleSheetInstance.Create(doc, sheet.Id, schedule.Id, insertionPoint);
        }

        private static View3D FindView3DByName(Document doc, string name)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(View3D))
                .Cast<View3D>()
                .FirstOrDefault(v => string.Equals(v.Name, name,
                                                    StringComparison.OrdinalIgnoreCase));
        }

        private static void PlaceViewsOnSheet(Document doc, ViewSheet sheet,
            View3D viewTop, View3D viewBottom)
        {
            const double mmToFt = 1.0 / 304.8;

            const double marginTopMm         =  4.0;
            const double marginLeftMm        = 14.0;
            const double marginBottomMm      = 29.0;
            const double marginBottomExtraMm =  6.0;
            const double sheetHeightMm       = 297.0;
            const double sheetWidthMm        = 420.0;

            double viewBottomMm      = marginBottomMm + marginBottomExtraMm;
            double availableHeightMm = sheetHeightMm - marginTopMm - viewBottomMm;
            double halfHeightMm      = availableHeightMm / 2.0;
            double maxViewWidthMm    = sheetWidthMm / 2.0 - marginLeftMm;

            double halfHeightFt   = halfHeightMm   * mmToFt;
            double maxWidthFt     = maxViewWidthMm * mmToFt;
            double marginLeftFt   = marginLeftMm   * mmToFt;
            double marginBottomFt = viewBottomMm   * mmToFt;
            double yMidFt         = (viewBottomMm + halfHeightMm)      * mmToFt;
            double yTopFt         = (viewBottomMm + availableHeightMm) * mmToFt;

            Viewport vpTop    = Viewport.Create(doc, sheet.Id, viewTop.Id,    new XYZ(0.5, 0.8, 0));
            Viewport vpBottom = Viewport.Create(doc, sheet.Id, viewBottom.Id, new XYZ(0.5, 0.3, 0));
            doc.Regenerate();

            ScaleViewToFit(viewTop,    vpTop,    halfHeightFt, maxWidthFt);
            ScaleViewToFit(viewBottom, vpBottom, halfHeightFt, maxWidthFt);
            doc.Regenerate();

            MoveViewportToArea(doc, vpTop,    marginLeftFt, yMidFt,         yTopFt, maxWidthFt);
            MoveViewportToArea(doc, vpBottom, marginLeftFt, marginBottomFt, yMidFt, maxWidthFt);

            string dllFolder       = Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location);
            string labelFamilyPath = Path.Combine(dllFolder, "Resources", "CSL-Vista.rfa");
            ApplyViewportLabelFamily(doc, labelFamilyPath, vpTop, vpBottom);
            doc.Regenerate();

            XYZ labelOffset      = new XYZ(0, -6.0 / 304.8, 0);
            vpTop.LabelOffset    = labelOffset;
            vpBottom.LabelOffset = labelOffset;
        }

        private static void ScaleViewToFit(View3D view, Viewport vp,
            double maxHeightFt, double maxWidthFt)
        {
            Outline outline = vp.GetBoxOutline();
            double w = outline.MaximumPoint.X - outline.MinimumPoint.X;
            double h = outline.MaximumPoint.Y - outline.MinimumPoint.Y;
            if (h <= 0 || w <= 0) return;

            double factor = Math.Max(h / maxHeightFt, w / maxWidthFt);
            if (factor > 1.01)
                view.Scale = (int)Math.Ceiling(view.Scale * factor);
        }

        private static void MoveViewportToArea(Document doc, Viewport vp,
            double xLeftFt, double yBottomFt, double yTopFt, double maxWidthFt)
        {
            double targetCenterX = xLeftFt + maxWidthFt / 2.0;
            double targetCenterY = (yBottomFt + yTopFt)  / 2.0;

            XYZ current     = vp.GetBoxCenter();
            XYZ translation = new XYZ(targetCenterX - current.X,
                                      targetCenterY - current.Y, 0);
            ElementTransformUtils.MoveElement(doc, vp.Id, translation);
        }

        private string BuildSheetName(string week, string month, bool isMonthly)
        {
            if (isMonthly)
            {
                string monthName = string.Empty;
                if (DateTime.TryParseExact(month, "MM/yyyy",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out DateTime dt))
                {
                    monthName = MapMonthName(dt.Month);
                }
                return $"AVANCE MENSUAL {monthName}".Trim();
            }
            return $"AVANCE SEMANA {week}";
        }

        private static string MapMonthName(int month)
        {
            switch (month)
            {
                case  1: return "ENERO";
                case  2: return "FEBRERO";
                case  3: return "MARZO";
                case  4: return "ABRIL";
                case  5: return "MAYO";
                case  6: return "JUNIO";
                case  7: return "JULIO";
                case  8: return "AGOSTO";
                case  9: return "SETIEMBRE";
                case 10: return "OCTUBRE";
                case 11: return "NOVIEMBRE";
                case 12: return "DICIEMBRE";
                default: return string.Empty;
            }
        }

        private string BuildSheetNumber(Document doc, string docName)
        {
            string[] parts = docName.Split('-');
            if (parts.Length < 8) return docName;

            parts[1] = "CSL";

            var usedNumbers = new HashSet<string>(
                new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .Select(s => s.SheetNumber),
                StringComparer.OrdinalIgnoreCase);

            int seq = 1;
            string candidate;
            do
            {
                parts[7]  = seq.ToString("D3");
                candidate = string.Join("-", parts.Take(8));
                seq++;
            }
            while (usedNumbers.Contains(candidate));

            return candidate;
        }

        private static void SetSheetParam(ViewSheet sheet, string paramName, string value)
        {
            Parameter p = sheet.LookupParameter(paramName);
            if (p != null && !p.IsReadOnly && p.StorageType == StorageType.String)
                p.Set(value);
        }

        private static string GetConfigPath()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BimManagement", "Resources");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "config.json");
        }

        // ── Helpers de UI ──────────────────────────────────────────────────────

        private static void Log(string message)
        {
            var view = App.m_CreateReportSheetView;
            if (view == null) return;
            view.Dispatcher.Invoke(() => view.Log(message));
        }

        private static void UpdateProgress(int current, int total)
        {
            var view = App.m_CreateReportSheetView;
            if (view == null) return;
            view.Dispatcher.Invoke(() => view.SetProgress(current, total));
        }
    }
}

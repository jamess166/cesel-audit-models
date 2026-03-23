using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace BimManagement
{
    /// <summary>
    /// Detecta UT y especialidad desde el nombre del modelo, carga el Excel
    /// de partidas correspondiente y llena CSL-Unidad / CSL-Cantidad en cada
    /// elemento de la lista recibida.
    /// </summary>
    public class ReportParameterFiller
    {
        private readonly Document _doc;

        private const string ParamPoWbs      = "PO-WBS";
        private const string ParamCslUnidad  = "CSL-Unidad";
        private const string ParamCslCantidad = "CSL-Cantidad";

        // Especialidades que se redirigen a ISS
        private static readonly HashSet<string> IssSpecialties =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "ACI", "AGU", "DES", "DRE" };

        public ReportParameterFiller(Document doc)
        {
            _doc = doc;
        }

        // ── Punto de entrada ─────────────────────────────────────────────────

        public void Fill(IEnumerable<Element> elements)
        {
            // 1. Detectar UT y especialidad desde el nombre del archivo
            string docName = Path.GetFileNameWithoutExtension(_doc.Title);
            if (!TryParseDocumentName(docName, out string ut, out string specialty))
                throw new InvalidOperationException(
                    $"No se pudo detectar UT ni especialidad del nombre de archivo: '{docName}'\n" +
                    "Se espera el formato: CODIGO-XXX-UTn-XXX-XXX-XXX-ESPECIALIDAD-Nombre");

            // 2. Construir ruta del Excel
            string dllFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string excelPath = Path.Combine(dllFolder, "Resources", "Partidas", ut, $"{ut}-{specialty}.xlsx");

            // 3. Cargar partidas; para UT6 con ARQ o EST, cargar también el fallback del otro
            Dictionary<string, ReportExcelReader.PartidaData> partidas =
                ReportExcelReader.Load(excelPath);

            Dictionary<string, ReportExcelReader.PartidaData> partidasFallback = null;
            if (ut.Equals("UT6", StringComparison.OrdinalIgnoreCase))
            {
                string fallbackSpecialty = specialty.Equals("ARQ", StringComparison.OrdinalIgnoreCase)
                    ? "EST" : specialty.Equals("EST", StringComparison.OrdinalIgnoreCase)
                    ? "ARQ" : null;

                if (fallbackSpecialty != null)
                {
                    string fallbackPath = Path.Combine(dllFolder, "Resources", "Partidas",
                        ut, $"{ut}-{fallbackSpecialty}.xlsx");
                    if (File.Exists(fallbackPath))
                        partidasFallback = ReportExcelReader.Load(fallbackPath);
                }
            }

            // 4. Llenar parámetros en cada elemento
            int filled  = 0;
            int skipped = 0;

            foreach (Element elem in elements)
            {
                string wbs = GetStringParam(elem, ParamPoWbs);
                if (string.IsNullOrEmpty(wbs)) { skipped++; continue; }

                // Buscar en el Excel principal; si no hay, intentar el fallback (solo UT6 ARQ↔EST)
                if (!partidas.TryGetValue(wbs, out ReportExcelReader.PartidaData partida))
                {
                    if (partidasFallback == null ||
                        !partidasFallback.TryGetValue(wbs, out partida))
                    { skipped++; continue; }
                }

                // CSL-Unidad
                SetStringParam(elem, ParamCslUnidad, partida.Unidad);

                // CSL-Cantidad: redondeado a 3 decimales
                double? cantidad = ResolveQuantity(elem, partida.Unidad, partida.Factor);
                if (cantidad.HasValue)
                    SetDoubleParam(elem, ParamCslCantidad, Math.Round(cantidad.Value, 3));

                filled++;
            }

            // Resumen diagnóstico (opcional, se puede quitar en producción)
            if (skipped > 0)
            {
                Autodesk.Revit.UI.TaskDialog.Show("CSL - Reporte",
                    $"Elementos llenados : {filled}\n" +
                    $"Elementos omitidos : {skipped} (sin PO-WBS o sin partida en el Excel)");
            }
        }

        // ── Detección de nombre de archivo ───────────────────────────────────

        /// <summary>
        /// Parsea el nombre del archivo con estructura:
        ///   [0]-[1]-[2:UT]-[3]-[4]-[5]-[6:ESPECIALIDAD]-[7...]
        /// </summary>
        private bool TryParseDocumentName(string name, out string ut, out string specialty)
        {
            ut = null; specialty = null;

            string[] parts = name.Split('-');
            if (parts.Length < 7) return false;

            ut = parts[2].Trim();
            string rawSpecialty = parts[6].Trim();
            specialty = IssSpecialties.Contains(rawSpecialty) ? "ISS" : rawSpecialty;

            return !string.IsNullOrEmpty(ut) && !string.IsNullOrEmpty(specialty);
        }

        // ── Resolución de cantidad ────────────────────────────────────────────

        private double? ResolveQuantity(Element elem, string unidad, double factor)
        {
            double? raw = null;

            if      (IsUnit(unidad, "und"))  raw = 1.0;
            else if (IsUnit(unidad, "m2"))   raw = GetArea(elem);
            else if (IsUnit(unidad, "m3"))   raw = GetVolume(elem);
            else if (IsUnit(unidad, "m"))    raw = GetLength(elem);

            if (!raw.HasValue) return null;

            return raw.Value * factor;
        }

        private static bool IsUnit(string unidad, string expected)
            => string.Equals(unidad?.Trim(), expected, StringComparison.OrdinalIgnoreCase);

        // ── Obtención de área ─────────────────────────────────────────────────

        private double? GetArea(Element elem)
        {
            // Parámetros built-in más comunes para área
            var bips = new[]
            {
                BuiltInParameter.HOST_AREA_COMPUTED,
                BuiltInParameter.STRUCTURAL_SECTION_AREA,
                BuiltInParameter.SURFACE_AREA,
                BuiltInParameter.ROOM_AREA,
            };

            double? val = TryBuiltIns(elem, bips);
            if (val.HasValue) return ToSquareMeters(val.Value);

            // Búsqueda por nombre
            val = FindParamByNames(elem, "area", "área", "surface area", "área neta");
            if (val.HasValue) return ToSquareMeters(val.Value);

            return null;
        }

        // ── Obtención de volumen ──────────────────────────────────────────────

        private double? GetVolume(Element elem)
        {
            var bips = new[]
            {
                BuiltInParameter.HOST_VOLUME_COMPUTED,
            };

            double? val = TryBuiltIns(elem, bips);
            if (val.HasValue) return ToCubicMeters(val.Value);

            val = FindParamByNames(elem, "volume", "volumen", "vol", "solid volume");
            if (val.HasValue) return ToCubicMeters(val.Value);

            return null;
        }

        // ── Obtención de longitud ─────────────────────────────────────────────

        private double? GetLength(Element elem)
        {
            //var bips = new[]
            //{
            //    BuiltInParameter.CURVE_ELEM_LENGTH,
            //    BuiltInParameter.INSTANCE_LENGTH_PARAM,
            //    BuiltInParameter.STRUCTURAL_FRAME_CUT_LENGTH,
            //};

            //double? val = TryBuiltIns(elem, bips);
            //if (val.HasValue) return ToMeters(val.Value);

            //val = FindParamByNames(elem, "length", "longitud", "len", "largo");
            //if (val.HasValue) return ToMeters(val.Value);

            //return null;
            // 1. Intentar por geometría (mejor opción)
            var locCurve = elem.Location as LocationCurve;
            if (locCurve != null)
                return ToMeters(locCurve.Curve.Length);

            // 2. Intentar BuiltInParameters
            var bips = new[]
            {
                BuiltInParameter.CURVE_ELEM_LENGTH,
                BuiltInParameter.INSTANCE_LENGTH_PARAM,
                BuiltInParameter.STRUCTURAL_FRAME_CUT_LENGTH,
            };

            double? val = TryBuiltIns(elem, bips);
            if (val.HasValue) return ToMeters(val.Value);

            // 3. Buscar por nombre
            val = FindParamByNames(elem, "length", "longitud", "len", "largo");
            if (val.HasValue) return ToMeters(val.Value);

            return null;
        }

        // ── Helpers de parámetros ─────────────────────────────────────────────

        private static double? TryBuiltIns(Element elem, BuiltInParameter[] bips)
        {
            foreach (var bip in bips)
            {
                try
                {
                    Parameter p = elem.get_Parameter(bip);
                    if (p != null && p.HasValue && p.StorageType == StorageType.Double)
                        return p.AsDouble();
                }
                catch { }
            }
            return null;
        }

        private static double? FindParamByNames(Element elem, params string[] names)
        {
            foreach (Parameter p in elem.Parameters)
            {
                if (p.StorageType != StorageType.Double || !p.HasValue) continue;

                string pName = p.Definition?.Name;
                if (string.IsNullOrEmpty(pName)) continue;

                foreach (string name in names)
                {
                    if (pName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                        return p.AsDouble();
                }
            }
            return null;
        }

        private static string GetStringParam(Element elem, string name)
        {
            Parameter p = elem.LookupParameter(name);
            if (p == null || !p.HasValue) return null;
            return p.StorageType == StorageType.String
                ? p.AsString()?.Trim()
                : p.AsValueString()?.Trim();
        }

        private static void SetStringParam(Element elem, string name, string value)
        {
            try
            {
                Parameter p = elem.LookupParameter(name);
                if (p != null && p.StorageType == StorageType.String)
                    p.Set(value ?? string.Empty);
            }
            catch (Exception) { }
        }

        private static void SetDoubleParam(Element elem, string name, double value)
        {
            try
            {
                Parameter p = elem.LookupParameter(name);
                if (p != null && p.StorageType == StorageType.Double)
                    p.Set(value);
            }
            catch (Exception) { }           
        }

        // ── Conversión de unidades internas de Revit (pies) ──────────────────

        private static double ToSquareMeters(double sqFt)
            => UnitUtils.ConvertFromInternalUnits(sqFt, UnitTypeId.SquareMeters);

        private static double ToCubicMeters(double cuFt)
            => UnitUtils.ConvertFromInternalUnits(cuFt, UnitTypeId.CubicMeters);

        private static double ToMeters(double ft)
            => UnitUtils.ConvertFromInternalUnits(ft, UnitTypeId.Meters);
    }
}

using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BimManagement
{
    /// <summary>
    /// Recopila los elementos (o sus partes) que deben incluirse en el reporte,
    /// según el modo semanal o mensual.
    ///
    /// Regla de partes:
    ///   Si un elemento tiene partes Y alguna de esas partes cumple el criterio
    ///   → se toman las partes coincidentes y se descarta el elemento padre.
    ///   Si ninguna parte cumple (o el elemento no tiene partes)
    ///   → se evalúa el elemento directamente.
    /// </summary>
    public class ReportElementFilter
    {
        private readonly Document _doc;
        private readonly bool     _isMonthly;
        private readonly string   _weekValue;   // ej. "45"
        private readonly string   _monthValue;  // ej. "03/2025"

        private const string ParamPoCars      = "PO-CARS";
        private const string ParamPoFechaConst = "PO-FECHA CONSTRUIDA"; // formato dd/MM/yyyy

        public ReportElementFilter(Document doc, bool isMonthly, string weekValue, string monthValue)
        {
            _doc        = doc;
            _isMonthly  = isMonthly;
            _weekValue  = weekValue?.Trim();
            _monthValue = monthValue?.Trim();
        }

        // ── Punto de entrada ─────────────────────────────────────────────────

        /// <summary>
        /// Devuelve la lista de elementos (o partes) que participan en el reporte.
        /// </summary>
        public List<Element> Collect()
        {
            return _isMonthly ? CollectByMonth() : CollectByWeek();
        }

        // ── Modo semanal ──────────────────────────────────────────────────────

        private List<Element> CollectByWeek()
        {
            if (string.IsNullOrEmpty(_weekValue))
                return new List<Element>();

            var result = new List<Element>();

            foreach (Element elem in GetModelElements())
            {
                if (TryGetMatchingParts(elem, MatchesWeek, out List<Element> parts))
                {
                    result.AddRange(parts);   // partes coincidentes → sustituyen al padre
                }
                else if (MatchesWeek(elem))
                {
                    result.Add(elem);
                }
            }

            return result;
        }

        private bool MatchesWeek(Element elem)
        {
            string val = GetStringValue(elem, ParamPoCars);
            return !string.IsNullOrEmpty(val) &&
                   string.Equals(val, _weekValue, StringComparison.OrdinalIgnoreCase);
        }

        // ── Modo mensual ──────────────────────────────────────────────────────

        private List<Element> CollectByMonth()
        {
            if (string.IsNullOrEmpty(_monthValue))
                return new List<Element>();

            // Parsear el mes objetivo desde "MM/yyyy"
            if (!DateTime.TryParseExact(_monthValue, "MM/yyyy",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime target))
                return new List<Element>();

            var result = new List<Element>();

            foreach (Element elem in GetModelElements())
            {
                if (TryGetMatchingParts(elem, e => MatchesMonth(e, target), out List<Element> parts))
                {
                    result.AddRange(parts);
                }
                else if (MatchesMonth(elem, target))
                {
                    result.Add(elem);
                }
            }

            return result;
        }

        private bool MatchesMonth(Element elem, DateTime target)
        {
            string val = GetStringValue(elem, ParamPoFechaConst);
            if (string.IsNullOrEmpty(val)) return false;

            if (!DateTime.TryParseExact(val, "dd/MM/yyyy",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime fecha))
                return false;

            return fecha.Month == target.Month && fecha.Year == target.Year;
        }

        // ── Lógica de partes ──────────────────────────────────────────────────

        /// <summary>
        /// Si el elemento tiene partes que cumplan el predicado, las devuelve en
        /// <paramref name="matchingParts"/> y retorna true.
        /// Si no hay partes coincidentes (o el elemento no tiene partes), retorna false.
        /// </summary>
        private bool TryGetMatchingParts(Element elem, Func<Element, bool> predicate,
                                         out List<Element> matchingParts)
        {
            matchingParts = new List<Element>();

            if (!PartUtils.HasAssociatedParts(_doc, elem.Id))
                return false;

            ICollection<ElementId> partIds = PartUtils.GetAssociatedParts(
                _doc, elem.Id, includePartsWithAssociatedParts: true, includeAllChildren: false);

            foreach (ElementId id in partIds)
            {
                Element part = _doc.GetElement(id);
                if (part != null && predicate(part))
                    matchingParts.Add(part);
            }

            return matchingParts.Count > 0;
        }

        // ── Colector de elementos del modelo ─────────────────────────────────

        private IEnumerable<Element> GetModelElements()
        {
            //return new FilteredElementCollector(_doc)
            //    .WhereElementIsNotElementType()
            //    .Where(e => e.Category != null
            //             && e.Category.CategoryType == CategoryType.Model
            //             && e.Category.HasMaterialQuantities);
            var builtInCats = new List<BuiltInCategory>
            {
                BuiltInCategory.OST_Walls,
                BuiltInCategory.OST_Floors,
                BuiltInCategory.OST_Ceilings,
                BuiltInCategory.OST_Roofs,
                BuiltInCategory.OST_Columns,
                BuiltInCategory.OST_StructuralColumns,
                BuiltInCategory.OST_StructuralFraming,
                BuiltInCategory.OST_StructuralFoundation,
                BuiltInCategory.OST_Stairs,
                BuiltInCategory.OST_Ramps,
                BuiltInCategory.OST_Doors,
                BuiltInCategory.OST_Windows,
                BuiltInCategory.OST_GenericModel,
                BuiltInCategory.OST_Furniture,
                BuiltInCategory.OST_FurnitureSystems,
                BuiltInCategory.OST_Casework,
                BuiltInCategory.OST_Railings,
                BuiltInCategory.OST_StairsRailing,
                BuiltInCategory.OST_Topography,
                BuiltInCategory.OST_Site,
                BuiltInCategory.OST_Parking,
                BuiltInCategory.OST_Roads,
                BuiltInCategory.OST_Planting,
                BuiltInCategory.OST_MechanicalEquipment,
                BuiltInCategory.OST_ElectricalEquipment,
                BuiltInCategory.OST_ElectricalFixtures,
                BuiltInCategory.OST_LightingFixtures,
                BuiltInCategory.OST_PlumbingFixtures,
                BuiltInCategory.OST_PipeCurves,
                BuiltInCategory.OST_DuctCurves,
                BuiltInCategory.OST_CableTray,
                BuiltInCategory.OST_Conduit,
                BuiltInCategory.OST_StructuralStiffener,
                BuiltInCategory.OST_Entourage,
                BuiltInCategory.OST_Mass,
                BuiltInCategory.OST_CurtainWallMullions,
                BuiltInCategory.OST_CurtainWallPanels,
                BuiltInCategory.OST_SpecialityEquipment,
                BuiltInCategory.OST_Parts
            };

            var filter = new ElementMulticategoryFilter(builtInCats);

            return new FilteredElementCollector(_doc)
                .WhereElementIsNotElementType()
                .WherePasses(filter);
        }

        // ── Helper de parámetros ──────────────────────────────────────────────

        private static string GetStringValue(Element elem, string paramName)
        {
            Parameter p = elem.LookupParameter(paramName);
            if (p == null || !p.HasValue) return null;

            return p.StorageType == StorageType.String
                ? p.AsString()?.Trim()
                : p.AsValueString()?.Trim();
        }
    }
}

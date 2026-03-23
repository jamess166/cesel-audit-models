using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BimManagement
{
    /// <summary>
    /// Crea dos vistas 3D isométricas con sus filtros de vista correspondientes:
    ///   CSL-V3D-AVANCE SEMANA/MENSUAL {periodo}            → filtro PO-EJECUTADO ACTUAL
    ///   CSL-V3D-AVANCE SEMANA/MENSUAL {periodo} Acumulado  → filtro PO-EJECUTADO ACUMULADO
    /// </summary>
    public class Report3DViewCreator
    {
        private readonly Document _doc;
        private readonly bool     _isMonthly;
        private readonly string   _weekValue;
        private readonly string   _monthValue;

        // Nombres de filtro incluyen el tag para ser únicos por semana/mes
        private string FilterActual      => $"PO-CSL-EJECUTADO ACTUAL {_tag}";
        private string FilterAcumulado   => $"PO-CSL-EJECUTADO ACUMULADO {_tag}";
        private string FilterAnterior    => $"PO-CSL-EJECUTADO ANTERIOR {_tag}";
        private string FilterNoEjecutado => $"PO-CSL-NO EJECUTADO {_tag}";

        private string _tag => _isMonthly ? $"MENSUAL {_monthValue}" : $"SEMANA {_weekValue}";

        private const string ParamPoCars      = "PO-CARS";
        private const string ParamPoFecha     = "PO-FECHA CONSTRUIDA";
        private const string ParamPoEstado    = "PO-ESTADO CONSTRUCCION";

        public Report3DViewCreator(Document doc, bool isMonthly,
                                   string weekValue, string monthValue)
        {
            _doc        = doc;
            _isMonthly  = isMonthly;
            _weekValue  = weekValue?.Trim();
            _monthValue = monthValue?.Trim();
        }

        // ── Punto de entrada ─────────────────────────────────────────────────

        public void Create()
        {
            string tag    = _isMonthly ? $"MENSUAL {_monthValue}" : $"SEMANA {_weekValue}";
            string nameActual    = $"CSL-V3D-AVANCE {tag}";
            string nameAcumulado = $"CSL-V3D-AVANCE {tag} Acumulado";

            ViewFamilyType vft = GetThreeDViewFamilyType();

            // Categorías obtenidas desde los bindings reales de cada parámetro
            IList<ElementId> catEstado = GetBoundCategories(ParamPoEstado);
            IList<ElementId> catCars   = GetBoundCategories(ParamPoCars);
            IList<ElementId> catFecha  = GetBoundCategories(ParamPoFecha);

            // Filtro Anterior:
            //   semanal  → PO-ESTADO + PO-CARS
            //   mensual  → PO-ESTADO + PO-FECHA CONSTRUIDA
            IList<ElementId> catAnterior = _isMonthly
                ? Intersect(catEstado, catFecha)
                : Intersect(catEstado, catCars);

            // Filtro Actual: semanal usa PO-CARS, mensual usa PO-FECHA CONSTRUIDA
            IList<ElementId> catActual = _isMonthly ? catFecha : catCars;

            // Filtros de vista (crear o actualizar si ya existen)
            ParameterFilterElement pfActual      = EnsureFilter(FilterActual,      BuildRulesActual(),      catActual);
            ParameterFilterElement pfAcumulado   = EnsureFilter(FilterAcumulado,   BuildRulesAcumulado(),   catEstado);
            ParameterFilterElement pfAnterior    = EnsureFilter(FilterAnterior,    BuildRulesAnterior(),    catAnterior);
            ParameterFilterElement pfNoEjecutado = EnsureFilter(FilterNoEjecutado, BuildRulesNoEjecutado(), catEstado);

            // Overrides de color
            ElementId solidFill = GetSolidFillPatternId();

            OverrideGraphicSettings ogsNoEjecutado      = BuildOverrides(
                solidFill, new Color(180, 180, 180), transparency: 90);

            OverrideGraphicSettings ogsActual            = BuildOverrides(
                solidFill, new Color(0, 128, 64),   transparency: 0);

            OverrideGraphicSettings ogsAcumulado         = BuildOverrides(
                solidFill, new Color(0, 0, 255),    transparency: 0);

            OverrideGraphicSettings ogsAnterior = BuildOverrides(
                solidFill, new Color(80, 80, 80), transparency: 0); // gris oscuro

            // Vistas 3D
            View3D viewActual    = CreateOrReplaceView(nameActual,    vft);
            View3D viewAcumulado = CreateOrReplaceView(nameAcumulado, vft);

            // Vista Actual (semanal): anterior gris oscuro + actual verde + no ejecutado transparente
            ApplyFilter(viewActual, pfAnterior,    ogsAnterior);
            ApplyFilter(viewActual, pfActual,      ogsActual);
            ApplyFilter(viewActual, pfNoEjecutado, ogsNoEjecutado);

            // Vista Acumulado: acumulado azul + no ejecutado gris transparente
            ApplyFilter(viewAcumulado, pfAcumulado,   ogsAcumulado);
            ApplyFilter(viewAcumulado, pfNoEjecutado, ogsNoEjecutado);
        }


        // ── Reglas de filtro ──────────────────────────────────────────────────

        private ElementFilter BuildRulesActual()
        {
            if (_isMonthly)
            {
                // PO-FECHA CONSTRUIDA contiene "MM/yyyy"
                FilterRule rule = ParameterFilterRuleFactory.CreateContainsRule(
                    GetParameterId(ParamPoFecha), _monthValue, false);
                return new ElementParameterFilter(rule);
            }
            else
            {
                // PO-CARS == número de semana (detecta tipo INTEGER automáticamente)
                FilterRule rule = BuildEqualityRule(ParamPoCars, _weekValue, false);
                return new ElementParameterFilter(rule);
            }
        }

        private ElementFilter BuildRulesAcumulado()
        {
            // PO-ESTADO CONSTRUCCION == "Ejecutado"  (igual para semanal y mensual)
            FilterRule rule = ParameterFilterRuleFactory.CreateEqualsRule(
                GetParameterId(ParamPoEstado), "Ejecutado", false);
            return new ElementParameterFilter(rule);
        }

        private ElementFilter BuildRulesAnterior()
        {
            ElementFilter ejecutado = new ElementParameterFilter(
                ParameterFilterRuleFactory.CreateEqualsRule(
                    GetParameterId(ParamPoEstado), "Ejecutado", false));

            ElementFilter noPeriodoActual;

            if (_isMonthly)
            {
                // Mensual: PO-FECHA CONSTRUIDA no contiene el mes actual
                noPeriodoActual = new ElementParameterFilter(
                    ParameterFilterRuleFactory.CreateNotContainsRule(
                        GetParameterId(ParamPoFecha), _monthValue, false));
            }
            else
            {
                // Semanal: PO-CARS distinto de la semana actual (detecta tipo INTEGER)
                noPeriodoActual = new ElementParameterFilter(
                    BuildEqualityRule(ParamPoCars, _weekValue, negate: true));
            }

            return new LogicalAndFilter(new List<ElementFilter> { ejecutado, noPeriodoActual });
        }

        private ElementFilter BuildRulesNoEjecutado()
        {
            // Sin valor en PO-ESTADO CONSTRUCCION  O  distinto de "Ejecutado"
            ElementId paramId = GetParameterId(ParamPoEstado);

            ElementFilter sinValor    = new ElementParameterFilter(
                ParameterFilterRuleFactory.CreateHasNoValueParameterRule(paramId));

            ElementFilter noEjecutado = new ElementParameterFilter(
                ParameterFilterRuleFactory.CreateNotEqualsRule(paramId, "Ejecutado", false));

            return new LogicalOrFilter(new List<ElementFilter> { sinValor, noEjecutado });
        }

        // ── Filtros de vista ──────────────────────────────────────────────────

        private ParameterFilterElement EnsureFilter(string name,
            ElementFilter elementFilter, IList<ElementId> catIds)
        {
            // Eliminar si ya existe (mismo tag, re-ejecución)
            ParameterFilterElement existing = new FilteredElementCollector(_doc)
                .OfClass(typeof(ParameterFilterElement))
                .Cast<ParameterFilterElement>()
                .FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
                _doc.Delete(existing.Id);

            if (catIds.Count == 0)
                throw new InvalidOperationException(
                    $"Sin categorías de modelo vinculadas para crear el filtro '{name}'.");

            ParameterFilterElement pfe = ParameterFilterElement.Create(_doc, name, catIds);
            pfe.SetElementFilter(elementFilter);
            return pfe;
        }

        private static void ApplyFilter(View3D view, ParameterFilterElement filter,
                                        OverrideGraphicSettings ogs)
        {
            if (!view.IsFilterApplied(filter.Id))
                view.AddFilter(filter.Id);

            view.SetFilterVisibility(filter.Id, true);
            view.SetFilterOverrides(filter.Id, ogs);
        }

        private static OverrideGraphicSettings BuildOverrides(
            ElementId solidFillId, Color color, int transparency)
        {
            var ogs = new OverrideGraphicSettings();

            // Superficie
            if (solidFillId != ElementId.InvalidElementId)
            {
                ogs.SetSurfaceForegroundPatternId(solidFillId);
                ogs.SetSurfaceForegroundPatternColor(color);
                ogs.SetSurfaceForegroundPatternVisible(true);

                ogs.SetCutForegroundPatternId(solidFillId);
                ogs.SetCutForegroundPatternColor(color);
                ogs.SetCutForegroundPatternVisible(true);
            }

            ogs.SetSurfaceTransparency(transparency);

            // Líneas de proyección y corte
            ogs.SetProjectionLineColor(color);

            return ogs;
        }

        private ElementId GetSolidFillPatternId()
        {
            FillPatternElement solidFill = new FilteredElementCollector(_doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .FirstOrDefault(fp => fp.GetFillPattern().IsSolidFill);

            return solidFill?.Id ?? ElementId.InvalidElementId;
        }

        // ── Vistas 3D ─────────────────────────────────────────────────────────

        private View3D CreateOrReplaceView(string name, ViewFamilyType vft)
        {
            View3D existing = new FilteredElementCollector(_doc)
                .OfClass(typeof(View3D))
                .Cast<View3D>()
                .FirstOrDefault(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
                _doc.Delete(existing.Id);

            View3D view = View3D.CreateIsometric(_doc, vft.Id);
            view.Name            = name;
            view.PartsVisibility = PartsVisibility.ShowPartsOnly;
            view.DisplayStyle    = DisplayStyle.Shading; // Shaded sin bordes (Show Edges OFF)

            // Ocultar anotaciones de grillas y niveles
            HideCategoryInView(view, BuiltInCategory.OST_Grids);
            HideCategoryInView(view, BuiltInCategory.OST_Levels);

            // Desactivar "Show imported categories" → ocultar todos los DWG importados
            HideImportedCategories(view);

            return view;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private ViewFamilyType GetThreeDViewFamilyType()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(v => v.ViewFamily == ViewFamily.ThreeDimensional)
                ?? throw new InvalidOperationException(
                    "No se encontró un tipo de vista 3D en el proyecto.");
        }

        /// <summary>
        /// Devuelve las categorías de modelo (CategoryType.Model) a las que está
        /// vinculado el parámetro. Se excluyen categorías internas/anotación/analíticas
        /// que no admiten filtros de vista aunque el parámetro esté asignado a ellas.
        /// </summary>
        private IList<ElementId> GetBoundCategories(string paramName)
        {
            BindingMap bm = _doc.ParameterBindings;
            DefinitionBindingMapIterator it = bm.ForwardIterator();
            while (it.MoveNext())
            {
                if (!string.Equals(it.Key?.Name, paramName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (it.Current is InstanceBinding ib)
                    return ib.Categories
                              .Cast<Category>()
                              .Where(c => c.CategoryType == CategoryType.Model)
                              .Select(c => c.Id)
                              .ToList();
            }

            // El parámetro no está vinculado
            return new List<ElementId>();
        }

        /// <summary>
        /// Intersección de dos listas de ElementId.
        /// </summary>
        private static IList<ElementId> Intersect(IList<ElementId> a, IList<ElementId> b)
        {
            var setB = new HashSet<ElementId>(b);
            return a.Where(id => setB.Contains(id)).ToList();
        }

        /// <summary>
        /// Desactiva "Show imported categories in this View" en la vista.
        /// </summary>
        private static void HideImportedCategories(View3D view)
        {
            view.AreImportCategoriesHidden = true;
        }

        private void HideCategoryInView(View3D view, BuiltInCategory cat)
        {
            ElementId catId = new ElementId(cat);
            if (view.CanCategoryBeHidden(catId))
                view.SetCategoryHidden(catId, true);
        }

        private ElementId GetParameterId(string paramName)
        {
            return GetParamInfo(paramName).Id;
        }

        /// <summary>
        /// Obtiene el Id y la definición de un parámetro vinculado al documento,
        /// buscando directamente en el mapa de ParameterBindings para garantizar
        /// consistencia con GetBoundCategories.
        /// </summary>
        private (ElementId Id, InternalDefinition Def) GetParamInfo(string paramName)
        {
            BindingMap bm = _doc.ParameterBindings;
            DefinitionBindingMapIterator it = bm.ForwardIterator();
            while (it.MoveNext())
            {
                if (!string.Equals(it.Key?.Name, paramName, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (it.Key is InternalDefinition internalDef)
                    return (internalDef.Id, internalDef);
            }

            throw new InvalidOperationException(
                $"No se encontró el parámetro '{paramName}' en el documento.");
        }

        /// <summary>
        /// Crea una regla de igualdad o desigualdad usando el overload correcto
        /// según el tipo del parámetro (INTEGER vs TEXT). Usa ForgeTypeId (API moderna).
        /// </summary>
        private FilterRule BuildEqualityRule(string paramName, string value, bool negate)
        {
            var (paramId, def) = GetParamInfo(paramName);

            // ForgeTypeId es la forma moderna de detectar el tipo de parámetro
            ForgeTypeId dataType = def.GetDataType();

            // Parámetro entero (ej. número de semana almacenado como INTEGER)
            if (dataType == SpecTypeId.Int.Integer && int.TryParse(value, out int intVal))
                return negate
                    ? ParameterFilterRuleFactory.CreateNotEqualsRule(paramId, intVal)
                    : ParameterFilterRuleFactory.CreateEqualsRule(paramId, intVal);

            // Texto (default)
            return negate
                ? ParameterFilterRuleFactory.CreateNotEqualsRule(paramId, value, false)
                : ParameterFilterRuleFactory.CreateEqualsRule(paramId, value, false);
        }
    }
}

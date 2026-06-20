using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;

namespace BimManagement.Commands.PerimeterFence
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    internal class PerimeterFenceCommand : IExternalCommand
    {
        const string FamilyName      = "Columna_Cimentacion_Prefabricada";
        const string PanelFamilyName = "Tarjetas_Cerco";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            // --- Selección ---
            Curve axisCurve;
            IList<Reference> terrainRefs;
            try
            {
                Reference curveRef = uidoc.Selection.PickObject(
                    ObjectType.Element,
                    new CurveElementFilter(),
                    "Seleccione el eje del cerco (línea o arco del modelo)");

                CurveElement curveElem = doc.GetElement(curveRef) as CurveElement;
                if (curveElem == null)
                {
                    message = "El elemento seleccionado no es una curva válida.";
                    return Result.Failed;
                }
                axisCurve = curveElem.GeometryCurve;

                terrainRefs = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    "Seleccione los elementos de terreno (puede seleccionar varios)");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            // --- Validar familia ---
            FamilySymbol symbol = PerimeterFenceTools.FindFamilySymbol(doc, FamilyName);
            if (symbol == null)
            {
                TaskDialog.Show("Error",
                    $"No se encontró la familia '{FamilyName}' en el documento. " +
                    "Cargue la familia antes de ejecutar el comando.");
                return Result.Failed;
            }

            // --- Validar familia de panel ---
            FamilySymbol panelSymbol = PerimeterFenceTools.FindFamilySymbol(doc, PanelFamilyName);
            if (panelSymbol == null)
            {
                TaskDialog.Show("Error",
                    $"No se encontró la familia '{PanelFamilyName}' en el documento. " +
                    "Cargue la familia antes de ejecutar el comando.");
                return Result.Failed;
            }

            // --- Nivel de referencia ---
            Level referenceLevel = PerimeterFenceTools.FindLowestLevel(doc);
            if (referenceLevel == null)
            {
                TaskDialog.Show("Error", "No se encontró ningún nivel en el documento.");
                return Result.Failed;
            }

            // --- Geometría del terreno ---
            var terrainSolids = new List<Solid>();
            foreach (Reference r in terrainRefs)
            {
                Solid s = PerimeterFenceTools.GetSolidFromElement(doc.GetElement(r));
                if (s != null) terrainSolids.Add(s);
            }
            if (terrainSolids.Count == 0)
            {
                TaskDialog.Show("Error",
                    "No se pudo obtener geometría sólida de los elementos de terreno seleccionados.");
                return Result.Failed;
            }

            // --- Constantes configurables ---
            double spacing    = UnitUtils.ConvertToInternalUnits(2.13, UnitTypeId.Meters);
            double stepHeight = UnitUtils.ConvertToInternalUnits(5,    UnitTypeId.Centimeters);
            double trimAmount = UnitUtils.ConvertToInternalUnits(10,   UnitTypeId.Centimeters);

            // --- Calcular puntos y elevaciones ---
            List<XYZ> curvePoints = PerimeterFenceTools.GetPointsAlongCurve(
                axisCurve, spacing, trimAmount, trimAmount);

            // Elevation of the reference level in Revit internal units (feet)
            double levelElevation = referenceLevel.Elevation;

            // Phase 1 — sample solid surface elevation at every candidate point.
            var rawElevations = new double?[curvePoints.Count];
            for (int i = 0; i < curvePoints.Count; i++)
            {
                if (PerimeterFenceTools.TryGetElevationAtXY(
                        terrainSolids, curvePoints[i].X, curvePoints[i].Y, out double rawElev))
                    rawElevations[i] = rawElev;
            }

            // Phase 2 — fill gaps where the ray missed the solid by borrowing the nearest
            // valid neighbour elevation (search backward first, then forward).
            for (int i = 0; i < rawElevations.Length; i++)
            {
                if (rawElevations[i].HasValue) continue;

                for (int j = i - 1; j >= 0 && !rawElevations[i].HasValue; j--)
                    if (rawElevations[j].HasValue) rawElevations[i] = rawElevations[j];

                for (int j = i + 1; j < rawElevations.Length && !rawElevations[i].HasValue; j++)
                    if (rawElevations[j].HasValue) rawElevations[i] = rawElevations[j];
            }

            // Phase 2b — constrain each solid-surface elevation to the minimum of its
            // immediate neighbours' solid-surface elevations.
            //
            // Why this eliminates the floating-panel problem:
            //   The panel between column i and i+1 sits at max(z_i, z_{i+1}).
            //   For that max to be embedded at BOTH endpoints we need:
            //     z_i  ≤ solidZ_{i+1}   (so the higher column doesn't float at i+1's XY)
            //     z_{i+1} ≤ solidZ_i    (so the higher column doesn't float at i's XY)
            //   Constraining z_i ≤ min(solidZ_{i-1}, solidZ_{i+1}) satisfies both conditions
            //   for every adjacent pair, without any iteration.
            var solidSnapshot = (double?[])rawElevations.Clone(); // unmodified solid surfaces
            for (int i = 0; i < rawElevations.Length; i++)
            {
                if (!rawElevations[i].HasValue) continue;
                double ceiling = rawElevations[i].Value;
                if (i > 0 && solidSnapshot[i - 1].HasValue)
                    ceiling = Math.Min(ceiling, solidSnapshot[i - 1].Value);
                if (i < rawElevations.Length - 1 && solidSnapshot[i + 1].HasValue)
                    ceiling = Math.Min(ceiling, solidSnapshot[i + 1].Value);
                rawElevations[i] = ceiling;
            }

            // Phase 3 — floor every constrained elevation to the nearest 5 cm step and
            // build the final placements list. Column height calculation runs after this.
            var placements = new List<(XYZ xy, double offsetFromLevel)>();

            for (int i = 0; i < curvePoints.Count; i++)
            {
                if (!rawElevations[i].HasValue) continue;

                double rawOffset = rawElevations[i].Value - levelElevation;
                // Math.Floor ensures the element always ends up inside the solid
                // rather than floating above it.
                double quantizedOffset = Math.Floor(rawOffset / stepHeight) * stepHeight;

                XYZ pt = curvePoints[i];
                // Revit interprets XYZ.Z as the offset FROM the reference level, not as an
                // absolute coordinate. Passing quantizedOffset here positions the instance at
                // level + quantizedOffset = terrain surface elevation.
                placements.Add((new XYZ(pt.X, pt.Y, quantizedOffset), quantizedOffset));
            }

            if (placements.Count == 0)
            {
                TaskDialog.Show("Aviso",
                    "No se generaron puntos de colocación. " +
                    "Verifique que la curva se superponga al elemento de terreno seleccionado.");
                return Result.Cancelled;
            }

            // --- Calcular altura de cada columna ---
            // Rule: the top of column i must clear 4 m above the higher of the two adjacent
            // base elevations.  ColumnHeight(i) = 4 m + max(0, Offset(i+1) − Offset(i)).
            // Last column: its top must clear 4 m above the highest base in the entire sequence.
            double minClearance = UnitUtils.ConvertToInternalUnits(4.0, UnitTypeId.Meters);

            double maxOffset = double.MinValue;
            foreach (var (_, off) in placements)
                if (off > maxOffset) maxOffset = off;

            var heights = new double[placements.Count];
            for (int i = 0; i < placements.Count - 1; i++)
                heights[i] = minClearance
                    + Math.Max(0.0, placements[i + 1].offsetFromLevel - placements[i].offsetFromLevel);
            heights[placements.Count - 1] =
                maxOffset + minClearance - placements[placements.Count - 1].offsetFromLevel;

            // --- Insertar instancias en una única transacción ---
            using (Transaction tx = new Transaction(doc, "Colocar cerco prefabricado"))
            {
                tx.Start();

                if (!symbol.IsActive)
                {
                    symbol.Activate();
                    doc.Regenerate();
                }

                for (int i = 0; i < placements.Count; i++)
                {
                    var (xy, offsetFromLevel) = placements[i];

                    // xy.Z encodes the offset from the reference level (confirmed behaviour:
                    // Revit does NOT treat it as an absolute Z). The instance is therefore
                    // placed at level.Elevation + xy.Z = terrain surface elevation.
                    FamilyInstance fi = doc.Create.NewFamilyInstance(
                        xy, symbol, referenceLevel, StructuralType.NonStructural);

                    // Belt-and-suspenders: also set the parameter explicitly in case the
                    // auto-value differs (e.g. some family hosting modes).
                    Parameter elevParam =
                        fi.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM)
                        ?? fi.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM);

                    if (elevParam != null && !elevParam.IsReadOnly)
                        elevParam.Set(offsetFromLevel);

                    Parameter heightParam = fi.LookupParameter("Altura Columna");
                    if (heightParam != null && !heightParam.IsReadOnly)
                        heightParam.Set(heights[i]);
                }

                // --- Panels (Tarjetas_Cerco): one between each pair of adjacent columns ---
                if (!panelSymbol.IsActive)
                {
                    panelSymbol.Activate();
                    doc.Regenerate();
                }

                for (int i = 0; i < placements.Count - 1; i++)
                {
                    // Both endpoints share the higher Z of the two adjacent column bases
                    // so the panel remains horizontal.
                    double panelZ = Math.Max(placements[i].xy.Z, placements[i + 1].xy.Z);
                    XYZ ptA = new XYZ(placements[i].xy.X,     placements[i].xy.Y,     panelZ);
                    XYZ ptB = new XYZ(placements[i + 1].xy.X, placements[i + 1].xy.Y, panelZ);
                    Line panelLine = Line.CreateBound(ptA, ptB);

                    FamilyInstance panel = doc.Create.NewFamilyInstance(
                        panelLine, panelSymbol, referenceLevel, StructuralType.NonStructural);

                    Parameter panelOffsetParam =
                        panel.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM)
                        ?? panel.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM);

                    if (panelOffsetParam != null && !panelOffsetParam.IsReadOnly)
                        panelOffsetParam.Set(panelZ);
                }

                tx.Commit();
            }

            return Result.Succeeded;
        }

        private class CurveElementFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is CurveElement;
            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }
}

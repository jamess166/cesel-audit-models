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

            // --- Dirección de inicio ---
            TaskDialog dirDialog = new TaskDialog("Dirección del cerco");
            dirDialog.MainInstruction = "¿Desde qué extremo inicia la secuencia de columnas?";
            dirDialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Derecha", "Inicia desde el extremo actual de la curva (comportamiento por defecto).");
            dirDialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Izquierda", "Invierte el punto de inicio: la secuencia parte desde el extremo opuesto.");
            TaskDialogResult dirResult = dirDialog.Show();
            if (dirResult == TaskDialogResult.Close) return Result.Cancelled;
            bool reversed = dirResult == TaskDialogResult.CommandLink2;

            // --- Constantes configurables ---
            double spacing    = UnitUtils.ConvertToInternalUnits(2.13, UnitTypeId.Meters);
            double stepHeight = UnitUtils.ConvertToInternalUnits(5,    UnitTypeId.Centimeters);
            double trimAmount = UnitUtils.ConvertToInternalUnits(10,   UnitTypeId.Centimeters);

            // --- Calcular puntos y elevaciones ---
            // Reverse the curve itself so sampling starts from the opposite physical end;
            // this moves the "remainder" short segment to that end as well.
            Curve samplingCurve = reversed ? axisCurve.CreateReversed() : axisCurve;
            List<XYZ> curvePoints = PerimeterFenceTools.GetPointsAlongCurve(
                samplingCurve, spacing, trimAmount, trimAmount);

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
            var placements = new List<(XYZ xy, double offsetFromLevel, double tangentAngle)>();

            for (int i = 0; i < curvePoints.Count; i++)
            {
                if (!rawElevations[i].HasValue) continue;

                double rawOffset = rawElevations[i].Value - levelElevation;
                // Math.Floor ensures the element always ends up inside the solid
                // rather than floating above it.
                double quantizedOffset = Math.Floor(rawOffset / stepHeight) * stepHeight;

                XYZ pt = curvePoints[i];

                // Tangent direction at this point — used to rotate the column so it
                // aligns with the fence axis regardless of the line's orientation.
                Transform deriv = samplingCurve.ComputeDerivatives(
                    samplingCurve.Project(pt).Parameter, false);
                double tangentAngle = Math.Atan2(deriv.BasisX.Y, deriv.BasisX.X);

                // Revit interprets XYZ.Z as the offset FROM the reference level, not as an
                // absolute coordinate. Passing quantizedOffset here positions the instance at
                // level + quantizedOffset = terrain surface elevation.
                placements.Add((new XYZ(pt.X, pt.Y, quantizedOffset), quantizedOffset, tangentAngle));
            }

            if (placements.Count == 0)
            {
                TaskDialog.Show("Aviso",
                    "No se generaron puntos de colocación. " +
                    "Verifique que la curva se superponga al elemento de terreno seleccionado.");
                return Result.Cancelled;
            }

            // --- Calcular altura de cada columna ---
            // Rule: the top of column i must clear 4 m above the higher of its two adjacent
            // panel elevations.  Panel between i and i+1 sits at max(offset[i], offset[i+1]),
            // so column i needs: height[i] >= 4m + max(0, offset[i-1]-offset[i], offset[i+1]-offset[i]).
            // This correctly handles both uphill and downhill slopes in either direction.
            double minClearance = UnitUtils.ConvertToInternalUnits(4.0, UnitTypeId.Meters);

            var heights = new double[placements.Count];
            for (int i = 0; i < placements.Count; i++)
            {
                double myOffset = placements[i].offsetFromLevel;
                double extra = 0.0;
                if (i > 0)
                    extra = Math.Max(extra, placements[i - 1].offsetFromLevel - myOffset);
                if (i < placements.Count - 1)
                    extra = Math.Max(extra, placements[i + 1].offsetFromLevel - myOffset);
                heights[i] = minClearance + extra;
            }

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
                    var (xy, offsetFromLevel, tangentAngle) = placements[i];

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

                    // Rotate the column around the Z-axis to align with the fence axis direction.
                    // Families default to the +X orientation; tangentAngle rotates them to match
                    // the curve tangent at this insertion point.
                    if (Math.Abs(tangentAngle) > 1e-6)
                    {
                        Line rotAxis = Line.CreateBound(
                            new XYZ(xy.X, xy.Y, 0),
                            new XYZ(xy.X, xy.Y, 1));
                        ElementTransformUtils.RotateElement(doc, fi.Id, rotAxis, tangentAngle);
                    }
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

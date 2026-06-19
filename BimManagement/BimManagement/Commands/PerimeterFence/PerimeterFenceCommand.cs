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
        const string FamilyName = "Columna_Cimentacion_Prefabricada";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            // --- Selección ---
            Curve axisCurve;
            Element terrainElement;
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

                Reference terrainRef = uidoc.Selection.PickObject(
                    ObjectType.Element,
                    "Seleccione el elemento de terreno o superficie de apoyo");
                terrainElement = doc.GetElement(terrainRef);
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

            // --- Nivel de referencia ---
            Level referenceLevel = PerimeterFenceTools.FindLowestLevel(doc);
            if (referenceLevel == null)
            {
                TaskDialog.Show("Error", "No se encontró ningún nivel en el documento.");
                return Result.Failed;
            }

            // --- Geometría del terreno ---
            Solid terrainSolid = PerimeterFenceTools.GetSolidFromElement(terrainElement);
            if (terrainSolid == null)
            {
                TaskDialog.Show("Error",
                    "No se pudo obtener la geometría sólida del elemento de terreno seleccionado.");
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

            var placements = new List<(XYZ xy, double offsetFromLevel)>();
            double lastQuantized = double.NaN;

            foreach (XYZ pt in curvePoints)
            {
                if (!PerimeterFenceTools.TryGetElevationAtXY(
                        terrainSolid, pt.X, pt.Y, out double rawElev))
                    continue;

                // Vertical distance from Level 1 to the highest intersection on the solid surface.
                // This is the value that goes directly into FAMILY_BASE_LEVEL_OFFSET_PARAM.
                double rawOffset = rawElev - levelElevation;

                double quantizedOffset;
                if (double.IsNaN(lastQuantized))
                {
                    // First point: floor to the nearest 5 cm step (prefer lower, never higher)
                    quantizedOffset = Math.Floor(rawOffset / stepHeight) * stepHeight;
                }
                else
                {
                    quantizedOffset = PerimeterFenceTools.QuantizeElevation(rawOffset, stepHeight, lastQuantized);
                }
                lastQuantized = quantizedOffset;

                // Revit interprets XYZ.Z as the offset FROM the reference level, not as an
                // absolute coordinate. Passing quantizedOffset here positions the instance at
                // level + quantizedOffset = terrain surface elevation. Revit also auto-populates
                // FAMILY_BASE_LEVEL_OFFSET_PARAM with this value.
                placements.Add((new XYZ(pt.X, pt.Y, quantizedOffset), quantizedOffset));
            }

            if (placements.Count == 0)
            {
                TaskDialog.Show("Aviso",
                    "No se generaron puntos de colocación. " +
                    "Verifique que la curva se superponga al elemento de terreno seleccionado.");
                return Result.Cancelled;
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

                foreach (var (xy, offsetFromLevel) in placements)
                {
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

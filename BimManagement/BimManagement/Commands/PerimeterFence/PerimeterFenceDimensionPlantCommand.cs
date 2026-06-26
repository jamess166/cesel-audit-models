using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BimManagement.Commands.PerimeterFence
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    internal class PerimeterFenceDimensionPlantCommand : IExternalCommand
    {
        const string FamilyName  = "Columna_Cimentacion_Prefabricada";
        const string DimTypeName = "CLS_Cota_Seccion";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document   doc   = uidoc.Document;

            if (!(doc.ActiveView is ViewPlan planView))
            {
                TaskDialog.Show("Error", "El comando debe ejecutarse desde una vista en planta.");
                return Result.Failed;
            }

            DimensionType dimType = new FilteredElementCollector(doc)
                .OfClass(typeof(DimensionType))
                .Cast<DimensionType>()
                .FirstOrDefault(t => string.Equals(t.Name, DimTypeName,
                                                    StringComparison.OrdinalIgnoreCase));

            if (dimType == null)
            {
                TaskDialog.Show("Error", $"No se encontró el tipo de cota \"{DimTypeName}\".");
                return Result.Failed;
            }

            IList<Reference> pickedRefs;
            try
            {
                pickedRefs = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new ColumnSelectionFilter(),
                    $"Seleccione mínimo 2 columnas \"{FamilyName}\" a anotar");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            if (pickedRefs.Count < 2)
            {
                TaskDialog.Show("Error", "Debe seleccionar mínimo 2 columnas.");
                return Result.Failed;
            }

            var columns = pickedRefs
                .Select(r => doc.GetElement(r) as FamilyInstance)
                .Where(fi => fi != null)
                .ToList();

            // Direction defined by first and last column in pick order
            XYZ dir = ComputeDirection(columns.First(), columns.Last());
            if (dir == null)
            {
                TaskDialog.Show("Error", "Las columnas seleccionadas están en la misma posición.");
                return Result.Failed;
            }

            // Sort all columns along that direction
            var sorted = columns
                .OrderBy(fi => GetCenter2D(fi).DotProduct(dir))
                .ToList();

            using (Transaction tx = new Transaction(doc, "Cotas Columnas en Planta"))
            {
                tx.Start();

                if (!TryPlaceDimension(doc, planView, dimType, sorted, dir, out string err))
                {
                    tx.RollBack();
                    TaskDialog.Show("Error", err ?? "Error desconocido al crear la cota.");
                    return Result.Failed;
                }

                tx.Commit();
            }

            return Result.Succeeded;
        }

        // ─────────────────────────────────────────────────────────────────────────

        private static bool TryPlaceDimension(
            Document doc, ViewPlan view, DimensionType dimType,
            List<FamilyInstance> sorted, XYZ dir, out string error)
        {
            error = null;

            var refArray = new ReferenceArray();
            foreach (FamilyInstance fi in sorted)
            {
                Reference centerRef = GetCenterRef(fi, dir);
                if (centerRef == null)
                {
                    error = $"No se encontró plano de referencia central para el elemento {fi.Id}.";
                    return false;
                }
                refArray.Append(centerRef);
            }

            if (refArray.Size < 2) { error = "Menos de 2 referencias válidas."; return false; }

            // Perpendicular direction in XY (rotate dir 90° CCW)
            XYZ perpDir = new XYZ(-dir.Y, dir.X, 0);

            double startDot    = GetCenter2D(sorted.First()).DotProduct(dir);
            double endDot      = GetCenter2D(sorted.Last()).DotProduct(dir);
            double avgPerpDot  = sorted.Select(fi => GetCenter2D(fi).DotProduct(perpDir)).Average();

            double margin = UnitUtils.ConvertToInternalUnits(0.5, UnitTypeId.Meters);
            double offset = UnitUtils.ConvertToInternalUnits(0.5, UnitTypeId.Meters);

            // Dim line is parallel to dir, offset to the perpDir side
            double dimPerpDot = avgPerpDot + offset;
            double z = sorted.First().get_BoundingBox(null).Min.Z;

            XYZ pt1Base = dir * (startDot - margin) + perpDir * dimPerpDot;
            XYZ pt2Base = dir * (endDot   + margin) + perpDir * dimPerpDot;
            XYZ pt1 = new XYZ(pt1Base.X, pt1Base.Y, z);
            XYZ pt2 = new XYZ(pt2Base.X, pt2Base.Y, z);

            Line dimLine;
            try   { dimLine = Line.CreateBound(pt1, pt2); }
            catch (Exception ex) { error = $"line: {ex.Message}"; return false; }

            try
            {
                Dimension dim = doc.Create.NewDimension(view, dimLine, refArray);
                if (dim == null) { error = "NewDimension devolvió null."; return false; }
                dim.ChangeTypeId(dimType.Id);
                return true;
            }
            catch (Exception ex) { error = $"NewDimension: {ex.Message}"; return false; }
        }

        // Returns the center reference plane of fi most aligned with dir.
        // Uses FamilyInstance.GetReferences() which targets the family's own reference
        // planes — the same planes Revit highlights when dimensioning manually.
        private static Reference GetCenterRef(FamilyInstance fi, XYZ dir)
        {
            XYZ facing2d = new XYZ(fi.FacingOrientation.X, fi.FacingOrientation.Y, 0);
            XYZ hand2d   = new XYZ(fi.HandOrientation.X,   fi.HandOrientation.Y,   0);

            double lenF = facing2d.GetLength();
            double lenH = hand2d.GetLength();

            double dotF = lenF > 1e-6 ? Math.Abs(facing2d.Normalize().DotProduct(dir)) : 0;
            double dotH = lenH > 1e-6 ? Math.Abs(hand2d.Normalize().DotProduct(dir))   : 0;

            // CenterFrontBack normal ≈ FacingOrientation; CenterLeftRight normal ≈ HandOrientation
            FamilyInstanceReferenceType primary   = dotF >= dotH
                ? FamilyInstanceReferenceType.CenterFrontBack
                : FamilyInstanceReferenceType.CenterLeftRight;
            FamilyInstanceReferenceType secondary = dotF >= dotH
                ? FamilyInstanceReferenceType.CenterLeftRight
                : FamilyInstanceReferenceType.CenterFrontBack;

            IList<Reference> refs = fi.GetReferences(primary);
            if (refs != null && refs.Count > 0) return refs[0];

            refs = fi.GetReferences(secondary);
            return refs != null && refs.Count > 0 ? refs[0] : null;
        }

        // ─────────────────────────────────────────────────────────────────────────

        private static XYZ ComputeDirection(FamilyInstance first, FamilyInstance last)
        {
            XYZ p1    = GetCenter2D(first);
            XYZ p2    = GetCenter2D(last);
            XYZ delta = new XYZ(p2.X - p1.X, p2.Y - p1.Y, 0);
            return delta.GetLength() < 1e-9 ? null : delta.Normalize();
        }

        private static XYZ GetCenter2D(FamilyInstance fi)
        {
            if (fi.Location is LocationPoint lp)
                return new XYZ(lp.Point.X, lp.Point.Y, 0);
            BoundingBoxXYZ bb = fi.get_BoundingBox(null);
            return new XYZ((bb.Min.X + bb.Max.X) / 2, (bb.Min.Y + bb.Max.Y) / 2, 0);
        }

        private class ColumnSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) =>
                elem is FamilyInstance fi &&
                string.Equals(fi.Symbol.Family.Name, FamilyName,
                              StringComparison.OrdinalIgnoreCase);

            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }
}

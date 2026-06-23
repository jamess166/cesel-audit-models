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
    internal class PerimeterFenceDimensionElevationCommand : IExternalCommand
    {
        const string FamilyName  = "Columna_Cimentacion_Prefabricada";
        const string DimTypeName = "CLS_Cota_Seccion";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document   doc   = uidoc.Document;

            if (!(doc.ActiveView is ViewSection sectionView))
            {
                TaskDialog.Show("Error", "El comando debe ejecutarse desde una vista de sección.");
                return Result.Failed;
            }

            DimensionType dimType = new FilteredElementCollector(doc)
                .OfClass(typeof(DimensionType))
                .Cast<DimensionType>()
                .FirstOrDefault(t => string.Equals(t.Name, DimTypeName,
                                                    StringComparison.OrdinalIgnoreCase));

            if (dimType == null)
            {
                TaskDialog.Show("Error",
                    $"No se encontró el tipo de cota \"{DimTypeName}\".");
                return Result.Failed;
            }

            IList<Reference> refs;
            try
            {
                refs = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new ColumnSelectionFilter(),
                    $"Seleccione las columnas \"{FamilyName}\" a anotar");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            int placed  = 0;
            int skipped = 0;

            using (Transaction tx = new Transaction(doc, "Cotas Columnas Cimentación"))
            {
                tx.Start();

                foreach (Reference r in refs)
                {
                    if (!(doc.GetElement(r) is FamilyInstance fi)) continue;

                    string err = null;
                    if (TryPlaceDimension(doc, sectionView, dimType, fi, out err))
                        placed++;
                    else
                        skipped++;
                }

                tx.Commit();
            }

            TaskDialog.Show("Cotas Columnas",
                $"Colocadas: {placed}\nOmitidas:  {skipped}");

            return Result.Succeeded;
        }

        // ─────────────────────────────────────────────────────────────────────────

        private static bool TryPlaceDimension(
            Document doc, ViewSection view, DimensionType dimType,
            FamilyInstance fi, out string error)
        {
            error = null;

            // Do NOT set opt.View: setting it gives section-cut geometry where face
            // references may be null. Full symbol geometry gives stable document-level
            // references (same pattern as PerimeterFenceSpotElevationCommand).
            Options opt = doc.Application.Create.NewGeometryOptions();
            opt.ComputeReferences = true;
            opt.DetailLevel       = ViewDetailLevel.Fine;

            GeometryElement geoElem = fi.get_Geometry(opt);
            if (geoElem == null) { error = "no geometry"; return false; }

            // Locate the outer GeometryInstance that wraps the family symbol
            GeometryInstance outerGi = null;
            foreach (GeometryObject obj in geoElem)
                if (obj is GeometryInstance gi) { outerGi = gi; break; }

            if (outerGi == null) { error = "no GeometryInstance"; return false; }

            // Collect all solids from symbol geometry, tracking each solid's local→world transform
            var solidList = new List<(Solid solid, Transform toWorld)>();
            CollectSolids(outerGi.GetSymbolGeometry(), outerGi.Transform, solidList);

            if (solidList.Count < 2)
            {
                error = $"only {solidList.Count} solid(s) found";
                return false;
            }

            // Pick the two largest solids by volume (ignores small auxiliary geometry such as
            // connector plates or symbolic representations), then sort by world Z so
            // index 0 = lower (foundation block) and index 1 = upper (column shaft).
            var sorted = solidList
                .OrderByDescending(s => s.solid.Volume)
                .Take(2)
                .OrderBy(s => GetWorldCenterZ(s.solid, s.toWorld))
                .ToList();

            var (lowerSolid, lowerT) = sorted[0];
            var (upperSolid, upperT) = sorted[1];

            var (lowerBottomRef, localLBZ) = GetHorizontalFace(lowerSolid, facingDown: true);
            var (lowerTopRef,    localLTZ) = GetHorizontalFace(lowerSolid, facingDown: false);
            var (upperTopRef,    localUTZ) = GetHorizontalFace(upperSolid, facingDown: false);

            if (lowerBottomRef == null || lowerTopRef == null || upperTopRef == null)
            {
                error = "one or more face references null";
                return false;
            }

            // Local face Z → world Z via the solid's accumulated transform
            double zLowerBottom = lowerT.OfPoint(new XYZ(0, 0, localLBZ)).Z;
            double zUpperTop    = upperT.OfPoint(new XYZ(0, 0, localUTZ)).Z;

            var refArray = new ReferenceArray();
            refArray.Append(lowerBottomRef);
            refArray.Append(lowerTopRef);
            refArray.Append(upperTopRef);

            // Position dimension line 0.4 m to the left of the column in the view
            BoundingBoxXYZ bb        = fi.get_BoundingBox(null);
            XYZ            viewRight = view.RightDirection;

            double leftEdge = new[]
            {
                new XYZ(bb.Min.X, bb.Min.Y, 0).DotProduct(viewRight),
                new XYZ(bb.Min.X, bb.Max.Y, 0).DotProduct(viewRight),
                new XYZ(bb.Max.X, bb.Min.Y, 0).DotProduct(viewRight),
                new XYZ(bb.Max.X, bb.Max.Y, 0).DotProduct(viewRight),
            }.Min();

            double dimOffset  = UnitUtils.ConvertToInternalUnits(0.4, UnitTypeId.Meters);
            double dimLineDot = leftEdge - dimOffset;

            XYZ colOrigin = (fi.Location as LocationPoint)?.Point
                            ?? new XYZ(
                                (bb.Min.X + bb.Max.X) / 2,
                                (bb.Min.Y + bb.Max.Y) / 2,
                                bb.Min.Z);

            // Shift colOrigin along viewRight until its projection equals dimLineDot
            XYZ dimBase = colOrigin
                + viewRight * (dimLineDot - colOrigin.DotProduct(viewRight));

            double margin = UnitUtils.ConvertToInternalUnits(0.3, UnitTypeId.Meters);
            XYZ pt1 = new XYZ(dimBase.X, dimBase.Y, zLowerBottom - margin);
            XYZ pt2 = new XYZ(dimBase.X, dimBase.Y, zUpperTop    + margin);

            Line dimLine;
            try   { dimLine = Line.CreateBound(pt1, pt2); }
            catch (Exception ex) { error = $"line: {ex.Message}"; return false; }

            try
            {
                Dimension dim = doc.Create.NewDimension(view, dimLine, refArray);
                if (dim == null) { error = "NewDimension returned null"; return false; }
                dim.ChangeTypeId(dimType.Id);
                return true;
            }
            catch (Exception ex)
            {
                error = $"NewDimension: {ex.Message}";
                return false;
            }
        }

        // ─────────────────────────────────────────────────────────────────────────

        private static void CollectSolids(
            GeometryElement geo, Transform toWorld, List<(Solid, Transform)> result)
        {
            foreach (GeometryObject obj in geo)
            {
                if (obj is Solid s && s.Volume > 1e-9)
                    result.Add((s, toWorld));
                else if (obj is GeometryInstance gi)
                    CollectSolids(gi.GetSymbolGeometry(), toWorld.Multiply(gi.Transform), result);
            }
        }

        private static double GetWorldCenterZ(Solid solid, Transform toWorld)
        {
            BoundingBoxXYZ bb = solid.GetBoundingBox();
            XYZ localCenter = new XYZ(
                (bb.Min.X + bb.Max.X) / 2,
                (bb.Min.Y + bb.Max.Y) / 2,
                (bb.Min.Z + bb.Max.Z) / 2);
            return toWorld.OfPoint(localCenter).Z;
        }

        // Returns the best horizontal face (facing up or down) and its local Z elevation.
        private static (Reference faceRef, double localZ) GetHorizontalFace(
            Solid solid, bool facingDown)
        {
            Reference best  = null;
            double    bestZ = facingDown ? double.MaxValue : double.MinValue;

            foreach (Face face in solid.Faces)
            {
                if (!(face is PlanarFace pf))                           continue;
                if (Math.Abs(Math.Abs(pf.FaceNormal.Z) - 1.0) > 0.01) continue;
                if ((pf.FaceNormal.Z < 0) != facingDown)               continue;
                if (face.Reference == null)                             continue;

                double z = pf.Origin.Z;
                if (facingDown ? z < bestZ : z > bestZ)
                {
                    bestZ = z;
                    best  = face.Reference;
                }
            }

            return (best, bestZ);
        }

        // ─────────────────────────────────────────────────────────────────────────

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

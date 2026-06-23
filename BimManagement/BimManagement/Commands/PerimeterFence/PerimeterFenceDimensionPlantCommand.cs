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
                Reference faceRef = GetBestVerticalFaceRef(doc, fi, dir);
                if (faceRef == null)
                {
                    error = $"No se encontró referencia de cara para el elemento {fi.Id}.";
                    return false;
                }
                refArray.Append(faceRef);
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

        // Returns the best vertical planar-face reference on fi whose horizontal
        // normal is most aligned with dir (parallel reference plane to adjacent column).
        // Same geometry strategy as PerimeterFenceDimensionElevationCommand:
        // symbol geometry without opt.View gives stable document-level references.
        private static Reference GetBestVerticalFaceRef(Document doc, FamilyInstance fi, XYZ dir)
        {
            Options opt = doc.Application.Create.NewGeometryOptions();
            opt.ComputeReferences = true;
            opt.DetailLevel       = ViewDetailLevel.Fine;

            GeometryElement geoElem = fi.get_Geometry(opt);
            if (geoElem == null) return null;

            GeometryInstance outerGi = null;
            foreach (GeometryObject obj in geoElem)
                if (obj is GeometryInstance gi) { outerGi = gi; break; }

            if (outerGi == null) return null;

            var solids = new List<(Solid solid, Transform toWorld)>();
            CollectSolids(outerGi.GetSymbolGeometry(), outerGi.Transform, solids);

            Reference bestRef = null;
            double    bestDot = -1.0;

            foreach (var (solid, toWorld) in solids)
            {
                foreach (Face face in solid.Faces)
                {
                    if (!(face is PlanarFace pf)) continue;
                    if (face.Reference == null)    continue;

                    // Transform face normal to world space (linear part only)
                    XYZ worldNormal = toWorld.OfVector(pf.FaceNormal);

                    // Project to horizontal plane; skip faces that are nearly horizontal
                    XYZ horizNormal = new XYZ(worldNormal.X, worldNormal.Y, 0);
                    double horizLen = horizNormal.GetLength();
                    if (horizLen < 0.1) continue;

                    // Absolute alignment with dim direction: the face whose horizontal
                    // normal is most parallel to dir is perpendicular to the dim line,
                    // producing a reference parallel to the same face on adjacent columns.
                    double dot = Math.Abs(horizNormal.Normalize().DotProduct(dir));
                    if (dot > bestDot)
                    {
                        bestDot = dot;
                        bestRef = face.Reference;
                    }
                }
            }

            return bestRef;
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

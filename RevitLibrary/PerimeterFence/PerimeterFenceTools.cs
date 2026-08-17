using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitLibrary
{
    public static class PerimeterFenceTools
    {
        /// <summary>
        /// Returns evenly spaced points along a curve at every <paramref name="spacing"/> internal units.
        /// <paramref name="startOffset"/> and <paramref name="endOffset"/> shorten the effective
        /// range from each end (all values in Revit internal units).
        /// </summary>
        public static List<XYZ> GetPointsAlongCurve(
            Curve curve, double spacing,
            double startOffset = 0.0, double endOffset = 0.0)
        {
            var points = new List<XYZ>();
            double length = curve.Length;
            double effectiveEnd = length - endOffset;

            if (effectiveEnd <= startOffset)
                return points;

            for (double t = startOffset; t < effectiveEnd - 1e-9; t += spacing)
                points.Add(curve.Evaluate(t / length, true));

            return points;
        }

        /// <summary>
        /// Extracts the solid with the largest volume from the element's geometry.
        /// Returns null if no solid is found.
        /// </summary>
        public static Solid GetSolidFromElement(Element element)
        {
            var opts = new Options
            {
                DetailLevel = ViewDetailLevel.Fine,
                ComputeReferences = false
            };
            GeometryElement geom = element.get_Geometry(opts);
            return geom == null ? null : FindLargestSolid(geom);
        }

        private static Solid FindLargestSolid(GeometryElement geomElement)
        {
            Solid largest = null;
            foreach (GeometryObject obj in geomElement)
            {
                Solid candidate = null;
                if (obj is Solid s && s.Volume > 1e-9)
                    candidate = s;
                else if (obj is GeometryInstance gi)
                    candidate = FindLargestSolid(gi.GetInstanceGeometry());

                if (candidate != null && (largest == null || candidate.Volume > largest.Volume))
                    largest = candidate;
            }
            return largest;
        }

        /// <summary>
        /// Casts a vertical ray downward at (x, y) and returns the highest Z intersection
        /// against <paramref name="terrainSolid"/>. All values in Revit internal units.
        /// </summary>
        public static bool TryGetElevationAtXY(
            Solid terrainSolid,
            double x, double y,
            double searchTop, double searchBottom,
            out double elevation)
        {
            elevation = 0.0;

            Line ray;
            try
            {
                ray = Line.CreateBound(
                    new XYZ(x, y, searchTop),
                    new XYZ(x, y, searchBottom));
            }
            catch { return false; }

            SolidCurveIntersection result = terrainSolid.IntersectWithCurve(
                ray, new SolidCurveIntersectionOptions());

            if (result == null || result.SegmentCount == 0)
                return false;

            double maxZ = double.MinValue;
            for (int i = 0; i < result.SegmentCount; i++)
            {
                Curve seg = result.GetCurveSegment(i);
                double z0 = seg.GetEndPoint(0).Z;
                double z1 = seg.GetEndPoint(1).Z;
                if (z0 > maxZ) maxZ = z0;
                if (z1 > maxZ) maxZ = z1;
            }

            elevation = maxZ;
            return true;
        }

        /// <summary>
        /// Quantizes <paramref name="rawElevation"/> in steps of <paramref name="stepHeight"/>.
        /// Returns <paramref name="lastQuantized"/> unchanged when the absolute difference is
        /// less than one full step; otherwise advances by complete steps toward rawElevation.
        /// All values in Revit internal units.
        /// </summary>
        public static double QuantizeElevation(
            double rawElevation,
            double stepHeight,
            double lastQuantized)
        {
            double diff = rawElevation - lastQuantized;
            if (Math.Abs(diff) < stepHeight)
                return lastQuantized;

            // Advance by as many complete steps as the difference allows
            double steps = diff > 0
                ? Math.Floor(diff / stepHeight)
                : Math.Ceiling(diff / stepHeight);

            return lastQuantized + steps * stepHeight;
        }

        /// <summary>
        /// Returns the first FamilySymbol whose Family.Name matches <paramref name="familyName"/>
        /// (case-insensitive). Returns null if not found.
        /// </summary>
        public static FamilySymbol FindFamilySymbol(Document doc, string familyName)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(fs => fs.Family.Name.Equals(
                    familyName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Returns the Level with the lowest elevation in the document.
        /// </summary>
        public static Level FindLowestLevel(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .FirstOrDefault();
        }
    }
}

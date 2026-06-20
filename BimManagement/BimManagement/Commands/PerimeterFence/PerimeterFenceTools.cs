using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BimManagement.Commands.PerimeterFence
{
    internal static class PerimeterFenceTools
    {
        /// <summary>
        /// Returns points along a curve separated by <paramref name="spacing"/>, always including
        /// the trimmed endpoint. If the last segment exceeds one spacing it is split into equal
        /// sub-intervals rounded to the nearest 0.1 m. The minimum sub-interval is 1 m; the final
        /// gap to the endpoint is exempt from that minimum. All values in Revit internal units.
        /// </summary>
        public static List<XYZ> GetPointsAlongCurve(
            Curve curve, double spacing,
            double startOffset = 0.0, double endOffset = 0.0)
        {
            var points = new List<XYZ>();
            double length = curve.Length;
            double effectiveEnd = length - endOffset;
            const double tol = 1e-6;

            if (effectiveEnd <= startOffset + tol)
                return points;

            double minSpacing = UnitUtils.ConvertToInternalUnits(1.0, UnitTypeId.Meters);

            // First point at start offset
            double lastT = startOffset;
            points.Add(curve.Evaluate(lastT / length, true));

            // Regular points: only add a point if it still leaves >= minSpacing before the endpoint.
            // This prevents the last regular point from landing too close to the endpoint.
            while (lastT + spacing <= effectiveEnd - minSpacing + tol)
            {
                lastT += spacing;
                points.Add(curve.Evaluate(lastT / length, true));
            }

            double remaining = effectiveEnd - lastT;

            if (remaining <= tol)
                return points; // last regular point coincides with endpoint

            if (remaining <= spacing)
            {
                // Remaining fits within one spacing; add the endpoint.
                // The gap may be < minSpacing but the endpoint is always exempt.
                points.Add(curve.Evaluate(effectiveEnd / length, true));
            }
            else
            {
                // remaining > spacing: the last segment must be split.
                // This happens when the next regular point would leave < minSpacing before endpoint.
                int n = (int)Math.Ceiling(remaining / spacing);

                double subSpacing = remaining / n;

                // Guard: ensure sub-spacing >= 1 m (reduce n if necessary)
                while (subSpacing < minSpacing && n > 1)
                {
                    n--;
                    subSpacing = remaining / n;
                }

                // Round intermediate sub-intervals to the nearest 5 cm multiple.
                // The final gap to the endpoint is whatever remains and is exempt from this rule.
                double subSpacingM = UnitUtils.ConvertFromInternalUnits(subSpacing, UnitTypeId.Meters);
                subSpacingM = Math.Round(subSpacingM / 0.05) * 0.05;
                if (subSpacingM < 1.0) subSpacingM = 1.0;
                double subSpacingRounded = UnitUtils.ConvertToInternalUnits(subSpacingM, UnitTypeId.Meters);

                // n − 1 intermediate points
                for (int i = 1; i < n; i++)
                {
                    double t = lastT + i * subSpacingRounded;
                    if (t >= effectiveEnd - tol) break;
                    points.Add(curve.Evaluate(t / length, true));
                }

                // Endpoint always last
                points.Add(curve.Evaluate(effectiveEnd / length, true));
            }

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
        /// Shoots a vertical ray downward at (x, y) and returns the highest Z where the ray
        /// enters the solid. The highest endpoint across all intersection segments is the top
        /// surface. offset = elevation - level.Elevation gives FAMILY_BASE_LEVEL_OFFSET_PARAM.
        /// All values in Revit internal units (feet).
        /// </summary>
        public static bool TryGetElevationAtXY(Solid solid, double x, double y, out double elevation)
        {
            elevation = 0.0;

            // Ray spans the full practical height range of any project
            double top    = UnitUtils.ConvertToInternalUnits( 5000, UnitTypeId.Meters);
            double bottom = UnitUtils.ConvertToInternalUnits( -500, UnitTypeId.Meters);

            Line ray;
            try { ray = Line.CreateBound(new XYZ(x, y, top), new XYZ(x, y, bottom)); }
            catch { return false; }

            SolidCurveIntersection result = solid.IntersectWithCurve(
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
        /// Tries each solid in <paramref name="solids"/> and returns the highest surface Z found.
        /// </summary>
        public static bool TryGetElevationAtXY(
            IList<Solid> solids, double x, double y, out double elevation)
        {
            elevation = 0.0;
            bool found = false;
            foreach (Solid solid in solids)
            {
                if (TryGetElevationAtXY(solid, x, y, out double z) && (!found || z > elevation))
                {
                    elevation = z;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>
        /// Quantizes elevation in steps of <paramref name="stepHeight"/>.
        /// Ignores changes smaller than one full step; advances by complete steps otherwise.
        /// All values in Revit internal units.
        /// </summary>
        public static double QuantizeElevation(
            double rawElevation, double stepHeight, double lastQuantized)
        {
            double diff = rawElevation - lastQuantized;
            if (Math.Abs(diff) < stepHeight)
                return lastQuantized;

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

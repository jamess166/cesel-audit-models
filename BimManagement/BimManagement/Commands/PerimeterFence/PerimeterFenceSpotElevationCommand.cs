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
    internal class PerimeterFenceSpotElevationCommand : IExternalCommand
    {
        const string PanelFamilyName       = "Tarjetas_Cerco";
        const string SpotElevationTypeName = "CSL_Elevación_Cerco";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document   doc   = uidoc.Document;

            if (!(doc.ActiveView is ViewSection sectionView))
            {
                TaskDialog.Show("Error", "El comando debe ejecutarse desde una vista de sección.");
                return Result.Failed;
            }

            SpotDimensionType spotType = new FilteredElementCollector(doc)
                .OfClass(typeof(SpotDimensionType))
                .Cast<SpotDimensionType>()
                .FirstOrDefault(t => string.Equals(t.Name, SpotElevationTypeName,
                                                    StringComparison.OrdinalIgnoreCase));

            if (spotType == null)
            {
                TaskDialog.Show("Error", $"No se encontró el tipo \"{SpotElevationTypeName}\".");
                return Result.Failed;
            }

            IList<Reference> refs;
            try
            {
                refs = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new PanelSelectionFilter(),
                    $"Seleccione los paneles \"{PanelFamilyName}\" a anotar");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            int placed  = 0;
            int skipped = 0;

            Options opt = doc.Application.Create.NewGeometryOptions();
            opt.ComputeReferences = true;
            opt.DetailLevel       = ViewDetailLevel.Fine;

            using (Transaction tx = new Transaction(doc, "Niveles en Punto Cerco"))
            {
                tx.Start();

                foreach (Reference r in refs)
                {
                    if (!(doc.GetElement(r) is FamilyInstance panel)) continue;

                    if (TryPlaceSpotElevation(doc, sectionView, spotType, panel, opt))
                        placed++;
                    else
                        skipped++;
                }

                tx.Commit();
            }

            TaskDialog.Show("Niveles en Punto",
                $"Colocados: {placed}\nOmitidos:  {skipped}");

            return Result.Succeeded;
        }

        // ─────────────────────────────────────────────────────────────────────────

        private static bool TryPlaceSpotElevation(
            Document doc, ViewSection view, SpotDimensionType spotType,
            FamilyInstance panel, Options opt)
        {
            GeometryElement geo = panel.get_Geometry(opt);
            if (geo == null) return false;

            // Find the outer GeometryInstance and its local→world transform
            GeometryInstance gi = null;
            foreach (GeometryObject o in geo)
                if (o is GeometryInstance inst) { gi = inst; break; }
            if (gi == null) return false;

            Transform t      = gi.Transform;
            GeometryElement symGeo = gi.GetSymbolGeometry();

            // Find the highest solid in symbol (local) space
            Solid  bestSolid  = null;
            double bestLocalZ = double.MinValue;
            CollectHighestSolid(symGeo, ref bestSolid, ref bestLocalZ);
            if (bestSolid == null) return false;

            // Find the top horizontal edge with a valid reference
            Reference edgeRef    = null;
            XYZ       worldMid   = null;
            double    topLocalZ  = double.MinValue;

            foreach (Edge edge in bestSolid.Edges)
            {
                if (edge.Reference == null) continue;
                XYZ p0 = edge.AsCurve().GetEndPoint(0);
                XYZ p1 = edge.AsCurve().GetEndPoint(1);
                if (Math.Abs(p0.Z - p1.Z) > 1e-4) continue; // must be horizontal
                if (p0.Z <= topLocalZ) continue;

                topLocalZ = p0.Z;
                edgeRef   = edge.Reference;

                // Evaluate gives true local-space midpoint on symbol geometry;
                // apply the instance transform to get world-space position.
                XYZ localMid = edge.AsCurve().Evaluate(0.5, true);
                worldMid = t.OfPoint(localMid);
            }

            if (edgeRef == null) return false;

            try
            {
                SpotDimension sd = doc.Create.NewSpotElevation(
                    view, edgeRef, worldMid, worldMid, worldMid, worldMid, false);
                if (sd == null) return false;
                sd.ChangeTypeId(spotType.Id);

                // Shift text 3.137 internal units along the view's right direction
                sd.TextPosition = Transform.CreateTranslation(view.RightDirection * 3.137)
                                           .OfPoint(sd.TextPosition);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ─────────────────────────────────────────────────────────────────────────

        private static void CollectHighestSolid(
            GeometryElement geo, ref Solid best, ref double bestZ)
        {
            foreach (GeometryObject obj in geo)
            {
                if (obj is Solid s && s.Volume > 1e-9)
                {
                    double z = SolidMaxZ(s);
                    if (z > bestZ) { bestZ = z; best = s; }
                }
                else if (obj is GeometryInstance gi)
                    CollectHighestSolid(gi.GetSymbolGeometry(), ref best, ref bestZ);
            }
        }

        private static double SolidMaxZ(Solid s)
        {
            double z = double.MinValue;
            foreach (Edge e in s.Edges)
            {
                z = Math.Max(z, e.AsCurve().GetEndPoint(0).Z);
                z = Math.Max(z, e.AsCurve().GetEndPoint(1).Z);
            }
            return z;
        }

        private class PanelSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) =>
                elem is FamilyInstance fi &&
                string.Equals(fi.Symbol.Family.Name, PanelFamilyName,
                              StringComparison.OrdinalIgnoreCase);

            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }
}

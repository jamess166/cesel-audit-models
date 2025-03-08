using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace BimManagement.Commands.Utils
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class PickLinkedFaceWorkPlane : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Get application and document objects
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uiapp.ActiveUIDocument.Document;
            Selection sel = uidoc.Selection;

            //variables estaticas (si las necesitas)
            RevitTools.uiApp = uiapp;
            RevitTools.doc = doc;
            RevitTools.app = app;
            RevitTools.uidoc = uidoc;
            RevitTools.sel = sel;
            RevitTools.elements = null;

            try
            {
                Debug.WriteLine("Start Command");

                // Paso 1: Seleccionar el dispositivo de alarma
                TaskDialog.Show("Script", "Solicitando selección del dispositivo...");
                Reference elementRef = sel.PickObject(ObjectType.Element, "Selecciona el dispositivo de alarma contra incendios");
                Element fireAlarm = doc.GetElement(elementRef.ElementId);

                if (fireAlarm.Category.Id.IntegerValue != (int)BuiltInCategory.OST_FireAlarmDevices)
                {
                    TaskDialog.Show("Error", "El elemento seleccionado no es un Fire Alarm Device.");
                    return Result.Failed;
                }

                LocationPoint location = fireAlarm.Location as LocationPoint;
                if (location == null)
                {
                    TaskDialog.Show("Error", "El elemento no tiene punto de ubicación.");
                    return Result.Failed;
                }

                XYZ currentPoint = location.Point;
                //TaskDialog.Show("Info", $"Punto actual: X={currentPoint.X}, Y={currentPoint.Y}, Z={currentPoint.Z}");

                // Paso 2: Seleccionar una cara en el modelo vinculado
                //TaskDialog.Show("Script", "Selecciona un punto en una cara del modelo vinculado...");

                // Obtener todos los documentos vinculados
                IEnumerable<Document> linkedDocs = GetLinkedDocuments(doc);

                // Permitir seleccionar un punto en cualquier elemento (incluidos los vinculados)
                Reference pickedRef = sel.PickObject(ObjectType.PointOnElement, "Selecciona un punto en una cara del modelo vinculado");

                // Obtener el elemento seleccionado en el documento activo
                Element elem = doc.GetElement(pickedRef.ElementId);

                // Obtener la posición global del punto seleccionado
                XYZ pickedPoint = pickedRef.GlobalPoint;

                // Obtener el ID del elemento que contiene la cara seleccionada
                string stableRef = pickedRef.ConvertToStableRepresentation(doc);
                string[] refParts = stableRef.Split(':');
                string elementIdStr = refParts[refParts.Length - 3];

                int elementId;
                if (!Int32.TryParse(elementIdStr, out elementId))
                {
                    TaskDialog.Show("Error", "No se pudo obtener el ID del elemento vinculado.");
                    return Result.Failed;
                }

                // Verificar si el elemento seleccionado es una instancia vinculada
                RevitLinkInstance linkInstance = null;
                if (elem is RevitLinkInstance)
                {
                    linkInstance = elem as RevitLinkInstance;
                }
                else
                {
                    TaskDialog.Show("Error", "El elemento seleccionado no es un modelo vinculado.");
                    return Result.Failed;
                }

                // Encontrar el documento vinculado correspondiente
                Document linkedDoc = null;
                foreach (Document d in linkedDocs)
                {
                    if (elem.Name.Contains(d.Title))
                    {
                        linkedDoc = d;
                        break;
                    }
                }

                if (linkedDoc == null)
                {
                    TaskDialog.Show("Error", "No se pudo encontrar el documento vinculado.");
                    return Result.Failed;
                }

                // Obtener el elemento vinculado
                Element linkedElement = linkedDoc.GetElement(new ElementId(elementId));
                if (linkedElement == null)
                {
                    TaskDialog.Show("Error", "No se pudo encontrar el elemento en el modelo vinculado.");
                    return Result.Failed;
                }

                // Configurar opciones para obtener la geometría
                Options options = new Options();
                options.ComputeReferences = true;

                // Buscar la cara que contiene el punto seleccionado
                PlanarFace selectedFace = null;
                foreach (GeometryObject geomObj in linkedElement.get_Geometry(options))
                {
                    if (geomObj is Solid)
                    {
                        Solid solid = geomObj as Solid;
                        foreach (Face face in solid.Faces)
                        {
                            try
                            {
                                // Verificar si la cara contiene el punto seleccionado
                                if (face.Project(pickedPoint).XYZPoint.DistanceTo(pickedPoint) < 0.001)
                                {
                                    if (face is PlanarFace)
                                    {
                                        selectedFace = face as PlanarFace;
                                        break;
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                // Ignorar errores de proyección
                            }
                        }
                    }
                    if (selectedFace != null) break;
                }

                if (selectedFace == null)
                {
                    TaskDialog.Show("Error", "No se pudo encontrar una cara plana en el punto seleccionado.");
                    return Result.Failed;
                }

                // Obtener la normal y origen de la cara seleccionada
                XYZ faceNormal = selectedFace.FaceNormal;
                XYZ faceOrigin = selectedFace.Origin;

                // Aplicar la transformación del modelo vinculado
                Transform transform = linkInstance.GetTotalTransform();
                faceNormal = transform.OfVector(faceNormal);
                faceOrigin = transform.OfPoint(faceOrigin);

                FamilyInstance fireAlarmInstance = fireAlarm as FamilyInstance;
                // Transformar la referencia del modelo vinculado al documento principal
                Reference transformedRef = selectedFace.Reference.CreateLinkReference(linkInstance);

                GeometryObject geometryObject = linkInstance.GetLinkDocument().GetElement(reference.ElementId)
    .GetGeometryObjectFromReference(reference) as GeometryObject;


                // Crear un nuevo plano de trabajo y mover el dispositivo
                using (Transaction trans = new Transaction(doc, "Crear plano de trabajo y mover Fire Alarm"))
                {
                    trans.Start();

                    // Obtener la familia y el nivel
                    FamilySymbol fireAlarmSymbol = fireAlarmInstance.Symbol;
                    Level level = doc.GetElement(fireAlarmInstance.LevelId) as Level;

                    // Verificar que la familia está activada
                    if (!fireAlarmSymbol.IsActive)
                    {
                        fireAlarmSymbol.Activate();
                        doc.Regenerate();
                    }

                    Debug.WriteLine("Creando Familia");

                    // Crear la nueva instancia en la cara del vínculo transformada al documento activo
                    FamilyInstance newFireAlarm = doc.Create.NewFamilyInstance(
                        transformedRef,  // 🔹 Referencia transformada
                        pickedPoint,
                        XYZ.BasisZ,
                        fireAlarmSymbol
                    );

                    // Eliminar la instancia antigua
                    //doc.Delete(fireAlarmInstance.Id);

                    trans.Commit();
                }

                TaskDialog.Show("Éxito", "Se creó un nuevo plano de trabajo basado en la cara seleccionada y se movió el dispositivo de alarma contra incendios.");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"Error: {ex.Message}");
                Debug.WriteLine(ex.ToString());
                return Result.Failed;
            }
        }

        // Obtener referencias a archivos vinculados
        public static IEnumerable<ExternalFileReference> GetLinkedFileReferences(Document document)
        {
            var collector = new FilteredElementCollector(document);

            var linkedElements = collector
                .OfClass(typeof(RevitLinkType))
                .Select(x => x.GetExternalFileReference())
                .ToList();

            return linkedElements;
        }

        // Obtener documentos vinculados
        public static IEnumerable<Document> GetLinkedDocuments(Document document)
        {
            var linkedfiles = GetLinkedFileReferences(document);

            var linkedFileNames = linkedfiles
                .Select(x => ModelPathUtils.ConvertModelPathToUserVisiblePath(x.GetAbsolutePath()))
                .ToList();

            return document.Application.Documents
                .Cast<Document>()
                .Where(doc => linkedFileNames.Any(fileName => doc.PathName.Equals(fileName)));
        }
    }
}

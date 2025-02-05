using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static BimManagement.UpdateConstructionDataCommand;
using Application = Autodesk.Revit.ApplicationServices.Application;

namespace BimManagement
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class UpdateConstructionDataCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Get application and document objects
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uiapp.ActiveUIDocument.Document;
            Selection sel = uidoc.Selection;

            //variables estaticas
            RevitTools.uiApp = uiapp;
            RevitTools.doc = doc;
            RevitTools.app = app;
            RevitTools.uidoc = uidoc;
            RevitTools.sel = sel;
            RevitTools.elements = null;

            // Crear y configurar el diálogo de selección de archivo
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Archivos de Revit (*.rvt)|*.rvt",
                Title = "Seleccionar modelo de Revit"
            };

            if (openFileDialog.ShowDialog() != DialogResult.OK)
            {
                return Result.Cancelled;
            }

            string filePath = openFileDialog.FileName;

            // Abrir el documento seleccionado
            Document selectedDoc = null;
            ModelPath modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(filePath);

            // Opciones de apertura
            OpenOptions openOpt = new OpenOptions
            {
                DetachFromCentralOption = DetachFromCentralOption.DetachAndPreserveWorksets
            };

            // Abrir el documento
            selectedDoc = uiapp.Application.OpenDocumentFile(modelPath, openOpt);

            // Lista para almacenar todos los elementos y sus parámetros
            List<ElementData> elementsData = new List<ElementData>();

            if (selectedDoc != null)
            {               
                // Recolectar elementos
                FilteredElementCollector collector = new FilteredElementCollector(selectedDoc)
                    .WhereElementIsNotElementType()
                    .WherePasses(new LogicalOrFilter(new List<ElementFilter>
                    {
                            new ElementCategoryFilter(BuiltInCategory.OST_StructuralFraming),
                            new ElementCategoryFilter(BuiltInCategory.OST_StructuralColumns),
                            new ElementCategoryFilter(BuiltInCategory.OST_StructuralFoundation),
                            new ElementCategoryFilter(BuiltInCategory.OST_Doors),
                            new ElementCategoryFilter(BuiltInCategory.OST_Windows),
                            new ElementCategoryFilter(BuiltInCategory.OST_Walls),
                            new ElementCategoryFilter(BuiltInCategory.OST_Floors),
                            new ElementCategoryFilter(BuiltInCategory.OST_Stairs),
                            new ElementCategoryFilter(BuiltInCategory.OST_Parts),
                    }));

                // Almacenar datos de elementos
                foreach (Element elem in collector)
                {
                    ElementData elementData = new ElementData(elem);
                    if (elementData.Parameters.Any())  // Solo agregar elementos que tengan parámetros
                    {
                        elementsData.Add(elementData);
                    }
                }
            }

            // Actualizar elementos en el documento actual
            using (Transaction trans = new Transaction(doc, "Actualizar Parámetros"))
            {
                trans.Start();
                int updatedElements = 0;
                int updatedParameters = 0;

                foreach (ElementData elemData in elementsData)
                {
                    Element currentElement = doc.GetElement(elemData.Id);

                    if (currentElement != null)
                    {
                        bool elementUpdated = false;

                        foreach (ParameterData paramData in elemData.Parameters)
                        {
                            // Buscar parámetro por GUID
                            Parameter matchingParam = currentElement.Parameters
                                .Cast<Parameter>()
                                .FirstOrDefault(p => p.IsShared &&
                                                  p.GUID.ToString() == paramData.Guid);

                            if (matchingParam != null && !matchingParam.IsReadOnly)
                            {
                                try
                                {
                                    switch (paramData.StorageType)
                                    {
                                        case StorageType.String:
                                            matchingParam.Set(paramData.Value);
                                            break;
                                        case StorageType.Integer:
                                            matchingParam.Set(int.Parse(paramData.Value));
                                            break;
                                        case StorageType.Double:
                                            matchingParam.Set(double.Parse(paramData.Value));
                                            break;
                                        case StorageType.ElementId:
                                            matchingParam.Set(new ElementId(int.Parse(paramData.Value)));
                                            break;
                                    }
                                    updatedParameters++;
                                    elementUpdated = true;
                                }
                                catch (Exception ex)
                                {
                                    TaskDialog.Show("Error",
                                        $"Error al actualizar parámetro GUID:{paramData.Guid} " +
                                        $"en elemento {elemData.Id}: {ex.Message}");
                                }
                            }
                        }

                        if (elementUpdated)
                        {
                            updatedElements++;
                        }
                    }
                }

                trans.Commit();

                TaskDialog.Show("Resultado",
                $"Proceso completado.\n" +
                    $"Elementos actualizados: {updatedElements} de {elementsData.Count}\n" +
                    $"Parámetros actualizados: {updatedParameters}");
            }

            selectedDoc.Close(false);

            return Result.Succeeded;
        }


        public class ParameterData
        {
            public string Guid { get; set; }
            public string Value { get; set; }
            public StorageType StorageType { get; set; }

            public ParameterData(Parameter param)
            {
                if (param.IsShared && param.GUID != null)
                {
                    this.Guid = param.GUID.ToString();
                }

                this.StorageType = param.StorageType;

                switch (param.StorageType)
                {
                    case StorageType.String:
                        this.Value = param.AsString();
                        break;
                    case StorageType.Integer:
                        this.Value = param.AsInteger().ToString();
                        break;
                    case StorageType.Double:
                        this.Value = param.AsDouble().ToString();
                        break;
                    case StorageType.ElementId:
                        this.Value = param.AsElementId().ToString();
                        break;
                }
            }
        }

        public class ElementData
        {
            public ElementId Id { get; set; }
            public List<ParameterData> Parameters { get; set; }

            public ElementData(Element elem)
            {
                this.Id = elem.Id;
                this.Parameters = new List<ParameterData>();

                foreach (Parameter param in elem.Parameters)
                {
                    if (param.HasValue && !param.IsReadOnly && param.IsShared)
                    {
                        this.Parameters.Add(new ParameterData(param));
                    }
                }
            }
        }
    }
}

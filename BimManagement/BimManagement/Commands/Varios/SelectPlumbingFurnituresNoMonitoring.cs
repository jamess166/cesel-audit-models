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
using static BimManagement.UpdateConstructionDataCommand;

namespace BimManagement
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class SelectPlumbingFurnituresNoMonitoring : IExternalCommand
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

            // Recolectar elementos
            FilteredElementCollector collector = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .WherePasses(new LogicalOrFilter(new List<ElementFilter>
                {
                            new ElementCategoryFilter(BuiltInCategory.OST_PlumbingFixtures),
                }));

            List<Element> elementsNoMonitoring = new List<Element>();

            // Almacenar datos de elementos
            foreach (Element elem in collector)
            {
                if(!elem.IsMonitoringLinkElement())
                {
                    elementsNoMonitoring.Add(elem);
                }
            }

            sel.SetElementIds(elementsNoMonitoring.Select(e => e.Id).ToList());

            return Result.Succeeded;
        }
    }
}

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace BimManagement
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ShareSheetsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, 
            ElementSet elements)
        {
            // Get application and document objects
            UIApplication uiApp = commandData.Application;
            Document doc = uiApp.ActiveUIDocument.Document;
                       
            // Implementa la lógica para buscar una vista por nombre aquí            
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            collector.OfClass(typeof(ViewSheet));

            //foreach (ViewSheet vs in collector)
            //{
            //    TaskDialog.Show("revit", vs.SheetNumber);
            //}

            ViewSheet desiredView = collector.Cast<ViewSheet>()
                .FirstOrDefault(v => v.SheetNumber.Equals("101"));

            TaskDialog.Show("Revit", desiredView.SheetNumber);

            return Result.Succeeded;
        }
    }
}


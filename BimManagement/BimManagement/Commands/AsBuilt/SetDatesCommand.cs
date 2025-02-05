using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using BimManagement.Controller;
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
    public class SetDatesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message,
            ElementSet elements)
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


            ShowConductionSelectionDialog();

            //set of Excel
            SetAllDates.SetDatesofExcel();

            return Result.Succeeded;
        }

        public void ShowConductionSelectionDialog()
        {
            TaskDialog dialog = new TaskDialog("Selección de Conducción");
            dialog.MainInstruction = "Por favor, elija la conducción:";
            dialog.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No;
            dialog.DefaultButton = TaskDialogResult.Yes;
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Conducción CM3");
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Conducción Galindo");

            TaskDialogResult result = dialog.Show();

            if (result == TaskDialogResult.CommandLink1)
            {
                ViewSetDatesTools.NameSheetExcel = "CM3_METRADO";
            }
            else if (result == TaskDialogResult.CommandLink2)
            {
                ViewSetDatesTools.NameSheetExcel = "CGA_METRADO";
            }
            else
            {
                // El usuario ha cerrado el diálogo sin hacer una selección.
                // Puedes manejar esto según tus necesidades.
            }
        }
    }
}

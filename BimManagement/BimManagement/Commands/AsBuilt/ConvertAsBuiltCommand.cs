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
    public class ConvertAsBuiltCommand : IExternalCommand
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


            bool result = ShowConductionSelectionDialog();
            if (!result) return Result.Cancelled;

            //Purgar Parametros
            PurgeUnusedParametersTools.PurgeParameters();

            //Purgar Vistas que no se ven
            PurgeUnusedParametersTools.PurgeViews();

            //Agregar y llenar parametros
            AddSharedParameters.AddAndFillParameters();

            return Result.Succeeded;
        }

        public bool ShowConductionSelectionDialog()
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
                ViewAsBuiltTools.IsCM3 = true;
                ViewAsBuiltTools.IsConduction = true;
                ViewAsBuiltTools._01_11_DSI_Activo = "590 - Caballo Muerto 03";
            }
            else if (result == TaskDialogResult.CommandLink2)
            {
                ViewAsBuiltTools.IsCM3 = false;
                ViewAsBuiltTools.IsConduction = true;
                ViewAsBuiltTools._01_11_DSI_Activo = "590 - Conducción Galindo";
            }
            else
            {
                return false;
                // El usuario ha cerrado el diálogo sin hacer una selección.
                // Puedes manejar esto según tus necesidades.
            }

            return true;
        }
    }
}

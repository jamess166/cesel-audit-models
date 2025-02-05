#region Imported Namespaces

//.NET common used namespaces
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//Revit.NET common used namespaces
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.DB.Structure;

#endregion

namespace BimManagement
{
    /// <summary>
    /// Handler class destinated to provide an event handler for actions and modifications 
    /// of the active document
    /// </summary>
    class ShareSheetHandler : IExternalEventHandler
    {
        /// <summary>
        /// Event handler that provides context to execute actions over the Active UI 
        /// Document and set Sheet Parameter
        /// </summary>
        /// <param name="app"></param>
        public void Execute(UIApplication app)
        {
            Document doc = app.ActiveUIDocument.Document;
            //using (Transaction tr = new Transaction(doc,"Assign Sheet"))
            //{
                try
                {
                    //tr.Start();
                    ShareSheetsTools.Execute();
                    //tr.Commit();
                }
                catch (Exception)
                {
                    TaskDialog.Show("Share Sheet: Error"
                        , "An error has occured. Cannot assign sheet");
                }
            //}
        }

        public string GetName()
        {
            return "Sheet public Handler";
        }
    }
}

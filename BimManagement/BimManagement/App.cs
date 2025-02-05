#region Imported Namespaces

//.NET common used namespaces
//Revit.NET common used namespaces
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
//Revit.NET extend used namespaces
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
//FrataLibrary.NET common used namespaces
using RevitLibrary;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

#endregion

namespace BimManagement
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    internal class App : IExternalApplication
    {
        public const string ribbonTab = "CESEL";
        public const string ribbonPanel = "EOPNP Chorrillos";

        /// <summary>
        /// Este metodo implementa la aplicación que sera invocada cuando Revit inicie 
        /// antes que cualquier archivo o plantilla sea cargado.
        /// </summary>
        /// <param name="application">Uun objeto que se pasa desde la aplicacion externa que contiene la aplicacion controlada.</param>
        /// <returns>Devuelve el resultado del estado de la aplicacion externa. Resultado Exitoso, la aplicación cargo correctamente. Resultado Cancelado, el usuario cancelo la carga del aplicativo.  Resultado False, la aplicación no cargo y el usuario debe hacer correcciones.</returns>
        public Result OnStartup(UIControlledApplication application)
        {
            //Crear el ribbon tab y ribbon panel
            RibbonPanel panel = InterfaceRibbon.CreateRibbonPanel(application, ribbonTab, ribbonPanel);

            //Agregar boton de Copy Parameter
            //panel.AddItem(CreatePushButtons.ButtonShareSheet());
            panel.AddItem(CreatePushButtons.ButtonUpdateConstructionData());
            panel.AddItem(CreatePushButtons.ButtonModelAudit());

            m_MyForm = null;   // no dialog needed yet; the command will bring it
            thisApp = this;  // static access to this application instance

            return Result.Succeeded;
        }

        // class instance
        public static App thisApp = null;

        public static ShareSheetsView m_MyForm;

        /// <summary>
        /// Este metodo implementa la aplicación que sera invocada cuando Revit este cerrandodse 
        /// Todos los documentos abiertos se cierran antes de invocar este metodo.
        /// </summary>
        /// <param name="application">Uun objeto que se pasa desde la aplicacion externa que contiene la aplicacion controlada.</param>
        /// <returns>Devuelve el resultado del estado de la aplicacion externa.</returns>
        public Result OnShutdown(UIControlledApplication application)
        {
            if (m_MyForm != null && m_MyForm.Visibility.Equals(Visibility.Visible))
            {
                m_MyForm.Close();
            }
            return Result.Succeeded;
        }

        /// <summary>
        /// Mostrar formulario principal
        /// </summary>
        public void ShowForm()
        {
            //si no ha creado el dialogo aun y muestra
            if (m_MyForm != null) { return; }

            //Handler principal
            ShareSheetHandler handler = new ShareSheetHandler();

            //registro del evento
            ExternalEvent exEvent = ExternalEvent.Create(handler);

            //Mostrar windows
            m_MyForm = new ShareSheetsView(exEvent);
            m_MyForm.Closed += MyFormClosed;
            m_MyForm.Show();
        }

        /// <summary>
        /// Evento para vaciar el formulario luego de cerrar
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MyFormClosed(object sender, EventArgs e)
        {
            m_MyForm = null;
        }
    }
}
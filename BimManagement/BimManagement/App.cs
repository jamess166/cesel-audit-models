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
            panel.AddItem(CreatePushButtons.ButtonModelAuditBuilding());
            panel.AddItem(CreatePushButtons.ButtonWeeklyReport());
            panel.AddItem(CreatePushButtons.ButtonReportSheet());
            panel.AddItem(CreatePushButtons.ButtonExportPDF());


            m_WeeklyReportView = null;
            thisApp = this;  // static access to this application instance

            return Result.Succeeded;
        }

        // class instance
        public static App thisApp = null;

        public static WeeklyReportView       m_WeeklyReportView;
        public static CreateReportSheetView  m_CreateReportSheetView;

        /// <summary>
        /// Este metodo implementa la aplicación que sera invocada cuando Revit este cerrandodse 
        /// Todos los documentos abiertos se cierran antes de invocar este metodo.
        /// </summary>
        /// <param name="application">Uun objeto que se pasa desde la aplicacion externa que contiene la aplicacion controlada.</param>
        /// <returns>Devuelve el resultado del estado de la aplicacion externa.</returns>
        public Result OnShutdown(UIControlledApplication application)
        {
            if (m_WeeklyReportView != null && m_WeeklyReportView.Visibility.Equals(Visibility.Visible))
            {
                m_WeeklyReportView.Close();
            }

            if (m_CreateReportSheetView != null && m_CreateReportSheetView.Visibility.Equals(Visibility.Visible))
            {
                m_CreateReportSheetView.Close();
            }

            return Result.Succeeded;
        }

        public void ShowWindowWeeklyReport()
        {
            //si no ha creado el dialogo aun y muestra
            if (m_WeeklyReportView != null) { return; }

            //Handler principal
            UpdateFilesHandler handler = new UpdateFilesHandler();

            //registro del evento
            ExternalEvent exEvent = ExternalEvent.Create(handler);

            //Mostrar windows
            m_WeeklyReportView = new WeeklyReportView(exEvent);
            m_WeeklyReportView.Closed += WeeklyReportViewClosed;
            m_WeeklyReportView.Show();
        }

        public void ShowWindowCreateReportSheet()
        {
            if (m_CreateReportSheetView != null) { return; }

            CreateReportSheetHandler handler = new CreateReportSheetHandler();
            ExternalEvent exEvent = ExternalEvent.Create(handler);

            m_CreateReportSheetView = new CreateReportSheetView(exEvent);
            m_CreateReportSheetView.Closed += CreateReportSheetViewClosed;
            m_CreateReportSheetView.Show();
        }

        private void CreateReportSheetViewClosed(object sender, EventArgs e)
        {
            m_CreateReportSheetView = null;
        }

        /// <summary>
        /// Evento para vaciar el formulario luego de cerrar
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WeeklyReportViewClosed(object sender, EventArgs e)
        {
            m_WeeklyReportView = null;
        }
    }
}
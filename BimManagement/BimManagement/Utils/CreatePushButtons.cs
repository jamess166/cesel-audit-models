using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BimManagement
{
    public class CreatePushButtons
    {
        public static PushButtonData ButtonShareSheet()
        {
            RevitLibrary.ButtonData buttonData = new RevitLibrary.ButtonData()
            {
                AvailabilityClassName = string.Empty,
                Icon = Properties.Resources.Share,
                Name = "Enviar\nPlanos",
                Text = "Enviar\nPlanos",
                AssemblyName = Assembly.GetExecutingAssembly().Location,
                ClassName = "BimManagement.ShareSheetsApp",
                ShortDescription = "Prepara la documentacion para enviar",
                LongDescription = "Obtiene los planos del modelo para luego buscar esos nombres en los " +
                "archivos segun la revision que se indica, luego genera un modelo de revit para cada plano" +
                " y los pega en la carpeta de COMPARTIDOS con las misma estructura de de carpetas",

                Contextual = "https://www.cesel.com.pe/"
            };

            PushButtonData btnData = RevitLibrary.CreatePushButtons.PushButton(buttonData,
                Properties.Resources.ToolTip);

            return btnData;
        }

        public static PushButtonData ButtonUpdateConstructionData()
        {
            RevitLibrary.ButtonData buttonData = new RevitLibrary.ButtonData()
            {
                AvailabilityClassName = string.Empty,
                Icon = Properties.Resources.Share,
                Name = "Actualizar\nDatos",
                Text = "Actualizar\nDatos",
                AssemblyName = Assembly.GetExecutingAssembly().Location,
                ClassName = "BimManagement.UpdateConstructionDataCommand",
                ShortDescription = "Obtiene la información de obra de otro modelo",
                LongDescription = "Seleccionar el modelo del que se quiere obtiener los valores de parametros del modelo de Obra, " +
                "y actualiza la información al modelo abierto comparando comparando su ID de cada elemento",

                Contextual = "https://www.cesel.com.pe/"
            };

            PushButtonData btnData = RevitLibrary.CreatePushButtons.PushButton(buttonData,
                Properties.Resources.ToolTip);

            return btnData;
        }

        public static PushButtonData ButtonModelAudit()
        {
            RevitLibrary.ButtonData buttonData = new RevitLibrary.ButtonData()
            {
                AvailabilityClassName = string.Empty,
                Icon = Properties.Resources.audit,
                Name = "Auditar\nModelo",
                Text = "Auditar\nModelo",
                AssemblyName = Assembly.GetExecutingAssembly().Location,
                ClassName = "BimManagement.ModelAuditCommand",
                ShortDescription = "Realiza la auditoria del modelo",
                LongDescription = "Utilizando el formato de auditoria de Modelo, se valida o no el cumplimiento de los " +
                "items a revisar segun el plan de ejecución BIM",

                Contextual = "https://www.cesel.com.pe/"
            };

            PushButtonData btnData = RevitLibrary.CreatePushButtons.PushButton(buttonData,
                Properties.Resources.ToolTip);

            return btnData;
        }

        public static PushButtonData ButtonModelAuditBuilding()
        {
            RevitLibrary.ButtonData buttonData = new RevitLibrary.ButtonData()
            {
                AvailabilityClassName = string.Empty,
                Icon = Properties.Resources.audit,
                Name = "Auditar\nModelo Obra",
                Text = "Auditar\nModelo Obra",
                AssemblyName = Assembly.GetExecutingAssembly().Location,
                ClassName = "BimManagement.ModelAuditBuildingCommand",
                ShortDescription = "Realiza la auditoria del modelo de Obra",
                LongDescription = "Utilizando el formato de auditoria de Modelo, se valida la información mínima que deberá " +
                "tener el modelo de Obra",

                Contextual = "https://www.cesel.com.pe/"
            };

            PushButtonData btnData = RevitLibrary.CreatePushButtons.PushButton(buttonData,
                Properties.Resources.ToolTip);

            return btnData;
        }
    }
}

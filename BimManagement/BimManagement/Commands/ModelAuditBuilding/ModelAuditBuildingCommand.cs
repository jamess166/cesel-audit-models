using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using BimManagement.Commands.ModelAudit;
using BimManagement.Commands.ModelAudit.Config;
using BimManagement.Commands.ModelAuditBuilding;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using static BimManagement.UpdateConstructionDataCommand;
using Application = Autodesk.Revit.ApplicationServices.Application;

namespace BimManagement
{

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ModelAuditBuildingCommand : IExternalCommand
    {
        ValidationResult result_2d { get; set; }
        ValidationResult result_4a { get; set; }
        ValidationResult result_5b { get; set; }

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

            // Obtener la ruta del modelo actual
            string modelPath = doc.PathName;
            string modelDirectory = Path.GetDirectoryName(modelPath);
            string modelName = doc.Title.Replace(".rvt", "");
            string destinationExcelPath = Path.Combine(modelDirectory, modelName + ".xlsx");

            //Obtener el Template del archivo
            CopyTemplateXLS(modelPath, modelDirectory, modelName, destinationExcelPath);
            DateTime dateAudit = DateTime.Now;
            Speciality specialty = DetectSpecialty(modelName);

            //Validación 2d ----------------------------------------------------------------------------------
            var validatorCoordinates = new CheckSharedCoordinates();
            result_2d = validatorCoordinates.ValidateCoordinates();
            Debug.WriteLine("Finish 2d");

            //validacion 4a
            var checkParameters = new CheckParameters();
            result_4a = checkParameters.ValidateParameters(specialty,false);
            Debug.WriteLine("Finish 4a");

            List<string> parameters = new List<string>();
            parameters.Add("PO-ELEMENTO");
            parameters.Add("PO-ESTADO CONSTRUCCIÓN");
            parameters.Add("PO-SECTOR");
            parameters.Add("PO-NRO SEMANA");
            parameters.Add("PO-SEMANA PROYECTO");
            parameters.Add("PO-NRO REVISION");
            parameters.Add("PO-PLANO EN REVISION");
            parameters.Add("PO-PLANO REPLANTEO");
            parameters.Add("PO-FECHA PROGRAMADA");
            parameters.Add("PO-VACIADO (V/H)");
            parameters.Add("PO-VALORIZADO");
            parameters.Add("PO-WBS");

            var checkValueParameters = new CheckValueParameters();
            result_5b = checkValueParameters.ValidateParameters(parameters);
            Debug.WriteLine("Finish 5b");

            //Set adata Excel
            SetDataExcel(destinationExcelPath, dateAudit, modelName, specialty);

            TaskDialog.Show("CESEL", "Se ha terminado la revisión");

            return Result.Succeeded;
        }

        private void SetDataExcel(string excelPath, DateTime dateAudit, string modelName, Speciality specialty)
        {
            // Configuración para habilitar el uso de EPPlus (obligatorio en versiones recientes)
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            // Abre el archivo Excel
            using (ExcelPackage package = new ExcelPackage(new FileInfo(excelPath)))
            {
                // Obtén la primera hoja (o selecciona otra si es necesario)
                ExcelWorksheet worksheet = package.Workbook.Worksheets[0];

                // Modifica las celdas especificadas
                worksheet.Cells["F3"].Value = dateAudit;
                worksheet.Cells["E8"].Value = modelName;
                worksheet.Cells["D3"].Value = specialty.ToString();

                ExcelResultHandler excelResultHandler = new ExcelResultHandler();

                //Item 2d
                excelResultHandler.SetResultInCell(result_2d, worksheet, "E9", "F9");       

                //Item 4a
                excelResultHandler.SetResultInCell(result_4a, worksheet, "E10", "F10");

                //Item 5a
                excelResultHandler.SetResultInCell(result_5b, worksheet, "E11", "F11");               

                package.Save();
            }
        }

        // Diccionario con las palabras clave y su especialidad correspondiente
        private static readonly Dictionary<string, Speciality> specialtyKeywords = new Dictionary<string, Speciality>
        {
            { "ARQ", Speciality.ARQUITECTURA },
            { "EST", Speciality.ESTRUCTURAS },
            { "IEE", Speciality.ELECTRICAS },
            { "ISS", Speciality.SANITARIAS },
            { "AGU", Speciality.SANITARIAS },
            { "DES", Speciality.SANITARIAS },
            { "ACI", Speciality.SANITARIAS },
            { "DRE", Speciality.SANITARIAS },
            { "IMM", Speciality.MECANICAS },
            { "COM", Speciality.COMUNICACIONES },
            { "EQP", Speciality.ARQUITECTURA },
            { "SSA", Speciality.ARQUITECTURA },
        };

        private static Speciality DetectSpecialty(string fileName)
        {
            // Convertir el nombre del archivo a mayúsculas para hacer la comparación insensible a mayúsculas/minúsculas
            string upperFileName = fileName.ToUpper();

            // Buscar si alguna de las palabras clave está presente en el nombre del archivo
            foreach (var keyword in specialtyKeywords)
            {
                if (upperFileName.Contains(keyword.Key))
                {
                    return keyword.Value;
                }
            }

            return Speciality.ARQUITECTURA;
        }

        private void CopyTemplateXLS(string modelPath, string modelDirectory, string modelName, string destinationExcelPath)
        {
            // Obtener la ruta del DLL
            string dllPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string dllDirectory = Path.GetDirectoryName(dllPath);

            // Construir la ruta del archivo Excel original
            string sourceExcelPath = Path.Combine(dllDirectory, "Resources", "audit format building.xlsx");

            // Verificar si el archivo origen existe
            if (!File.Exists(sourceExcelPath))
            {
                TaskDialog.Show("Error", "No se encontró el archivo 'audit format.xlsx' en la carpeta Resources.");
            }

            // Copiar el archivo
            File.Copy(sourceExcelPath, destinationExcelPath, true);
        }
    }
}

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using BimManagement.Commands.ModelAudit;
using BimManagement.Commands.ModelAudit.Config;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static BimManagement.UpdateConstructionDataCommand;
using Application = Autodesk.Revit.ApplicationServices.Application;

namespace BimManagement
{
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; }
        public bool IsRelevant { get; set; }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ModelAuditCommand : IExternalCommand
    {
        ValidationResult result_1a { get; set; }
        ValidationResult result_1b { get; set; }
        ValidationResult result_2b { get; set; }
        ValidationResult result_2c { get; set; }
        ValidationResult result_2d { get; set; }
        ValidationResult result_2e { get; set; }
        ValidationResult result_2g { get; set; }
        ValidationResult result_3a { get; set; }
        ValidationResult result_3c { get; set; }
        ValidationResult result_4a { get; set; }
        ValidationResult result_5a { get; set; }
        ValidationResult result_6a { get; set; }
        ValidationResult result_8a { get; set; }
        ValidationResult result_8c { get; set; }
        ValidationResult result_8d { get; set; }



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

            //Data general
            string deliverable = "Entregable 07";
            DateTime dateAudit = DateTime.Now;
            //Speciality especialidad = Speciality.ARQUITECTURA;
            Speciality specialty = DetectSpecialty(modelName);

            //Validación 1a ----------------------------------------------------------------------------------
            var validatorModelName = new CheckModelNames();
            result_1a = validatorModelName.ValidateModelName(modelName);

            //Validación 1b ----------------------------------------------------------------------------------
            result_1b = CkeckFileSize.ValidateFileSize(modelPath);

            //Validación 1c ----------------------------------------------------------------------------------
            var validatorLevelName = new CheckLevelNames();
            result_2b = validatorLevelName.ValidateLevelNaming();

            //Validación 2c ----------------------------------------------------------------------------------
            if (specialty != Speciality.ARQUITECTURA)
            {
                result_2c = CheckGridsWidhArchitecture.CompareGridsLocation();
            }

            //Validación 2d ----------------------------------------------------------------------------------
            var validatorCoordinates = new CheckSharedCoordinates();
            result_2d = validatorCoordinates.ValidateCoordinates();

            //Validacion 2e
            var validatorViewOrganization = new CheckViewOrganization();
            result_2e = validatorViewOrganization.ValidateViews(specialty);

            //validacion 2g
            var geometryValidator = new GeometryValidator();
            result_2g = geometryValidator.ValidateGeometryDistance();

            //validacion 3a
            var familyNameValidator = new FamilyNameValidator();
            result_3a = familyNameValidator.ValidateFamilies(RevitTools.doc);

            //validacion 3c
            var checkInPlaceFamily = new CheckInPlaceFamily();
            result_3c = checkInPlaceFamily.ValidateModelInPlaceElements(RevitTools.doc);

            //validacion 4a
            var checkParameters = new CheckParameters();
            result_4a = checkParameters.ValidateParameters(specialty);

            //validacion 5a
            var checkDWGInModel = new CheckDWGInModelAndFamily();
            result_5a = checkDWGInModel.ValidateNoLinkedDWGs(RevitTools.doc);

            //validacion 6a
            var checkWarnings = new CheckWarnings();
            result_6a = checkWarnings.ValidateWarnings(RevitTools.doc);

            //validacion 8a
            var checkActiveWorkset = new CheckActiveWorkset();
            result_8a = checkActiveWorkset.ValidateActiveWorksets(RevitTools.doc);

            //validacion 8c
            var checkVoidTitleBlocks = new CheckVoidTitleBlocks();
            result_8c = checkVoidTitleBlocks.ValidateTitleBlocksContainViews(RevitTools.doc);

            //validacion 8d
            var checkHaveValueGroupView = new CheckHaveValueGroupView();
            result_8d = checkHaveValueGroupView.ValidateViewGroupParameters(RevitTools.doc);

            //Set adata Excel
            SetDataExcel(destinationExcelPath, deliverable, dateAudit, modelName, specialty);

            return Result.Succeeded;
        }

        private void SetDataExcel(string excelPath, string deliverable, DateTime dateAudit, string modelName, Speciality specialty)
        {
            // Configuración para habilitar el uso de EPPlus (obligatorio en versiones recientes)
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            // Abre el archivo Excel
            using (ExcelPackage package = new ExcelPackage(new FileInfo(excelPath)))
            {
                // Obtén la primera hoja (o selecciona otra si es necesario)
                ExcelWorksheet worksheet = package.Workbook.Worksheets[0];

                // Modifica las celdas especificadas
                worksheet.Cells["F2"].Value = deliverable;
                worksheet.Cells["F3"].Value = dateAudit;
                worksheet.Cells["E8"].Value = modelName;
                worksheet.Cells["D3"].Value = specialty.ToString();

                ExcelResultHandler excelResultHandler = new ExcelResultHandler();

                //Item 1a
                excelResultHandler.SetResultInCell(result_1a, worksheet, "E9", "F9");

                //Item 1b
                excelResultHandler.SetResultInCell(result_1b, worksheet, "E10", "F10");

                //Item 2b
                excelResultHandler.SetResultInCell(result_2b, worksheet, "E16", "F16");

                //Item 2c
                excelResultHandler.SetResultInCell(result_2c, worksheet, "E17", "F17");

                //Item 2d
                excelResultHandler.SetResultInCell(result_2d, worksheet, "E18", "F18");

                //Item 2e
                excelResultHandler.SetResultInCell(result_2e, worksheet, "E19", "F19");

                //Item 2g
                excelResultHandler.SetResultInCell(result_2g, worksheet, "E21", "F21");

                //Item 3a
                excelResultHandler.SetResultInCell(result_3a, worksheet, "E22", "F22");

                //Item 3c
                excelResultHandler.SetResultInCell(result_3c, worksheet, "E24", "F24");

                //Item 4a
                excelResultHandler.SetResultInCell(result_4a, worksheet, "E25", "F25");

                //Item 5a
                excelResultHandler.SetResultInCell(result_5a, worksheet, "E28", "F28");

                //Item 6a
                excelResultHandler.SetResultInCell(result_6a, worksheet, "E31", "F31");

                //Item 8a
                excelResultHandler.SetResultInCell(result_8a, worksheet, "E36", "F36");

                //Item 8c
                excelResultHandler.SetResultInCell(result_8c, worksheet, "E38", "F38");

                //Item 8d
                excelResultHandler.SetResultInCell(result_8d, worksheet, "E39", "F39");

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

        public static Speciality DetectSpecialty(string fileName)
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
            string sourceExcelPath = Path.Combine(dllDirectory, "Resources", "audit format.xlsx");

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

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
    public class DeliverableData
    {
        public string Entregable { get; set; }
        public DateTime Fecha { get; set; }
    }


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
        ValidationResult result_1d { get; set; }
        ValidationResult result_2b { get; set; }
        ValidationResult result_2c { get; set; }
        ValidationResult result_2d { get; set; }
        ValidationResult result_2e { get; set; }
        ValidationResult result_2g { get; set; }
        ValidationResult result_3a { get; set; }
        ValidationResult result_3c { get; set; }
        ValidationResult result_4a { get; set; }
        ValidationResult result_5a { get; set; }
        ValidationResult result_5b { get; set; }

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

            // Preguntar al usuario qué tipo de ejecución desea
            TaskDialogResult dialogResult = ShowExecutionModeDialog();

            if (dialogResult == TaskDialogResult.CommandLink1)
            {
                // Ejecutar en modelo actual
                return ExecuteSingleModel(commandData, ref message, elements);
            }
            else if (dialogResult == TaskDialogResult.CommandLink2)
            {
                // Ejecutar en múltiples modelos
                return ExecuteMultipleModels(uiapp);
            }
            else
            {
                // Usuario canceló
                return Result.Cancelled;
            }
        }
        private List<FileInfo> GetAllRevitFiles(string folderPath)
        {
            List<FileInfo> revitFiles = new List<FileInfo>();

            try
            {
                DirectoryInfo dirInfo = new DirectoryInfo(folderPath);

                // Buscar archivos .rvt recursivamente, excluyendo backups
                var files = dirInfo.GetFiles("*.rvt", SearchOption.AllDirectories)
                                  .Where(f => !f.Name.Contains(".0") && // Excluir backups (.0001.rvt)
                                             !f.DirectoryName.Contains("backup") && // Excluir carpetas backup
                                             !f.Name.StartsWith("~")) // Excluir archivos temporales
                                  .ToList();

                revitFiles.AddRange(files);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"Error al buscar archivos: {ex.Message}");
            }

            return revitFiles;
        }

        private Result ExecuteSingleModel(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            //UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uiapp.ActiveUIDocument.Document;
            //Selection sel = uidoc.Selection;

            //// Variables estáticas
            //RevitTools.uiApp = uiapp;
            //RevitTools.doc = doc;
            //RevitTools.app = app;
            //RevitTools.uidoc = uidoc;
            //RevitTools.sel = sel;
            //RevitTools.elements = null;

            // Ejecutar validaciones para el modelo actual
            ProcessModel(doc, app);

            TaskDialog.Show("CESEL", "Se ha terminado la revisión del modelo actual");
            return Result.Succeeded;
        }

        private Result ExecuteMultipleModels(UIApplication uiapp)
        {
            // Seleccionar carpeta usando OpenFileDialog
            string selectedFolder = string.Empty;

            using (var openFile = new OpenFileDialog())
            {
                openFile.ValidateNames = false;
                openFile.CheckFileExists = false;
                openFile.CheckPathExists = true;
                openFile.FileName = "Seleccione la carpeta con los modelos de Revit";
                openFile.Title = "Seleccionar Carpeta";

                if (openFile.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return Result.Cancelled;
                }

                selectedFolder = Path.GetDirectoryName(openFile.FileName);
            }

            if (string.IsNullOrEmpty(selectedFolder))
            {
                return Result.Cancelled;
            }

            // Buscar todos los archivos .rvt en la carpeta y subcarpetas
            List<FileInfo> revitFiles = GetAllRevitFiles(selectedFolder);

            if (revitFiles.Count == 0)
            {
                TaskDialog.Show("CESEL", "No se encontraron archivos de Revit en la carpeta seleccionada");
                return Result.Failed;
            }

            // Confirmar con el usuario
            TaskDialogResult confirm = TaskDialog.Show("Confirmación",
                $"Se encontraron {revitFiles.Count} archivos de Revit.\n¿Desea continuar con la validación?",
                TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);

            if (confirm != TaskDialogResult.Yes)
            {
                return Result.Cancelled;
            }

            // Procesar cada modelo
            var app = uiapp.Application;
            int total = revitFiles.Count;
            int current = 0;
            int successful = 0;
            int failed = 0;
            List<string> errorMessages = new List<string>();

            foreach (var fileInfo in revitFiles)
            {
                try
                {
                    string name = fileInfo.Name;
                    string path = fileInfo.FullName;

                    // Abrir el modelo
                    var modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(path);
                    var openOpts = new OpenOptions { Audit = false };
                    var doc = app.OpenDocumentFile(modelPath, openOpts);

                    // Procesar el modelo
                    ProcessModel(doc, app);

                    // Cerrar el modelo guardando los cambios
                    doc.Close(false); // false = no guardar cambios

                    successful++;
                    System.Diagnostics.Debug.WriteLine($"✔ Finalizado: {name}");
                }
                catch (Exception ex)
                {
                    failed++;
                    string errorMsg = $"✖ Error en '{fileInfo.Name}': {ex.Message}";
                    errorMessages.Add(errorMsg);
                    System.Diagnostics.Debug.WriteLine(errorMsg);
                }

                current++;
                // Aquí podrías mostrar progreso si tienes una ventana de progreso
            }

            // Mostrar resumen
            string summary = $"Validación completada:\n\n" +
                            $"Total de modelos: {total}\n" +
                            $"Exitosos: {successful}\n" +
                            $"Fallidos: {failed}";

            if (errorMessages.Count > 0)
            {
                summary += "\n\nErrores:\n" + string.Join("\n", errorMessages.Take(10));
                if (errorMessages.Count > 10)
                {
                    summary += $"\n... y {errorMessages.Count - 10} errores más";
                }
            }

            TaskDialog.Show("CESEL - Resumen", summary);

            return Result.Succeeded;
        }

        private void ProcessModel(Document doc, Application app)
        {
            RevitTools.doc = doc;
            RevitTools.app = app;

            // Obtener la ruta del modelo actual
            string modelPath = doc.PathName;
            string modelDirectory = Path.GetDirectoryName(modelPath);
            string modelName = doc.Title.Replace(".rvt", "");
            string destinationExcelPath = Path.Combine(modelDirectory, modelName + ".xlsx");

            // Obtener el Template del archivo
            CopyTemplateXLS(modelPath, modelDirectory, modelName, destinationExcelPath);

            // Data general
            var loadedData = JsonHelper.LoadDeliverable(modelDirectory);
            string deliverable = loadedData.Entregable;
            DateTime dateAudit = loadedData.Fecha;
            Speciality specialty = DetectSpecialty(modelName);

            // Ejecutar todas las validaciones
            var validatorModelName = new CheckModelNames();
            result_1a = validatorModelName.ValidateModelName(modelName);

            result_1b = CkeckFileSize.ValidateFileSize(modelPath);

            var checkIsInModelList = new CheckIsInModelList();
            result_1d = checkIsInModelList.ValidateFileNameInExcel(modelName);

            var validatorLevelName = new CheckLevelNames();
            result_2b = validatorLevelName.ValidateLevelNaming();

            if (specialty != Speciality.ARQUITECTURA && specialty != Speciality.EQUIPAMIENTO)
            {
                result_2c = CheckGridsWidhArchitecture.CompareGridsLocation(modelPath);
            }

            var validatorCoordinates = new CheckSharedCoordinates();
            result_2d = validatorCoordinates.ValidateCoordinates();

            var validatorViewOrganization = new CheckViewOrganization();
            result_2e = validatorViewOrganization.ValidateViews(specialty);

            var geometryValidator = new GeometryValidator();
            result_2g = geometryValidator.ValidateGeometryDistance();

            var familyNameValidator = new FamilyNameValidator();
            result_3a = familyNameValidator.ValidateFamilies(doc);

            var checkInPlaceFamily = new CheckInPlaceFamily();
            result_3c = checkInPlaceFamily.ValidateModelInPlaceElements(doc);

            var checkParameters = new CheckParameters();
            result_4a = checkParameters.ValidateParameters(specialty);

            var checkDWGInModel = new CheckDWGInModelAndFamily();
            result_5a = checkDWGInModel.ValidateNoLinkedDWGs(doc);

            var CheckParametersExtension = new CheckParametersExtension();
            result_5b = CheckParametersExtension.ValidateRequiredParameters(specialty);

            var checkWarnings = new CheckWarnings();
            //result_6a = checkWarnings.ValidateWarnings(doc);

            var checkActiveWorkset = new CheckActiveWorkset();
            result_8a = checkActiveWorkset.ValidateActiveWorksets(doc);

            var checkVoidTitleBlocks = new CheckVoidTitleBlocks();
            result_8c = checkVoidTitleBlocks.ValidateTitleBlocksContainViews(doc);

            var checkHaveValueGroupView = new CheckHaveValueGroupView();
            result_8d = checkHaveValueGroupView.ValidateViewGroupParameters(doc);

            // Set data Excel
            SetDataExcel(destinationExcelPath, deliverable, dateAudit, modelName, specialty);
        }
               

        //public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        //{
        //    // Get application and document objects
        //    UIApplication uiapp = commandData.Application;
        //    UIDocument uidoc = uiapp.ActiveUIDocument;
        //    Application app = uiapp.Application;
        //    Document doc = uiapp.ActiveUIDocument.Document;
        //    Selection sel = uidoc.Selection;

        //    //variables estaticas
        //    RevitTools.uiApp = uiapp;
        //    RevitTools.doc = doc;
        //    RevitTools.app = app;
        //    RevitTools.uidoc = uidoc;
        //    RevitTools.sel = sel;
        //    RevitTools.elements = null;

        //    // Obtener la ruta del modelo actual
        //    string modelPath = doc.PathName;
        //    string modelDirectory = Path.GetDirectoryName(modelPath);
        //    string modelName = doc.Title.Replace(".rvt", "");
        //    string destinationExcelPath = Path.Combine(modelDirectory, modelName + ".xlsx");

        //    //Obtener el Template del archivo
        //    CopyTemplateXLS(modelPath, modelDirectory, modelName, destinationExcelPath);

        //    //Data general
        //    // Leer datos del JSON
        //    var loadedData = JsonHelper.LoadDeliverable(modelDirectory);

        //    //string deliverable = "Entregable 10";
        //    string deliverable = loadedData.Entregable;
        //    DateTime dateAudit = loadedData.Fecha;
        //    //Speciality especialidad = Speciality.ARQUITECTURA;
        //    Speciality specialty = DetectSpecialty(modelName);


        //    //Validación 1a ----------------------------------------------------------------------------------
        //    var validatorModelName = new CheckModelNames();
        //    result_1a = validatorModelName.ValidateModelName(modelName);

        //    //Validación 1b ----------------------------------------------------------------------------------
        //    result_1b = CkeckFileSize.ValidateFileSize(modelPath);

        //    //Validación 1d
        //    var checkIsInModelList = new CheckIsInModelList();
        //    result_1d = checkIsInModelList.ValidateFileNameInExcel(modelName);

        //    //Validación 2b ----------------------------------------------------------------------------------
        //    var validatorLevelName = new CheckLevelNames();
        //    result_2b = validatorLevelName.ValidateLevelNaming();

        //    //Validación 2c ----------------------------------------------------------------------------------
        //    if (specialty != Speciality.ARQUITECTURA && specialty != Speciality.EQUIPAMIENTO)
        //    {
        //        result_2c = CheckGridsWidhArchitecture.CompareGridsLocation();
        //    }

        //    //Validación 2d ----------------------------------------------------------------------------------
        //    var validatorCoordinates = new CheckSharedCoordinates();
        //    result_2d = validatorCoordinates.ValidateCoordinates();

        //    //Validacion 2e
        //    var validatorViewOrganization = new CheckViewOrganization();
        //    result_2e = validatorViewOrganization.ValidateViews(specialty);

        //    //validacion 2g
        //    var geometryValidator = new GeometryValidator();
        //    result_2g = geometryValidator.ValidateGeometryDistance();

        //    //validacion 3a
        //    var familyNameValidator = new FamilyNameValidator();
        //    result_3a = familyNameValidator.ValidateFamilies(RevitTools.doc);

        //    //validacion 3c
        //    var checkInPlaceFamily = new CheckInPlaceFamily();
        //    result_3c = checkInPlaceFamily.ValidateModelInPlaceElements(RevitTools.doc);

        //    //validacion 4a
        //    var checkParameters = new CheckParameters();
        //    result_4a = checkParameters.ValidateParameters(specialty);

        //    //validacion 5a
        //    var checkDWGInModel = new CheckDWGInModelAndFamily();
        //    result_5a = checkDWGInModel.ValidateNoLinkedDWGs(RevitTools.doc);

        //    //validacion 5b
        //    var CheckParametersExtension = new CheckParametersExtension();
        //    result_5b = CheckParametersExtension.ValidateRequiredParameters(specialty);

        //    //validacion 6a
        //    var checkWarnings = new CheckWarnings();
        //    //result_6a = checkWarnings.ValidateWarnings(RevitTools.doc);

        //    //validacion 8a
        //    var checkActiveWorkset = new CheckActiveWorkset();
        //    result_8a = checkActiveWorkset.ValidateActiveWorksets(RevitTools.doc);

        //    //validacion 8c
        //    var checkVoidTitleBlocks = new CheckVoidTitleBlocks();
        //    result_8c = checkVoidTitleBlocks.ValidateTitleBlocksContainViews(RevitTools.doc);

        //    //validacion 8d
        //    var checkHaveValueGroupView = new CheckHaveValueGroupView();
        //    result_8d = checkHaveValueGroupView.ValidateViewGroupParameters(RevitTools.doc);

        //    //Set adata Excel
        //    SetDataExcel(destinationExcelPath, deliverable, dateAudit, modelName, specialty);

        //    TaskDialog.Show("CESEL", "Se ha terminado la revisión");

        //    return Result.Succeeded;
        //}

        private TaskDialogResult ShowExecutionModeDialog()
        {
            TaskDialog dialog = new TaskDialog("Modo de Ejecución");
            dialog.MainInstruction = "¿Cómo desea ejecutar la validación?";
            dialog.MainContent = "Seleccione una opción:";

            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1,
                "Modelo Actual",
                "Ejecutar validación solo en el modelo actualmente abierto");

            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2,
                "Múltiples Modelos",
                "Seleccionar carpeta y ejecutar en todos los modelos encontrados");

            dialog.CommonButtons = TaskDialogCommonButtons.Cancel;
            dialog.DefaultButton = TaskDialogResult.CommandLink1;

            return dialog.Show();
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

                //Item 1d
                excelResultHandler.SetResultInCell(result_1d, worksheet, "E12", "F12");

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

                //Item 5b
                excelResultHandler.SetResultInCell(result_5b, worksheet, "E29", "F29");

                //Item 6a
                //excelResultHandler.SetResultInCell(result_6a, worksheet, "E31", "F31");

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
            { "EQP", Speciality.EQUIPAMIENTO },
            { "SSA", Speciality.ARQUITECTURA },
        };

        public static Speciality DetectSpecialty(string fileName)
        {
            // Convertir a mayúsculas y separar por guiones
            string[] segments = fileName.ToUpper().Split('-');

            foreach (string segment in segments)
            {
                if (specialtyKeywords.TryGetValue(segment, out Speciality detected))
                {
                    return detected;
                }
            }

            // Si no se encuentra, puedes retornar una especialidad por defecto
            return Speciality.ARQUITECTURA;
        }
        
        // public static Speciality DetectSpecialty(string fileName)
        // {
        //     // Convertir el nombre del archivo a mayúsculas para hacer la comparación insensible a mayúsculas/minúsculas
        //     string upperFileName = fileName.ToUpper();
        //
        //     // Buscar si alguna de las palabras clave está presente en el nombre del archivo
        //     foreach (var keyword in specialtyKeywords)
        //     {
        //         if (upperFileName.Contains(keyword.Key))
        //         {
        //             return keyword.Value;
        //         }
        //     }
        //
        //     return Speciality.ARQUITECTURA;
        // }
          
        public void CopyTemplateXLS(string modelPath, string modelDirectory, string modelName, string destinationExcelPath)
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

#region Imported Namespaces

//.NET common used namespaces
//Revit.NET common used namespaces
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.Creation;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Autodesk.Revit.UI.Selection;
using RevitLibrary;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Xml.Linq;

#endregion

namespace BimManagement
{
    //Main execute
    internal class ShareSheetsTools
    {
        public static string LastDocument { get; set; }
        public static string modelFile { get; set; }
        public static string sharePath { get; set; }
        public static string revisionPath { get; set; }

        public static string ModelPath { get; set; }

        /// <summary>
        /// Ejecucion Principal
        /// </summary>
        public static void Execute()
        {
            try
            {
                // Select the base element
                modelFile = RevitTools.ReturnModelPath(RevitTools.doc);
                if (string.IsNullOrEmpty(modelFile)) return;

                string modelPath = System.IO.Path.GetDirectoryName(modelFile);
                //string relativePath = GetRelativePath(modelPath);

                ModelPath = GetModelPath();
                revisionPath = GetRevisionPath();
                sharePath = GetSpecificSharePath();

                List<ElementId> elementsID = ViewTools.SelectedSheets
                                            .Select(sheetDetail => sheetDetail.Sheet.Id)
                                            .ToList();

                string pdfPath = ViewTools.DivideNativePDF
                                ? System.IO.Path.Combine(revisionPath, "PDF")
                                : revisionPath;

                if (ViewTools.CreatePDFToo)
                {
                    ExportToPDFTools.ExportSheetToPDF(elementsID, pdfPath);
                }

                string rvtPath = ViewTools.DivideNativePDF
                                ? System.IO.Path.Combine(revisionPath, "NATIVOS")
                                : revisionPath;

                CreateRevitDoc(rvtPath);

                //Eliminar backups
                DeleteBackupsRvt(revisionPath);

                string message = $"Se han copiado {ViewTools.SelectedSheets.Count()} en la carpeta de En " +
                    $"Proceso como revision REV {ViewTools.Revision}";

                //Copiar a las carpetas compartidas
                if (ViewTools.CopyShared)
                {
                    CopyDirectory(revisionPath, sharePath);

                    message = message + ", asi como en la carpeta de compartidos";
                }

                TaskDialog.Show("CASA", message);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("CASA", ex.Message);
            }
        }

        public static string GetModelPath()
        {
            string modelPath = System.IO.Path.GetDirectoryName(modelFile);
            //string relativePath = GetRelativePath(modelPath);

            return modelPath;
        }

        /// <summary>
        /// Borra los backups de RVT
        /// </summary>
        /// <param name="revisionPath"></param>
        static void DeleteBackupsRvt(string revisionPath)
        {
            string[] archivosAEliminar;

            archivosAEliminar = Directory.GetFiles(ViewTools.DivideNativePDF
                ? (revisionPath + "\\NATIVOS") : revisionPath, "*.000*.rvt");

            // Elimina cada archivo
            foreach (string archivo in archivosAEliminar)
            {
                File.Delete(archivo);
            }
        }

        /// <summary>
        /// Copiar el directorio completo carpetas y archivos
        /// </summary>
        /// <param name="source"></param>
        /// <param name="destination"></param>
        static void CopyDirectory(string source, string destination)
        {
            // Asegúrate de que el directorio de origen exista
            if (!Directory.Exists(source)) return;

            // Crea el directorio de destino si no existe

            Directory.CreateDirectory(destination ??
                throw new ArgumentNullException(nameof(destination)));

            // Obtiene la lista de archivos y carpetas en el directorio de origen
            string[] files = Directory.GetFiles(source);
            string[] directories = Directory.GetDirectories(source);

            // Copia archivos
            foreach (string file in files)
            {
                string destinationFile = System.IO.Path
                        .Combine(destination, System.IO.Path.GetFileName(file));
                File.Copy(file, destinationFile, true);
            }

            // Copia carpetas (recursivamente)
            foreach (string directory in directories)
            {
                string destinationSubdirectory = System.IO.Path
                    .Combine(destination, System.IO.Path.GetFileName(directory));
                CopyDirectory(directory, destinationSubdirectory);
            }
        }

        /// <summary>
        /// Copia los documentos de revit
        /// </summary>
        /// <param name="destinePath"></param>
        /// <param name="filtersPdf"></param>
        /// <param name="typeFile"></param>
        private static void CreateRevitDoc(string destinePath)
        {
            // Guarda todos los cambios en el documento original
            RevitTools.doc.Save();

            Directory.CreateDirectory(destinePath ??
                throw new ArgumentNullException(nameof(destinePath)));

            // Ahora puedes copiar los archivos a sharePath
            foreach (SheetDetail sheetDetail in ViewTools.SelectedSheets)
            {
                Parameter revisionParam = sheetDetail.Sheet.get_Parameter(BuiltInParameter.SHEET_CURRENT_REVISION);
                string revision = revisionParam.AsString();
                string destinationPath = System.IO.Path.Combine(destinePath
                    , sheetDetail.NumberSheet + "-" + revision);

                // Realiza la copia del modelo completo
                SaveAsOptions saveAsOptions = new SaveAsOptions();
                saveAsOptions.OverwriteExistingFile = true;
                saveAsOptions.MaximumBackups = 1;
                RevitTools.doc.SaveAs($"{destinationPath}.rvt", saveAsOptions);

                // Cambia la vista inicial del nuevo modelo
                ChangeInitialView(sheetDetail.Sheet);
            }
        }

        /// <summary>
        /// Cambiar la vista nueva y guarda el modelo
        /// </summary>
        /// <param name="viewName"></param>
        static void ChangeInitialView(ViewSheet sheet)
        {
            FilteredElementCollector startingViewSettingsCollector = 
                new FilteredElementCollector(RevitTools.doc);
            startingViewSettingsCollector.OfClass(typeof(StartingViewSettings));

            StartingViewSettings viewSettings = (StartingViewSettings)startingViewSettingsCollector.FirstOrDefault();

            Autodesk.Revit.DB.View startingView = null;

            foreach (StartingViewSettings settings in startingViewSettingsCollector)
            {                
                startingView = (Autodesk.Revit.DB.View)RevitTools.doc.GetElement(settings.ViewId);
            }

            // Establece la vista inicial del modelo
            if (sheet != null)
            {
                RevitTools.uidoc.ActiveView = sheet;
                
                RevitTools.doc.Save();
            }
        }

        /// <summary>
        /// Devuelve el path para guardar las revisiones
        /// </summary>
        /// <param name="relativePath"></param>
        /// <returns></returns>
        private static string GetRevisionPath()
        {
            string revisionPath = ModelPath;

            if (!string.IsNullOrEmpty(ViewTools.DirectoryPath))
            {
                revisionPath = ViewTools.DirectoryPath;
            }

            //Agregar revision
            revisionPath += $"\\REV {ViewTools.Revision.ToUpper()}";

            return revisionPath;
        }

        /// <summary>
        /// Devuelve un path especifico
        /// </summary>
        /// <param name="relativePath"></param>
        /// <returns></returns>
        private static string GetSpecificSharePath()
        {
            if (!ModelPath.Contains(@"01 EN PROCESO\03 REPLANTEO")) return string.Empty;

            return ModelPath.Replace(@"01 EN PROCESO\03 REPLANTEO", @"02 COMPARTIDO\02 REPLANTEO")
                + $"\\REV {ViewTools.Revision.ToUpper()}";
        }
    }
}
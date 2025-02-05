using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace BimManagement
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class PrintPDF : IExternalCommand
    {
        public Document doc { get; set; }

        public Result Execute(ExternalCommandData commandData, ref string message,
            ElementSet elements)
        {
            // Get application and document objects
            UIApplication uiApp = commandData.Application;
            doc = uiApp.ActiveUIDocument.Document;

            //// Implementa la lógica para buscar una vista por nombre aquí            
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            List<ElementId> sheetsId = collector
                .OfClass(typeof(ViewSheet)).ToElementIds().ToList();

            string path = @"C:\Users\Usuario\Desktop\Test";

            ExportSheetToPDF(sheetsId, path);

            //ViewSheet desiredView = collector.Cast<ViewSheet>()
            //    .FirstOrDefault(v => v.SheetNumber.Equals("101"));

            //TaskDialog.Show("Revit", desiredView.SheetNumber);

            return Result.Succeeded;
        }

        /// <summary>
        /// Return Options PDF
        /// </summary>
        /// <returns></returns>
        public PDFExportOptions PDFExportOptions()
        {
            PDFExportOptions optionsPDF = new PDFExportOptions();

            optionsPDF.ExportQuality = PDFExportQualityType.DPI4000;
            optionsPDF.RasterQuality = RasterQualityType.Presentation;
            optionsPDF.AlwaysUseRaster = true;
            optionsPDF.PaperFormat = ExportPaperFormat.ISO_A3;
            optionsPDF.ZoomType = ZoomType.Zoom;
            optionsPDF.ZoomPercentage = 100;
            optionsPDF.HideCropBoundaries = true;
            optionsPDF.HideReferencePlane = true;
            optionsPDF.HideScopeBoxes = true;
            optionsPDF.HideUnreferencedViewTags = true;
            optionsPDF.MaskCoincidentLines = true;
            optionsPDF.ViewLinksInBlue = false;
            optionsPDF.ColorDepth = ColorDepthType.Color;
            optionsPDF.StopOnError = false;
            optionsPDF.Combine = false;

            return optionsPDF;
        }

        /// <summary>
        /// Rule name file Export PDF
        /// </summary>
        /// <returns></returns>
        public static List<TableCellCombinedParameterData> CombineParameterData()
        {
            TableCellCombinedParameterData cellCombinedNumber = TableCellCombinedParameterData.Create();
            BuiltInParameter paramNumberr = BuiltInParameter.SHEET_NUMBER;
            ElementId paramNumberId = new ElementId(paramNumberr);

            cellCombinedNumber.Separator = "-";
            cellCombinedNumber.ParamId = paramNumberId;

            TableCellCombinedParameterData cellCombinedRevision = TableCellCombinedParameterData.Create();
            BuiltInParameter paramRevision = BuiltInParameter.SHEET_CURRENT_REVISION;
            ElementId paramRevisionId = new ElementId(paramRevision);

            cellCombinedRevision.ParamId = paramRevisionId;

            List<TableCellCombinedParameterData> tableCellCombineds = new List<TableCellCombinedParameterData>();
            tableCellCombineds.Add(cellCombinedNumber);
            tableCellCombineds.Add(cellCombinedRevision);

            return tableCellCombineds;
        }

        /// <summary>
        /// Export PDF File
        /// </summary>
        /// <param name="sheetsId"></param>
        /// <param name="path"></param>
        public void ExportSheetToPDF(List<ElementId> sheetsId, string path)
        {
            PDFExportOptions optionsPDF = PDFExportOptions();

            List<TableCellCombinedParameterData> tableCellCombineds = CombineParameterData();
            optionsPDF.SetNamingRule(tableCellCombineds);

            doc.Export(path, sheetsId, optionsPDF);
        }
    }
}
using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace BimManagement
{
    public class ExportToPDFTools
    {
        /// <summary>
        /// Return Options PDF
        /// </summary>
        /// <returns></returns>
        public static PDFExportOptions PDFExportOptions()
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
        public static void ExportSheetToPDF(List<ElementId> sheetsId, string path)
        {
            PDFExportOptions optionsPDF = PDFExportOptions();

            List<TableCellCombinedParameterData> tableCellCombineds = CombineParameterData();
            optionsPDF.SetNamingRule(tableCellCombineds);

            //if (!Directory.Exists(path))
            //{
            //    Directory.CreateDirectory(path);
            //}

            Directory.CreateDirectory(path ?? 
                throw new ArgumentNullException(nameof(path)));


            RevitTools.doc.Export(path, sheetsId, optionsPDF);
        }
    }
}

#region Imported Namespaces

//.NET common used namespaces
//Revit.NET common used namespaces
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Autodesk.Revit.UI.Selection;
using RevitLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

#endregion

namespace BimManagement
{
    public class SheetDetail
    {
        public bool IsSelected { get; set; }
        public string NumberSheet { get; set; }
        public string Revision { get; set; }
        public ViewSheet Sheet { get; set; }

        // Constructor privado para evitar instancias externas
        public SheetDetail()
        {
            // Inicializar propiedades u otras configuraciones si es necesario
            IsSelected = false;
            NumberSheet = string.Empty;
            Revision = string.Empty;
            Sheet = null;
        }

        // Instancia única de la clase SheetDetail
        private static SheetDetail instance = new SheetDetail();

        // Propiedad para acceder a la instancia única
        public static SheetDetail Instance
        {
            get { return instance; }
        }
    }

    public class Tools
    {
        public static bool ExistPath(string path)
        {
            if (!Directory.Exists(path)) return false;
            return true;
        }
    }

    public class ViewSheetComparer : IComparer<ViewSheet>
    {
        public int Compare(ViewSheet sheet1, ViewSheet sheet2)
        {
            return sheet1.SheetNumber.CompareTo(sheet2.SheetNumber);
        }
    }
    /// <summary>
    /// Herramientas para obtner elementos
    /// </summary>
    internal class RevitTools
    {
        public static UIApplication uiApp { get; set; }
        public static ExternalCommandData commandData { get; set; }
        public static UIDocument uidoc { get; internal set; }
        public static Application app { get; set; }
        public static Document doc { get; set; }
        public static Selection sel { get; set; }
        public static IList<Element> elements { get; set; }
        public static List<SheetDetail> SheetDetails { get; set; }

        /// <summary>
        /// Devuelve el directorio del modelo
        /// </summary>
        /// <param name="doc"></param>
        /// <returns></returns>
        internal static string ReturnModelPath(Autodesk.Revit.DB.Document doc)
        {
            //string pathOrigen = string.Empty;

            //No hace nada si es un modelo en la nube
            if (doc.IsModelInCloud) { return string.Empty; }

            string pathOrigen = doc.IsWorkshared
                    ? ModelPathUtils.ConvertModelPathToUserVisiblePath(doc.GetWorksharingCentralModelPath())
                    : doc.PathName;

            //regresa si no se puede obtener la direccion
            if (string.IsNullOrEmpty(pathOrigen)) { return string.Empty; }

            return pathOrigen;
        }

        /// <summary>
        /// Encuentra la vista del modelo
        /// </summary>
        /// <param name="viewName"></param>
        /// <returns></returns>
        public static ViewSheet FindViewByName(string viewName)
        {
            //string deleteRevision = viewName.Replace("-" + ViewTools.RevisionCBS, string.Empty);
            string deleteRevision = viewName.Substring(0, viewName.Length - 4);

            // Implementa la lógica para buscar una vista por nombre aquí            
            FilteredElementCollector collector = new FilteredElementCollector(RevitTools.doc);
            collector.OfClass(typeof(ViewSheet));
            ViewSheet desiredView = collector.Cast<ViewSheet>()
                .FirstOrDefault(v => v.SheetNumber.Equals(deleteRevision));

            return desiredView;
        }

        /// <summary>
        /// Retorna la lista de planos del modelo
        /// </summary>
        /// <returns></returns>
        public static List<SheetDetail> GetSheets()
        {
            if (RevitTools.doc == null) { return null; }

            //obtengo todos los planos del modelo            
            List<ViewSheet> listViewSheet = new FilteredElementCollector(RevitTools.doc)
                .OfCategory(BuiltInCategory.OST_Sheets)
                .WhereElementIsNotElementType()
                .Cast<ViewSheet>()
                .ToList();

            // Ordenar la lista de ViewSheet por el número de lámina usando el comparador personalizado
            listViewSheet.Sort(new ViewSheetComparer());

            if (!listViewSheet.Any()) { return null; }

            List<SheetDetail> sheetCollection = listViewSheet
                                    .Select(sheet => new SheetDetail
                                    {
                                        IsSelected = true,
                                        NumberSheet = sheet.SheetNumber,
                                        Sheet = sheet,
                                        Revision = sheet
                                        .get_Parameter(BuiltInParameter.SHEET_CURRENT_REVISION)?
                                        .AsString() ?? string.Empty
                                    })
                                    .ToList();

            //Guardar la lista de planos estatica
            SheetDetails = sheetCollection;
            return sheetCollection;
        }

        public static IList<Element> GetElements()
        {
            if (elements != null) return elements;

            ElementCategoryFilter columnsFilter = new ElementCategoryFilter(BuiltInCategory.OST_StructuralColumns);
            ElementCategoryFilter beamsFilter = new ElementCategoryFilter(BuiltInCategory.OST_StructuralFraming);
            ElementCategoryFilter footingsFilter = new ElementCategoryFilter(BuiltInCategory.OST_StructuralFoundation);
            ElementCategoryFilter rebarFilter = new ElementCategoryFilter(BuiltInCategory.OST_Rebar);
            ElementCategoryFilter floorsFilter = new ElementCategoryFilter(BuiltInCategory.OST_Floors);
            ElementCategoryFilter wallsFilter = new ElementCategoryFilter(BuiltInCategory.OST_Walls);
            ElementCategoryFilter genericFilter = new ElementCategoryFilter(BuiltInCategory.OST_GenericModel);
            ElementCategoryFilter partsFilter = new ElementCategoryFilter(BuiltInCategory.OST_Parts);

            LogicalOrFilter combinedFilter = new LogicalOrFilter(new List<ElementFilter>
            { columnsFilter, beamsFilter, footingsFilter, rebarFilter, floorsFilter, wallsFilter,genericFilter, partsFilter });
            FilteredElementCollector collector = new FilteredElementCollector(RevitTools.doc);

            elements = collector.WherePasses(combinedFilter).WhereElementIsNotElementType().ToElements();
            return elements;
        }
        
    }

    internal class ViewTools
    {
        public static ViewModel ViewModel { get; set; }
        public static string Revision { get; set; }
        public static string DirectoryPath { get; set; }
        public static bool DivideNativePDF { get; set; }
        public static bool CopyShared { get; set; }
        public static bool CreatePDFToo { get; set; }

        public static List<SheetDetail> SelectedSheets { get; set; }
    }
}
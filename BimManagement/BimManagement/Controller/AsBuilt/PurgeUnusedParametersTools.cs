using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace BimManagement.Controller
{
    public class ParameterDetail
    {
        public int Id { get; set; }
        public Parameter ParameterElement { get; set; }
        public bool HasValue { get; set; }
    }

    public class PurgeUnusedParametersTools
    {
        public static void PurgeParameters()
        {            
            // Obtener elementos
            IList<Element> elements = RevitTools.GetElements();
            
            // Conjunto para almacenar los IDs de las definiciones de parámetros sin valor
            IList<ParameterDetail> allParameters = new List<ParameterDetail>();

            // Iterar a través de cada elemento
            foreach (Element element in elements)
            {
                ProcessElementParameters(element, allParameters);
                ProcessMaterialParameters(element, allParameters);                
            }

            IList<Definition> emptyParameters = allParameters
                .Where(p => p.HasValue == false)
                .Select(p => p.ParameterElement.Definition)
                .ToList();

            string usedViewsNames = string.Join("\n", emptyParameters.Select(v => v.Name));
            //Debug.WriteLine(usedViewsNames);

            if (allParameters == null || !allParameters.Any()) return;

            DeleteParameter(emptyParameters);
        }

        // Método para procesar los parámetros de un elemento
        private static void ProcessElementParameters(Element element, IList<ParameterDetail> allParameters)
        {
            foreach (Parameter parameter in element.Parameters)
            {
                ParameterDetail parameterDetail = GetOrCreateParameterDetail(parameter, allParameters);
                UpdateParameterDetail(parameter, parameterDetail);
            }
        }

        // Método para procesar los parámetros de un material asociado a un elemento
        private static void ProcessMaterialParameters(Element element, IList<ParameterDetail> allParameters)
        {
            foreach (ElementId materialId in element.GetMaterialIds(false))
            {
                Element materialElement = RevitTools.doc.GetElement(materialId);
                if (materialElement == null) continue;

                foreach (Parameter parameter in materialElement.Parameters)
                {
                    ParameterDetail parameterDetail = GetOrCreateParameterDetail(parameter, allParameters);
                    UpdateParameterDetail(parameter, parameterDetail);
                }
            }
        }

        // Método para obtener o crear un ParameterDetail para un Parameter dado
        private static ParameterDetail GetOrCreateParameterDetail(Parameter parameter, IList<ParameterDetail> allParameters)
        {
            ParameterDetail parameterDetail = allParameters.FirstOrDefault(p => p.Id == parameter.Id.IntegerValue);
            if (parameterDetail == null)
            {
                parameterDetail = new ParameterDetail
                {
                    Id = parameter.Id.IntegerValue,
                    ParameterElement = parameter,
                    HasValue = parameter.Definition.Name.Contains("DAT_Elemento") ? true : parameter.HasValue
                };
                allParameters.Add(parameterDetail);
            }
            return parameterDetail;
        }

        // Método para actualizar un ParameterDetail según el Parameter dado
        private static void UpdateParameterDetail(Parameter parameter, ParameterDetail parameterDetail)
        {
            if (parameter.HasValue || parameter.Definition.Name.Contains("DAT_Elemento"))
            {
                parameterDetail.HasValue = true;
            }
        }

        private static void DeleteParameter(IList<Definition> emptyParameters)
        {
            using (Transaction transaction = new Transaction(RevitTools.doc, "Eliminar parámetros de proyecto"))
            {
                transaction.Start();

                foreach (Definition parameterDef in emptyParameters)
                {
                    try
                    {
                        // Remover la definición del parámetro de proyecto
                        RevitTools.doc.ParameterBindings.Remove(parameterDef);
                    }
                    catch { }
                }

                transaction.Commit();
            }
        }

        public static void PurgeViews()
        {
            List<View> unUsedViews = new List<View>();

            //  Get all sheets
            IEnumerable<ViewSheet> sheets = new FilteredElementCollector(RevitTools.doc)
                        .OfClass(typeof(ViewSheet))
                        .Cast<ViewSheet>()
                        .Where(x => !x.IsTemplate);

            //  Get all views placed on a sheet
            HashSet<ElementId> viewsOnSheets = new HashSet<ElementId>(sheets.SelectMany(x => x.GetAllPlacedViews()));

            //  Return the views that aren't placed on a sheet
            IEnumerable<View> views = new FilteredElementCollector(RevitTools.doc)
                        .OfClass(typeof(ViewSheet))
                        .Cast<View>()
                        .Where(x => !x.IsTemplate);

            foreach (View view in views)
            {
                if (!viewsOnSheets.Contains(view.Id))
                    unUsedViews.Add(view);
            }

            // Mostrar los nombres de las vistas y los schedules utilizados
            string usedViewsNames = string.Join("\n", unUsedViews.Select(v => v.Name));

            //Debug.WriteLine(usedViewsNames);
        }
    }
}

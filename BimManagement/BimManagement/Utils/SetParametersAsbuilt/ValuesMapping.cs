using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Structure;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BimManagement.Utils
{
    public static class ValuesMapping
    {
        public static void SetThicknessByCategory(Element element, Parameter par)
        {
            BuiltInCategory category = (BuiltInCategory)element.Category.Id.IntegerValue;

            string familyAndType = element.get_Parameter(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM)?.AsValueString()?.ToUpper();
            string material = element.get_Parameter(BuiltInParameter.DPART_MATERIAL_ID_PARAM)?.AsValueString()?.ToUpper();

            double thickness = 0;

            if (category == BuiltInCategory.OST_StructuralColumns ||
                category == BuiltInCategory.OST_Parts ||
                category == BuiltInCategory.OST_StructuralFoundation)
            {
                if ((category == BuiltInCategory.OST_StructuralColumns && familyAndType.Contains("MURO")) ||
                    (category == BuiltInCategory.OST_Parts && material.Contains("MURO")))
                {
                    thickness = ViewAsBuiltTools._02_03_DSI_Espesor_muro;
                }
                else if ((category == BuiltInCategory.OST_StructuralColumns && familyAndType.Contains("LOSA")) ||
                         (category == BuiltInCategory.OST_Parts && material.Contains("LOSA")))
                {
                    if ((category == BuiltInCategory.OST_StructuralColumns && familyAndType.Contains("FONDO")) ||
                        (category == BuiltInCategory.OST_Parts && material.Contains("FONDO")))
                    {
                        thickness = ViewAsBuiltTools._02_03_DSI_Espesor_losaFondo;
                    }
                    else if ((category == BuiltInCategory.OST_StructuralColumns && familyAndType.Contains("SUPERIOR")) ||
                             (category == BuiltInCategory.OST_Parts && material.Contains("SUPERIOR")))
                    {
                        par.Set(ViewAsBuiltTools._02_03_DSI_Espesor_LosaSuperior);
                    }
                    else
                    {
                        thickness = ViewAsBuiltTools._02_03_DSI_Espesor_losaFondo;
                    }
                }
                else if ((category == BuiltInCategory.OST_StructuralColumns && familyAndType.Contains("SOLADO")) ||
                         (category == BuiltInCategory.OST_Parts && material.Contains("SOLADO")))
                {
                    thickness = ViewAsBuiltTools._02_03_DSI_Espesor_solado;
                }
            }
            else if(category == BuiltInCategory.OST_GenericModel)
            {
                thickness = 0.02;
            }

            par.Set(UnitUtils.ConvertToInternalUnits(thickness, UnitTypeId.Meters)); ;
        }


        public static void SetHeightByCategory(Element element, Parameter par)
        {
            BuiltInCategory category = (BuiltInCategory)element.Category.Id.IntegerValue;

            if (ShouldSetHeight(category, element))
            {
                double height = GetHeight(category);
                par.Set(height);
            }
            else
            {
                par.Set(0);
            }
        }

        private static bool ShouldSetHeight(BuiltInCategory category, Element element)
        {
            if (category == BuiltInCategory.OST_StructuralColumns ||
                category == BuiltInCategory.OST_Parts ||
                category == BuiltInCategory.OST_StructuralFoundation)
            {
                string familyAndType = element.get_Parameter(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM)?.AsValueString()?.ToUpper();
                string material = element.get_Parameter(BuiltInParameter.DPART_MATERIAL_ID_PARAM)?.AsValueString()?.ToUpper();

                return (category == BuiltInCategory.OST_StructuralColumns && familyAndType != null && familyAndType.Contains("MURO")) ||
                       (category == BuiltInCategory.OST_Parts && material != null && material.Contains("MURO"));
            }

            return false;
        }

        private static double GetHeight(BuiltInCategory category)
        {
            if (category == BuiltInCategory.OST_StructuralColumns ||
                category == BuiltInCategory.OST_Parts ||
                category == BuiltInCategory.OST_StructuralFoundation)
            {
                return UnitUtils.ConvertToInternalUnits(ViewAsBuiltTools._02_04_DSI_Altura, UnitTypeId.Meters);
            }

            return 0;
        }

        public static void SetAreaByCategory(Element element, Parameter par)
        {
            // Obtener los parámetros de longitud y altura
            Parameter parLength = GetParameter(element, SharedParameters._02_02_DSI_Longitud);
            Parameter parHeight = GetParameter(element, SharedParameters._02_04_DSI_Altura);

            if (parLength != null && parHeight != null)
            {
                // Calcular el área y establecer el valor del parámetro
                double area = parLength.AsDouble() * parHeight.AsDouble();
                par.Set(area);
            }
            else
            {
                par.Set(0);
            }
        }

        private static Parameter GetParameter(Element element, SharedParameter sharedParameter)
        {
            // Intentar obtener el parámetro por su GUID
            Parameter parameter = element.get_Parameter(sharedParameter.Guid);

            // Si el parámetro no se encuentra por su GUID, intentar obtenerlo por su nombre
            if (parameter == null)
            {
                parameter = element.LookupParameter(sharedParameter.Name);
            }

            return parameter;
        }

        public static void SetVolumenByCategory(Element element, Parameter par)
        {
            BuiltInCategory category = (BuiltInCategory)element.Category.Id.IntegerValue;

            if (category != BuiltInCategory.OST_Rebar && category != BuiltInCategory.OST_GenericModel)
            {
                double volumen = 0.0;

                if (category == BuiltInCategory.OST_Parts)
                {
                    Parameter volume = element.get_Parameter(BuiltInParameter.DPART_VOLUME_COMPUTED);
                    if (volume != null)
                    {
                        volumen = volume.AsDouble();
                    }
                }
                else
                {
                    Parameter volume = element.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED);
                    if (volume != null)
                    {
                        volumen = volume.AsDouble();
                    }
                }

                par.Set(volumen);
            }          
            else
            {
                par.Set(0);
            }
        }

        public static void SetWeightByCategory(Element element, Parameter par)
        {
            BuiltInCategory category = (BuiltInCategory)element.Category.Id.IntegerValue;

            if (category == BuiltInCategory.OST_Rebar)
            {
                //cantidad
                Rebar rebar = element as Rebar;
                double totalLength = rebar.TotalLength;
                // Obtener el tipo de la familia del refuerzo
                ElementId typeId = rebar.GetTypeId();
                Element elementType = RevitTools.doc.GetElement(typeId);

                Parameter nominal = elementType.LookupParameter("Peso Nominal");

                if (nominal != null)
                {
                    // Obtener el valor del parámetro "Peso Nominal"
                    double nominalConvert = nominal.AsDouble();
                    double weight = totalLength * nominalConvert;

                    par.Set(weight);
                }
            }
            else
            {
                par.Set(0);
            }
        }
    }
}

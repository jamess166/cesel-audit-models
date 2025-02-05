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
    public static class ValuesProjectMapping
    {
        public static void SetAssignVolumeCodeWork(Element element, Parameter par)
        {
            if (!ViewAsBuiltTools.IsConduction)
                return;

            BuiltInCategory category = (BuiltInCategory)element.Category.Id.IntegerValue;

            string familyAndType = element.get_Parameter(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM)?.AsValueString()?.ToUpper() ?? string.Empty;
            string material = element.get_Parameter(BuiltInParameter.DPART_MATERIAL_ID_PARAM)?.AsValueString()?.ToUpper() ?? string.Empty;

            string volumeCodeWork = GetVolumeCodeWork(category, familyAndType, material);

            par.Set(volumeCodeWork);
        }

        private static string GetVolumeCodeWork(BuiltInCategory category, string familyAndType, string material)
        {
            if (category == BuiltInCategory.OST_StructuralColumns || category == BuiltInCategory.OST_StructuralFraming)
            {
                return GetVolumeCodeWorkForNameContent(familyAndType);
            }
            else if (category == BuiltInCategory.OST_Parts)
            {
                return GetVolumeCodeWorkForNameContent(material);
            }

            return "";
        }

        private static string GetVolumeCodeWorkForNameContent(string attributeValue)
        {
            if (attributeValue.Contains("MURO"))
            {
                return ViewAsBuiltTools.IsCM3 ? "04.01.02.03.02.01" : "05.01.02.03.02.01";
            }
            else if (attributeValue.Contains("LOSA"))
            {
                return ViewAsBuiltTools.IsCM3 ? "04.01.02.03.01.01" : "05.01.02.03.01.01";
            }
            else if (attributeValue.Contains("SOLADO"))
            {
                return ViewAsBuiltTools.IsCM3 ? "04.01.02.02.01.01" : "05.01.02.02.01.01";
            }

            return "";
        }


        public static void SetAssignVolumeUniclass(Element element, Parameter par)
        {
            BuiltInCategory category = (BuiltInCategory)element.Category.Id.IntegerValue;

            string familyAndType = element.get_Parameter(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM)?.AsValueString()?.ToUpper() ?? string.Empty;
            string material = element.get_Parameter(BuiltInParameter.DPART_MATERIAL_ID_PARAM)?.AsValueString()?.ToUpper() ?? string.Empty;

            if ((category == BuiltInCategory.OST_StructuralColumns ||
                 category == BuiltInCategory.OST_Parts ||
                 category == BuiltInCategory.OST_StructuralFoundation) &&
                (!familyAndType.Contains("SOLADO") && !material.Contains("SOLADO")))
            {
                par.Set("Pr_20_31_16");
            }
            else if(familyAndType.Contains("SOLADO") || material.Contains("SOLADO"))
            {
                par.Set("Pr_20_31_16");
            }
            else
            {
                par.Set(string.Empty);
            }
        }


        public static void SetAssignVolumeWorkName(Element element, Parameter par)
        {
            BuiltInCategory category = (BuiltInCategory)element.Category.Id.IntegerValue;

            string familyAndType = element.get_Parameter(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM)?.AsValueString()?.ToUpper() ?? string.Empty;
            string material = element.get_Parameter(BuiltInParameter.DPART_MATERIAL_ID_PARAM)?.AsValueString()?.ToUpper() ?? string.Empty;

            string nameWork = string.Empty;

            if (category == BuiltInCategory.OST_StructuralColumns || category == BuiltInCategory.OST_StructuralFraming ||
                category == BuiltInCategory.OST_Parts ||
                category == BuiltInCategory.OST_StructuralFoundation)
            {
                if ((category == BuiltInCategory.OST_StructuralColumns && familyAndType.Contains("MURO")) ||
                    (category == BuiltInCategory.OST_Parts && material.Contains("MURO")))
                {
                    nameWork = "CONCRETO F'C=280 KG/CM2, A/C<= 0.50 - MURO";
                }
                else if ((category == BuiltInCategory.OST_StructuralColumns && familyAndType.Contains("LOSA")) ||
                         (category == BuiltInCategory.OST_Parts && material.Contains("LOSA")))
                {
                    if ((category == BuiltInCategory.OST_StructuralColumns && familyAndType.Contains("FONDO")) ||
                        (category == BuiltInCategory.OST_Parts && material.Contains("FONDO")))
                    {
                        nameWork = "CONCRETO F'C=280 KG/CM2, A/C<= 0.50 - LOSA DE FONDO";
                    }
                    else if ((category == BuiltInCategory.OST_StructuralColumns && familyAndType.Contains("SUPERIOR")) ||
                        (category == BuiltInCategory.OST_Parts && material.Contains("FONDO")))
                    {
                        nameWork = "CONCRETO F'C=280 KG/CM2, A/C<= 0.50 - LOSA SUPERIOR";
                    }
                    else
                    {
                        nameWork = "CONCRETO F'C=280 KG/CM2, A/C<= 0.50 - LOSA DE FONDO";
                    }
                }
                else if ((category == BuiltInCategory.OST_StructuralColumns && familyAndType.Contains("SOLADO")) ||
                         (category == BuiltInCategory.OST_StructuralFraming && familyAndType.Contains("SOLADO")) ||
                         (category == BuiltInCategory.OST_Parts && material.Contains("SOLADO")))
                {
                    nameWork = "CONCRETO F'C= 100KG/CM2 - SOLADO";
                }
            }

            par.Set(nameWork); ;
        }

        public static void SetAssignAreaCodeWork(Element element, Parameter par)
        {
            if (!ViewAsBuiltTools.IsConduction)
                return;

            BuiltInCategory category = (BuiltInCategory)element.Category.Id.IntegerValue;

            string familyAndType = element.get_Parameter(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM)?.AsValueString()?.ToUpper() ?? string.Empty;
            string material = element.get_Parameter(BuiltInParameter.DPART_MATERIAL_ID_PARAM)?.AsValueString()?.ToUpper() ?? string.Empty;

            string areaCodeWork = GetAreaCodeWork(category, familyAndType, material);

            par.Set(areaCodeWork);
        }

        private static string GetAreaCodeWork(BuiltInCategory category, string familyAndType, string material)
        {
            if (category == BuiltInCategory.OST_StructuralColumns && familyAndType != null)
            {
                return GetAreaCodeWorkForCategory(familyAndType);
            }
            else if (category == BuiltInCategory.OST_Parts && material != null)
            {
                return GetAreaCodeWorkForCategory(material);
            }
            else if (category == BuiltInCategory.OST_GenericModel)
            {
                return ViewAsBuiltTools.IsCM3 ? "04.01.02.04.01.01" : "05.01.02.04.01";
            }

            return "";
        }

        private static string GetAreaCodeWorkForCategory(string attributeValue)
        {
            if (attributeValue.Contains("MURO"))
            {
                return ViewAsBuiltTools.IsCM3 ? "04.01.02.03.02.02" : "05.01.02.03.02.02";
            }
            else if (attributeValue.Contains("LOSA"))
            {
                return ViewAsBuiltTools.IsCM3 ? "04.01.02.03.01.02" : "05.01.02.03.01.02";
            }

            return "";
        }

        public static void SetAssignAreaUniclass(Element element, Parameter par)
        {
            BuiltInCategory category = (BuiltInCategory)element.Category.Id.IntegerValue;

            string familyAndType = element.get_Parameter(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM)?.AsValueString()?.ToUpper() ?? string.Empty;
            string material = element.get_Parameter(BuiltInParameter.DPART_MATERIAL_ID_PARAM)?.AsValueString()?.ToUpper() ?? string.Empty;

            if ((category == BuiltInCategory.OST_StructuralColumns ||
                 category == BuiltInCategory.OST_Parts ||
                 category == BuiltInCategory.OST_StructuralFoundation) &&
                (!familyAndType.Contains("SOLADO") && !material.Contains("SOLADO")))
            {
                par.Set("Pr_25_71_29");
            }
            else if (category == BuiltInCategory.OST_GenericModel)
            {
                par.Set("Pr_35_90_27");
            }
            else
            {
                par.Set(string.Empty);
            }
        }

        public static void SetAssignAreaWorkName(Element element, Parameter par)
        {
            BuiltInCategory category = (BuiltInCategory)element.Category.Id.IntegerValue;

            string familyAndType = element.get_Parameter(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM)?.AsValueString()?.ToUpper() ?? string.Empty;
            string material = element.get_Parameter(BuiltInParameter.DPART_MATERIAL_ID_PARAM)?.AsValueString()?.ToUpper() ?? string.Empty;

            string nameWork = string.Empty;

            if (category == BuiltInCategory.OST_StructuralColumns ||
                category == BuiltInCategory.OST_Parts ||
                category == BuiltInCategory.OST_StructuralFoundation)
            {
                if ((category == BuiltInCategory.OST_StructuralColumns && familyAndType.Contains("MURO")) ||
                    (category == BuiltInCategory.OST_Parts && material.Contains("MURO")))
                {
                    nameWork = "ENCOFRADO Y DESENCOFRADO - MURO";
                }
                else if ((category == BuiltInCategory.OST_StructuralColumns && familyAndType.Contains("LOSA")) ||
                         (category == BuiltInCategory.OST_Parts && material.Contains("LOSA")))
                {
                    if ((category == BuiltInCategory.OST_StructuralColumns && familyAndType.Contains("FONDO")) ||
                        (category == BuiltInCategory.OST_Parts && material.Contains("FONDO")))
                    {
                        nameWork = "ENCOFRADO Y DESENCOFRADO - LOSA DE FONDO";
                    }
                    else if ((category == BuiltInCategory.OST_StructuralColumns && familyAndType.Contains("SUPERIOR")) ||
                            (category == BuiltInCategory.OST_Parts && material.Contains("SUPERIOR")))
                    {
                        nameWork = "ENCOFRADO Y DESENCOFRADO - LOSA SUPERIOR";
                    }
                    else
                    {
                        nameWork = "ENCOFRADO Y DESENCOFRADO - LOSA DE FONDO";
                    }
                }
            }
            else if (category == BuiltInCategory.OST_GenericModel)
            {
                nameWork = "JUNTAS DE DILATACIÓN (JMD1)";
            }

            par.Set(nameWork); ;
        }

        public static void SetAssignWeigthCodeWork(Element element, Parameter par)
        {
            if (!ViewAsBuiltTools.IsConduction)
                return;

            BuiltInCategory category = (BuiltInCategory)element.Category.Id.IntegerValue;

            //Considerar cuando sea refuerzo

            string familyAndType = element.get_Parameter(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM)?.AsValueString()?.ToUpper() ?? string.Empty;
            string material = element.get_Parameter(BuiltInParameter.DPART_MATERIAL_ID_PARAM)?.AsValueString()?.ToUpper() ?? string.Empty;

            string weightCodeWork = GetWeightCodeWork(category, familyAndType, material);

            par.Set(weightCodeWork);
        }

        private static string GetWeightCodeWork(BuiltInCategory category, string familyAndType, string material)
        {
            if (category == BuiltInCategory.OST_StructuralColumns && familyAndType != null)
            {
                return GetWeightCodeWorkForCategory(familyAndType);
            }
            else if (category == BuiltInCategory.OST_Parts && material != null)
            {
                return GetWeightCodeWorkForCategory(material);
            }
            
            return "";
        }

        private static string GetWeightCodeWorkForCategory(string attributeValue)
        {
            if (attributeValue.Contains("MURO"))
            {
                return ViewAsBuiltTools.IsCM3 ? "04.01.02.03.02.03" : "05.01.02.03.02.03";
            }
            else if (attributeValue.Contains("LOSA"))
            {
                return ViewAsBuiltTools.IsCM3 ? "04.01.02.03.01.03" : "05.01.02.03.01.03";
            }

            return "";
        }

        public static void SetAssignWeightUniclass(Element element, Parameter par)
        {
            BuiltInCategory category = (BuiltInCategory)element.Category.Id.IntegerValue;

            string familyAndType = element.get_Parameter(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM)?.AsValueString()?.ToUpper() ?? string.Empty;
            string material = element.get_Parameter(BuiltInParameter.DPART_MATERIAL_ID_PARAM)?.AsValueString()?.ToUpper() ?? string.Empty;

            if ((category == BuiltInCategory.OST_StructuralColumns ||
                 category == BuiltInCategory.OST_Parts ||
                 category == BuiltInCategory.OST_StructuralFoundation) &&
                (!familyAndType.Contains("SOLADO") && !material.Contains("SOLADO")))
            {
                par.Set("Pr_20_96_71_14");
            }
            else if(category == BuiltInCategory.OST_Rebar)
            {
                par.Set("Pr_20_96_71_14");
            }
            else
            {
                par.Set(string.Empty);
            }
        }

        public static void SetAssignWeightWorkName(Element element, Parameter par)
        {
            BuiltInCategory category = (BuiltInCategory)element.Category.Id.IntegerValue;

            string familyAndType = element.get_Parameter(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM)?.AsValueString()?.ToUpper() ?? string.Empty;
            string material = element.get_Parameter(BuiltInParameter.DPART_MATERIAL_ID_PARAM)?.AsValueString()?.ToUpper() ?? string.Empty;

            string nameWork = string.Empty;

            if ((category == BuiltInCategory.OST_StructuralColumns ||
                 category == BuiltInCategory.OST_Parts ||
                 category == BuiltInCategory.OST_StructuralFoundation) &&
                (!familyAndType.Contains("SOLADO") && !material.Contains("SOLADO")))
            {
                nameWork = "ACERO DE REFUERZO FY=4200 KG/CM2";
            }
            else if (category == BuiltInCategory.OST_Rebar)
            {
                par.Set("ACERO DE REFUERZO FY=4200 KG/CM2");
            }

            par.Set(nameWork); 
        }

        public static void SetAssignWeightName(Element element, Parameter par)
        {
            
        }
    }
}

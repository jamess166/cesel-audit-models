using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BimManagement.Utils
{
    public class MaterialMapping
    {
        private Dictionary<BuiltInCategory, string> categoryMaterialMap;

        public MaterialMapping()
        {
            // Inicializar el mapa de categoría-material
            categoryMaterialMap = new Dictionary<BuiltInCategory, string>
        {
            { BuiltInCategory.OST_StructuralFraming, "Concreto Simple/Concreto Armado" },
            { BuiltInCategory.OST_StructuralColumns, "Concreto Armado" },
            { BuiltInCategory.OST_Rebar, "Acero de Refuerzo" },
            { BuiltInCategory.OST_Parts, "Concreto Armado" },
            { BuiltInCategory.OST_GenericModel, "Poliestireno expandido" }
        };
        }

        public string GetMaterialByCategory(Element element)
        {
            BuiltInCategory category = (BuiltInCategory)element.Category.Id.IntegerValue;

            // Verificar si el elemento está en el diccionario
            if (categoryMaterialMap.ContainsKey(category))
            {
                string material = categoryMaterialMap[category];

                // Si es una parte, determinar el material en función de ciertos parámetros
                if (category == BuiltInCategory.OST_Parts)
                {
                    string familyAndType = element.get_Parameter(BuiltInParameter.DPART_MATERIAL_ID_PARAM)?.AsValueString()?.ToUpper();
                    if (familyAndType != null && familyAndType.Contains("SOLADO"))
                    {
                        material = "Concreto Simple";
                    }
                }
                else if (category == BuiltInCategory.OST_Parts)
                {
                    string familyAndType = element.get_Parameter(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM)?.AsValueString()?.ToUpper();
                    if (familyAndType != null && familyAndType.Contains("SOLADO"))
                    {
                        material = "Concreto Simple";
                    }
                }

                return material;
            }
            else
            {
                // Si no se encuentra en el diccionario, devolver un valor predeterminado o lanzar una excepción
                return string.Empty;
            }
        }
    }

    public class SubdisciplineMapping
    {
        private Dictionary<BuiltInCategory, string> categorySubdisciplineMap;

        public SubdisciplineMapping()
        {
            // Inicializar el mapa de categoría-subdisciplina
            categorySubdisciplineMap = new Dictionary<BuiltInCategory, string>
        {
            { BuiltInCategory.OST_StructuralFraming, "Solado/Losa/Muro" },
            { BuiltInCategory.OST_StructuralColumns, "" },
            { BuiltInCategory.OST_Rebar, "Acero de Refuerzo" },
            { BuiltInCategory.OST_Parts, "Solado/Losa/Muro" },
            { BuiltInCategory.OST_GenericModel, "Junta de dilatación" }
        };
        }

        public string GetSubdisciplineByCategory(Element element)
        {
            BuiltInCategory category = (BuiltInCategory)element.Category.Id.IntegerValue;

            if (categorySubdisciplineMap.ContainsKey(category))
            {
                string subdiscipline = categorySubdisciplineMap[category];
                string familyAndType = element.get_Parameter(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM)?.AsValueString()?.ToUpper();
                string material = element.get_Parameter(BuiltInParameter.DPART_MATERIAL_ID_PARAM)?.AsValueString()?.ToUpper();
                
                if ((category == BuiltInCategory.OST_StructuralColumns && familyAndType != null) ||
                    (category == BuiltInCategory.OST_Parts && material != null))
                {                    
                    if ((category == BuiltInCategory.OST_StructuralColumns && familyAndType.Contains("MURO")) ||
                        (category == BuiltInCategory.OST_Parts && material.Contains("MURO")))
                    {
                        return "Muro";
                    }
                    else if ((category == BuiltInCategory.OST_StructuralColumns && familyAndType.Contains("LOSA")) ||
                             (category == BuiltInCategory.OST_Parts && material.Contains("LOSA")))
                    {
                        if ((category == BuiltInCategory.OST_StructuralColumns && familyAndType.Contains("FONDO")) ||
                            (category == BuiltInCategory.OST_Parts && material.Contains("FONDO")))
                        {
                            return "Losa de fondo";
                        }
                        else if ((category == BuiltInCategory.OST_StructuralColumns && familyAndType.Contains("SUPERIOR")) ||
                                 (category == BuiltInCategory.OST_Parts && material.Contains("SUPERIOR")))
                        {
                            return "Losa Superior";
                        }
                        else
                        {
                            return "Losa de fondo";
                        }
                    }
                    else if ((category == BuiltInCategory.OST_StructuralColumns && familyAndType.Contains("SOLADO")) ||
                             (category == BuiltInCategory.OST_Parts && material.Contains("SOLADO")))
                    {
                        return "Solado";
                    }
                }

                return subdiscipline;
            }
            else
            {
                return string.Empty;
            }
        }
    }

    public class UnitMapping
    {
        private Dictionary<BuiltInCategory, string> categoryMaterialMap;

        public UnitMapping()
        {
            // Inicializar el mapa de categoría-material
            categoryMaterialMap = new Dictionary<BuiltInCategory, string>
        {
            { BuiltInCategory.OST_StructuralFraming, "m2/m3/Kg" },
            { BuiltInCategory.OST_StructuralColumns, "m2/m3/Kg" },
            { BuiltInCategory.OST_Rebar, "kg" },
            { BuiltInCategory.OST_Parts, "m2/m3/Kg" },
            { BuiltInCategory.OST_GenericModel, "m2" }
        };
        }

        public string GetUnitByCategory(Element element)
        {
            BuiltInCategory category = (BuiltInCategory)element.Category.Id.IntegerValue;

            // Verificar si el elemento está en el diccionario
            if (categoryMaterialMap.ContainsKey(category))
            {
                string material = categoryMaterialMap[category];

                // Si es una parte, determinar el material en función de ciertos parámetros
                if (category == BuiltInCategory.OST_Parts)
                {
                    string familyAndType = element.get_Parameter(BuiltInParameter.DPART_MATERIAL_ID_PARAM)?.AsValueString()?.ToUpper();
                    if (familyAndType != null && familyAndType.Contains("SOLADO"))
                    {
                        material = "m3";
                    }
                }
                else if (category == BuiltInCategory.OST_Parts)
                {
                    string familyAndType = element.get_Parameter(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM)?.AsValueString()?.ToUpper();
                    if (familyAndType != null && familyAndType.Contains("SOLADO"))
                    {
                        material = "m3";
                    }
                }

                return material;
            }
            else
            {
                // Si no se encuentra en el diccionario, devolver un valor predeterminado o lanzar una excepción
                return string.Empty;
            }
        }
    }
}

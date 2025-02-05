using Autodesk.Revit.DB;
using BimManagement.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BimManagement.Controller
{
    public class FillSharedParameter
    {
        public static void FillParameter(IList<SharedParameter> sharedParameters)
        {
            IList<Element> elements = RevitTools.GetElements();
            Categories categories = RevitTools.doc.Settings.Categories;

            using (Transaction transaction = new Transaction(RevitTools.doc, "Llenar Parametros Compartidos"))
            {
                // Crear una instancia de MaterialMapping
                MaterialMapping materialMapping = new MaterialMapping();
                SubdisciplineMapping subdisciplineMapping = new SubdisciplineMapping();
                UnitMapping unitMapping = new UnitMapping();


                transaction.Start();

                foreach (Element element in elements)
                {
                    foreach (SharedParameter parameter in sharedParameters)
                    {
                        Parameter par = element.get_Parameter(parameter.Guid);
                        if (par == null)
                        {
                            par = element.LookupParameter(parameter.Name);
                        }
                        if (par == null) continue;

                        switch (parameter.Name)
                        {
                            // Identificacion ////////////////////////////////////////
                            case "01_01_DSI_Proyecto":
                                par.Set("400115");
                                break;
                            case "01_02_DSI_Localizador":
                                par.Set("Paquete 4B - Conducciones San Carlos");
                                break;
                            case "01_03_DSI_Estado":
                                par.Set("RIBA 5");
                                break;
                            case "01_04_DSI_Clasificación":
                                par.Set("En_32_95_14");
                                break;
                            case "01_05_DSI_Tipología":
                                par.Set("Modelo de Proyecto Constructivo");
                                break;
                            case "01_06_DSI_Disciplina":
                                par.Set("Estructuras");
                                break;
                            case "01_07_DSI_Subdisciplina":
                                par.Set(subdisciplineMapping.GetSubdisciplineByCategory(element));
                                break;
                            case "01_08_DSI_Material":
                                par.Set(materialMapping.GetMaterialByCategory(element));
                                break;
                            case "01_09_DSI_Contratista":
                                par.Set("Consorcio Besalco Stracon");
                                break;
                            case "01_10_DSI_Subcontratista":
                                par.Set("Construcción y Administración S.A. - CASA");
                                break;
                            case "01_11_DSI_Activo":
                                par.Set(ViewAsBuiltTools._01_11_DSI_Activo);
                                break;
                            // Cantidades ////////////////////////////////////////////
                            case "02_01_DSI_Unidad":
                                par.Set(unitMapping.GetUnitByCategory(element));
                                break;
                            case "02_02_DSI_Longitud":
                                double length = UnitUtils
                                    .ConvertToInternalUnits(GetLengthByCategory(element), UnitTypeId.Meters);
                                par.Set(length);
                                break;
                            case "02_03_DSI_Espesor":
                                Utils.ValuesMapping.SetThicknessByCategory(element, par);
                                break;
                            case "02_04_DSI_Altura":
                                Utils.ValuesMapping.SetHeightByCategory(element, par);
                                break;
                            case "02_05_DSI_Area":
                                Utils.ValuesMapping.SetAreaByCategory(element, par);
                                break;
                            case "02_06_DSI_Volumen":
                                Utils.ValuesMapping.SetVolumenByCategory(element, par);
                                break;
                            case "02_07_DSI_Peso":
                                Utils.ValuesMapping.SetWeightByCategory(element, par);
                                break;
                            // Proyecto /////////////////////////////////////////////
                            case "03_01_DSI_Fase Obra":
                                par.Set("Construcción");
                                break;
                            case "03_03_01_DSI_Código de Partida":
                                Utils.ValuesProjectMapping.SetAssignVolumeCodeWork(element, par);
                                break;
                            case "03_03_01_DSI_Código Uniclass de Partida":
                                Utils.ValuesProjectMapping.SetAssignVolumeUniclass(element, par);
                                break;
                            case "03_03_01_DSI_Nombre de Partida":
                                Utils.ValuesProjectMapping.SetAssignVolumeWorkName(element, par);
                                break;
                            case "03_03_01_DSI_Metrado (m3)":
                                Utils.ValuesMapping.SetVolumenByCategory(element, par);
                                break;
                            // Area ///////////////////////////////////////////////
                            case "03_03_02_DSI_Código de Partida":
                                Utils.ValuesProjectMapping.SetAssignAreaCodeWork(element, par);
                                break;
                            case "03_03_02_DSI_Código Uniclass de Partida":
                                Utils.ValuesProjectMapping.SetAssignAreaUniclass(element, par);
                                break;
                            case "03_03_02_DSI_Nombre de Partida":
                                Utils.ValuesProjectMapping.SetAssignAreaWorkName(element, par);
                                break;
                            // Acero /////////////////////////////////////////////                     
                            case "03_03_03_DSI_Código de Partida":
                                Utils.ValuesProjectMapping.SetAssignWeigthCodeWork(element, par);
                                break;
                            case "03_03_03_DSI_Código Uniclass de Partida":
                                Utils.ValuesProjectMapping.SetAssignWeightUniclass(element, par);
                                break;
                            case "03_03_03_DSI_Nombre de Partida":
                                Utils.ValuesProjectMapping.SetAssignWeightWorkName(element, par);
                                break;
                            case "03_03_03_DSI_Metrado (Kg.)":
                                Utils.ValuesProjectMapping.SetAssignWeightName(element, par);
                                break;
                            // LOI //////////////////////////////////////////////
                            case "04_01_DSI_Controles de Calidad":
                                par.Set("https://docs.b360.autodesk.com/projects/8282e4b9-f9a4-4b9c-b717-a76fd18f15ce/folders/urn:adsk.wipprod:fs.folder:co._H9Bhq--RV-Exs53b6Nb-A");
                                break;
                            case "04_02_DSI_Fotografías":
                                par.Set("https://docs.b360.autodesk.com/projects/8282e4b9-f9a4-4b9c-b717-a76fd18f15ce/folders/urn:adsk.wipprod:fs.folder:co.DE4jlZ3mQcO5UGYv6sekaQ");
                                break;
                            case "04_03_DSI_Certificaciones":
                                par.Set("https://docs.b360.autodesk.com/projects/8282e4b9-f9a4-4b9c-b717-a76fd18f15ce/folders/urn:adsk.wipprod:fs.folder:co.P3ahE_zxQLmz8XXls_89xA");
                                break;
                            case "04_04_DSI_Planos AsBuilt":
                                par.Set("https://docs.b360.autodesk.com/projects/8282e4b9-f9a4-4b9c-b717-a76fd18f15ce/folders/urn:adsk.wipprod:fs.folder:co.5TqF1DPLSQSBIlqu41uHNA");
                                break;
                            default:
                                break;
                        }
                    }
                }

                transaction.Commit();
            }
        }

        private static double GetLengthByCategory(Element element)
        {
            Categories categories = RevitTools.doc.Settings.Categories;

            if (element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_GenericModel ||
                element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Rebar)
            {
                return 0;
            }

            return ViewAsBuiltTools._02_02_DSI_Longitud;
        }



        //private static string GetSubdisciplineByCategory(Element element)
        //{
        //    Categories categories = RevitTools.doc.Settings.Categories;

        //    if (element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_StructuralFraming)
        //    {
        //        return "Solado/Losa/Muro";
        //    }
        //    if (element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_StructuralColumns)
        //    {
        //        string familyAndType = element.get_Parameter(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM)
        //            .AsValueString()
        //            .ToUpper();

        //        if (familyAndType.Contains("MURO"))
        //        {
        //            return "Muro";
        //        }
        //        else if (familyAndType.Contains("LOSA"))
        //        {
        //            if (familyAndType.Contains("FONDO"))
        //            {
        //                return "Losa de fondo";
        //            }
        //            else if (familyAndType.Contains("SUPERIOR"))
        //            {
        //                return "Losa Superior";
        //            }
        //            else
        //            {
        //                return "Losa de fondo";
        //            }
        //        }
        //        else if (familyAndType.Contains("SOLADO"))
        //        {
        //            return "Solado";
        //        }
        //        return "Solado/Losa/Muro";
        //    }
        //    else if (element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Rebar)
        //    {
        //        return "Acero de Refuerzo";
        //    }
        //    else if (element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Parts)
        //    {
        //        //Verificar que material tiene
        //        Parameter parameter = element.get_Parameter(BuiltInParameter.DPART_MATERIAL_ID_PARAM);
        //        string value = parameter.AsValueString().ToUpper();

        //        if (value.Contains("SOLADO"))
        //        {
        //            return "Solado";
        //        }
        //        else if (value.Contains("LOSA"))
        //        {
        //            if (value.Contains("FONDO"))
        //            {
        //                return "Losa de fondo";
        //            }
        //            else if (value.Contains("SUPERIOR"))
        //            {
        //                return "Losa Superior";
        //            }
        //            else
        //            {
        //                return "Losa de fondo";
        //            }
        //        }
        //        else if (value.Contains("MURO"))
        //        {
        //            return "Muro";
        //        }

        //        return string.Empty;
        //    }
        //    else if (element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_GenericModel)
        //    {
        //        return "Junta de dilatación";
        //    }

        //    return string.Empty;
        //}

        //private static string GetMaterialByCategory(Element element)
        //{
        //    Categories categories = RevitTools.doc.Settings.Categories;

        //    if (element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_StructuralFraming)
        //    {
        //        return "Concreto Simple/Concreto Armado";
        //    }
        //    if (element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_StructuralColumns)
        //    {
        //        string familyAndType = element.get_Parameter(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM)
        //            .AsValueString()
        //            .ToUpper();

        //        if (familyAndType.Contains("SOLADO"))
        //        {
        //            return "Concreto Simple";
        //        }
        //        else
        //        {
        //            return "Concreto Armado";
        //        }
        //    }
        //    else if (element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Rebar)
        //    {
        //        return "Acero de Refuerzo";
        //    }
        //    else if (element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Parts)
        //    {
        //        //Verificar que material tiene
        //        Parameter parameter = element.get_Parameter(BuiltInParameter.DPART_MATERIAL_ID_PARAM);
        //        string value = parameter.AsValueString().ToUpper();

        //        if (value.Contains("SOLADO"))
        //        {
        //            return "Concreto Simple";
        //        }
        //        else
        //        {
        //            return "Concreto Armado";
        //        }
        //    }
        //    else if (element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_GenericModel)
        //    {
        //        return "Poliestireno expandido";
        //    }

        //    return string.Empty;
        //}

        //private static string GetMaterialByCategory(Category category)
        //{
        //    if (GetCategoryGroup(category) == "Concreto" ||
        //        GetCategoryGroup(category) == "Parte")
        //    {
        //        return "Concreto Armado";
        //    }
        //    else if (GetCategoryGroup(category) == "Acero")
        //    {
        //        return "Acero";
        //    }
        //    else if (GetCategoryGroup(category) == "Junta")
        //    {
        //        return "Poliestireno expandido";
        //    }

        //    return string.Empty;
        //}

        //private static string GetCategoryGroup(Category category)
        //{
        //    Categories categories = RevitTools.doc.Settings.Categories;

        //    if (category.Id.IntegerValue == (int)BuiltInCategory.OST_StructuralFraming ||
        //        category.Id.IntegerValue == (int)BuiltInCategory.OST_StructuralColumns ||
        //        category.Id.IntegerValue == (int)BuiltInCategory.OST_Floors ||
        //        category.Id.IntegerValue == (int)BuiltInCategory.OST_Walls)
        //    {
        //        return "Concreto";
        //    }
        //    else if (category.Id.IntegerValue == (int)BuiltInCategory.OST_Rebar)
        //    {
        //        return "Acero";
        //    }
        //    else if (category.Id.IntegerValue == (int)BuiltInCategory.OST_Parts)
        //    {
        //        return "Parte";
        //    }
        //    else if (category.Id.IntegerValue == (int)BuiltInCategory.OST_GenericModel)
        //    {
        //        return "Junta";
        //    }
        //    else
        //    {
        //        return null;
        //    }
        //}
    }
}

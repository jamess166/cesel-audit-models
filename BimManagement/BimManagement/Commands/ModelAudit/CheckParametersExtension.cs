using Autodesk.Revit.DB;
using BimManagement.Commands.ModelAudit.Config;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BimManagement.Commands.ModelAudit
{
    public static class RevitExtensions
    {
        public static bool IsType(this Element element)
        {
            return element is ElementType;
        }
    }

    public class CheckParametersExtension
    {
        private static List<SpecialtyParameters> _config;

        public ValidationResult ValidateRequiredParameters(Speciality specialty)
        {
            _config = CheckParameters.GetParametersConfig();

            if (_config == null || !_config.Any())
            {
                return new ValidationResult
                {
                    IsValid = false,
                    IsRelevant = false,
                    Message = "La configuración de parámetros no ha sido cargada correctamente."
                };
            }

            // Obtener los parámetros requeridos para la especialidad
            var requiredParameters = _config
                .Where(s => s.Specialty.Equals(specialty.ToString(), StringComparison.Ordinal) ||
                             s.Specialty.Equals("TODOS", StringComparison.Ordinal))
                .SelectMany(s => s.Parameters
                    .Where(p => p.Required)
                    .Select(p => new ParameterDefinition
                    {
                        Name = s.Prefix + p.Name,
                        ParameterType = p.ParameterType,
                        Group = p.Group,
                        Scope = p.Scope,
                        Required = p.Required
                    }))
                .ToList();

            if (!requiredParameters.Any())
            {
                return new ValidationResult
                {
                    IsValid = true,
                    IsRelevant = true,
                    Message = "No hay parámetros requeridos para validar en esta especialidad."
                };
            }

            // Obtener todos los elementos del modelo
            //var collector = new FilteredElementCollector(RevitTools.doc);
            //var elements = collector.WhereElementIsNotElementType().ToElements();
            // Obtener elementos relevantes según la especialidad
            var collector = GetRelevantElementsForSpecialty(specialty);
            var elements = collector.ToElements();

            // Diccionario para almacenar elementos con parámetros requeridos faltantes
            var elementsWithMissingValues = new Dictionary<Element, List<string>>();

            foreach (var element in elements)
            {
                foreach (var requiredParam in requiredParameters)
                {
                    // Verificar si el parámetro es aplicable al elemento (según el scope)
                    bool isApplicable = (requiredParam.Scope == "EJEMPLAR" && !element.IsType()) ||
                                       (requiredParam.Scope == "TIPO" && element.IsType());

                    if (!isApplicable) continue;

                    // Verificar si el elemento tiene el parámetro
                    var param = element.LookupParameter(requiredParam.Name);
                    if (param == null) continue;

                    // Verificar si el parámetro tiene valor
                    bool hasValue = false;
                    switch (requiredParam.ParameterType)
                    {
                        case "TEXT":
                        case "SI/NO":
                        case "YES/NO":
                            hasValue = !string.IsNullOrEmpty(param.AsValueString());
                            break;
                        case "LENGTH":
                        case "AREA":
                        case "VOLUMEN":
                        case "NÚMERO":
                        case "ENTERO":
                            hasValue = param.AsDouble() > 0;
                            break;
                        case "ÁNGULO":
                            hasValue = param.AsDouble() != 0;
                            break;
                        default:
                            hasValue = param.HasValue;
                            break;
                    }

                    if (!hasValue)
                    {
                        if (!elementsWithMissingValues.ContainsKey(element))
                        {
                            elementsWithMissingValues[element] = new List<string>();
                        }
                        elementsWithMissingValues[element].Add(requiredParam.Name);
                    }
                }
            }

            // Construir el mensaje de resultado
            var messageBuilder = new StringBuilder();
            bool isValid = !elementsWithMissingValues.Any();

            if (!isValid)
            {
                messageBuilder.AppendLine("Elementos con parámetros requeridos sin valor:");
                foreach (var kvp in elementsWithMissingValues)
                {
                    var element = kvp.Key;
                    messageBuilder.AppendLine($"- Elemento Id: {element.Id.IntegerValue} ({element.Name})");
                    messageBuilder.AppendLine("  Parámetros requeridos sin valor:");
                    foreach (var paramName in kvp.Value)
                    {
                        messageBuilder.AppendLine($"  • {paramName}");
                    }
                    messageBuilder.AppendLine();
                }
            }
            else
            {
                //messageBuilder.AppendLine("Todos los parámetros requeridos tienen valores asignados.");
            }

            return new ValidationResult
            {
                IsValid = isValid,
                IsRelevant = true,
                Message = messageBuilder.ToString().Trim()
            };
        }

        private FilteredElementCollector GetRelevantElementsForSpecialty(Speciality specialty)
        {
            var collector = new FilteredElementCollector(RevitTools.doc)
                .WhereElementIsNotElementType();

            switch (specialty)
            {
                case Speciality.ESTRUCTURAS:
                    return collector.WherePasses(new LogicalOrFilter(new List<ElementFilter>
                {
                    // Elementos estructurales principales
                    new ElementCategoryFilter(BuiltInCategory.OST_StructuralFraming),
                    new ElementCategoryFilter(BuiltInCategory.OST_StructuralColumns),
                    new ElementCategoryFilter(BuiltInCategory.OST_StructuralFoundation),
            
                    // Muros y losas estructurales
                    new ElementCategoryFilter(BuiltInCategory.OST_Walls),
                    new ElementCategoryFilter(BuiltInCategory.OST_Floors),
                    new ElementCategoryFilter(BuiltInCategory.OST_Roofs),
                }));

                case Speciality.ARQUITECTURA:
                    return collector.WherePasses(new LogicalOrFilter(new List<ElementFilter>
                {
                    // Elementos arquitectónicos principales
                    new ElementCategoryFilter(BuiltInCategory.OST_Walls),
                    new ElementCategoryFilter(BuiltInCategory.OST_Floors),
                    new ElementCategoryFilter(BuiltInCategory.OST_Doors),
                    new ElementCategoryFilter(BuiltInCategory.OST_Windows),
                    new ElementCategoryFilter(BuiltInCategory.OST_Stairs),
                    new ElementCategoryFilter(BuiltInCategory.OST_Ceilings),
                    new ElementCategoryFilter(BuiltInCategory.OST_Roofs),
                    new ElementCategoryFilter(BuiltInCategory.OST_Ramps),
                    new ElementCategoryFilter(BuiltInCategory.OST_Railings),
                    new ElementCategoryFilter(BuiltInCategory.OST_StairsRailing),
                    new ElementCategoryFilter(BuiltInCategory.OST_Columns),
                    new ElementCategoryFilter(BuiltInCategory.OST_CurtainWallPanels),
                    new ElementCategoryFilter(BuiltInCategory.OST_CurtainWallMullions),
            
                    // Mobiliario y equipamiento
                    new ElementCategoryFilter(BuiltInCategory.OST_Furniture),
                    new ElementCategoryFilter(BuiltInCategory.OST_Casework),
                    new ElementCategoryFilter(BuiltInCategory.OST_SpecialityEquipment),
                    new ElementCategoryFilter(BuiltInCategory.OST_Entourage),
                    new ElementCategoryFilter(BuiltInCategory.OST_Planting),
                    new ElementCategoryFilter(BuiltInCategory.OST_GenericModel),
                }));

                case Speciality.ELECTRICAS:
                case Speciality.MECANICAS:
                case Speciality.SANITARIAS:
                case Speciality.COMUNICACIONES:
                    return collector.WherePasses(new LogicalOrFilter(new List<ElementFilter>
                {
                    // Elementos host para instalaciones
                    new ElementCategoryFilter(BuiltInCategory.OST_Walls),
                    new ElementCategoryFilter(BuiltInCategory.OST_Floors),
                    new ElementCategoryFilter(BuiltInCategory.OST_Ceilings),
                    new ElementCategoryFilter(BuiltInCategory.OST_MEPSpaces),
                    new ElementCategoryFilter(BuiltInCategory.OST_HVAC_Zones),
            
                    // Elementos eléctricos
                    new ElementCategoryFilter(BuiltInCategory.OST_ElectricalFixtures),
                    new ElementCategoryFilter(BuiltInCategory.OST_ElectricalEquipment),
                    new ElementCategoryFilter(BuiltInCategory.OST_LightingFixtures),
                    new ElementCategoryFilter(BuiltInCategory.OST_LightingDevices),
                    new ElementCategoryFilter(BuiltInCategory.OST_ElectricalCircuit),
                    new ElementCategoryFilter(BuiltInCategory.OST_CommunicationDevices),
                    new ElementCategoryFilter(BuiltInCategory.OST_DataDevices),
                    new ElementCategoryFilter(BuiltInCategory.OST_TelephoneDevices),
                    new ElementCategoryFilter(BuiltInCategory.OST_SecurityDevices),
                    new ElementCategoryFilter(BuiltInCategory.OST_FireAlarmDevices),
                    new ElementCategoryFilter(BuiltInCategory.OST_NurseCallDevices),
                    new ElementCategoryFilter(BuiltInCategory.OST_CableTray),
                    new ElementCategoryFilter(BuiltInCategory.OST_CableTrayFitting),
                    new ElementCategoryFilter(BuiltInCategory.OST_Conduit),
                    new ElementCategoryFilter(BuiltInCategory.OST_ConduitFitting),
                    new ElementCategoryFilter(BuiltInCategory.OST_Wire),
            
                    // Equipos mecánicos y HVAC
                    new ElementCategoryFilter(BuiltInCategory.OST_MechanicalEquipment),
                    new ElementCategoryFilter(BuiltInCategory.OST_DuctTerminal),
                    new ElementCategoryFilter(BuiltInCategory.OST_DuctCurves),
                    new ElementCategoryFilter(BuiltInCategory.OST_DuctFitting),
                    new ElementCategoryFilter(BuiltInCategory.OST_DuctAccessory),
                    new ElementCategoryFilter(BuiltInCategory.OST_DuctInsulations),
                    new ElementCategoryFilter(BuiltInCategory.OST_DuctLinings),
                    new ElementCategoryFilter(BuiltInCategory.OST_FlexDuctCurves),
                    new ElementCategoryFilter(BuiltInCategory.OST_DuctSystem),
                    new ElementCategoryFilter(BuiltInCategory.OST_DuctColorFillLegends),
            
                    // Tuberías y plomería
                    new ElementCategoryFilter(BuiltInCategory.OST_PlumbingFixtures),
                    new ElementCategoryFilter(BuiltInCategory.OST_PipeCurves),
                    new ElementCategoryFilter(BuiltInCategory.OST_PipeFitting),
                    new ElementCategoryFilter(BuiltInCategory.OST_PipeAccessory),
                    new ElementCategoryFilter(BuiltInCategory.OST_PipeInsulations),
                    new ElementCategoryFilter(BuiltInCategory.OST_FlexPipeCurves),
                    new ElementCategoryFilter(BuiltInCategory.OST_PipingSystem),
                    new ElementCategoryFilter(BuiltInCategory.OST_PipeColorFillLegends),
            
                    // Sprinklers y protección contra incendio
                    new ElementCategoryFilter(BuiltInCategory.OST_Sprinklers),
                       
                    // Elementos genéricos MEP
                    new ElementCategoryFilter(BuiltInCategory.OST_GenericModel),
                    new ElementCategoryFilter(BuiltInCategory.OST_SpecialityEquipment),
                }));

                case Speciality.EQUIPAMIENTO:
                    return collector.WherePasses(new LogicalOrFilter(new List<ElementFilter>
            {
                new ElementCategoryFilter(BuiltInCategory.OST_Walls),
                new ElementCategoryFilter(BuiltInCategory.OST_Floors),
                new ElementCategoryFilter(BuiltInCategory.OST_StructuralFraming),
                new ElementCategoryFilter(BuiltInCategory.OST_StructuralColumns),
                new ElementCategoryFilter(BuiltInCategory.OST_Doors),
                new ElementCategoryFilter(BuiltInCategory.OST_Windows),
                new ElementCategoryFilter(BuiltInCategory.OST_SpecialityEquipment),
                new ElementCategoryFilter(BuiltInCategory.OST_Furniture),
                new ElementCategoryFilter(BuiltInCategory.OST_MedicalEquipment),
                new ElementCategoryFilter(BuiltInCategory.OST_FoodServiceEquipment),
                new ElementCategoryFilter(BuiltInCategory.OST_ElectricalEquipment),
                new ElementCategoryFilter(BuiltInCategory.OST_MechanicalEquipment),
                new ElementCategoryFilter(BuiltInCategory.OST_ZoneEquipment),
            }));

                default:
                    return collector.WherePasses(new LogicalOrFilter(new List<ElementFilter>
            {
                new ElementCategoryFilter(BuiltInCategory.OST_Walls),
                new ElementCategoryFilter(BuiltInCategory.OST_Floors),
                new ElementCategoryFilter(BuiltInCategory.OST_StructuralFraming),
                new ElementCategoryFilter(BuiltInCategory.OST_StructuralColumns),
                new ElementCategoryFilter(BuiltInCategory.OST_Doors),
                new ElementCategoryFilter(BuiltInCategory.OST_Windows),
            }));
            }
        }
    }
}

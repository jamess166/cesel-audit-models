﻿using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BimManagement.Commands.ModelAudit.Config;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace BimManagement.Commands.ModelAudit
{
    public class CheckParameters
    {
        private readonly List<SpecialtyParameters> _config;

        public CheckParameters()
        {
            try
            {
                // Obtener la ruta del DLL
                string dllPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string dllDirectory = Path.GetDirectoryName(dllPath);
                // Construir la ruta del archivo de configuración
                string configPath = Path.Combine(dllDirectory, "Resources", "parameters-config.json");

                if (!File.Exists(configPath))
                {
                    throw new FileNotFoundException($"Archivo de configuración no encontrado en: {configPath}");
                }

                string configJson = File.ReadAllText(configPath);

                // Cargar el JSON como un JObject
                var jsonObject = JObject.Parse(configJson);

                // Extraer la lista de parámetros de la clave "parameters"
                _config = jsonObject["parameters"]?.ToObject<List<SpecialtyParameters>>()
                          ?? throw new JsonException("Error al deserializar la lista de parámetros");

            }
            catch (Exception ex)
            {
                throw new Exception($"Error al cargar la configuración: {ex.Message}", ex);
            }
        }

        private string GetParameterType(Parameter parameter)
        {
            if (parameter == null) return "DESCONOCIDO";

            ForgeTypeId dataType = parameter.Definition.GetDataType();

            if (dataType == SpecTypeId.Boolean.YesNo)
                return "SI/NO";
            if (dataType == SpecTypeId.String.Text)
                return "TEXT";
            if (dataType == SpecTypeId.Length)
                return "DIMENSION";
            if (dataType == SpecTypeId.Area)
                return "AREA";
            if (dataType == SpecTypeId.Volume)
                return "VOLUMEN";
            if (dataType == SpecTypeId.Number)
                return "NÚMERO";
            if (dataType == SpecTypeId.Int.Integer)
                return "ENTERO";
            if (dataType == SpecTypeId.Length)
                return "ÁNGULO";

            return dataType.ToString();
        }

        private string GetParameterGroup(ForgeTypeId groupTypeId)
        {
            if (groupTypeId == GroupTypeId.Data)
                return "DATA";
            if (groupTypeId == GroupTypeId.Text)
                return "TEXT";
            if (groupTypeId == GroupTypeId.General)
                return "GENERAL";
            if (groupTypeId == GroupTypeId.Construction)
                return "CONSTRUCCIÓN";

            return groupTypeId.ToString();
        }

        public List<ParameterDefinition> GetParameters()
        {
            //HashSet<ParameterDefinition> uniqueParameters = new HashSet<ParameterDefinition>();
            HashSet<ParameterDefinition> uniqueParameters = new HashSet<ParameterDefinition>(new ParameterDefinitionComparer());


            // Obtener el BindingMap del documento
            BindingMap bindings = RevitTools.doc.ParameterBindings;

            // Crear un diccionario para almacenar el tipo de binding de cada parámetro
            Dictionary<string, string> parameterBindingTypes = new Dictionary<string, string>();

            // Primero, recopilar todos los bindings
            DefinitionBindingMapIterator it = bindings.ForwardIterator();
            while (it.MoveNext())
            {
                Definition d = it.Key as Definition;
                Binding b = it.Current as Binding;

                if (d != null)
                {
                    parameterBindingTypes[d.Name] = (b is InstanceBinding) ? "EJEMPLAR" : "TIPO";
                }
            }

            FilteredElementCollector collector = new FilteredElementCollector(RevitTools.doc)
                .WhereElementIsNotElementType() // Obtiene instancias
                .UnionWith(new FilteredElementCollector(RevitTools.doc).WhereElementIsElementType());

            foreach (Element element in collector)
            {
                ICollection<Parameter> parameters = element.GetOrderedParameters();
                foreach (Parameter param in parameters)
                {
                    if (!param.IsShared) continue;                   

                    if (param != null && param.Definition != null)
                    {
                        string scope = "TIPO"; // valor por defecto
                        if (parameterBindingTypes.ContainsKey(param.Definition.Name))
                        {
                            scope = parameterBindingTypes[param.Definition.Name];
                        }

                        var paramDefinition = new ParameterDefinition
                        {
                            Name = param.Definition.Name,
                            ParameterType = GetParameterType(param),                            
                            Group = GetParameterGroup(param.Definition.GetGroupTypeId()),
                            Scope = scope
                        };
                        uniqueParameters.Add(paramDefinition);
                    }
                }
            }

            return uniqueParameters.ToList();
        }


        public ValidationResult ValidateParameters(Speciality specialty)
        {
            if (_config == null || !_config.Any())
            {
                return new ValidationResult
                {
                    IsValid = false,
                    IsRelevant = false,
                    Message = "La configuración de parámetros no ha sido cargada correctamente."
                };
            }

            List<ParameterDefinition> actualParameters = GetParameters();

            //// Obtener los parámetros esperados (según especialidad + "TODOS")
            //var matchingSpecialties = _config.Where(s =>
            //    s.Specialty.Equals(specialty.ToString(), StringComparison.OrdinalIgnoreCase) ||
            //    s.Specialty.Equals("TODOS", StringComparison.OrdinalIgnoreCase)).ToList();
            // Obtener los parámetros esperados (según especialidad + "TODOS"), aplicando el prefijo en la comparación

            // Obtener los parámetros esperados(según especialidad + "TODOS") y agregando el prefijo al nombre

            var matchingSpecialties = _config
                .Where(s =>
                    s.Specialty.Equals(specialty.ToString(), StringComparison.Ordinal) ||
                    s.Specialty.Equals("TODOS", StringComparison.Ordinal))
                .Select(s => new SpecialtyParameters
                {
                    Specialty = s.Specialty,
                    Prefix = s.Prefix,
                    Parameters = s.Parameters.Select(p => new ParameterDefinition
                    {
                        Name = s.Prefix + p.Name,  // Se agrega el prefijo al nombre del parámetro                        
                        ParameterType = p.ParameterType,
                        Group = p.Group,
                        Scope = p.Scope
                    }).ToList()
                }).ToList();


            if (!matchingSpecialties.Any())
            {
                return new ValidationResult
                {
                    IsValid = false,
                    IsRelevant = false,
                    Message = $"No se encontró configuración para la especialidad {specialty}."
                };
            }

            var expectedParameters = matchingSpecialties.SelectMany(s => s.Parameters).ToList();

            // Comparaciones
            var missingParameters = expectedParameters
                .Where(ep => !actualParameters.Any(ap => ap.Name.Equals(ep.Name, StringComparison.Ordinal)))
                .ToList();

            var extraParameters = actualParameters
                .Where(ap => !expectedParameters.Any(ep => ep.Name.Equals(ap.Name, StringComparison.Ordinal)))
                .ToList();

            var incorrectParameters = actualParameters
                .Where(ap => expectedParameters.Any(ep => ep.Name.Equals(ap.Name, StringComparison.Ordinal)) &&
                             !expectedParameters.Any(ep => AreParametersEqual(ep, ap)))
                .ToList();

            var isValid = !missingParameters.Any() && !extraParameters.Any() && !incorrectParameters.Any();

            // Construcción de mensajes
            var messageBuilder = new StringBuilder();

            if (missingParameters.Any())
            {
                messageBuilder.AppendLine("Parámetros faltantes:");
                foreach (var p in missingParameters)
                {
                    messageBuilder.AppendLine($"{p.Name} (Tipo: {p.ParameterType}, Grupo: {p.Group}, Ejemplar o Tipo: {p.Scope})");
                }
                messageBuilder.AppendLine("\n\n");
            }

            if (extraParameters.Any())
            {
                messageBuilder.AppendLine("Parámetros no esperados:");
                foreach (var p in extraParameters)
                {
                    messageBuilder.AppendLine($"{p.Name}");
                }
                messageBuilder.AppendLine("\n\n");
            }

            if (incorrectParameters.Any())
            {
                messageBuilder.AppendLine("Parámetros con valores incorrectos:");
                foreach (var p in incorrectParameters)
                {
                    var expected = expectedParameters.First(ep => ep.Name.Equals(p.Name, StringComparison.Ordinal));
                    messageBuilder.AppendLine($"- {p.Name}");
                    messageBuilder.AppendLine($"  Parámetro esperado -> Tipo: {expected.ParameterType}, Grupo: {expected.Group}, Ejemplar o Tipo: {expected.Scope}");
                    messageBuilder.AppendLine($"  Parámetro existente -> Tipo: {p.ParameterType}, Grupo: {p.Group}, Ejemplar o Tipo: {p.Scope}");
                    messageBuilder.AppendLine();
                }
            }

            return new ValidationResult
            {
                IsValid = isValid,
                IsRelevant = matchingSpecialties.Any(s => s.Specialty.Equals("TODOS", StringComparison.Ordinal)),
                Message = messageBuilder.ToString().Trim()
            };
        }

        /// <summary>
        /// Compara dos parámetros para verificar si son equivalentes en todas sus propiedades.
        /// </summary>
        private bool AreParametersEqual(ParameterDefinition expected, ParameterDefinition actual)
        {
            return (expected.Name).Equals(actual.Name, StringComparison.Ordinal) &&
                   //expected.Discipline.Equals(actual.Discipline, StringComparison.OrdinalIgnoreCase) &&
                   expected.ParameterType.Equals(actual.ParameterType, StringComparison.Ordinal) &&
                   expected.Group.Equals(actual.Group, StringComparison.Ordinal) &&
                   expected.Scope.Equals(actual.Scope, StringComparison.Ordinal);
        }
    }
}

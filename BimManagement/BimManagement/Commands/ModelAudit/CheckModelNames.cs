using Autodesk.Revit.DB;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BimManagement.Commands.ModelAudit
{
    public class CheckModelNames
    {
        private readonly FileNameConfig _config;

        public CheckModelNames()
        {
            try
            {
                // Obtener la ruta del DLL
                string dllPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string dllDirectory = Path.GetDirectoryName(dllPath);
                // Construir la ruta del archivo de configuración
                string configPath = Path.Combine(dllDirectory, "Resources", "filename-config.json");

                if (!File.Exists(configPath))
                {
                    throw new FileNotFoundException($"Archivo de configuración no encontrado en: {configPath}");
                }

                string configJson = File.ReadAllText(configPath);
                _config = JsonConvert.DeserializeObject<FileNameConfig>(configJson);

                if (_config == null)
                {
                    throw new JsonException("Error al deserializar el archivo de configuración");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al cargar la configuración: {ex.Message}", ex);
            }
        }

        public ValidationResult ValidateModelName(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Message = "El nombre del modelo no puede estar vacío",
                    IsRelevant = false
                };
            }

            // Verificar espacios si no están permitidos
            if (!_config.Structure.ConsiderSpaces && modelName.Contains(" "))
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Message = "El nombre no debe contener espacios",
                    IsRelevant = true
                };
            }

            // Dividir el nombre en partes
            string[] parts = modelName.Split(new[] { _config.Structure.Separator }, StringSplitOptions.None);

            // Validar número de partes
            if (parts.Length != _config.Structure.TotalParts)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Message = $"El nombre debe tener {_config.Structure.TotalParts} partes separadas por '{_config.Structure.Separator}'",
                    IsRelevant = true
                };
            }

            var errors = new List<string>();

            // Validar cada parte
            for (int i = 0; i < parts.Length && i < _config.Parts.Count; i++)
            {
                var partConfig = _config.Parts[i];
                var part = parts[i];                       

                // Validar valores permitidos
                if (partConfig.AllowedValues != null && partConfig.AllowedValues.Any())
                {
                    var allowedCodes = partConfig.AllowedValues.Select(av => av.Code).ToList();
                    if (!allowedCodes.Contains(part))
                    {
                        errors.Add($"Valor no permitido '{part}' para {partConfig.Name}.");
                    }
                }
            }

            return new ValidationResult
            {
                IsValid = !errors.Any(),
                Message = errors.Any() ? string.Join(". ", errors) : string.Empty,
                IsRelevant = true
            };
        }

        public string GetDescriptionForCode(string partName, string code)
        {
            var part = _config.Parts.FirstOrDefault(p => p.Name == partName);
            if (part == null) return null;

            var allowedValue = part.AllowedValues?.FirstOrDefault(av => av.Code == code);
            return allowedValue?.Description;
        }

        public List<AllowedValue> GetAllowedValues(string partName)
        {
            var part = _config.Parts.FirstOrDefault(p => p.Name == partName);
            return part?.AllowedValues ?? new List<AllowedValue>();
        }        
    }
}

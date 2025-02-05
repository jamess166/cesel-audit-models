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
    public class LevelConfig
    {
        public List<string> ValidLevelNames { get; set; }
    }

    public class CheckLevelNames
    {
        private readonly LevelConfig _config;

        public CheckLevelNames()
        {
            try
            {
                // Obtener la ruta del DLL
                string dllPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string dllDirectory = Path.GetDirectoryName(dllPath);
                // Construir la ruta del archivo de configuración
                string configPath = Path.Combine(dllDirectory, "Resources", "levelname-config.json");

                if (!File.Exists(configPath))
                {
                    throw new FileNotFoundException($"Archivo de configuración no encontrado en: {configPath}");
                }

                string configJson = File.ReadAllText(configPath);
                _config = JsonConvert.DeserializeObject<LevelConfig>(configJson);

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

        public ValidationResult ValidateLevelNaming()
        {
            try
            {                
                if (_config == null)
                {
                    throw new JsonException("Error al deserializar el archivo de configuración");
                }

                // Obtener todos los niveles del documento
                FilteredElementCollector collector = new FilteredElementCollector(RevitTools.doc)
                    .OfClass(typeof(Level));

                var levels = collector.Cast<Level>().ToList();
                var invalidLevels = new List<string>();

                // Validar cada nivel del modelo
                foreach (var level in levels)
                {
                    string levelName = level.Name.Trim();

                    // Validar si el nombre del nivel está en la lista predefinida
                    if (!_config.ValidLevelNames.Contains(levelName))
                    {
                        invalidLevels.Add($"'{levelName}'");
                    }
                }

                // Preparar mensaje de resultado
                if (invalidLevels.Any())
                {
                    return new ValidationResult
                    {
                        IsValid = false,
                        IsRelevant = true,
                        Message = $"Los niveles no cumplen con el PEB: {string.Join(", ", invalidLevels)}"
                    };
                }

                return new ValidationResult
                {
                    IsValid = true,
                    IsRelevant = true,
                    Message = ""
                };
            }
            catch (Exception ex)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    IsRelevant = false,
                    Message = $"Error al validar la nomenclatura de niveles: {ex.Message}"
                };
            }
        }        
    }
}

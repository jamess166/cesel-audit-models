using Autodesk.Revit.DB;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BimManagement.Commands.ModelAudit
{
    public class SharedCoordinatesConfig
    {
        [JsonProperty("sharedCoordinates")]
        public CoordinatesValues SharedCoordinates { get; set; }
    }

    public class CoordinatesValues
    {
        [JsonProperty("norte")]
        public double Norte { get; set; }

        [JsonProperty("este")]
        public double Este { get; set; }

        [JsonProperty("elevacion")]
        public double Elevacion { get; set; }

        [JsonProperty("tolerancia")]
        public double Tolerancia { get; set; }
    }

    public class SharedGetCoordinatesDoc
    {
        public (double norte, double este, double elevacion) GetSharedCoordinates()
        {
            // Obtener el punto base del proyecto
            BasePoint projectBasePoint = null;
            FilteredElementCollector collector = new FilteredElementCollector(RevitTools.doc)
                .OfClass(typeof(BasePoint));

            foreach (BasePoint bp in collector)
            {
                if (!bp.IsShared)
                {
                    projectBasePoint = bp;
                    break;
                }
            }

            if (projectBasePoint == null)
            {
                throw new Exception("No se pudo encontrar el punto base del proyecto");
            }

            // Obtener las coordenadas
            double norte = projectBasePoint.get_Parameter(BuiltInParameter.BASEPOINT_NORTHSOUTH_PARAM).AsDouble();
            double este = projectBasePoint.get_Parameter(BuiltInParameter.BASEPOINT_EASTWEST_PARAM).AsDouble();
            double elevacion = projectBasePoint.get_Parameter(BuiltInParameter.BASEPOINT_ELEVATION_PARAM).AsDouble();

            // Convertir de pies a metros (Revit usa pies internamente)
            norte = UnitUtils.ConvertFromInternalUnits(norte, UnitTypeId.Meters);
            este = UnitUtils.ConvertFromInternalUnits(este, UnitTypeId.Meters);
            elevacion = UnitUtils.ConvertFromInternalUnits(elevacion, UnitTypeId.Meters);

            return (norte, este, elevacion);
        }
    }

    public class CheckSharedCoordinates
    {
        private readonly SharedCoordinatesConfig _config;

        public CheckSharedCoordinates()
        {
            try
            {
                string dllPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string dllDirectory = Path.GetDirectoryName(dllPath);
                string configPath = Path.Combine(dllDirectory, "Resources", "coordinates-config.json");

                if (!File.Exists(configPath))
                {
                    throw new FileNotFoundException($"Archivo de configuración de coordenadas no encontrado en: {configPath}");
                }

                string configJson = File.ReadAllText(configPath);
                _config = JsonConvert.DeserializeObject<SharedCoordinatesConfig>(configJson);

                if (_config == null)
                {
                    throw new JsonException("Error al deserializar el archivo de configuración de coordenadas");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al cargar la configuración de coordenadas: {ex.Message}", ex);
            }
        }

        public ValidationResult ValidateCoordinates()
        {
            SharedGetCoordinatesDoc sharedGetCoordinatesDoc = new SharedGetCoordinatesDoc();
            (double norte, double este, double elevacion) = sharedGetCoordinatesDoc.GetSharedCoordinates();

            var errors = new List<string>();
            var tolerance = _config.SharedCoordinates.Tolerancia;

            // Validar Norte
            if (Math.Abs(norte - _config.SharedCoordinates.Norte) > tolerance)
            {
                errors.Add($"Norte: {norte:F3}");
            }

            // Validar Este
            if (Math.Abs(este - _config.SharedCoordinates.Este) > tolerance)
            {
                errors.Add($"Este: {este:F3}");
            }

            // Validar Elevación
            if (Math.Abs(elevacion - _config.SharedCoordinates.Elevacion) > tolerance)
            {
                errors.Add($"Elevación: {elevacion:F3}");
            }

            // Si hay errores, formar el mensaje con los valores de las coordenadas
            string message = string.Empty;
            if (errors.Any())
            {
                message = $"Las coordenadas compartidas no son las especificadas en el PEB: {string.Join(", ", errors)}";
            }

            return new ValidationResult
            {
                IsValid = !errors.Any(),
                Message = message,
                IsRelevant = true
            };
        }

        public CoordinatesValues GetExpectedCoordinates()
        {
            return _config.SharedCoordinates;
        }
    }
}

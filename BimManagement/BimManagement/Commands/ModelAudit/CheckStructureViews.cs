using Autodesk.Revit.DB;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace BimManagement.Commands.ModelAudit.Config
{
    public enum Speciality
    {
        ARQUITECTURA,
        ESTRUCTURAS,
        SEGURIDAD,
        SANITARIAS,
        MECANICAS,
        ELECTRICAS,
        COMUNICACIONES,
    }

    public class ViewOrganizationConfig
    {
        [JsonProperty("viewOrganization")]
        public Dictionary<string, EspecialidadConfig> ViewOrganization { get; set; }
    }

    public class EspecialidadConfig
    {
        [JsonProperty("gruposVista01")]
        public List<GrupoVista01Config> GruposVista01 { get; set; }
    }

    public class GrupoVista01Config
    {
        [JsonProperty("nombre")]
        public string Nombre { get; set; }

        [JsonProperty("gruposVista02")]
        public List<string> GruposVista02 { get; set; }
    }

    public class ViewData
    {
        public string GrupoVista01 { get; set; }
        public string GrupoVista02 { get; set; }
        public string ViewName { get; set; }
    }

    public class CheckViewOrganization
    {
        private readonly ViewOrganizationConfig _config;

        public CheckViewOrganization()
        {
            try
            {
                string dllPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string dllDirectory = Path.GetDirectoryName(dllPath);
                string configPath = Path.Combine(dllDirectory, "Resources", "view-organization-config.json");

                if (!File.Exists(configPath))
                {
                    throw new FileNotFoundException($"Archivo de configuración de organización de vistas no encontrado en: {configPath}");
                }

                string configJson = File.ReadAllText(configPath);
                _config = JsonConvert.DeserializeObject<ViewOrganizationConfig>(configJson);

                if (_config == null)
                {
                    throw new JsonException("Error al deserializar el archivo de configuración de organización de vistas");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al cargar la configuración de organización de vistas: {ex.Message}", ex);
            }
        }
               
        public IEnumerable<View> GetModelViews(Document doc)
        {
            // Obtener todas las vistas del modelo
            var collector = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v.ViewType != ViewType.Undefined && !v.IsTemplate); // Excluir plantillas

            // Filtrar vistas por el tipo deseado y asegurarse de que no sean dependientes
            var filteredViews = collector.Where(v =>
                v.ViewType == ViewType.FloorPlan ||  // Planta arquitectura
                v.ViewType == ViewType.EngineeringPlan ||  // Planta de ingeniería (puede ser estructuras)
                v.ViewType == ViewType.Section ||  // Secciones
                v.ViewType == ViewType.ThreeD ||  // Vistas 3D
                v.ViewType == ViewType.Elevation ||  // Elevaciones
                v.ViewType == ViewType.CeilingPlan ||  // Ceiling Plans
                v.ViewType == ViewType.DraftingView // Drafting View
            )
            .Where(v =>
            {
                // Comprobar si la vista no es dependiente
                ElementId parentId = v.GetPrimaryViewId();
                return ((int)parentId.Value) == -1; // Vista no dependiente
            })
            .ToList();

            return filteredViews;
        }

        public ValidationResult ValidateViews(Speciality especialidad)
        {
            IEnumerable<View> revitViews = GetModelViews(RevitTools.doc);

            if (revitViews == null) throw new ArgumentNullException(nameof(revitViews));

            var errorsGrupoVista01 = new List<string>();
            var errorsGrupoVista02 = new List<string>();

            // Verificar si existe configuración para la especialidad
            if (!_config.ViewOrganization.TryGetValue(especialidad.ToString(), out var especialidadConfig))
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Message = $"No se encontró configuración para la especialidad {especialidad}",
                    IsRelevant = false
                };
            }

            // Validar cada vista
            foreach (var view in revitViews)
            {
                // Obtener el valor de GrupoVista01 y verificar si es válido
                var grupoVista01Value = view.LookupParameter("PR-GRUPO DE VISTAS 01")?.AsString();
                var grupoVista01Config = especialidadConfig.GruposVista01
                    .FirstOrDefault(g => g.Nombre == grupoVista01Value);

                if (grupoVista01Config == null)
                {
                    // Si no es válido, agregar la vista a los errores
                    errorsGrupoVista01.Add(view.Name);
                    continue;
                    // Pasar a la siguiente vista
                }

                // Obtener el valor de GrupoVista02 y verificar si es válido dentro del GrupoVista01 seleccionado
                var grupoVista02Value = view.LookupParameter("PR-GRUPO DE VISTAS 02")?.AsString();
                if (!grupoVista01Config.GruposVista02.Contains(grupoVista02Value))
                {
                    // Si no es válido, agregar la vista a los errores
                    errorsGrupoVista02.Add(view.Name);
                }
            }

            // Construir el mensaje de validación
            var message = string.Empty;

            if (errorsGrupoVista01.Any())
            {
                message += $"Vistas con Grupo de Vista 01 incorrecto: {string.Join(", ", errorsGrupoVista01)}.\n";
            }

            if (errorsGrupoVista02.Any())
            {
                message += $"Vistas con Grupo de Vista 02 incorrecto: {string.Join(", ", errorsGrupoVista02)}.";
            }

            // Retornar el resultado de validación
            return new ValidationResult
            {
                IsValid = !errorsGrupoVista01.Any() && !errorsGrupoVista02.Any(),
                Message = message.Trim(),
                IsRelevant = true
            };
        }
    }
}

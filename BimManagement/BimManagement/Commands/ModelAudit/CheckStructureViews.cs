using Autodesk.Revit.DB;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

    public class EspecialidadConfig
    {
        [JsonProperty("diseño")]
        public List<string> Diseño { get; set; }

        [JsonProperty("documentacion")]
        public List<string> Documentacion { get; set; }
    }

    public class ViewOrganizationConfig
    {
        [JsonProperty("viewOrganization")]
        public Dictionary<string, EspecialidadConfig> ViewOrganization { get; set; }
    }

    //public class ViewOrganizationConfig
    //{
    //    [JsonProperty("viewOrganization")]
    //    public Dictionary<string, EspecialidadConfig> ViewOrganization { get; set; }
    //}

    //public class EspecialidadConfig
    //{
    //    [JsonProperty("gruposVista01")]
    //    public List<GrupoVista01Config> GruposVista01 { get; set; }
    //}

    //public class GrupoVista01Config
    //{
    //    [JsonProperty("nombre")]
    //    public string Nombre { get; set; }

    //    [JsonProperty("gruposVista02")]
    //    public List<string> GruposVista02 { get; set; }
    //}

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
            var revitViews = GetModelViews(RevitTools.doc);
            if (revitViews == null) throw new ArgumentNullException(nameof(revitViews));

            // Validar configuración por especialidad
            if (!_config.ViewOrganization.TryGetValue(especialidad.ToString(), out var especialidadConfig))
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Message = $"No se encontró configuración para la especialidad {especialidad}",
                    IsRelevant = false
                };
            }

            var errorsGrupoVista01 = new List<string>();
            var errorsGrupoVista02 = new List<string>();
            var numerosPorGrupo01 = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);


            //var numerosGrupoVista02 = new List<int>();

            foreach (var view in revitViews)
            {
                var (grupo01, grupo02) = GetViewGroups(view);

                if (string.IsNullOrWhiteSpace(grupo01) || string.IsNullOrWhiteSpace(grupo02))
                {
                    errorsGrupoVista01.Add(view.Name);
                    continue;
                }

                // Validar Grupo 01
                if (!IsValidGrupo01(grupo01))
                {
                    errorsGrupoVista01.Add($"{view.Name} - '{grupo01}' debe ser 'I. DISEÑO' o 'II. DOCUMENTACION'");
                    continue;
                }

                // Validar Grupo 02
                if (!IsValidGrupo02(grupo02, grupo01, especialidadConfig))
                {
                    errorsGrupoVista02.Add($"{view.Name} - '{grupo02}' no es válido para {grupo01}");
                }

                // Extraer número para validación de secuencia
                if (TryExtractNumber(grupo02, out int numero))
                {
                    if (!numerosPorGrupo01.ContainsKey(grupo01))
                        numerosPorGrupo01[grupo01] = new List<int>();

                    numerosPorGrupo01[grupo01].Add(numero);
                }
            }

            // Validar secuencia de números
            foreach (var kvp in numerosPorGrupo01)
            {
                ValidateNumberSequence(kvp.Value, errorsGrupoVista02, kvp.Key);
            }

            return BuildValidationResult(errorsGrupoVista01, errorsGrupoVista02);
        }

        private (string grupo01, string grupo02) GetViewGroups(View view)
        {
            var grupo01 = view.LookupParameter("PR-GRUPO DE VISTAS 01")?.AsString();
            var grupo02 = view.LookupParameter("PR-GRUPO DE VISTAS 02")?.AsString();
            return (grupo01, grupo02);
        }

        private bool IsValidGrupo01(string grupo01)
        {
            return grupo01 == "I. DISEÑO" || grupo01 == "II. DOCUMENTACION";
        }

        private bool IsValidGrupo02(string grupo02, string grupo01, EspecialidadConfig config)
        {
            var grupo02Texto = Regex.Replace(grupo02, @"^\d+\.\s*", "").Trim();
            var allowedItems = grupo01 == "I. DISEÑO" ? config.Diseño : config.Documentacion;

            return allowedItems.Any(permitido =>
                string.Equals(Regex.Replace(permitido, @"^\d+\.\s*", "").Trim(), grupo02Texto, StringComparison.OrdinalIgnoreCase));
        }

        private bool TryExtractNumber(string grupo02, out int numero)
        {
            numero = 0; // Valor por defecto
            var match = Regex.Match(grupo02, @"^(\d+)\.");
            if (match.Success)
            {
                return int.TryParse(match.Groups[1].Value, out numero);
            }
            return false;
        }

        private void ValidateNumberSequence(List<int> numeros, List<string> errors, string grupo01)
        {
            if (numeros.Count <= 1) return;

            var secuencia = numeros.Distinct().OrderBy(n => n).ToList();
            for (int i = 1; i < secuencia.Count; i++)
            {
                if (secuencia[i] != secuencia[i - 1] + 1)
                {
                    errors.Add($"Numeración discontinua en '{grupo01}': {string.Join(", ", secuencia)}");
                    break;
                }
            }
        }

        private ValidationResult BuildValidationResult(List<string> errorsGrupo01, List<string> errorsGrupo02)
        {
            var mensaje = "";
            if (errorsGrupo01.Any())
                mensaje += $"Vistas con Grupo de Vista 01 incorrecto:\n{string.Join("\n", errorsGrupo01)}\n\n";
            if (errorsGrupo02.Any())
                mensaje += $"Vistas con Grupo de Vista 02 incorrecto o mal numeradas:\n{string.Join("\n", errorsGrupo02)}";

            return new ValidationResult
            {
                IsValid = !errorsGrupo01.Any() && !errorsGrupo02.Any(),
                Message = mensaje.Trim(),
                IsRelevant = true
            };
        }


        //public ValidationResult ValidateViews(Speciality especialidad)
        //{
        //    IEnumerable<View> revitViews = GetModelViews(RevitTools.doc);
        //    if (revitViews == null) throw new ArgumentNullException(nameof(revitViews));

        //    var errorsGrupoVista01 = new List<string>();
        //    var errorsGrupoVista02 = new List<string>();
        //    var numerosGrupoVista02 = new List<int>();

        //    // Validar configuración por especialidad
        //    if (!_config.ViewOrganization.TryGetValue(especialidad.ToString(), out var especialidadConfig))
        //    {
        //        return new ValidationResult
        //        {
        //            IsValid = false,
        //            Message = $"No se encontró configuración para la especialidad {especialidad}",
        //            IsRelevant = false
        //        };
        //    }

        //    foreach (var view in revitViews)
        //    {
        //        string grupo01 = view.LookupParameter("PR-GRUPO DE VISTAS 01")?.AsString();
        //        string grupo02 = view.LookupParameter("PR-GRUPO DE VISTAS 02")?.AsString();

        //        if (string.IsNullOrWhiteSpace(grupo01) || string.IsNullOrWhiteSpace(grupo02))
        //        {
        //            errorsGrupoVista01.Add(view.Name);
        //            continue;
        //        }

        //        var grupoConfig = especialidadConfig.GruposVista01.FirstOrDefault(g => g.Nombre == grupo01);
        //        if (grupoConfig == null)
        //        {
        //            errorsGrupoVista01.Add(view.Name);
        //            continue;
        //        }

        //        // Extraer texto sin número (por ejemplo, "1. CORTES" -> "CORTES")
        //        string grupo02Texto = Regex.Replace(grupo02, @"^\d+\.\s*", "").Trim();

        //        // Verificar si ese texto está en la lista del JSON
        //        bool esTextoValido = grupoConfig.GruposVista02
        //            .Any(permitido => string.Equals(permitido.Trim(), grupo02Texto, StringComparison.OrdinalIgnoreCase));

        //        if (!esTextoValido)
        //        {
        //            errorsGrupoVista02.Add($"{view.Name} - '{grupo02}' no es válido");
        //        }

        //        // Extraer número para validación de secuencia
        //        var match = Regex.Match(grupo02, @"^(\d+)\.");
        //        if (match.Success && int.TryParse(match.Groups[1].Value, out int numero))
        //        {
        //            numerosGrupoVista02.Add(numero);
        //        }
        //    }

        //    // Validar que los números sean consecutivos (si hay más de uno)
        //    var secuencia = numerosGrupoVista02.Distinct().OrderBy(n => n).ToList();
        //    for (int i = 1; i < secuencia.Count; i++)
        //    {
        //        if (secuencia[i] != secuencia[i - 1] + 1)
        //        {
        //            errorsGrupoVista02.Add($"Numeración discontinua: {string.Join(", ", secuencia)}");
        //            break;
        //        }
        //    }

        //    var mensaje = "";
        //    if (errorsGrupoVista01.Any())
        //        mensaje += $"Vistas con Grupo de Vista 01 incorrecto:\n{string.Join("\n", errorsGrupoVista01)}\n\n";

        //    if (errorsGrupoVista02.Any())
        //        mensaje += $"Vistas con Grupo de Vista 02 incorrecto o mal numeradas:\n{string.Join("\n", errorsGrupoVista02)}";

        //    return new ValidationResult
        //    {
        //        IsValid = !errorsGrupoVista01.Any() && !errorsGrupoVista02.Any(),
        //        Message = mensaje.Trim(),
        //        IsRelevant = true
        //    };
        //}


        //public ValidationResult ValidateViews(Speciality especialidad)
        //{
        //    IEnumerable<View> revitViews = GetModelViews(RevitTools.doc);

        //    if (revitViews == null) throw new ArgumentNullException(nameof(revitViews));

        //    var errorsGrupoVista01 = new List<string>();
        //    var errorsGrupoVista02 = new List<string>();

        //    // Verificar si existe configuración para la especialidad
        //    if (!_config.ViewOrganization.TryGetValue(especialidad.ToString(), out var especialidadConfig))
        //    {
        //        return new ValidationResult
        //        {
        //            IsValid = false,
        //            Message = $"No se encontró configuración para la especialidad {especialidad}",
        //            IsRelevant = false
        //        };
        //    }

        //    // Validar cada vista
        //    foreach (var view in revitViews)
        //    {
        //        // Obtener el valor de GrupoVista01 y verificar si es válido
        //        var grupoVista01Value = view.LookupParameter("PR-GRUPO DE VISTAS 01")?.AsString();
        //        var grupoVista01Config = especialidadConfig.GruposVista01
        //            .FirstOrDefault(g => g.Nombre == grupoVista01Value);

        //        if (grupoVista01Config == null)
        //        {
        //            // Si no es válido, agregar la vista a los errores
        //            errorsGrupoVista01.Add(view.Name);
        //            continue;
        //            // Pasar a la siguiente vista
        //        }

        //        // Obtener el valor de GrupoVista02 y verificar si es válido dentro del GrupoVista01 seleccionado
        //        var grupoVista02Value = view.LookupParameter("PR-GRUPO DE VISTAS 02")?.AsString();
        //        if (!grupoVista01Config.GruposVista02.Contains(grupoVista02Value))
        //        {
        //            // Si no es válido, agregar la vista a los errores
        //            errorsGrupoVista02.Add(view.Name);
        //        }
        //    }

        //    // Construir el mensaje de validación
        //    var message = string.Empty;

        //    if (errorsGrupoVista01.Any())
        //    {
        //        message += $"Vistas con Grupo de Vista 01 incorrecto:\n {string.Join("\n ", errorsGrupoVista01)}.\n\n";
        //    }

        //    if (errorsGrupoVista02.Any())
        //    {
        //        message += $"Vistas con Grupo de Vista 02 incorrecto:\n {string.Join("\n ", errorsGrupoVista02)}.";
        //    }

        //    // Retornar el resultado de validación
        //    return new ValidationResult
        //    {
        //        IsValid = !errorsGrupoVista01.Any() && !errorsGrupoVista02.Any(),
        //        Message = message.Trim(),
        //        IsRelevant = true
        //    };
        //}

    }

}


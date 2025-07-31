using Autodesk.Revit.DB;
using BimManagement.Commands.ModelAudit.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BimManagement.Commands.ModelAudit
{
    public class CheckDWGInModelAndFamily
    {
        public ValidationResult ValidateNoLinkedDWGs(Document doc)
        {
            var messageBuilder = new StringBuilder();
            bool hasDWGsProhibidos = false;

            try
            {
                var tiposProhibidos = new[]
                {
                    ViewType.FloorPlan,
                    ViewType.CeilingPlan,
                    ViewType.Section,
                    ViewType.Elevation,
                    ViewType.EngineeringPlan
                };

                // Buscar TODOS los DWGs en el modelo
                var todosDWGs = new FilteredElementCollector(doc)
                    .OfClass(typeof(ImportInstance))
                    .Cast<ImportInstance>()
                    .Where(x => EsDWG(doc, x))
                    .ToList();

                if (!todosDWGs.Any())
                {
                    return new ValidationResult
                    {
                        IsValid = true,
                        IsRelevant = false,
                        Message = "No se encontraron DWGs en el modelo."
                    };
                }

                // Verificar cada DWG
                foreach (var dwg in todosDWGs)
                {
                    var nombreDWG = ObtenerNombreDWG(doc, dwg);
                    var status = dwg.IsLinked ? "Vinculado" : "Importado";

                    // Si el DWG está en una vista específica
                    if (dwg.OwnerViewId != ElementId.InvalidElementId)
                    {
                        var vista = doc.GetElement(dwg.OwnerViewId) as View;
                        if (vista != null && tiposProhibidos.Contains(vista.ViewType))
                        {
                            hasDWGsProhibidos = true;
                            messageBuilder.AppendLine($"- {status}: {nombreDWG} (Vista: {vista.Name}, Tipo: {vista.ViewType}) ❌ NO PERMITIDO");
                        }
                        else
                        {
                            messageBuilder.AppendLine($"- {status}: {nombreDWG} (Vista: {vista?.Name ?? "desconocida"}, Tipo: {vista?.ViewType}) ✅ OK");
                        }
                    }
                    else
                    {
                        // DWG a nivel proyecto - verificar si aparece en vistas prohibidas
                        bool apareceEnVistaProhibida = false;
                        var vistasProhibidas = new FilteredElementCollector(doc)
                            .OfClass(typeof(View))
                            .Cast<View>()
                            .Where(v => !v.IsTemplate && tiposProhibidos.Contains(v.ViewType))
                            .ToList();

                        foreach (var vista in vistasProhibidas)
                        {
                            try
                            {
                                var dwgEnVista = new FilteredElementCollector(doc, vista.Id)
                                    .OfClass(typeof(ImportInstance))
                                    .FirstOrDefault(x => x.Id == dwg.Id);

                                if (dwgEnVista != null)
                                {
                                    apareceEnVistaProhibida = true;
                                    hasDWGsProhibidos = true;
                                    messageBuilder.AppendLine($"- {status}: {nombreDWG} (Aparece en vista prohibida: {vista.Name}, Tipo: {vista.ViewType}) ❌ NO PERMITIDO");
                                    break; // Solo reportar la primera vista prohibida donde aparece
                                }
                            }
                            catch
                            {
                                // Continuar con la siguiente vista si hay error
                            }
                        }

                        if (!apareceEnVistaProhibida)
                        {
                            messageBuilder.AppendLine($"- {status}: {nombreDWG} (Nivel proyecto - no aparece en vistas prohibidas) ✅ OK");
                        }
                    }
                }

                if (hasDWGsProhibidos)
                {
                    return new ValidationResult
                    {
                        IsValid = false,
                        IsRelevant = true,
                        Message = $"El modelo contiene DWGs en vistas NO PERMITIDAS:\n{messageBuilder.ToString().Trim()}"
                    };
                }
                else
                {
                    return new ValidationResult
                    {
                        IsValid = true,
                        IsRelevant = false,
                        Message = string.Empty
                    };
                }

                //var mensaje = hasDWGsProhibidos ?
                //    "El modelo contiene DWGs en vistas NO PERMITIDAS:" :
                //    "El modelo contiene DWGs pero están en vistas permitidas:";

                //return new ValidationResult
                //{
                //    IsValid = !hasDWGsProhibidos,
                //    IsRelevant = true,
                //    Message = $"{mensaje}\n{messageBuilder.ToString().Trim()}"
                //};
            }
            catch (Exception ex)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    IsRelevant = false,
                    Message = $"Error al analizar DWGs: {ex.Message}"
                };
            }
        }

        internal ValidationResult ValidateRequiredParameters(Speciality specialty)
        {
            throw new NotImplementedException();
        }

        private bool EsDWG(Document doc, ImportInstance instance)
        {
            try
            {
                var importType = doc.GetElement(instance.GetTypeId());
                var typeName = importType?.Name?.ToLower() ?? "";
                var instanceName = instance.Name?.ToLower() ?? "";

                return typeName.Contains(".dwg") || instanceName.Contains(".dwg");
            }
            catch
            {
                return false;
            }
        }

        private string ObtenerNombreDWG(Document doc, ImportInstance instance)
        {
            try
            {
                var importType = doc.GetElement(instance.GetTypeId());
                return importType?.Name ?? instance.Name ?? "DWG sin nombre";
            }
            catch
            {
                return instance.Name ?? "DWG sin nombre";
            }
        }

        //public ValidationResult ValidateNoLinkedDWGs(Document doc)
        //{
        //    var messageBuilder = new StringBuilder();
        //    bool hasDWGs = false;

        //    try
        //    {
        //        var tiposPermitidos = new[]
        //        {
        //        ViewType.FloorPlan,
        //        ViewType.CeilingPlan,
        //        ViewType.Section,
        //        ViewType.Elevation,
        //        ViewType.EngineeringPlan
        //    };

        //        var collector = new FilteredElementCollector(doc)
        //            .OfClass(typeof(ImportInstance))
        //            .Cast<ImportInstance>()
        //            .Where(x =>
        //            {
        //                var viewId = x.OwnerViewId;
        //                if (viewId == ElementId.InvalidElementId)
        //                    return false;

        //                var view = doc.GetElement(viewId) as View;
        //                return view != null && tiposPermitidos.Contains(view.ViewType);
        //            })
        //            .ToList();

        //        var dwgInstances = collector.Where(x => x.IsLinked).ToList();
        //        var importedDwgs = collector.Where(x => !x.IsLinked).ToList();

        //        if (dwgInstances.Any() || importedDwgs.Any())
        //        {
        //            hasDWGs = true;
        //            messageBuilder.AppendLine("El modelo contiene los siguientes DWG en vistas válidas:");

        //            foreach (var dwg in dwgInstances)
        //            {
        //                var view = doc.GetElement(dwg.OwnerViewId) as View;
        //                messageBuilder.AppendLine($"- Vinculado: {dwg.Name} (Vista: {view?.Name ?? "desconocida"}, Tipo: {view?.ViewType})");
        //            }

        //            foreach (var dwg in importedDwgs)
        //            {
        //                var view = doc.GetElement(dwg.OwnerViewId) as View;
        //                messageBuilder.AppendLine($"- Importado: {dwg.Name} (Vista: {view?.Name ?? "desconocida"}, Tipo: {view?.ViewType})");
        //            }
        //        }

        //        return new ValidationResult
        //        {
        //            IsValid = !hasDWGs,
        //            IsRelevant = hasDWGs,
        //            Message = messageBuilder.ToString().Trim()
        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        return new ValidationResult
        //        {
        //            IsValid = false,
        //            IsRelevant = false,
        //            Message = $"Error al analizar DWGs: {ex.Message}"
        //        };
        //    }
        //}
    }


    //public class CheckDWGInModelAndFamily
    //{
    //    public ValidationResult ValidateNoLinkedDWGs(Document doc)
    //    {
    //        var messageBuilder = new StringBuilder();
    //        bool hasLinkedDWGs = false;
    //        bool hasFamilyDWGs = false;

    //        try
    //        {
    //            // Buscar DWGs vinculados en el modelo
    //            var dwgInstances = new FilteredElementCollector(doc)
    //                .OfClass(typeof(ImportInstance))
    //                .Cast<ImportInstance>()
    //                .Where(x => x.IsLinked)
    //                .ToList();

    //            // Buscar DWGs importados (no vinculados)
    //            var importedDwgs = new FilteredElementCollector(doc)
    //                .OfClass(typeof(ImportInstance))
    //                .Cast<ImportInstance>()
    //                .Where(x => !x.IsLinked)
    //                .ToList();

    //            if (dwgInstances.Any() || importedDwgs.Any())
    //            {
    //                hasLinkedDWGs = true;
    //                messageBuilder.AppendLine("El modelo contiene los siguientes DWG:");

    //                foreach (var dwg in dwgInstances)
    //                {
    //                    messageBuilder.AppendLine($"- Vinculado: {dwg.Category?.Name ?? "Sin categoría"}, id: {dwg.Id.Value}");
    //                }

    //                foreach (var dwg in importedDwgs)
    //                {
    //                    messageBuilder.AppendLine($"- Importado: {dwg.Category?.Name ?? "Sin categoría"}, id: {dwg.Id.Value}");
    //                }

    //                messageBuilder.AppendLine("\n\n");
    //            }

    //            // Buscar familias que contienen DWGs
    //            var familyCollector = new FilteredElementCollector(doc)
    //                .OfClass(typeof(Family))
    //                .Cast<Family>();

    //            var familiesWithDWGs = new List<string>();

    //            foreach (var family in familyCollector)
    //            {
    //                try
    //                {
    //                    if (!family.IsEditable)
    //                        continue;

    //                    using (Document familyDoc = doc.EditFamily(family))
    //                    {
    //                        if (familyDoc == null) continue;

    //                        var importsInFamily = new FilteredElementCollector(familyDoc)
    //                            .OfClass(typeof(ImportInstance))
    //                            .Cast<ImportInstance>()
    //                            .ToList();

    //                        if (importsInFamily.Any())
    //                        {
    //                            hasFamilyDWGs = true;
    //                            familiesWithDWGs.Add($"{family.Name} ({importsInFamily.Count} DWG{(importsInFamily.Count > 1 ? "s" : "")})");
    //                        }

    //                        familyDoc.Close(false);
    //                    }
    //                }
    //                catch (Exception ex)
    //                {
    //                    messageBuilder.AppendLine($"Error al analizar la familia {family.Name}: {ex.Message}");
    //                }
    //            }

    //            if (familiesWithDWGs.Any())
    //            {
    //                messageBuilder.AppendLine("Las siguientes familias contienen DWG:");
    //                foreach (var familyName in familiesWithDWGs)
    //                {
    //                    messageBuilder.AppendLine($"- {familyName}");
    //                }
    //            }

    //            return new ValidationResult
    //            {
    //                IsValid = !hasLinkedDWGs && !hasFamilyDWGs,
    //                IsRelevant = hasLinkedDWGs || hasFamilyDWGs,
    //                Message = messageBuilder.ToString().Trim()
    //            };
    //        }
    //        catch (Exception ex)
    //        {
    //            return new ValidationResult
    //            {
    //                IsValid = false,
    //                IsRelevant = false,
    //                Message = $"Error al analizar DWGs: {ex.Message}"
    //            };
    //        }
    //    }
    //}
}

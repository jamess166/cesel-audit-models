using Autodesk.Revit.DB;
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
            bool hasLinkedDWGs = false;
            bool hasFamilyDWGs = false;

            try
            {
                // Buscar DWGs vinculados en el modelo
                var dwgInstances = new FilteredElementCollector(doc)
                    .OfClass(typeof(ImportInstance))
                    .Cast<ImportInstance>()
                    .Where(x => x.IsLinked)
                    .ToList();

                // Buscar DWGs importados (no vinculados)
                var importedDwgs = new FilteredElementCollector(doc)
                    .OfClass(typeof(ImportInstance))
                    .Cast<ImportInstance>()
                    .Where(x => !x.IsLinked)
                    .ToList();

                if (dwgInstances.Any() || importedDwgs.Any())
                {
                    hasLinkedDWGs = true;
                    messageBuilder.AppendLine("El modelo contiene los siguientes DWG:");

                    foreach (var dwg in dwgInstances)
                    {
                        messageBuilder.AppendLine($"- Vinculado: {dwg.Category?.Name ?? "Sin categoría"}, id: {dwg.Id.Value}");
                    }

                    foreach (var dwg in importedDwgs)
                    {
                        messageBuilder.AppendLine($"- Importado: {dwg.Category?.Name ?? "Sin categoría"}, id: {dwg.Id.Value}");
                    }

                    messageBuilder.AppendLine("\n\n");
                }

                // Buscar familias que contienen DWGs
                var familyCollector = new FilteredElementCollector(doc)
                    .OfClass(typeof(Family))
                    .Cast<Family>();

                var familiesWithDWGs = new List<string>();

                foreach (var family in familyCollector)
                {
                    try
                    {
                        if (!family.IsEditable)
                            continue;

                        using (Document familyDoc = doc.EditFamily(family))
                        {
                            if (familyDoc == null) continue;

                            var importsInFamily = new FilteredElementCollector(familyDoc)
                                .OfClass(typeof(ImportInstance))
                                .Cast<ImportInstance>()
                                .ToList();

                            if (importsInFamily.Any())
                            {
                                hasFamilyDWGs = true;
                                familiesWithDWGs.Add($"{family.Name} ({importsInFamily.Count} DWG{(importsInFamily.Count > 1 ? "s" : "")})");
                            }

                            familyDoc.Close(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        messageBuilder.AppendLine($"Error al analizar la familia {family.Name}: {ex.Message}");
                    }
                }

                if (familiesWithDWGs.Any())
                {
                    messageBuilder.AppendLine("Las siguientes familias contienen DWG:");
                    foreach (var familyName in familiesWithDWGs)
                    {
                        messageBuilder.AppendLine($"- {familyName}");
                    }
                }

                return new ValidationResult
                {
                    IsValid = !hasLinkedDWGs && !hasFamilyDWGs,
                    IsRelevant = hasLinkedDWGs || hasFamilyDWGs,
                    Message = messageBuilder.ToString().Trim()
                };
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
    }
}

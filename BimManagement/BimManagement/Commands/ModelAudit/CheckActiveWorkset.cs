using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BimManagement.Commands.ModelAudit
{
    public class CheckActiveWorkset
    {
        public ValidationResult ValidateActiveWorksets(Document doc)
        {
            var messageBuilder = new StringBuilder();
            bool hasActiveWorksets = false;

            if (!doc.IsWorkshared)
            {
                return new ValidationResult
                {
                    IsValid = true,
                    IsRelevant = true,
                    Message = ""
                };
            }

            // Obtener los Worksets del modelo
            var activeWorksets = new FilteredWorksetCollector(doc)
                .OfKind(WorksetKind.UserWorkset)
                .Where(w => w.IsOpen)
                .ToList();

            if (activeWorksets.Any())
            {
                hasActiveWorksets = true;
                messageBuilder.AppendLine("Existen Worksets activos en el modelo:");
                foreach (var workset in activeWorksets)
                {
                    messageBuilder.AppendLine($"{workset.Name}");
                }
                messageBuilder.AppendLine();
            }

            return new ValidationResult
            {
                IsValid = !hasActiveWorksets,
                IsRelevant = hasActiveWorksets,
                Message = messageBuilder.ToString().Trim()
            };
        }
    }
}

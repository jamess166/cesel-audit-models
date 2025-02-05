using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BimManagement.Commands.ModelAudit
{
    public class CheckWarnings
    {
        public ValidationResult ValidateWarnings(Document doc)
        {
            var messageBuilder = new StringBuilder();

            // Obtener todos los warnings del modelo
            IList<FailureMessage> warnings = doc.GetWarnings();

            // Clasificar warnings por su criticidad
            var warningGroups = warnings
                .GroupBy(w => w.GetSeverity()) // Agrupar por severidad
                .OrderByDescending(g => g.Key) // Ordenar por nivel de criticidad
                .ToDictionary(g => g.Key, g => g.Count());

            bool hasWarnings = warningGroups.Any();

            if (hasWarnings)
            {
                messageBuilder.AppendLine("Existen warnings en el modelo:");

                foreach (var group in warningGroups)
                {
                    messageBuilder.AppendLine($"- {group.Key}: {group.Value} warning(s)");
                }
                messageBuilder.AppendLine();
            }

            return new ValidationResult
            {
                IsValid = !hasWarnings, // Será válido solo si NO hay warnings
                IsRelevant = true,
                Message = messageBuilder.ToString().Trim()
            };
        }
    }
}

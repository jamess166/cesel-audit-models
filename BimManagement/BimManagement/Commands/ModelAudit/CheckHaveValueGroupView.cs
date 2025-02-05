using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BimManagement.Commands.ModelAudit
{
    public class CheckHaveValueGroupView
    {
        public ValidationResult ValidateViewGroupParameters(Document doc)
        {
            var messageBuilder = new StringBuilder();
            bool allValid = true;

            // Obtener todas las vistas relevantes del modelo (excluyendo plantillas y vistas indefinidas)
            var collector = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v.ViewType != ViewType.Undefined && !v.IsTemplate);

            // Filtrar vistas por los tipos deseados
            var filteredViews = collector.Where(v =>
                v.ViewType == ViewType.FloorPlan ||
                v.ViewType == ViewType.EngineeringPlan ||
                v.ViewType == ViewType.Section ||
                v.ViewType == ViewType.ThreeD ||
                v.ViewType == ViewType.Elevation ||
                v.ViewType == ViewType.CeilingPlan ||
                v.ViewType == ViewType.DraftingView);

            var viewsWithMissingParams = new List<string>();

            foreach (var view in filteredViews)
            {
                string param1Value = GetParameterValue(view, "PR-GRUPO DE VISTAS 01");
                string param2Value = GetParameterValue(view, "PR-GRUPO DE VISTAS 02");

                if (string.IsNullOrWhiteSpace(param1Value) || string.IsNullOrWhiteSpace(param2Value))
                {
                    allValid = false;
                    viewsWithMissingParams.Add(view.Name);
                }
            }

            // Construcción del mensaje de error si hay vistas sin los parámetros llenos
            if (viewsWithMissingParams.Any())
            {
                messageBuilder.AppendLine("Las siguientes vistas no tienen valores en 'PR-GRUPO DE VISTAS 01' o 'PR-GRUPO DE VISTAS 02':");
                foreach (var name in viewsWithMissingParams)
                {
                    messageBuilder.AppendLine($"{name}");
                }
                messageBuilder.AppendLine();
            }

            return new ValidationResult
            {
                IsValid = allValid,
                IsRelevant = true,
                Message = messageBuilder.ToString().Trim()
            };
        }

        // Método auxiliar para obtener el valor de un parámetro como string
        private string GetParameterValue(View view, string parameterName)
        {
            Parameter param = view.LookupParameter(parameterName);
            return param != null && param.HasValue ? param.AsString() : string.Empty;
        }

    }
}

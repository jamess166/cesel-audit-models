using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BimManagement.Commands.ModelAuditBuilding
{
    public class CheckValueParameters
    {
        public ValidationResult ValidateParameters(List<string> parameterNames)
        {
            var invalidElements = new Dictionary<string, List<string>>();
            bool isExecutionError = false;

            try
            {
                //FilteredElementCollector collector = new FilteredElementCollector(RevitTools.doc)
                //    .WhereElementIsNotElementType();

                // Recolectar elementos
                FilteredElementCollector collector = new FilteredElementCollector(RevitTools.doc)
                    .WhereElementIsNotElementType()
                    .WherePasses(new LogicalOrFilter(new List<ElementFilter>
                    {
                            new ElementCategoryFilter(BuiltInCategory.OST_StructuralFraming),
                            new ElementCategoryFilter(BuiltInCategory.OST_StructuralColumns),
                            new ElementCategoryFilter(BuiltInCategory.OST_StructuralFoundation),
                            new ElementCategoryFilter(BuiltInCategory.OST_Doors),
                            new ElementCategoryFilter(BuiltInCategory.OST_Windows),
                            new ElementCategoryFilter(BuiltInCategory.OST_Walls),
                            new ElementCategoryFilter(BuiltInCategory.OST_Floors),
                            new ElementCategoryFilter(BuiltInCategory.OST_Stairs),
                            new ElementCategoryFilter(BuiltInCategory.OST_Parts),
                    }));

                foreach (string paramName in parameterNames)
                {
                    List<string> missingElements = new List<string>();

                    foreach (Element elem in collector)
                    {
                        Parameter param = elem.LookupParameter(paramName);
                        if (param == null || (param.StorageType == StorageType.String && string.IsNullOrEmpty(param.AsString())) ||
                            (param.StorageType == StorageType.Integer && param.AsInteger() == 0) ||
                            (param.StorageType == StorageType.Double && param.AsDouble() == 0))
                        {
                            string elementInfo = $"{elem.Category?.Name} {elem.Name} (id: {elem.Id.Value.ToString()})";
                            missingElements.Add(elementInfo);
                        }

                        Debug.WriteLine("verify parameter : " + paramName + " in element : " + elem.Id.Value.ToString());
                    }

                    if (missingElements.Any())
                        invalidElements[paramName] = missingElements;
                }
            }
            catch (Exception ex)
            {
                isExecutionError = true;
                return new ValidationResult
                {
                    IsValid = false,
                    Message = $"Error en la validación: {ex.Message}",
                    IsRelevant = false
                };
            }

            if (!invalidElements.Any())
            {
                return new ValidationResult
                {
                    IsValid = true,
                    Message = "Todos los elementos tienen valores en los parámetros especificados.",
                    IsRelevant = true
                };
            }

            StringBuilder message = new StringBuilder();
            foreach (var entry in invalidElements)
            {
                message.AppendLine($"Los siguientes elementos no tienen valores en \"{entry.Key}\":");
                message.AppendLine(string.Join("\n", entry.Value));
                message.AppendLine("\n");
            }

            return new ValidationResult
            {
                IsValid = false,
                Message = message.ToString().Trim(),
                IsRelevant = true
            };
        }
    }
}

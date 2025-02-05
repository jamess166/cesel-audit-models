using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BimManagement.Commands.ModelAudit.Config
{
    public class CheckInPlaceFamily
    {
        internal ValidationResult ValidateModelInPlaceElements(Document doc)
        {
            var result = new ValidationResult
            {
                IsValid = true,
                Message = "",
                IsRelevant = true
            };

            var modelInPlaceElements = new FilteredElementCollector(doc)
                .WherePasses(new ElementClassFilter(typeof(FamilyInstance)))
                .Cast<FamilyInstance>()
                .Where(fi => fi.Symbol.Family.IsInPlace)
                .ToList();

            if (modelInPlaceElements.Any())
            {
                result.IsValid = false;
                result.Message = "Se han encontrado los siguientes elementos In Place: " +
                    string.Join(", ", modelInPlaceElements.Select(e => e.Id.ToString()));
            }

            return result;
        }
    }
}

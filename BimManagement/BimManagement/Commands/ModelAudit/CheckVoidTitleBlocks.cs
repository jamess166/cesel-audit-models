using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BimManagement.Commands.ModelAudit
{
    public class CheckVoidTitleBlocks
    {
        public ValidationResult ValidateTitleBlocksContainViews(Document doc)
        {
            var messageBuilder = new StringBuilder();
            var titleBlocksWithoutViews = new List<string>();

            // Obtener todas las hojas de una vez, excluyendo la "Carátula"
            var sheets = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(sheet => !sheet.Name.Equals("OPENING SHEET", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Obtener todos los Viewports de una sola vez
            var viewportsDict = new FilteredElementCollector(doc)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>()
                .GroupBy(vp => vp.SheetId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Verificar cada hoja
            foreach (var sheet in sheets)
            {
                if (!viewportsDict.ContainsKey(sheet.Id) || !viewportsDict[sheet.Id].Any())
                {
                    titleBlocksWithoutViews.Add(sheet.Name);
                }
            }

            // Construcción del mensaje
            if (titleBlocksWithoutViews.Any())
            {
                messageBuilder.AppendLine("Planos que no contienen ninguna vista:");
                foreach (var name in titleBlocksWithoutViews.OrderBy(n => n))
                {
                    messageBuilder.AppendLine($"{name}");
                }
            }

            return new ValidationResult
            {
                IsValid = !titleBlocksWithoutViews.Any(),
                IsRelevant = true,
                Message = messageBuilder.ToString().Trim()
            };
        }
    }
}

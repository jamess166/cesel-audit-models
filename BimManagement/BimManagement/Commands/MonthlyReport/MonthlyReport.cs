using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

using Application = Autodesk.Revit.ApplicationServices.Application;

namespace BimManagement
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class MonthlyReport : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Get application and document objects
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uiapp.ActiveUIDocument.Document;
            Selection sel = uidoc.Selection;
            
            string antiguoParametro = "PO-SEMANAL";
            string nuevoParametro = "PO-FECHA CONSTRUIDA";
            string nuevoValor = "6/2025";

            int cambios = 0;

            // Obtener todas las tablas de planificación
            FilteredElementCollector collector = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSchedule));

            using (Transaction tx = new Transaction(doc, "Reemplazar filtros de tablas"))
            {
                tx.Start();

                foreach (ViewSchedule schedule in collector)
                {
                    // Omitir tablas clave
                    if (schedule.Definition.IsKeySchedule) continue;

                    ScheduleDefinition definition = schedule.Definition;
                    IList<ScheduleFilter> filtros = definition.GetFilters();

                    bool modificado = false;

                    for (int i = 0; i < filtros.Count; i++)
                    {
                        ScheduleFilter filtro = filtros[i];

                        // Obtener el campo actual del filtro
                        ScheduleField field = definition.GetField(filtro.FieldId);
                        string nombreParametro = field?.GetName();

                        if (nombreParametro == antiguoParametro)
                        {
                            // Buscar el nuevo campo schedulable
                            SchedulableField schedulableField = definition.GetSchedulableFields()
                                .FirstOrDefault(f => f.GetName(doc) == nuevoParametro);

                            if (schedulableField == null)
                            {
                                TaskDialog.Show("Campo no encontrado", $"El parámetro '{nuevoParametro}' " +
                                                                       $"no está disponible en la tabla '{schedule.Name}'.");
                                continue;
                            }

                            // Verificar si el campo ya fue agregado a la tabla
                            ScheduleField nuevoField = null;
                            int fieldCount = definition.GetFieldCount();
                            for (int j = 0; j < fieldCount; j++)
                            {
                                ScheduleField fieldTemp = definition.GetField(j);
                                if (fieldTemp.GetName() == nuevoParametro)
                                {
                                    nuevoField = fieldTemp;
                                    break;
                                }
                            }

                            // Si no está, lo agregamos
                            if (nuevoField == null)
                            {
                                nuevoField = definition.AddField(schedulableField);
                            }

                            // Crear y reemplazar el filtro
                            ScheduleFilter nuevoFiltro = new ScheduleFilter(nuevoField.FieldId, ScheduleFilterType.Contains, nuevoValor);
                            definition.RemoveFilter(i);
                            definition.InsertFilter(nuevoFiltro, i);
                            modificado = true;
                        }
                    }

                    if (modificado)
                    {
                        cambios++;
                    }
                }

                tx.Commit();
            }

            TaskDialog.Show("Resultado", $"Se modificaron {cambios} tabla(s).");
            return Result.Succeeded;
        }
    }
}
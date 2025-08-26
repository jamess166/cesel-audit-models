using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BimManagement
{
    public class UpdateFilesHandler : IExternalEventHandler
    {
        public void Execute(UIApplication uiApp)
        {
            var app = uiApp.Application;
            var uiDoc = uiApp.ActiveUIDocument;

            int total = WeeklyReportTools.Files.Count;
            int current = 0;

            foreach (var fileInfo in WeeklyReportTools.Files)
            {
                try
                {
                    string name = fileInfo.Name;
                    string path = fileInfo.FullName;

                    var modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(path);
                    var openOpts = new OpenOptions { Audit = false };
                    var doc = app.OpenDocumentFile(modelPath, openOpts);

                    using (Transaction trans = new Transaction(doc, "Actualizar membrete y filtros"))
                    {
                        trans.Start();
                        ActualizarMembretes(doc, WeeklyReportTools.IssueName, WeeklyReportTools.IssueDate);
                        ActualizarFiltrosTablas(doc, WeeklyReportTools.IssueName);
                        ActualizarFiltrosVisuales(doc, WeeklyReportTools.IssueName);
                        trans.Commit();
                    }

                    doc.Close(true);
                    Log($"✔ Finalizado: {name}");
                }
                catch (Exception ex)
                {
                    Log($"✖ Error en archivo '{fileInfo.Name}': {ex.Message}");
                }

                current++;
                UpdateProgress(current, total);
            }

            Log($"✔ Se ha terminado la actualización");

            AmorParaSheyla.MostrarMensaje();
        }

        public string GetName() => "Actualizar archivos Revit";

        private void ActualizarMembretes(Document doc, string nombreHoja, string fecha)
        {
            var viewSheets = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>();

            foreach (var sheet in viewSheets)
            {
                var titleBlock = new FilteredElementCollector(doc, sheet.Id)
                    .OfCategory(BuiltInCategory.OST_TitleBlocks)
                    .WhereElementIsNotElementType()
                    .FirstOrDefault();

                if (titleBlock != null)
                {
                    var nameParam = titleBlock.LookupParameter("Sheet Name");

                    if (nameParam != null && nameParam.HasValue)
                    {
                        string currentName = nameParam.AsString() ?? "";

                        if (currentName.ToUpper().Contains("SEMANA"))
                        {
                            nameParam.Set("SEMANA Nº " + nombreHoja);
                        }
                        else
                        {
                            nameParam.Set(currentName + " - SEMANA Nº " + nombreHoja);
                        }
                    }

                    var dateParam = titleBlock.get_Parameter(BuiltInParameter.SHEET_ISSUE_DATE);
                    dateParam?.Set(fecha);
                }
            }
        }

        private void ActualizarFiltrosTablas(Document doc, string valorPoCars)
        {
            var schedules = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSchedule))
                .Cast<ViewSchedule>();

            foreach (var sched in schedules)
            {
                var def = sched.Definition;
                bool tienePoCars = false;
                ScheduleFieldId poFieldId = null;

                // Verificar si ya existe PO-CARS en la tabla
                for (int j = 0; j < def.GetFieldCount(); j++)
                {
                    var field = def.GetField(j);
                    if (field?.GetName() == "PO-CARS")
                    {
                        tienePoCars = true;
                        poFieldId = field.FieldId;
                        break;
                    }
                }

                // Si no existe PO-CARS, verificar si hay campos antiguos para remover
                List<int> indicesARemover = new List<int>();
                if (!tienePoCars)
                {
                    for (int j = 0; j < def.GetFieldCount(); j++)
                    {
                        var field = def.GetField(j);
                        string fieldName = field?.GetName();

                        if (fieldName == "PO-SEMANAL" || fieldName == "PO-SEMANA PROYECTO")
                        {
                            // Marcar este campo para remover después
                            indicesARemover.Add(j);
                        }
                    }
                }

                // Si no encontramos PO-CARS, agregar el campo PO-CARS
                if (!tienePoCars)
                {
                    try
                    {
                        // Buscar el parámetro PO-CARS en el proyecto
                        var parameterId = GetParameterIdByName(doc, "PO-CARS");
                        if (parameterId != null)
                        {
                            var newField = def.AddField(ScheduleFieldType.Instance, parameterId);
                            poFieldId = newField.FieldId;
                            tienePoCars = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log del error si no se puede agregar el campo
                        System.Diagnostics.Debug.WriteLine($"Error al agregar campo PO-CARS: {ex.Message}");
                        continue;
                    }
                }

                // Remover campos antiguos después de agregar PO-CARS
                if (tienePoCars && indicesARemover.Count > 0)
                {
                    // Remover en orden inverso para no afectar los índices
                    for (int i = indicesARemover.Count - 1; i >= 0; i--)
                    {
                        try
                        {
                            def.RemoveField(indicesARemover[i]);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error al remover campo antiguo: {ex.Message}");
                        }
                    }
                }

                // Actualizar o crear filtros para PO-CARS
                if (tienePoCars && poFieldId != null)
                {
                    bool filtroActualizado = false;

                    // Buscar y actualizar filtros existentes
                    for (int i = 0; i < def.GetFilterCount(); i++)
                    {
                        var filtro = def.GetFilter(i);
                        var field = def.GetField(filtro.FieldId);
                        string fieldName = field?.GetName();

                        if (fieldName == "PO-CARS" || fieldName == "PO-SEMANAL" || fieldName == "PO-SEMANA PROYECTO")
                        {
                            var nuevoFiltro = new ScheduleFilter(poFieldId, filtro.FilterType, valorPoCars);
                            def.SetFilter(i, nuevoFiltro);
                            filtroActualizado = true;
                        }
                    }

                    // Si no se encontró ningún filtro existente, crear uno nuevo
                    if (!filtroActualizado)
                    {
                        try
                        {
                            var nuevoFiltro = new ScheduleFilter(poFieldId, ScheduleFilterType.Equal, valorPoCars);
                            def.AddFilter(nuevoFiltro);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error al agregar filtro: {ex.Message}");
                        }
                    }
                }
            }
        }

        // Método auxiliar para obtener el ID del parámetro por nombre
        private ElementId GetParameterIdByName(Document doc, string parameterName)
        {
            // Buscar en parámetros del proyecto
            var projectParams = new FilteredElementCollector(doc)
                .OfClass(typeof(ParameterElement))
                .Cast<ParameterElement>();

            foreach (var param in projectParams)
            {
                if (param.Name == parameterName)
                {
                    return param.Id;
                }
            }

            // Si no se encuentra como parámetro del proyecto, buscar en parámetros compartidos
            var sharedParams = new FilteredElementCollector(doc)
                .OfClass(typeof(SharedParameterElement))
                .Cast<SharedParameterElement>();

            foreach (var param in sharedParams)
            {
                if (param.Name == parameterName)
                {
                    return param.Id;
                }
            }

            return null;
        }

        private void ActualizarFiltrosVisuales(Document doc, string valorPoCars)
        {
            // Primero intentar obtener el parámetro PO-CARS
            var poCardsId = GetParamId(doc, "PO-CARS");
            if (poCardsId == ElementId.InvalidElementId)
            {
                System.Diagnostics.Debug.WriteLine("No se encontró el parámetro PO-CARS");
                return;
            }

            // Lista de filtros que necesitan ser actualizados
            var filtersToUpdate = new List<(string FilterName, FilterRule rule)>
            {
                ("PO-EJECUTADO ACTUAL", ParameterFilterRuleFactory.CreateGreaterOrEqualRule(poCardsId, valorPoCars, true)),
                ("PO-EJECUTADO ACUMULADO PREVIO", ParameterFilterRuleFactory.CreateLessRule(poCardsId, valorPoCars, true))
            };

            // Obtener todos los filtros de parámetros
            var allFilters = new FilteredElementCollector(doc)
                .OfClass(typeof(ParameterFilterElement))
                .Cast<ParameterFilterElement>()
                .ToList();

            foreach (var (filterName, newRule) in filtersToUpdate)
            {
                var filter = allFilters.FirstOrDefault(f => f.Name == filterName);
                if (filter == null)
                {
                    System.Diagnostics.Debug.WriteLine($"No se encontró el filtro: {filterName}");
                    continue;
                }

                var currentFilter = filter.GetElementFilter();
                bool needsUpdate = false;

                if (currentFilter is ElementParameterFilter epf)
                {
                    var rules = epf.GetRules();
                    foreach (var rule in rules)
                    {
                        var fr = rule as FilterRule;
                        if (fr == null)
                            continue;

                        var paramId = fr.GetRuleParameter();

                        var poCarsId = GetParamId(doc, "PO-CARS");
                        var oldPoSemanalId = GetParamId(doc, "PO-SEMANAL");
                        var oldPoSemanaProyectoId = GetParamId(doc, "PO-SEMANA PROYECTO");

                        if (paramId == oldPoSemanalId || paramId == oldPoSemanaProyectoId)
                        {
                            needsUpdate = true;
                            break;
                        }

                        // Solo si quieres forzar actualización si ya era PO-CARS y el valor cambió
                        if (paramId == poCarsId /* && valor anterior != valorPoCars */)
                        {
                            needsUpdate = true;
                            break;
                        }
                    }
                }

                if (needsUpdate)
                {
                    try
                    {
                        filter.SetElementFilter(new ElementParameterFilter(newRule));
                        System.Diagnostics.Debug.WriteLine($"Filtro '{filterName}' actualizado con nuevo valor PO-CARS = {valorPoCars}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error al actualizar filtro '{filterName}': {ex.Message}");
                    }
                }
            }
        }

        // Método auxiliar para obtener el ID del parámetro
        private ElementId GetParamId(Document doc, string paramName)
        {
            // Buscar en parámetros del proyecto
            var projectParams = new FilteredElementCollector(doc)
                .OfClass(typeof(ParameterElement))
                .Cast<ParameterElement>();

            foreach (var param in projectParams)
            {
                if (param.Name == paramName)
                {
                    return param.Id;
                }
            }

            // Si no se encuentra como parámetro del proyecto, buscar en parámetros compartidos
            var sharedParams = new FilteredElementCollector(doc)
                .OfClass(typeof(SharedParameterElement))
                .Cast<SharedParameterElement>();

            foreach (var param in sharedParams)
            {
                if (param.Name == paramName)
                {
                    return param.Id;
                }
            }

            return ElementId.InvalidElementId;
        }

        //private ElementId GetParamId(Document doc, string paramName)
        //{
        //    var paramElements = new FilteredElementCollector(doc)
        //        .OfClass(typeof(ParameterElement))
        //        .Cast<ParameterElement>();

        //    foreach (var param in paramElements)
        //    {
        //        if (param.Name == paramName)
        //            return param.Id;
        //    }

        //    return ElementId.InvalidElementId;
        //}

        private void Log(string message)
        {
            var view = WeeklyReportView.Instance;
            if (view == null) return;

            view.Dispatcher.Invoke(() =>
            {
                view.LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
                view.LogTextBox.ScrollToEnd();
            });
        }

        private void UpdateProgress(int current, int total)
        {
            var view = WeeklyReportView.Instance;
            if (view == null) return;

            view.Dispatcher.Invoke(() =>
            {
                view.ProgressBar.Value = current;
                view.ProgressBar.Maximum = total;
            });
        }
    }
}
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
                        ActualizarMembretes(doc, WeeklyReportTools.IssueName, WeeklyReportTools.IssuePeriod);
                        ActualizarFiltrosTablas(doc, WeeklyReportTools.IssueName, WeeklyReportTools.IssueMonth);
                        ActualizarFiltrosVisuales(doc, WeeklyReportTools.IssueName, WeeklyReportTools.IssueMonth);
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

        private void ActualizarMembretes(Document doc, string nombreHoja, string period)
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

                    if (WeeklyReportTools.IssueMonth == string.Empty)
                    {

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
                    }
                    else
                    {
                        nameParam.Set(WeeklyReportTools.IssueMonth);
                    }

                    var dateParam = titleBlock.get_Parameter(BuiltInParameter.SHEET_ISSUE_DATE);
                    dateParam?.Set(period);
                }
            }
        }

        private void ActualizarFiltrosTablas(Document doc, string valorPoCars, string valorFechaConstruida)
        {
            var schedules = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSchedule))
                .Cast<ViewSchedule>();

            // Determinar si es semanal o mensual
            bool esSemanal = string.IsNullOrEmpty(WeeklyReportTools.IssueMonth);

            // Definir parámetro y valor según el tipo
            string parametroActual = esSemanal ? "PO-CARS" : "PO-FECHA CONSTRUIDA";
            string valorActual = esSemanal ? valorPoCars : valorFechaConstruida;

            // Parámetros que se deben buscar/reemplazar
            string parametroAlterno = esSemanal ? "PO-FECHA CONSTRUIDA" : "PO-CARS";

            foreach (var sched in schedules)
            {
                var def = sched.Definition;
                bool tieneParametroActual = false;
                ScheduleFieldId fieldIdActual = null;

                // Verificar si ya existe el parámetro actual en la tabla
                for (int j = 0; j < def.GetFieldCount(); j++)
                {
                    var field = def.GetField(j);
                    if (field?.GetName() == parametroActual)
                    {
                        tieneParametroActual = true;
                        fieldIdActual = field.FieldId;
                        break;
                    }
                }

                // Si no existe el parámetro actual, buscar campos a remover
                List<int> indicesARemover = new List<int>();
                if (!tieneParametroActual)
                {
                    for (int j = 0; j < def.GetFieldCount(); j++)
                    {
                        var field = def.GetField(j);
                        string fieldName = field?.GetName();

                        // Remover el parámetro alterno si existe
                        if (fieldName == parametroAlterno)
                        {
                            indicesARemover.Add(j);
                        }
                    }
                }

                // Si no encontramos el parámetro actual, agregarlo
                if (!tieneParametroActual)
                {
                    try
                    {
                        var parameterId = GetParameterIdByName(doc, parametroActual);
                        if (parameterId != null)
                        {
                            var newField = def.AddField(ScheduleFieldType.Instance, parameterId);
                            fieldIdActual = newField.FieldId;
                            tieneParametroActual = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error al agregar campo {parametroActual}: {ex.Message}");
                        continue;
                    }
                }

                // Remover campos antiguos después de agregar el parámetro actual
                if (tieneParametroActual && indicesARemover.Count > 0)
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

                // Actualizar o crear filtros para el parámetro actual
                if (tieneParametroActual && fieldIdActual != null)
                {
                    bool filtroActualizado = false;
                    List<int> indicesFiltrosARemover = new List<int>();

                    // Buscar filtros relacionados con parámetros de control
                    for (int i = 0; i < def.GetFilterCount(); i++)
                    {
                        var filtro = def.GetFilter(i);
                        var field = def.GetField(filtro.FieldId);
                        string fieldName = field?.GetName();

                        // Solo procesar filtros de parámetros de control
                        if (fieldName == parametroActual ||
                            fieldName == parametroAlterno ||
                            fieldName == "PO-SEMANAL" ||
                            fieldName == "PO-SEMANA PROYECTO")
                        {
                            if (fieldName == parametroActual)
                            {
                                // Actualizar el filtro del parámetro actual
                                ScheduleFilterType tipoFiltro = esSemanal
                                    ? filtro.FilterType  // Mantener el tipo existente para semanal
                                    : ScheduleFilterType.Contains;  // Usar Contains para mensual

                                var nuevoFiltro = new ScheduleFilter(fieldIdActual, tipoFiltro, valorActual);
                                def.SetFilter(i, nuevoFiltro);
                                filtroActualizado = true;
                            }
                            else
                            {
                                // Marcar para remover filtros de parámetros antiguos o alternos
                                indicesFiltrosARemover.Add(i);
                            }
                        }
                    }

                    // Remover filtros obsoletos (en orden inverso)
                    for (int i = indicesFiltrosARemover.Count - 1; i >= 0; i--)
                    {
                        try
                        {
                            def.RemoveFilter(indicesFiltrosARemover[i]);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error al remover filtro obsoleto: {ex.Message}");
                        }
                    }

                    // Si no se encontró ningún filtro del parámetro actual, crear uno nuevo
                    if (!filtroActualizado)
                    {
                        try
                        {
                            // Tipo de filtro por defecto según semanal/mensual
                            ScheduleFilterType tipoFiltro = esSemanal
                                ? ScheduleFilterType.Equal
                                : ScheduleFilterType.Contains;

                            var nuevoFiltro = new ScheduleFilter(fieldIdActual, tipoFiltro, valorActual);
                            def.AddFilter(nuevoFiltro);

                            System.Diagnostics.Debug.WriteLine($"Filtro agregado: {parametroActual} {tipoFiltro} {valorActual}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error al agregar filtro: {ex.Message}");
                        }
                    }
                }

                // ACTUALIZAR TEMPLATE SEGÚN SEMANAL O MENSUAL
                ActualizarTemplateTabla(doc, sched, esSemanal);
            }
        }

        //private void ActualizarFiltrosTablas(Document doc, string valorPoCars, string valorFechConstruida)
        //{
        //    var schedules = new FilteredElementCollector(doc)
        //        .OfClass(typeof(ViewSchedule))
        //        .Cast<ViewSchedule>();

        //    // Determinar si es semanal o mensual
        //    bool esSemanal = string.IsNullOrEmpty(WeeklyReportTools.IssueMonth);

        //    foreach (var sched in schedules)
        //    {
        //        var def = sched.Definition;
        //        bool tienePoCars = false;
        //        ScheduleFieldId poFieldId = null;

        //        // Verificar si ya existe PO-CARS en la tabla
        //        for (int j = 0; j < def.GetFieldCount(); j++)
        //        {
        //            var field = def.GetField(j);
        //            if (field?.GetName() == "PO-CARS")
        //            {
        //                tienePoCars = true;
        //                poFieldId = field.FieldId;
        //                break;
        //            }
        //        }

        //        // Si no existe PO-CARS, verificar si hay campos antiguos para remover
        //        List<int> indicesARemover = new List<int>();
        //        if (!tienePoCars)
        //        {
        //            for (int j = 0; j < def.GetFieldCount(); j++)
        //            {
        //                var field = def.GetField(j);
        //                string fieldName = field?.GetName();

        //                if (fieldName == "PO-SEMANAL" || fieldName == "PO-SEMANA PROYECTO")
        //                {
        //                    // Marcar este campo para remover después
        //                    indicesARemover.Add(j);
        //                }
        //            }
        //        }

        //        // Si no encontramos PO-CARS, agregar el campo PO-CARS
        //        if (!tienePoCars)
        //        {
        //            try
        //            {
        //                // Buscar el parámetro PO-CARS en el proyecto
        //                var parameterId = GetParameterIdByName(doc, "PO-CARS");
        //                if (parameterId != null)
        //                {
        //                    var newField = def.AddField(ScheduleFieldType.Instance, parameterId);
        //                    poFieldId = newField.FieldId;
        //                    tienePoCars = true;
        //                }
        //            }
        //            catch (Exception ex)
        //            {
        //                // Log del error si no se puede agregar el campo
        //                System.Diagnostics.Debug.WriteLine($"Error al agregar campo PO-CARS: {ex.Message}");
        //                continue;
        //            }
        //        }

        //        // Remover campos antiguos después de agregar PO-CARS
        //        if (tienePoCars && indicesARemover.Count > 0)
        //        {
        //            // Remover en orden inverso para no afectar los índices
        //            for (int i = indicesARemover.Count - 1; i >= 0; i--)
        //            {
        //                try
        //                {
        //                    def.RemoveField(indicesARemover[i]);
        //                }
        //                catch (Exception ex)
        //                {
        //                    System.Diagnostics.Debug.WriteLine($"Error al remover campo antiguo: {ex.Message}");
        //                }
        //            }
        //        }

        //        // Actualizar o crear filtros para PO-CARS
        //        if (tienePoCars && poFieldId != null)
        //        {
        //            bool filtroActualizado = false;

        //            // Buscar y actualizar filtros existentes
        //            for (int i = 0; i < def.GetFilterCount(); i++)
        //            {
        //                var filtro = def.GetFilter(i);
        //                var field = def.GetField(filtro.FieldId);
        //                string fieldName = field?.GetName();

        //                if (fieldName == "PO-CARS" || fieldName == "PO-SEMANAL" || fieldName == "PO-SEMANA PROYECTO")
        //                {
        //                    var nuevoFiltro = new ScheduleFilter(poFieldId, filtro.FilterType, valorPoCars);
        //                    def.SetFilter(i, nuevoFiltro);
        //                    filtroActualizado = true;
        //                }
        //            }

        //            // Si no se encontró ningún filtro existente, crear uno nuevo
        //            if (!filtroActualizado)
        //            {
        //                try
        //                {
        //                    var nuevoFiltro = new ScheduleFilter(poFieldId, ScheduleFilterType.Equal, valorPoCars);
        //                    def.AddFilter(nuevoFiltro);
        //                }
        //                catch (Exception ex)
        //                {
        //                    System.Diagnostics.Debug.WriteLine($"Error al agregar filtro: {ex.Message}");
        //                }
        //            }
        //        }

        //        // ACTUALIZAR TEMPLATE SEGÚN SEMANAL O MENSUAL
        //        ActualizarTemplateTabla(doc, sched, esSemanal);
        //    }
        //}

        private void ActualizarTemplateTabla(Document doc, ViewSchedule schedule, bool esSemanal)
        {
            try
            {
                // Verificar si la tabla tiene un template asignado
                ElementId templateId = schedule.ViewTemplateId;

                if (templateId == null || templateId == ElementId.InvalidElementId)
                {
                    System.Diagnostics.Debug.WriteLine($"La tabla '{schedule.Name}' no tiene template asignado");
                    return;
                }

                // Obtener el template actual
                ViewSchedule templateActual = doc.GetElement(templateId) as ViewSchedule;

                if (templateActual == null)
                {
                    System.Diagnostics.Debug.WriteLine($"No se pudo obtener el template para '{schedule.Name}'");
                    return;
                }

                string nombreTemplateActual = templateActual.Name;
                string nombreNuevoTemplate;

                // Reemplazar MENSUAL por SEMANAL o viceversa
                if (esSemanal)
                {
                    // Si es semanal, reemplazar MENSUAL por SEMANAL
                    nombreNuevoTemplate = nombreTemplateActual.Replace("MENSUAL", "SEMANAL");
                }
                else
                {
                    // Si es mensual, reemplazar SEMANAL por MENSUAL
                    nombreNuevoTemplate = nombreTemplateActual.Replace("SEMANAL", "MENSUAL");
                }

                // Si el nombre no cambió, significa que ya tiene el template correcto
                if (nombreNuevoTemplate == nombreTemplateActual)
                {
                    System.Diagnostics.Debug.WriteLine($"El template '{nombreTemplateActual}' ya es del tipo correcto");
                    return;
                }

                // Buscar el nuevo template en el documento
                var templates = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSchedule))
                    .Cast<ViewSchedule>()
                    .Where(v => v.IsTemplate);

                var nuevoTemplate = templates.FirstOrDefault(t =>
                    t.Name.Equals(nombreNuevoTemplate, StringComparison.OrdinalIgnoreCase));

                if (nuevoTemplate != null)
                {
                    // Aplicar el nuevo template
                    schedule.ViewTemplateId = nuevoTemplate.Id;
                    System.Diagnostics.Debug.WriteLine($"Template actualizado de '{nombreTemplateActual}' a '{nombreNuevoTemplate}' para la tabla '{schedule.Name}'");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Template no encontrado: '{nombreNuevoTemplate}'");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al actualizar template de '{schedule.Name}': {ex.Message}");
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

        private void ActualizarFiltrosVisuales(Document doc, string valorPoCars, string valorFechaConstruida)
        {
            // Determinar si es semanal o mensual
            bool esSemanal = string.IsNullOrEmpty(WeeklyReportTools.IssueMonth);

            // Obtener AMBOS parámetros
            var parametroSemanal = GetParamId(doc, "PO-CARS");
            var parametroMensual = GetParamId(doc, "PO-FECHA CONSTRUIDA");

            if (parametroSemanal == ElementId.InvalidElementId || parametroMensual == ElementId.InvalidElementId)
            {
                System.Diagnostics.Debug.WriteLine("No se encontró uno de los parámetros necesarios");
                return;
            }

            // Determinar cuál parámetro usar y cuál eliminar
            var parametroActivo = esSemanal ? parametroSemanal : parametroMensual;
            var parametroEliminar = esSemanal ? parametroMensual : parametroSemanal;
            string nombreParametroActivo = esSemanal ? "PO-CARS" : "PO-FECHA CONSTRUIDA";
            string valorActivo = esSemanal ? valorPoCars : valorFechaConstruida;

            // Obtener todos los filtros de parámetros
            var allFilters = new FilteredElementCollector(doc)
                .OfClass(typeof(ParameterFilterElement))
                .Cast<ParameterFilterElement>()
                .ToList();

            foreach (var filter in allFilters)
            {
                string filterName = filter.Name;
                FilterRule newRule = null;

                // Detectar el tipo de filtro basado en el nombre
                if (filterName.Contains("PO-EJECUTADO ACTUAL"))
                {
                    if (esSemanal)
                    {
                        newRule = ParameterFilterRuleFactory.CreateGreaterOrEqualRule(parametroActivo, valorActivo, true);
                    }
                    else
                    {
                        newRule = ParameterFilterRuleFactory.CreateContainsRule(parametroActivo, valorActivo, true);
                    }
                }
                else if (filterName.Contains("PO-EJECUTADO ACUMULADO PREVIO"))
                {
                    if (esSemanal)
                    {
                        newRule = ParameterFilterRuleFactory.CreateLessRule(parametroActivo, valorActivo, true);
                    }
                    else
                    {
                        newRule = ParameterFilterRuleFactory.CreateNotContainsRule(parametroActivo, valorActivo, true);
                    }
                }
                else
                {
                    continue;
                }

                // Actualizar el filtro
                try
                {
                    // Obtener el filtro actual
                    ElementFilter filtroActual = filter.GetElementFilter();

                    // Lista para almacenar TODAS las reglas
                    List<FilterRule> todasLasReglas = new List<FilterRule>();

                    // Extraer TODAS las reglas del filtro actual
                    ExtractAllRules(filtroActual, todasLasReglas);

                    // DEBUG: Ver qué reglas tenemos antes de eliminar
                    System.Diagnostics.Debug.WriteLine($"\n=== Filtro: {filterName} ===");
                    foreach (var r in todasLasReglas)
                    {
                        ElementId paramId = ObtenerParametroDeRegla(r);
                        string paramName = paramId != ElementId.InvalidElementId ? doc.GetElement(paramId)?.Name ?? "Unknown" : "Invalid";
                        System.Diagnostics.Debug.WriteLine($"  Regla tipo: {r.GetType().Name}, Parámetro: {paramName} (ID: {paramId})");
                    }

                    // Eliminar las reglas de AMBOS parámetros (PO-CARS y PO-FECHA CONSTRUIDA)
                    todasLasReglas.RemoveAll(r =>
                    {
                        ElementId paramId = ObtenerParametroDeRegla(r);
                        bool debeEliminar = paramId == parametroActivo || paramId == parametroEliminar;

                        if (debeEliminar)
                        {
                            string paramName = doc.GetElement(paramId)?.Name ?? "Unknown";
                            System.Diagnostics.Debug.WriteLine($"  >> Eliminando regla del parámetro: {paramName}");
                        }

                        return debeEliminar;
                    });

                    // DEBUG: Ver qué reglas quedaron
                    System.Diagnostics.Debug.WriteLine($"Reglas restantes: {todasLasReglas.Count}");

                    // Agregar la nueva regla
                    todasLasReglas.Add(newRule);

                    // RECONSTRUIR el filtro: crear un ElementParameterFilter por cada regla individual
                    List<ElementFilter> filtrosIndividuales = new List<ElementFilter>();

                    foreach (var regla in todasLasReglas)
                    {
                        filtrosIndividuales.Add(new ElementParameterFilter(regla));
                    }

                    // Crear el filtro final
                    ElementFilter nuevoFiltroFinal;
                    if (filtrosIndividuales.Count == 1)
                    {
                        nuevoFiltroFinal = filtrosIndividuales[0];
                    }
                    else
                    {
                        nuevoFiltroFinal = new LogicalAndFilter(filtrosIndividuales);
                    }

                    filter.SetElementFilter(nuevoFiltroFinal);

                    string operador = esSemanal
                        ? (filterName.Contains("ACTUAL") ? ">=" : "<")
                        : (filterName.Contains("ACTUAL") ? "contiene" : "no contiene");

                    System.Diagnostics.Debug.WriteLine($"Filtro '{filterName}' reconstruido: {nombreParametroActivo} {operador} {valorActivo} (Total reglas: {todasLasReglas.Count})");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error al actualizar filtro '{filterName}': {ex.Message}");
                }
            }
        }

        // Método auxiliar para extraer TODAS las reglas recursivamente
        private void ExtractAllRules(ElementFilter filter, List<FilterRule> reglas)
        {
            if (filter is ElementParameterFilter epf)
            {
                reglas.AddRange(epf.GetRules());
            }
            else if (filter is LogicalAndFilter laf)
            {
                foreach (var subFilter in laf.GetFilters())
                {
                    ExtractAllRules(subFilter, reglas);
                }
            }
            else if (filter is LogicalOrFilter lof)
            {
                foreach (var subFilter in lof.GetFilters())
                {
                    ExtractAllRules(subFilter, reglas);
                }
            }
        }

        // Método auxiliar mejorado para extraer el parámetro de una regla (incluyendo FilterInverseRule)
        private ElementId ObtenerParametroDeRegla(FilterRule regla)
        {
            // Manejar FilterInverseRule
            if (regla is FilterInverseRule fir)
            {
                // Obtener la regla interna
                var innerRule = fir.GetInnerRule();
                return ObtenerParametroDeRegla(innerRule); // Llamada recursiva
            }

            // Reglas normales
            if (regla is FilterStringRule fsr)
                return fsr.GetRuleParameter();
            if (regla is FilterDoubleRule fdr)
                return fdr.GetRuleParameter();
            if (regla is FilterIntegerRule intRule)
                return intRule.GetRuleParameter();

            return ElementId.InvalidElementId;
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
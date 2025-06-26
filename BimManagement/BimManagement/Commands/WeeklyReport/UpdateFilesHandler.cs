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

        private void ActualizarFiltrosTablas(Document doc, string semana)
        {
            var schedules = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSchedule))
                .Cast<ViewSchedule>();

            foreach (var sched in schedules)
            {
                var def = sched.Definition;
                for (int i = 0; i < def.GetFilterCount(); i++)
                {
                    var filtro = def.GetFilter(i);
                    var field = def.GetField(filtro.FieldId);

                    if (field?.GetName() == "PO-SEMANAL")
                    {
                        var nuevoFiltro = new ScheduleFilter(filtro.FieldId, filtro.FilterType, semana);
                        def.SetFilter(i, nuevoFiltro);
                    }
                }
            }
        }

        private void ActualizarFiltrosVisuales(Document doc, string semana)
        {
            var paramId = GetParamId(doc, "PO-SEMANAL");
            if (paramId == ElementId.InvalidElementId) return;

            var filtersToUpdate = new List<(string FilterName, FilterRule rule)>
            {
                ("PO-EJECUTADO ACTUAL", ParameterFilterRuleFactory.CreateGreaterOrEqualRule(paramId, semana, true)),
                ("PO-EJECUTADO ACUMULADO PREVIO", ParameterFilterRuleFactory.CreateLessRule(paramId, semana, true))
            };

            foreach (var (filterName, rule) in filtersToUpdate)
            {
                var filter = new FilteredElementCollector(doc)
                    .OfClass(typeof(ParameterFilterElement))
                    .Cast<ParameterFilterElement>()
                    .FirstOrDefault(f => f.Name == filterName);

                if (filter != null)
                    filter.SetElementFilter(new ElementParameterFilter(rule));
            }
        }

        private ElementId GetParamId(Document doc, string paramName)
        {
            var paramElements = new FilteredElementCollector(doc)
                .OfClass(typeof(ParameterElement))
                .Cast<ParameterElement>();

            foreach (var param in paramElements)
            {
                if (param.Name == paramName)
                    return param.Id;
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
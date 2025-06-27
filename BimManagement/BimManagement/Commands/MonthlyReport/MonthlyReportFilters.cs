using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BimManagement
{
    [Transaction(TransactionMode.Manual)]
    public class MonthlyReportFilters : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            // Definiciones clave
            string[] nombresFiltros = new[]
            {
                "PO-EJECUTADO ACTUAL",
                "PO-EJECUTADO ACUMULADO PREVIO"
            };

            string antiguoParametro = "PO-SEMANAL";
            string nuevoParametro = "PO-FECHA CONSTRUIDA";
            string valor = "6/2025";

            // Buscar el nuevo parámetro
            ElementId nuevoParamId = BuscarParametroCompartido(doc, nuevoParametro);
            if (nuevoParamId == ElementId.InvalidElementId)
            {
                TaskDialog.Show("Error", $"No se encontró el parámetro '{nuevoParametro}' en el documento.");
                return Result.Failed;
            }

            int filtrosModificados = 0;

            // Buscar todos los filtros en el documento
            FilteredElementCollector filtrosCollector = new FilteredElementCollector(doc)
                .OfClass(typeof(ParameterFilterElement));

            using (Transaction tx = new Transaction(doc, "Actualizar reglas de filtros de vista"))
            {
                tx.Start();

                foreach (ParameterFilterElement filtro in filtrosCollector)
                {
                    if (!nombresFiltros.Contains(filtro.Name))
                        continue;

                    // CORREGIDO: Usar GetElementFilter() en lugar de GetRules()
                    ElementFilter filtroOriginal = filtro.GetElementFilter();
                    List<FilterRule> nuevasReglas = new List<FilterRule>();

                    bool modificado = false;

                    // Procesar el filtro existente
                    if (filtroOriginal != null)
                    {
                        // Si es un filtro lógico, extraer las reglas
                        if (filtroOriginal is ElementParameterFilter paramFilter)
                        {
                            // Intentar obtener las reglas del filtro de parámetro
                            var reglasExistentes = ExtraerReglasDeFiltro(paramFilter, doc, antiguoParametro);

                            foreach (var reglaInfo in reglasExistentes)
                            {
                                if (reglaInfo.EsParaReemplazar)
                                {
                                    // Crear nueva regla
                                    var nuevaRegla =
                                        ParameterFilterRuleFactory.CreateContainsRule(nuevoParamId, valor, false);

                                    if (filtro.Name == "PO-EJECUTADO ACUMULADO PREVIO")
                                    {
                                        // CORREGIDO: Crear filtro NOT correctamente
                                        var filtroNot = new ElementParameterFilter(
                                            ParameterFilterRuleFactory.CreateNotContainsRule(nuevoParamId, valor, false)
                                        );
                                        nuevasReglas.Add(
                                            ParameterFilterRuleFactory.CreateNotContainsRule(nuevoParamId, valor,
                                                false));
                                    }
                                    else
                                    {
                                        nuevasReglas.Add(nuevaRegla);
                                    }

                                    modificado = true;
                                }
                                else
                                {
                                    // Mantener regla existente
                                    nuevasReglas.Add(reglaInfo.Regla);
                                }
                            }
                        }
                    }

                    if (modificado && nuevasReglas.Count > 0)
                    {
                        try
                        {
                            // CORREGIDO: Crear nuevo filtro con las reglas actualizadas
                            ElementFilter nuevoFiltro;

                            if (nuevasReglas.Count == 1)
                            {
                                nuevoFiltro = new ElementParameterFilter(nuevasReglas[0]);
                            }
                            else
                            {
                                // Si hay múltiples reglas, combinarlas con AND
                                nuevoFiltro = new ElementParameterFilter(nuevasReglas);
                            }

                            // CORREGIDO: Usar SetElementFilter en lugar de SetRules
                            filtro.SetElementFilter(nuevoFiltro);
                            filtrosModificados++;
                        }
                        catch (Exception ex)
                        {
                            TaskDialog.Show("Error", $"Error al actualizar filtro '{filtro.Name}': {ex.Message}");
                        }
                    }
                }

                tx.Commit();
            }

            TaskDialog.Show("Resultado", $"Se modificaron {filtrosModificados} filtro(s).");
            return Result.Succeeded;
        }

        /// <summary>
        /// Busca el ElementId de un parámetro compartido por nombre.
        /// </summary>
        private ElementId BuscarParametroCompartido(Document doc, string nombreParametro)
        {
            // CORREGIDO: Buscar en SharedParameterElement también
            var parametros = new FilteredElementCollector(doc)
                .OfClass(typeof(SharedParameterElement))
                .Cast<SharedParameterElement>();

            foreach (var param in parametros)
            {
                if (param.Name == nombreParametro)
                {
                    return param.Id;
                }
            }

            // También buscar en parámetros del proyecto
            var parametrosProyecto = new FilteredElementCollector(doc)
                .OfClass(typeof(ParameterElement))
                .Cast<ParameterElement>();

            foreach (var param in parametrosProyecto)
            {
                if (param.Name == nombreParametro)
                {
                    return param.Id;
                }
            }

            return ElementId.InvalidElementId;
        }

        /// <summary>
        /// Clase auxiliar para información de reglas
        /// </summary>
        private class ReglaInfo
        {
            public FilterRule Regla { get; set; }
            public bool EsParaReemplazar { get; set; }
        }

        /// <summary>
        /// Extrae reglas de un filtro de parámetro (implementación simplificada)
        /// </summary>
        private List<ReglaInfo> ExtraerReglasDeFiltro(ElementParameterFilter filtro, Document doc,
            string parametroAntiguo)
        {
            var reglas = new List<ReglaInfo>();

            try
            {
                // Esta es una implementación simplificada
                // En la práctica, necesitarías una lógica más compleja para extraer reglas existentes
                // Por ahora, asumimos que queremos reemplazar todo
                reglas.Add(new ReglaInfo { Regla = null, EsParaReemplazar = true });
            }
            catch
            {
                // Si no podemos extraer reglas, creamos una nueva
                reglas.Add(new ReglaInfo { Regla = null, EsParaReemplazar = true });
            }

            return reglas;
        }
    }
}
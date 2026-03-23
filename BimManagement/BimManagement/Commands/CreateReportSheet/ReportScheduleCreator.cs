using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BimManagement
{
    /// <summary>
    /// Crea una tabla multucategoría en Revit con los campos, agrupación
    /// y filtros requeridos para el reporte semanal o mensual.
    /// </summary>
    public class ReportScheduleCreator
    {
        private readonly Document _doc;
        private readonly bool     _isMonthly;
        private readonly string   _weekValue;   // ej. "45"
        private readonly string   _monthValue;  // ej. "03/2025"

        // Orden de columnas en la tabla
        private static readonly string[] FieldOrder = new[]
        {
            "PO-WBS",
            "PO-ELEMENTO",
            "PR-NIVEL",
            "PO-ESTADO CONSTRUCCION",
            "PO-FECHA CONSTRUIDA",   // se ocultará
            "PO-CARS",
            "CSL-Unidad",
            "CSL-Cantidad",
        };

        // Campos por los que se agrupa
        private static readonly string[] GroupByFields = new[]
        {
            "PO-WBS",
            "PO-ELEMENTO",
            "PR-NIVEL",
        };

        public ReportScheduleCreator(Document doc, bool isMonthly,
                                     string weekValue, string monthValue)
        {
            _doc        = doc;
            _isMonthly  = isMonthly;
            _weekValue  = weekValue?.Trim();
            _monthValue = monthValue?.Trim();
        }

        // ── Punto de entrada ─────────────────────────────────────────────────

        public ViewSchedule Create()
        {
            // 1. Nombre de la tabla
            string scheduleName = _isMonthly
                ? $"CSL-TBL-AVANCE MENSUAL {_monthValue}"
                : $"CSL-TBL-AVANCE SEMANA {_weekValue}";

            // Si ya existe una tabla con ese nombre, eliminarla para recrearla
            DeleteIfExists(scheduleName);

            // 2. Crear tabla multi-categoría (ElementId.InvalidElementId = multi-cat)
            ViewSchedule schedule = ViewSchedule.CreateSchedule(
                _doc, ElementId.InvalidElementId);
            schedule.Name = scheduleName;

            ScheduleDefinition def = schedule.Definition;            

            // 3. Construir diccionario nombre → SchedulableField
            Dictionary<string, SchedulableField> availableFields =
                BuildFieldDictionary(def.GetSchedulableFields());

            // 4. Agregar campos en el orden definido
            var addedFields = new Dictionary<string, ScheduleField>(
                StringComparer.OrdinalIgnoreCase);

            foreach (string fieldName in FieldOrder)
            {
                if (!availableFields.TryGetValue(fieldName, out SchedulableField sf))
                    continue;

                ScheduleField schedField = def.AddField(sf);
                addedFields[fieldName] = schedField;
            }

            // 5. Encabezados personalizados de columna
            var columnHeadings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "PO-WBS",                 "Partida"  },
                { "PO-ELEMENTO",            "Elemento" },
                { "PR-NIVEL",               "Nivel"    },
                { "PO-ESTADO CONSTRUCCION", "Estado"   },
                { "PO-CARS",                "CARS"     },
                { "CSL-Unidad",             "Und."     },
                { "CSL-Cantidad",           "Cant."    },
            };
            foreach (var kv in columnHeadings)
                if (addedFields.TryGetValue(kv.Key, out ScheduleField hf))
                    hf.ColumnHeading = kv.Value;

            // 5b. Ajustar anchos de columna
            if (addedFields.TryGetValue("PO-ELEMENTO", out ScheduleField elemWidthField))
            {
                double current = elemWidthField.SheetColumnWidth;
                elemWidthField.SheetColumnWidth = current > 0 ? current * 2.0 : 50.0 / 304.8;
            }
            if (addedFields.TryGetValue("PO-CARS", out ScheduleField carsWidthField))
                carsWidthField.SheetColumnWidth *= 0.36; // 30% base + 20% más = 0.3 × 1.2
            if (addedFields.TryGetValue("CSL-Unidad", out ScheduleField unidadWidthField))
                unidadWidthField.SheetColumnWidth *= 0.3;
            if (addedFields.TryGetValue("CSL-Cantidad", out ScheduleField cantidadWidthField))
                cantidadWidthField.SheetColumnWidth *= 0.4;

            // 6. Ocultar PO-FECHA CONSTRUIDA
            if (addedFields.TryGetValue("PO-FECHA CONSTRUIDA", out ScheduleField hiddenField))
                hiddenField.IsHidden = true;

            // 6b. Itemize Every Instance = OFF
            def.IsItemized = false;

            // 6b. Gran total
            def.ShowGrandTotal = false;

            // 6c. CSL-Cantidad: activar cálculo de totales
            if (addedFields.TryGetValue("CSL-Cantidad", out ScheduleField cantidadField))
                cantidadField.DisplayType = ScheduleFieldDisplayType.Totals;

            // 5d. Grand Total activado
            def.ShowGrandTotal      = false;

            // 6. Agrupación con configuración individual por campo
            // PO-WBS: header OFF, footer ON con Total Only
            if (addedFields.TryGetValue("PO-WBS", out ScheduleField wbsGf))
                def.AddSortGroupField(new ScheduleSortGroupField(wbsGf.FieldId)
                {
                    ShowHeader    = false,
                    ShowFooter    = true,   // Total Only (tipo de footer no expuesto en API)
                    ShowBlankLine = false,
                });

            // PO-ELEMENTO: header OFF, footer OFF
            if (addedFields.TryGetValue("PO-ELEMENTO", out ScheduleField elemGf))
                def.AddSortGroupField(new ScheduleSortGroupField(elemGf.FieldId)
                {
                    ShowHeader    = false,
                    ShowFooter    = false,
                    ShowBlankLine = false,
                });

            // PR-NIVEL: header OFF, footer OFF
            if (addedFields.TryGetValue("PR-NIVEL", out ScheduleField nivelGf))
                def.AddSortGroupField(new ScheduleSortGroupField(nivelGf.FieldId)
                {
                    ShowHeader    = false,
                    ShowFooter    = false,
                    ShowBlankLine = false,
                });

            // 7. Filtro
            if (_isMonthly)
            {
                // PO-FECHA CONSTRUIDA contiene el "MM/yyyy" del mes indicado
                if (addedFields.TryGetValue("PO-FECHA CONSTRUIDA", out ScheduleField fechaField))
                {
                    def.AddFilter(new ScheduleFilter(
                        fechaField.FieldId,
                        ScheduleFilterType.Contains,
                        _monthValue));
                }
            }
            else
            {
                // PO-CARS igual al número de semana
                if (addedFields.TryGetValue("PO-CARS", out ScheduleField carsField))
                {
                    def.AddFilter(new ScheduleFilter(
                        carsField.FieldId,
                        ScheduleFilterType.Equal,
                        _weekValue));
                }
            }
            
            //Filtro si no tiene datos en unidad
            if(addedFields.TryGetValue("CSL-Unidad", out ScheduleField cslUnidad))
            {
                def.AddFilter(new ScheduleFilter(
                    cslUnidad.FieldId,
                    ScheduleFilterType.HasValue));
            }

            // 8. Aplicar tipo de texto 1.8mm a título, encabezados y cuerpo
            ApplyTextStyle(schedule, 1.8);

            return schedule;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Construye un diccionario nombre (insensible a mayúsculas) → SchedulableField.
        /// </summary>
        private Dictionary<string, SchedulableField> BuildFieldDictionary(
            IList<SchedulableField> schedulableFields)
        {
            var dict = new Dictionary<string, SchedulableField>(
                StringComparer.OrdinalIgnoreCase);

            foreach (SchedulableField sf in schedulableFields)
            {
                string name = GetFieldName(sf);
                if (!string.IsNullOrEmpty(name) && !dict.ContainsKey(name))
                    dict[name] = sf;
            }

            return dict;
        }

        /// <summary>
        /// Obtiene el nombre legible de un SchedulableField.
        /// </summary>
        private string GetFieldName(SchedulableField sf)
        {
            ElementId paramId = sf.ParameterId;
            if (paramId == ElementId.InvalidElementId) return string.Empty;

            int idValue = paramId.IntegerValue;

            if (idValue < 0)
            {
                // Parámetro built-in
                try
                {
                    return LabelUtils.GetLabelFor((BuiltInParameter)idValue);
                }
                catch { return string.Empty; }
            }
            else
            {
                // Parámetro de proyecto o compartido
                Element paramElem = _doc.GetElement(paramId);
                if (paramElem is ParameterElement pe)
                    return pe.GetDefinition().Name;

                return string.Empty;
            }
        }

        /// <summary>
        /// Busca o crea un TextNoteType con el tamaño indicado en mm y fuente Arial,
        /// luego lo aplica a los textos de título, encabezado y cuerpo de la tabla.
        /// </summary>
        private void ApplyTextStyle(ViewSchedule schedule, double textSizeMm)
        {
            const string typeName = "CSL-TXT-Arial-1.8mm";

            // Buscar o crear el TextNoteType con el tamaño indicado
            TextNoteType textType = new FilteredElementCollector(_doc)
                .OfClass(typeof(TextNoteType))
                .Cast<TextNoteType>()
                .FirstOrDefault(t => string.Equals(t.Name, typeName,
                                                    StringComparison.OrdinalIgnoreCase));

            if (textType == null)
            {
                TextNoteType baseType = new FilteredElementCollector(_doc)
                    .OfClass(typeof(TextNoteType))
                    .Cast<TextNoteType>()
                    .FirstOrDefault();
                if (baseType == null) return;

                textType = baseType.Duplicate(typeName) as TextNoteType;
                if (textType == null) return;

                Parameter sizeParam = textType.get_Parameter(BuiltInParameter.TEXT_SIZE);
                if (sizeParam != null && !sizeParam.IsReadOnly)
                    sizeParam.Set(textSizeMm / 304.8);

                Parameter fontParam = textType.get_Parameter(BuiltInParameter.TEXT_FONT);
                if (fontParam != null && !fontParam.IsReadOnly)
                    fontParam.Set("Arial");
            }

            ElementId typeId = textType.Id;

            // Asignar tipo de texto a título, encabezados y cuerpo
            schedule.TitleTextTypeId  = typeId;
            schedule.HeaderTextTypeId = typeId;
            schedule.BodyTextTypeId   = typeId;
        }

        /// <summary>
        /// Elimina una tabla existente con el mismo nombre para permitir recrearla.
        /// </summary>
        private void DeleteIfExists(string name)
        {
            ViewSchedule existing = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSchedule))
                .Cast<ViewSchedule>()
                .FirstOrDefault(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
                _doc.Delete(existing.Id);
        }
    }
}

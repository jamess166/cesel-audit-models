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
    public class MonthlyUpdateSheet : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            // Valores a establecer
            string sheetNameValue = "MENSUAL JUNIO";
            string sheetIssueDateValue = "01/06/25 - 30/06/25";

            int hojasModificadas = 0;

            try
            {
                // Obtener todas las hojas del proyecto
                FilteredElementCollector collector = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Sheets)
                    .WhereElementIsNotElementType();

                ICollection<Element> sheets = collector.ToElements();

                if (sheets.Count == 0)
                {
                    TaskDialog.Show("Información", "No se encontraron hojas en el proyecto.");
                    return Result.Succeeded;
                }

                using (Transaction tx = new Transaction(doc, "Actualizar parámetros de hojas"))
                {
                    tx.Start();

                    foreach (Element sheet in sheets)
                    {
                        ViewSheet viewSheet = sheet as ViewSheet;
                        if (viewSheet == null) continue;

                        bool modificado = false;

                        try
                        {
                            // Actualizar SHEET_NAME (nombre de la hoja)
                            Parameter sheetNameParam = sheet.get_Parameter(BuiltInParameter.SHEET_NAME);
                            if (sheetNameParam != null && !sheetNameParam.IsReadOnly)
                            {
                                string valorActual = sheetNameParam.AsString();
                                if (valorActual != sheetNameValue)
                                {
                                    sheetNameParam.Set(sheetNameValue);
                                    modificado = true;
                                }
                            }

                            // Actualizar SHEET_ISSUE_DATE (fecha de emisión)
                            Parameter sheetIssueDateParam = sheet.get_Parameter(BuiltInParameter.SHEET_ISSUE_DATE);
                            if (sheetIssueDateParam != null && !sheetIssueDateParam.IsReadOnly)
                            {
                                string valorActual = sheetIssueDateParam.AsString();
                                if (valorActual != sheetIssueDateValue)
                                {
                                    sheetIssueDateParam.Set(sheetIssueDateValue);
                                    modificado = true;
                                }
                            }

                            if (modificado)
                            {
                                hojasModificadas++;
                            }
                        }
                        catch (Exception ex)
                        {
                            // Continuar con la siguiente hoja si hay error
                            TaskDialog.Show("Advertencia",
                                $"Error al actualizar la hoja '{viewSheet.Name}' (Número: {viewSheet.SheetNumber}): {ex.Message}");
                        }
                    }

                    // Reemplazar texto en leyendas
                    int leyendasModificadas = ReemplazarTextoEnLeyendas(doc, "AVANCE SEMANAL", "AVANCE MENSUAL");

                    tx.Commit();
                }

                // Mostrar resultado
                TaskDialog.Show("Resultado",
                    $"Proceso completado.\n" +
                    $"Hojas procesadas: {sheets.Count}\n" +
                    $"Hojas modificadas: {hojasModificadas}\n\n" +
                    $"Valores establecidos:\n" +
                    $"• Nombre: {sheetNameValue}\n" +
                    $"• Fecha de emisión: {sheetIssueDateValue}");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = $"Error general: {ex.Message}";
                TaskDialog.Show("Error", message);
                return Result.Failed;
            }
        }

        /// <summary>
        /// Método para reemplazar texto solo en TextNotes de las leyendas
        /// </summary>
        private int ReemplazarTextoEnLeyendas(Document doc, string textoAntiguo, string textoNuevo)
        {
            int elementosModificados = 0;

            try
            {
                // Obtener todas las vistas de leyenda (Legend views)
                FilteredElementCollector viewCollector = new FilteredElementCollector(doc)
                    .OfClass(typeof(View));

                List<View> legendViews = new List<View>();
            
                foreach (View view in viewCollector)
                {
                    if (view.ViewType == ViewType.Legend)
                    {
                        legendViews.Add(view);
                    }
                }

                foreach (View legendView in legendViews)
                {
                    // Buscar solo TextNotes en cada leyenda
                    FilteredElementCollector textCollector = new FilteredElementCollector(doc, legendView.Id)
                        .OfCategory(BuiltInCategory.OST_TextNotes);

                    foreach (Element textElement in textCollector)
                    {
                        TextNote textNote = textElement as TextNote;
                        if (textNote != null)
                        {
                            string textoActual = textNote.Text;
                            if (textoActual.Contains(textoAntiguo))
                            {
                                string textoActualizado = textoActual.Replace(textoAntiguo, textoNuevo);
                                textNote.Text = textoActualizado;
                                elementosModificados++;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error en leyendas", $"Error al procesar leyendas: {ex.Message}");
            }

            return elementosModificados;
        }
    }
}
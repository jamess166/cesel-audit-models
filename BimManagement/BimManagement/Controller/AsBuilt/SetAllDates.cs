using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.IO.Packaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using OfficeOpenXml;
using System.Data.Common;
using System.Globalization;
using BimManagement.Utils;


namespace BimManagement.Controller
{
    public class ExcelElement
    {
        public string SheetNumber { get; set; }
        public string Solado { get; set; }

        public string AceroLosaFondo { get; set; }
        public string EncofradoLosaFondo { get; set; }
        public string ConcretoLosaFondo { get; set; }

        public string AceroMuro { get; set; }
        public string EncofradoMuro { get; set; }
        public string ConcretoMuro { get; set; }

        public string AceroLosaSuperior { get; set; }
        public string EncofradoLosaSuperior { get; set; }
        public string ConcretoLosaSuperior { get; set; }

        public string Junta { get; set; }
    }

    public class SetAllDates
    {
        public static void SetDatesofExcel()
        {
            string excelPath = GetDocumentExcel();
            if (string.IsNullOrEmpty(excelPath)) { return; }

            List<ExcelElement> allDataExcel = ReadExcel(excelPath);

            string sheetFinder = RevitTools.doc.Title;
            string sheetFinderFinal = sheetFinder.Substring(0, sheetFinder.Length - 4);
            List<ExcelElement> elementsFiltered = allDataExcel
                .Where(item => item.SheetNumber == sheetFinderFinal).ToList();

            IList<Element> elements = RevitTools.GetElements();

            using (Transaction transaction = new Transaction(RevitTools.doc, "Llenar Parametros Fechas"))
            {
                transaction.Start();
                SetDatesParameter(elements, elementsFiltered);
                transaction.Commit();
            }
        }

        public static string GetDocumentExcel()
        {
            string filePath = string.Empty;

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Archivos Excel (*.xlsx)|*.xlsx|Todos los archivos (*.*)|*.*";
                openFileDialog.FilterIndex = 1;
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    filePath = openFileDialog.FileName;
                }
            }

            if (filePath == string.Empty)
            {
                return string.Empty;
            }

            return filePath;
        }

        public static List<ExcelElement> ReadExcel(string excelPathi)
        {
            List<ExcelElement> excelParameters = new List<ExcelElement>();

            FileInfo excelFile = new FileInfo(excelPathi);

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using (ExcelPackage package = new ExcelPackage(excelFile))
            {
                //ExcelWorksheet worksheet = package.Workbook.Worksheets[ViewSetDatesTools.PositionSheetExcel];

                ExcelWorksheet worksheet = package.Workbook.Worksheets.FirstOrDefault(sheet => sheet.Name == ViewSetDatesTools.NameSheetExcel);
                if (worksheet == null) { return null; }

                int rowCount = worksheet.Dimension.Rows;

                for (int row = 3; row <= rowCount; row++)
                {
                    //string juntaJlC3 = ReturnValidateDate(worksheet,row, 21);
                    //string juntaJlC2 = ReturnValidateDate(worksheet, row, 22);
                    //string juntaJlC1B = ReturnValidateDate(worksheet, row, 23);
                    //string juntaJDM1 = ReturnValidateDate(worksheet, row, 24);
                    //string juntaJDM4 = ReturnValidateDate(worksheet, row, 25); 
                    //string juntaJLD = ReturnValidateDate(worksheet, row, 26);

                    string junta = string.Empty;

                    for (int i = 21; i <= 26; i++)
                    {
                        string valor = ReturnValidateDate(worksheet, row, i);
                        if (!string.IsNullOrEmpty(valor))
                        {
                            junta = valor;
                            break; // Detenemos el bucle tan pronto como encontramos un valor no vacío
                        }
                    }

                    ExcelElement parameter = new ExcelElement
                    {
                        SheetNumber = worksheet.Cells[row, 28].Text.Trim(),
                        Solado = ReturnValidateDate(worksheet, row, 4),

                        AceroLosaFondo = ReturnValidateDate(worksheet, row, 5),
                        EncofradoLosaFondo = ReturnValidateDate(worksheet, row, 6),
                        ConcretoLosaFondo = ReturnValidateDate(worksheet, row, 7),

                        AceroMuro = ReturnValidateDate(worksheet, row, 8),
                        EncofradoMuro = ReturnValidateDate(worksheet, row, 9),
                        ConcretoMuro = ReturnValidateDate(worksheet, row, 10),

                        AceroLosaSuperior = ReturnValidateDate(worksheet, row, 14),
                        EncofradoLosaSuperior = ReturnValidateDate(worksheet, row, 15),
                        ConcretoLosaSuperior = ReturnValidateDate(worksheet, row, 16),

                        Junta = junta,
                    };

                    excelParameters.Add(parameter);
                }
            }

            return excelParameters;
        }

        public static string ReturnValidateDate(ExcelWorksheet worksheet, int row, int column)
        {
            string date = worksheet.Cells[row, column].Text.Trim();
            // Intentar convertir el string a un objeto DateTime
            if (DateTime.TryParse(date, out DateTime fecha))
            {
                // Comparar el año con 2022
                if (fecha.Year > 2022)
                {
                    // La fecha tiene un año posterior a 2022
                    return date;
                }
            }
            // La fecha no es válida o tiene un año igual o anterior a 2022
            return string.Empty;
        }

        public static void SetDatesParameter(IList<Element> elements, List<ExcelElement> elementsFiltered)
        {
            foreach (Element element in elements)
            {
                if(element.Category == null) { continue; }
                BuiltInCategory category = (BuiltInCategory)element.Category.Id.IntegerValue;

                string familyAndType = element.get_Parameter(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM)?.AsValueString()?.ToUpper();
                string material = element.get_Parameter(BuiltInParameter.DPART_MATERIAL_ID_PARAM)?.AsValueString()?.ToUpper();

                if (category == BuiltInCategory.OST_StructuralColumns || category == BuiltInCategory.OST_StructuralFraming ||
                    category == BuiltInCategory.OST_Parts ||
                    category == BuiltInCategory.OST_StructuralFoundation)
                {
                    if ((category == BuiltInCategory.OST_StructuralColumns && familyAndType.Contains("MURO")) ||
                        (category == BuiltInCategory.OST_Parts && material.Contains("MURO")))
                    {
                        //filtrar el menor y el mayor valor de muros
                        SetDateMuro(elementsFiltered, element);
                    }
                    else if ((category == BuiltInCategory.OST_StructuralColumns && familyAndType.Contains("LOSA")) ||
                             (category == BuiltInCategory.OST_Parts && material.Contains("LOSA")))
                    {
                        if ((category == BuiltInCategory.OST_StructuralColumns && familyAndType.Contains("FONDO")) ||
                            (category == BuiltInCategory.OST_Parts && material.Contains("FONDO")))
                        {
                            //thickness = ViewAsBuiltTools._02_03_DSI_Espesor_losaFondo;
                            SetDateLosaFondo(elementsFiltered, element);
                        }
                        else if ((category == BuiltInCategory.OST_StructuralColumns && familyAndType.Contains("SUPERIOR")) ||
                                 (category == BuiltInCategory.OST_Parts && material.Contains("SUPERIOR")))
                        {
                            //par.Set(ViewAsBuiltTools._02_03_DSI_Espesor_LosaSuperior);
                            SetDateLosaSuperior(elementsFiltered, element);
                        }
                        else
                        {
                            SetDateLosaFondo(elementsFiltered, element);
                        }
                    }
                    else if ((category == BuiltInCategory.OST_StructuralColumns && familyAndType.Contains("SOLADO")) ||
                            (category == BuiltInCategory.OST_StructuralFraming && familyAndType.Contains("SOLADO")) ||
                             (category == BuiltInCategory.OST_Parts && material.Contains("SOLADO")))
                    {
                        SetDateSolado(elementsFiltered, element);
                    }
                }
                else if(category == BuiltInCategory.OST_GenericModel)
                {
                    SetDateJunta(elementsFiltered, element);
                }
            }
        }

        public static void SetDateElement(List<ExcelElement> elementsFiltered, Element element, 
            Func<ExcelElement, string> selector, 
            Guid inicioProgramado, Guid finProgramado, 
            Guid inicioReal, Guid finReal)
        {
            // Obtener la menor y mayor fecha
            DateTime? menorFechaLosaAcero = GetMinDate(elementsFiltered, selector);
            DateTime? mayorFechaLosaAcero = GetMaxDate(elementsFiltered, selector);

            SetDates(element, inicioProgramado, finProgramado, menorFechaLosaAcero, mayorFechaLosaAcero);
            SetDates(element, inicioReal, finReal, menorFechaLosaAcero, mayorFechaLosaAcero);
        }

        private static DateTime? GetMinDate(List<ExcelElement> elements, Func<ExcelElement, string> dateSelector)
        {
            var dates = elements
                .Where(e => !string.IsNullOrEmpty(dateSelector(e)))
                .Select(e => DateTime.TryParse(dateSelector(e), out DateTime date) ? (DateTime?)date : null)
                .Where(date => date.HasValue)
                .Select(date => date.Value);

            // Verifica si hay elementos en la secuencia antes de llamar a Min()
            if (dates.Any())
            {
                return dates.Min();
            }
            else
            {
                return null;
            }
        }

        private static DateTime? GetMaxDate(List<ExcelElement> elements, Func<ExcelElement, string> dateSelector)
        {
            var dates = elements
                .Where(e => !string.IsNullOrEmpty(dateSelector(e)))
                .Select(e => DateTime.TryParse(dateSelector(e), out DateTime date) ? (DateTime?)date : null)
                .Where(date => date.HasValue)
                .Select(date => date.Value);

            // Verifica si hay elementos en la secuencia antes de llamar a Min()
            if (dates.Any())
            {
                return dates?.Max();
            }
            else
            {
                return null;
            }            
        }

        private static void SetDates(Element element, Guid startGuid, Guid endGuid, DateTime? startDate, DateTime? endDate)
        {
            SetDateTime(element, startGuid, startDate);
            SetDateTime(element, endGuid, endDate);
        }

        public static void SetDateTime(Element element, Guid guidParameter, DateTime? dateTime)
        {
            Parameter par = element.get_Parameter(guidParameter);
            if (par == null) { return; }

            string date = dateTime?.ToString("dd.MM.yyyy");
            if (string.IsNullOrEmpty(date)) { return; }
            par.Set(date);
        }

        public static void SetDateSolado(List<ExcelElement> elementsFiltered, Element element)
        {
            SetDateElement(elementsFiltered, element,
                        e => e.Solado,
                        SharedParameters._03_03_01_DSI_Fecha_Inicio_Programado.Guid,
                        SharedParameters._03_03_01_DSI_Fecha_Fin_Programado.Guid,
                        SharedParameters._03_03_01_DSI_Fecha_Inicio_Real.Guid,
                        SharedParameters._03_03_01_DSI_Fecha_Fin_Real.Guid);
        }

        public static void SetDateLosaFondo(List<ExcelElement> elementsFiltered, Element element)
        {
            //Asignar acero
            SetDateElement(elementsFiltered, element,
                        e => e.AceroLosaFondo,
                        SharedParameters._03_03_03_DSI_Fecha_Inicio_Programado.Guid,
                        SharedParameters._03_03_03_DSI_Fecha_Fin_Programado.Guid,
                        SharedParameters._03_03_03_DSI_Fecha_Inicio_Real.Guid,
                        SharedParameters._03_03_03_DSI_Fecha_Fin_Real.Guid);

            //Asignar encofrado
            SetDateElement(elementsFiltered, element,
                        e => e.EncofradoLosaFondo,
                        SharedParameters._03_03_02_DSI_Fecha_Inicio_Programado.Guid,
                        SharedParameters._03_03_02_DSI_Fecha_Fin_Programado.Guid,
                        SharedParameters._03_03_02_DSI_Fecha_Inicio_Real.Guid,
                        SharedParameters._03_03_02_DSI_Fecha_Fin_Real.Guid);

            //Asignar concreto
            SetDateElement(elementsFiltered, element,
                        e => e.ConcretoLosaFondo,
                        SharedParameters._03_03_01_DSI_Fecha_Inicio_Programado.Guid,
                        SharedParameters._03_03_01_DSI_Fecha_Fin_Programado.Guid,
                        SharedParameters._03_03_01_DSI_Fecha_Inicio_Real.Guid,
                        SharedParameters._03_03_01_DSI_Fecha_Fin_Real.Guid);
        }

        public static void SetDateLosaSuperior(List<ExcelElement> elementsFiltered, Element element)
        {
            //Asignar acero
            SetDateElement(elementsFiltered, element,
                        e => e.AceroLosaSuperior,
                        SharedParameters._03_03_03_DSI_Fecha_Inicio_Programado.Guid,
                        SharedParameters._03_03_03_DSI_Fecha_Fin_Programado.Guid,
                        SharedParameters._03_03_03_DSI_Fecha_Inicio_Real.Guid,
                        SharedParameters._03_03_03_DSI_Fecha_Fin_Real.Guid);

            //Asignar encofrado
            SetDateElement(elementsFiltered, element,
                        e => e.EncofradoLosaSuperior,
                        SharedParameters._03_03_02_DSI_Fecha_Inicio_Programado.Guid,
                        SharedParameters._03_03_02_DSI_Fecha_Fin_Programado.Guid,
                        SharedParameters._03_03_02_DSI_Fecha_Inicio_Real.Guid,
                        SharedParameters._03_03_02_DSI_Fecha_Fin_Real.Guid);

            //Asignar concreto
            SetDateElement(elementsFiltered, element,
                        e => e.ConcretoLosaSuperior,
                        SharedParameters._03_03_01_DSI_Fecha_Inicio_Programado.Guid,
                        SharedParameters._03_03_01_DSI_Fecha_Fin_Programado.Guid,
                        SharedParameters._03_03_01_DSI_Fecha_Inicio_Real.Guid,
                        SharedParameters._03_03_01_DSI_Fecha_Fin_Real.Guid);
        }

        public static void SetDateMuro(List<ExcelElement> elementsFiltered, Element element)
        {
            //Asignar acero
            SetDateElement(elementsFiltered, element,
                        e => e.AceroMuro,
                        SharedParameters._03_03_03_DSI_Fecha_Inicio_Programado.Guid,
                        SharedParameters._03_03_03_DSI_Fecha_Fin_Programado.Guid,
                        SharedParameters._03_03_03_DSI_Fecha_Inicio_Real.Guid,
                        SharedParameters._03_03_03_DSI_Fecha_Fin_Real.Guid);

            //Asignar encofrado
            SetDateElement(elementsFiltered, element,
                        e => e.EncofradoMuro,
                        SharedParameters._03_03_02_DSI_Fecha_Inicio_Programado.Guid,
                        SharedParameters._03_03_02_DSI_Fecha_Fin_Programado.Guid,
                        SharedParameters._03_03_02_DSI_Fecha_Inicio_Real.Guid,
                        SharedParameters._03_03_02_DSI_Fecha_Fin_Real.Guid);

            //Asignar concreto
            SetDateElement(elementsFiltered, element,
                        e => e.ConcretoMuro,
                        SharedParameters._03_03_01_DSI_Fecha_Inicio_Programado.Guid,
                        SharedParameters._03_03_01_DSI_Fecha_Fin_Programado.Guid,
                        SharedParameters._03_03_01_DSI_Fecha_Inicio_Real.Guid,
                        SharedParameters._03_03_01_DSI_Fecha_Fin_Real.Guid);
        }


        public static void SetDateJunta(List<ExcelElement> elementsFiltered, Element element)
        {
            //Asignar acero
            SetDateElement(elementsFiltered, element,
                        e => e.Junta,
                        SharedParameters._03_03_02_DSI_Fecha_Inicio_Programado.Guid,
                        SharedParameters._03_03_02_DSI_Fecha_Fin_Programado.Guid,
                        SharedParameters._03_03_02_DSI_Fecha_Inicio_Real.Guid,
                        SharedParameters._03_03_02_DSI_Fecha_Fin_Real.Guid);
        }


        //private static void SetDateTime(Element element, Guid guid, DateTime? date)
        //{
        //    Parameter param = element.get_Parameter(guid);
        //    if (param != null)
        //    {
        //        if (date.HasValue)
        //        {
        //            param.Set(date.Value);
        //        }
        //        else
        //        {
        //            param.Set(string.Empty);
        //        }
        //    }
        //}


        //public static void SetDateLosaSuperior(List<ExcelElement> elementsFiltered, Element element)
        //{
        //    // Obtener la menor fecha Losa // Acero
        //    DateTime? menorFechaLosaAcero = elementsFiltered
        //        .Where(e => !string.IsNullOrEmpty(e.AceroLosaSuperior)) // Filtrar elementos que tengan la propiedad Solado no vacía
        //        .Select(e => DateTime.TryParse(e.AceroLosaSuperior, out DateTime fecha) ? (DateTime?)fecha : null) // Convertir la cadena Solado en DateTime y manejar valores nulos
        //        .Min();

        //    SetDateTime(element, SharedParameters._03_03_03_DSI_Fecha_Inicio_Programado.Guid, menorFechaLosaAcero);
        //    SetDateTime(element, SharedParameters._03_03_03_DSI_Fecha_Inicio_Real.Guid, menorFechaLosaAcero);

        //    // Obtener la mayor Losa // Acero
        //    DateTime? mayorFechaLosaAcero = elementsFiltered
        //        .Where(e => !string.IsNullOrEmpty(e.AceroLosaSuperior)) // Filtrar elementos que tengan la propiedad Solado no vacía
        //        .Select(e => DateTime.TryParse(e.AceroLosaSuperior, out DateTime fecha) ? (DateTime?)fecha : null) // Convertir la cadena Solado en DateTime y manejar valores nulos
        //        .Max();

        //    SetDateTime(element, SharedParameters._03_03_03_DSI_Fecha_Fin_Programado.Guid, mayorFechaLosaAcero);
        //    SetDateTime(element, SharedParameters._03_03_03_DSI_Fecha_Fin_Real.Guid, mayorFechaLosaAcero);

        //    // Obtener la menor fecha Losa // Concreto
        //    DateTime? menorFechaLosaConcreto = elementsFiltered
        //        .Where(e => !string.IsNullOrEmpty(e.ConcretoLosaSuperior)) // Filtrar elementos que tengan la propiedad Solado no vacía
        //        .Select(e => DateTime.TryParse(e.ConcretoLosaSuperior, out DateTime fecha) ? (DateTime?)fecha : null) // Convertir la cadena Solado en DateTime y manejar valores nulos
        //        .Min();

        //    SetDateTime(element, SharedParameters._03_03_01_DSI_Fecha_Inicio_Programado.Guid, menorFechaLosaAcero);
        //    SetDateTime(element, SharedParameters._03_03_01_DSI_Fecha_Inicio_Real.Guid, menorFechaLosaAcero);

        //    // Obtener la mayor Losa // Concreto
        //    DateTime? mayorFechaLosaConcreto = elementsFiltered
        //        .Where(e => !string.IsNullOrEmpty(e.ConcretoLosaSuperior)) // Filtrar elementos que tengan la propiedad Solado no vacía
        //        .Select(e => DateTime.TryParse(e.ConcretoLosaSuperior, out DateTime fecha) ? (DateTime?)fecha : null) // Convertir la cadena Solado en DateTime y manejar valores nulos
        //        .Max();

        //    SetDateTime(element, SharedParameters._03_03_01_DSI_Fecha_Fin_Programado.Guid, mayorFechaLosaConcreto);
        //    SetDateTime(element, SharedParameters._03_03_01_DSI_Fecha_Fin_Real.Guid, mayorFechaLosaConcreto);
        //}

        //public static void SetDateLosaFondo(List<ExcelElement> elementsFiltered, Element element)
        //{
        //    // Obtener la menor fecha Losa // Acero
        //    DateTime? menorFechaLosaAcero = elementsFiltered
        //        .Where(e => !string.IsNullOrEmpty(e.AceroLosaFondo)) // Filtrar elementos que tengan la propiedad Solado no vacía
        //        .Select(e => DateTime.TryParse(e.AceroLosaFondo, out DateTime fecha) ? (DateTime?)fecha : null) // Convertir la cadena Solado en DateTime y manejar valores nulos
        //        .Min();

        //    SetDateTime(element, SharedParameters._03_03_03_DSI_Fecha_Inicio_Programado.Guid, menorFechaLosaAcero);
        //    SetDateTime(element, SharedParameters._03_03_03_DSI_Fecha_Inicio_Real.Guid, menorFechaLosaAcero);

        //    // Obtener la mayor Losa // Acero
        //    DateTime? mayorFechaLosaAcero = elementsFiltered
        //        .Where(e => !string.IsNullOrEmpty(e.AceroLosaFondo)) // Filtrar elementos que tengan la propiedad Solado no vacía
        //        .Select(e => DateTime.TryParse(e.AceroLosaFondo, out DateTime fecha) ? (DateTime?)fecha : null) // Convertir la cadena Solado en DateTime y manejar valores nulos
        //        .Max();

        //    SetDateTime(element, SharedParameters._03_03_03_DSI_Fecha_Fin_Programado.Guid, mayorFechaLosaAcero);
        //    SetDateTime(element, SharedParameters._03_03_03_DSI_Fecha_Fin_Real.Guid, mayorFechaLosaAcero);

        //    // Obtener la menor fecha Losa // Concreto
        //    DateTime? menorFechaLosaConcreto = elementsFiltered
        //        .Where(e => !string.IsNullOrEmpty(e.ConcretoLosaFondo)) // Filtrar elementos que tengan la propiedad Solado no vacía
        //        .Select(e => DateTime.TryParse(e.ConcretoLosaFondo, out DateTime fecha) ? (DateTime?)fecha : null) // Convertir la cadena Solado en DateTime y manejar valores nulos
        //        .Min();

        //    SetDateTime(element, SharedParameters._03_03_01_DSI_Fecha_Inicio_Programado.Guid, menorFechaLosaAcero);
        //    SetDateTime(element, SharedParameters._03_03_01_DSI_Fecha_Inicio_Real.Guid, menorFechaLosaAcero);

        //    // Obtener la mayor Losa // Concreto
        //    DateTime? mayorFechaLosaConcreto = elementsFiltered
        //        .Where(e => !string.IsNullOrEmpty(e.ConcretoLosaFondo)) // Filtrar elementos que tengan la propiedad Solado no vacía
        //        .Select(e => DateTime.TryParse(e.ConcretoLosaFondo, out DateTime fecha) ? (DateTime?)fecha : null) // Convertir la cadena Solado en DateTime y manejar valores nulos
        //        .Max();

        //    SetDateTime(element, SharedParameters._03_03_01_DSI_Fecha_Fin_Programado.Guid, mayorFechaLosaConcreto);
        //    SetDateTime(element, SharedParameters._03_03_01_DSI_Fecha_Fin_Real.Guid, mayorFechaLosaConcreto);
        //}

        //public static void SetDateSolado(List<ExcelElement> elementsFiltered, Element element)
        //{
        //    // Obtener la menor fecha de Solado
        //    DateTime? menorFechaSolado = elementsFiltered
        //        .Where(e => !string.IsNullOrEmpty(e.Solado)) // Filtrar elementos que tengan la propiedad Solado no vacía
        //        .Select(e => DateTime.TryParse(e.Solado, out DateTime fecha) ? (DateTime?)fecha : null) // Convertir la cadena Solado en DateTime y manejar valores nulos
        //        .Min();

        //    SetDateTime(element, SharedParameters._03_03_01_DSI_Fecha_Inicio_Programado.Guid, menorFechaSolado);
        //    SetDateTime(element, SharedParameters._03_03_01_DSI_Fecha_Inicio_Real.Guid, menorFechaSolado);

        //    // Obtener la mayor fecha de Solado
        //    DateTime? mayorFechaSolado = elementsFiltered
        //        .Where(e => !string.IsNullOrEmpty(e.Solado)) // Filtrar elementos que tengan la propiedad Solado no vacía
        //        .Select(e => DateTime.TryParse(e.Solado, out DateTime fecha) ? (DateTime?)fecha : null) // Convertir la cadena Solado en DateTime y manejar valores nulos
        //        .Max();

        //    SetDateTime(element, SharedParameters._03_03_01_DSI_Fecha_Fin_Programado.Guid, mayorFechaSolado);
        //    SetDateTime(element, SharedParameters._03_03_01_DSI_Fecha_Fin_Real.Guid, mayorFechaSolado);
        //}
    }
}

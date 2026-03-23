using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;

namespace BimManagement
{
    public partial class CreateReportSheetView : Window
    {
        private readonly ExternalEvent _exEvent;

        // ── Propiedades de lectura ─────────────────────────────────────────────
        public bool   IsMonthly         => MensualRadio.IsChecked == true;
        public string Week              => IssueWeek.Text.Trim();
        public string Period            => PeriodoBox.Text.Trim();
        public string Month             => MonthBox.Text.Trim();
        public string FechaPresentacion => FechaPresentacionBox.Text.Trim();
        public bool   UseCurrentModel   => CurrentModelRadio.IsChecked == true;
        public string FolderPath        => FolderPathBox?.Text?.Trim() ?? string.Empty;
        public bool   IncludeSubfolders => IncludeSubfoldersCheck.IsChecked == true;
        public List<FileInfo> SelectedFiles => RvtFilesList.Items.OfType<FileInfo>().ToList();

        public CreateReportSheetView(ExternalEvent exEvent)
        {
            _exEvent = exEvent;
            InitializeComponent();

            // Fecha de presentación inicial: próximo lunes (modo semanal por defecto)
            DateTime today       = DateTime.Today;
            int daysToMonday     = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
            if (daysToMonday == 0) daysToMonday = 7;
            FechaPresentacionBox.Text = today.AddDays(daysToMonday).ToString("dd/MM/yyyy");
        }

        // ── Tipo de reporte ───────────────────────────────────────────────────
        private void ReportType_Changed(object sender, RoutedEventArgs e)
        {
            if (PeriodoBox == null) return;

            if (MensualRadio.IsChecked == true)
            {
                WeekPanel.Visibility  = Visibility.Collapsed;
                MonthPanel.Visibility = Visibility.Visible;

                DateTime now   = DateTime.Now;
                DateTime first = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
                DateTime last  = new DateTime(now.Year, now.Month, 1).AddDays(-1);

                MonthBox.Text   = first.ToString("MM/yyyy");
                PeriodoBox.Text = $"{first:dd/MM} - {last:dd/MM}";

                // Fecha de presentación: último día del mes en curso
                DateTime today        = DateTime.Today;
                DateTime lastDayMonth = new DateTime(today.Year, today.Month,
                    DateTime.DaysInMonth(today.Year, today.Month));
                FechaPresentacionBox.Text = lastDayMonth.ToString("dd/MM/yyyy");
            }
            else
            {
                WeekPanel.Visibility  = Visibility.Visible;
                MonthPanel.Visibility = Visibility.Collapsed;
                IssueWeek_TextChanged(null, null);

                // Fecha de presentación: próximo lunes
                DateTime today       = DateTime.Today;
                int daysToMonday     = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
                if (daysToMonday == 0) daysToMonday = 7;
                FechaPresentacionBox.Text = today.AddDays(daysToMonday).ToString("dd/MM/yyyy");
            }
        }

        // ── Semana cambiada → auto-populate período y carpeta ─────────────────
        private void IssueWeek_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (PeriodoBox == null) return;

            if (!int.TryParse(IssueWeek?.Text, out int week))
            {
                PeriodoBox.Text           = string.Empty;
                if (FolderPathBox != null) FolderPathBox.Text = string.Empty;
                return;
            }

            int sub = week - 27;
            var candidatos = new[]
            {
                $@"G:\Shared drives\Proyectos\BIM\2408-EOPNP_Chorrillos\OB\Informes Semanales\SEMANA {week} - {sub} SUP\Modelos\Supervisión",
                $@"G:\Unidades compartidas\Proyectos\BIM\2408-EOPNP_Chorrillos\OB\Informes Semanales\SEMANA {week} - {sub} SUP\Modelos\Supervisión",
                $@"G:\Shared drives\06-24300-CSL-Cesel\24303-CSL-EOPNP Chorrillos\OB\Informes Semanales\SEMANA {week} - {sub} SUP\Modelos\Supervisión",
                $@"G:\Unidades compartidas\06-24300-CSL-Cesel\24303-CSL-EOPNP Chorrillos\OB\Informes Semanales\SEMANA {week} - {sub} SUP\Modelos\Supervisión"
            };

            if (FolderPathBox != null)
                FolderPathBox.Text = candidatos.FirstOrDefault(Directory.Exists) ?? string.Empty;

            DateTime inicio = new DateTime(2023, 11, 13).AddDays((week - 1) * 7);
            DateTime fin    = inicio.AddDays(6);
            PeriodoBox.Text = $"{inicio:dd/MM} - {fin:dd/MM}";
        }

        // ── Alcance cambiado ──────────────────────────────────────────────────
        private void Scope_Changed(object sender, RoutedEventArgs e)
        {
            if (FolderPanel == null) return;

            bool usarCarpeta = FolderRadio.IsChecked == true;
            FolderPanel.Visibility      = usarCarpeta ? Visibility.Visible   : Visibility.Collapsed;
            CurrentModelInfo.Visibility = usarCarpeta ? Visibility.Collapsed : Visibility.Visible;
            FindFilesBtn.Visibility     = usarCarpeta ? Visibility.Visible   : Visibility.Collapsed;
        }

        // ── Seleccionar carpeta ───────────────────────────────────────────────
        private void SelectFolder_Click(object sender, RoutedEventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.ValidateNames   = false;
                dlg.CheckFileExists = false;
                dlg.CheckPathExists = true;
                dlg.FileName        = "Seleccionar carpeta";

                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    FolderPathBox.Text = Path.GetDirectoryName(dlg.FileName);
                    FindFiles_Click(null, null);
                }
            }
        }

        // ── Buscar archivos ───────────────────────────────────────────────────
        private void FindFiles_Click(object sender, RoutedEventArgs e)
        {
            string path = FolderPathBox?.Text?.Trim();
            if (!Directory.Exists(path))
            {
                Log("Ruta no válida o no encontrada.");
                return;
            }

            try
            {
                var option      = IncludeSubfoldersCheck.IsChecked == true
                                  ? SearchOption.AllDirectories
                                  : SearchOption.TopDirectoryOnly;
                var backupRegex = new Regex(@"\.\d{4}\.rvt$", RegexOptions.IgnoreCase);
                var files       = Directory.GetFiles(path, "*.rvt", option)
                                           .Where(f => !backupRegex.IsMatch(f))
                                           .ToArray();

                RvtFilesList.Items.Clear();
                foreach (var f in files)
                    RvtFilesList.Items.Add(new FileInfo(f));

                FilesCountLabel.Text = $"Archivos encontrados: {files.Length}";
                Log($"Se encontraron {files.Length} archivos .rvt.");
            }
            catch (Exception ex)
            {
                Log($"Error al buscar archivos: {ex.Message}");
            }
        }

        // ── Ejecutar ──────────────────────────────────────────────────────────
        private void Execute_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PeriodoBox.Text))
            {
                Log("El campo Período es obligatorio.");
                return;
            }

            if (FolderRadio.IsChecked == true && RvtFilesList.Items.Count == 0)
            {
                Log("No hay archivos para procesar. Use 'Buscar archivos' primero.");
                return;
            }

            // Transferir parámetros al contenedor estático para el handler
            CreateReportSheetTools.Week              = Week;
            CreateReportSheetTools.Month             = Month;
            CreateReportSheetTools.Period            = Period;
            CreateReportSheetTools.IsMonthly         = IsMonthly;
            CreateReportSheetTools.FechaPresentacion = FechaPresentacion;
            CreateReportSheetTools.UseCurrentModel   = UseCurrentModel;
            CreateReportSheetTools.Files             = SelectedFiles;

            Log("Iniciando proceso...");
            _exEvent.Raise();
        }

        // ── Cancelar ──────────────────────────────────────────────────────────
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        public void Log(string message)
        {
            LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            LogTextBox.ScrollToEnd();
        }

        public void SetProgress(int value, int max)
        {
            ProgressBar.Maximum = max;
            ProgressBar.Value   = value > max ? max : value;
            ProgressLabel.Text  = max > 0 ? $"Procesando {value} de {max}…" : string.Empty;
        }
    }
}

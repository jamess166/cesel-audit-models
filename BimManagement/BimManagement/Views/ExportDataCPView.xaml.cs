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
    public partial class ExportDataCPView : Window
    {
        private readonly ExternalEvent _exEvent;

        // ── Propiedades de lectura ─────────────────────────────────────────────
        public bool           UseCurrentModel => CurrentModelRadio.IsChecked == true;
        public string          FolderPath      => FolderPathBox?.Text?.Trim() ?? string.Empty;
        public bool            CombineIntoSingleFile => CombineRadio.IsChecked == true;
        public List<FileInfo>  SelectedFiles   => RvtFilesList.Items.OfType<FileInfo>().ToList();

        public ExportDataCPView(ExternalEvent exEvent)
        {
            _exEvent = exEvent;
            InitializeComponent();
            UpdateCombinedFileNamePreview();
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

        // ── Formato de salida cambiado ──────────────────────────────────────────
        private void OutputMode_Changed(object sender, RoutedEventArgs e)
        {
            if (CombineInfo == null) return;

            CombineInfo.Visibility = CombineRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            UpdateCombinedFileNamePreview();
        }

        private void UpdateCombinedFileNamePreview()
        {
            if (CombinedFileNamePreview == null) return;
            CombinedFileNamePreview.Text = $"ExportData_{DateTime.Now:yyMMdd_HHmm}.xlsx";
        }

        // ── Seleccionar carpeta (selector moderno tipo "Abrir archivo") ─────────
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
                var backupRegex = new Regex(@"\.\d{4}\.rvt$", RegexOptions.IgnoreCase);
                var files       = Directory.GetFiles(path, "*.rvt", SearchOption.AllDirectories)
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
            if (FolderRadio.IsChecked == true && RvtFilesList.Items.Count == 0)
            {
                Log("No hay archivos para procesar. Use 'Buscar archivos' primero.");
                return;
            }

            ExportDataCPTools.UseCurrentModel       = UseCurrentModel;
            ExportDataCPTools.FolderPath            = FolderPath;
            ExportDataCPTools.Files                 = SelectedFiles;
            ExportDataCPTools.CombineIntoSingleFile = CombineIntoSingleFile;

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

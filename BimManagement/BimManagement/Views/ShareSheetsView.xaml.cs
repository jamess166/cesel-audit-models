using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Forms;

namespace BimManagement
{
    /// <summary>
    /// Ítem de plano para la lista de exportación (modelo actual).
    /// </summary>
    public class SheetItem
    {
        public bool      IsSelected { get; set; } = true;
        public string    Number     { get; }
        public string    Name       { get; }
        public ElementId Id         { get; }
        public string    Display    => $"{Number}  –  {Name}";

        public SheetItem(ViewSheet sheet)
        {
            Number = sheet.SheetNumber;
            Name   = sheet.Name;
            Id     = sheet.Id;
        }
    }

    public partial class ShareSheetsView : Window
    {
        private readonly List<SheetItem> _items;

        // ── Resultados ────────────────────────────────────────────────────────
        public string           OutputPath      { get; private set; }
        public bool             UseCurrentModel => CurrentModelRadio.IsChecked == true;
        public List<SheetItem>  SelectedSheets  => _items.Where(i => i.IsSelected).ToList();
        public List<FileInfo>   SelectedFiles   => RvtFilesList.Items.OfType<FileInfo>().ToList();

        public ShareSheetsView(string suggestedOutputPath, List<SheetItem> sheets)
        {
            InitializeComponent();

            _items = sheets ?? new List<SheetItem>();

            OutputPath         = suggestedOutputPath;
            OutputPathBox.Text = suggestedOutputPath;

            SheetsList.ItemsSource = _items;
            UpdateCount();
        }

        // ── Cambio de alcance ─────────────────────────────────────────────────
        private void Scope_Changed(object sender, RoutedEventArgs e)
        {
            if (FolderPanel == null) return;

            bool carpeta = FolderRadio.IsChecked == true;
            FolderPanel.Visibility    = carpeta ? System.Windows.Visibility.Visible   : System.Windows.Visibility.Collapsed;
            SheetListPanel.Visibility = carpeta ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        }

        // ── Carpeta de modelos ────────────────────────────────────────────────
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
                    string folder      = Path.GetDirectoryName(dlg.FileName);
                    FolderPathBox.Text = folder;

                    // Auto-calcular carpeta destino: 3 niveles arriba + ANEXOS
                    string computed    = ComputeOutputPath(folder);
                    OutputPath         = computed;
                    OutputPathBox.Text = computed;

                    FindFiles_Click(null, null);
                }
            }
        }

        /// <summary>
        /// Sube <paramref name="levels"/> niveles desde <paramref name="folder"/>
        /// y devuelve la ruta con la subcarpeta "ANEXOS".
        /// </summary>
        private static string ComputeOutputPath(string folder, int levels = 3)
        {
            string current = folder;
            for (int i = 0; i < levels; i++)
            {
                string parent = Path.GetDirectoryName(current);
                if (string.IsNullOrEmpty(parent)) break;
                current = parent;
            }
            return Path.Combine(current, "ANEXOS");
        }

        private void FindFiles_Click(object sender, RoutedEventArgs e)
        {
            string path = FolderPathBox?.Text?.Trim();
            if (!Directory.Exists(path))
            {
                StatusLabel.Text = "Ruta no válida.";
                return;
            }

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
            StatusLabel.Text     = string.Empty;
        }

        // ── Carpeta destino ───────────────────────────────────────────────────
        private void ChangeFolder_Click(object sender, RoutedEventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.ValidateNames   = false;
                dlg.CheckFileExists = false;
                dlg.CheckPathExists = true;
                dlg.FileName        = "Seleccionar carpeta";

                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string p           = Path.GetDirectoryName(dlg.FileName);
                    OutputPath         = p;
                    OutputPathBox.Text = p;
                }
            }
        }

        // ── Selección masiva ──────────────────────────────────────────────────
        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _items) item.IsSelected = true;
            RefreshList();
        }

        private void DeselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _items) item.IsSelected = false;
            RefreshList();
        }

        private void RefreshList()
        {
            SheetsList.ItemsSource = null;
            SheetsList.ItemsSource = _items;
            UpdateCount();
        }

        private void UpdateCount()
        {
            int total    = _items.Count;
            int selected = _items.Count(i => i.IsSelected);
            CountLabel.Text = $"{selected} de {total} planos";
        }

        // ── Exportar ──────────────────────────────────────────────────────────
        private void Export_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(OutputPath))
            {
                StatusLabel.Text = "Debe indicar una carpeta destino.";
                return;
            }

            if (UseCurrentModel && !SelectedSheets.Any())
            {
                StatusLabel.Text = "Seleccione al menos un plano.";
                return;
            }

            if (!UseCurrentModel && RvtFilesList.Items.Count == 0)
            {
                StatusLabel.Text = "Busque archivos en la carpeta primero.";
                return;
            }

            DialogResult = true;
            Close();
        }

        // ── Cancelar ──────────────────────────────────────────────────────────
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

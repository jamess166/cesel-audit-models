using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Forms;

namespace BimManagement
{
    public partial class WeeklyReportView : Window
    {
        public WeeklyReportView()
        {
            InitializeComponent();
            IssueDatePicker.SelectedDate = new DateTime(2025, 2, 1);
        }

        private void SelectFolder_Click(object sender, RoutedEventArgs e)
        {
            // var dialog = new CommonOpenFileDialog
            // {
            //     IsFolderPicker = true,
            //     Title = "Seleccionar carpeta con modelos RVT"
            // };
            //
            // if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            // {
            //     FolderPathBox.Text = dialog.FileName;
            //     FindFiles_Click(null, null);
            // }
        }

        private void FindFiles_Click(object sender, RoutedEventArgs e)
        {
            // if (string.IsNullOrWhiteSpace(FolderPathBox.Text))
            // {
            //     Log("Por favor seleccione una carpeta primero");
            //     return;
            // }
            //
            // try
            // {
            //     var searchOption = IncludeSubfoldersCheck.IsChecked == true
            //         ? SearchOption.AllDirectories
            //         : SearchOption.TopDirectoryOnly;
            //
            //     var rvtFiles = Directory.GetFiles(FolderPathBox.Text, "*.rvt", searchOption);
            //     RvtFilesList.Items.Clear();
            //
            //     foreach (var file in rvtFiles)
            //     {
            //         var fileInfo = new FileInfo(file);
            //         RvtFilesList.Items.Add(fileInfo);
            //     }
            //
            //     Log($"Encontrados {rvtFiles.Length} archivos RVT");
            // }
            // catch (Exception ex)
            // {
            //     Log($"Error al buscar archivos: {ex.Message}");
            // }
        }

        private async void UpdateFiles_Click(object sender, RoutedEventArgs e)
        {
            // if (RvtFilesList.Items.Count == 0)
            // {
            //     Log("No hay archivos para procesar");
            //     return;
            // }
            //
            // if (!IssueDatePicker.SelectedDate.HasValue)
            // {
            //     Log("Por favor seleccione una fecha válida");
            //     return;
            // }
            //
            // var selectedDate = IssueDatePicker.SelectedDate.Value;
            // var filePaths = new List<string>();
            //
            // foreach (FileInfo fileInfo in RvtFilesList.Items)
            // {
            //     filePaths.Add(fileInfo.FullName);
            // }
            //
            // ProgressBar.IsIndeterminate = true;
            // UpdateFiles_Click.IsEnabled = false;
            //
            // try
            // {
            //     // Aquí llamarías a tu lógica de Revit para procesar los archivos
            //     // Esto es solo un ejemplo - necesitarás adaptarlo a tu add-in de Revit
            //     int processedCount = 0;
            //     int errorCount = 0;
            //
            //     foreach (var filePath in filePaths)
            //     {
            //         try
            //         {
            //             // Simular procesamiento (reemplazar con tu lógica real)
            //             await Task.Delay(100);
            //             processedCount++;
            //             Log($"Procesado: {Path.GetFileName(filePath)}");
            //         }
            //         catch (Exception ex)
            //         {
            //             errorCount++;
            //             Log($"Error procesando {Path.GetFileName(filePath)}: {ex.Message}");
            //         }
            //     }
            //
            //     Log($"Proceso completado. Archivos procesados: {processedCount}, errores: {errorCount}");
            // }
            // finally
            // {
            //     ProgressBar.IsIndeterminate = false;
            //     UpdateFiles_Click.IsEnabled = true;
            // }
        }

        private void Log(string message)
        {
            LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            LogTextBox.ScrollToEnd();
        }
    }
}
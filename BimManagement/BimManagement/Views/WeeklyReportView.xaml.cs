using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Forms;

namespace BimManagement
{
    public partial class WeeklyReportView : Window
    {
        public WeeklyReportView()
        {
            InitializeComponent();
            // IssueDatePicker.SelectedDate = new DateTime(2025, 2, 1);
        }

        private void SelectFolder_Click(object sender, RoutedEventArgs e)
        {
            string folderPath = string.Empty;
        
            using (var openFile = new OpenFileDialog())
            {
                openFile.ValidateNames = false;
                openFile.CheckFileExists = false;
                openFile.CheckPathExists = true;
                openFile.FileName = "Folder Selection";
            
                if (openFile.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    folderPath = Path.GetDirectoryName(openFile.FileName);
                    FolderPathBox.Text = folderPath;
                    FindFiles_Click(null, null);
                }
            }
        }

        private void FindFiles_Click(object sender, RoutedEventArgs e)
        {
            string path = FolderPathBox.Text;
            if (!Directory.Exists(path))
            {
                Log("Ruta no válida.");
                return;
            }

            try
            {
                // Obtener todos los archivos .rvt
                var searchOption = IncludeSubfoldersCheck.IsChecked == true 
                    ? SearchOption.AllDirectories 
                    : SearchOption.TopDirectoryOnly;
                
                string[] files = Directory.GetFiles(path, "*.rvt", searchOption);
             
                 // Expresión regular para excluir los que terminan en .0001.rvt, .0012.rvt, etc.
                Regex backupRegex = new Regex(@"\.\d{4}\.rvt$", RegexOptions.IgnoreCase);
                files = files.Where(f => !backupRegex.IsMatch(f)).ToArray();
            

                RvtFilesList.Items.Clear();
                foreach (var file in files)
                {
                    RvtFilesList.Items.Add(new FileInfo(file));
                }

                Log($"{files.Length} archivos encontrados.");
            }
            catch (Exception ex)
            {
                Log($"Error al buscar archivos: {ex.Message}");
            }
        }

        private async void UpdateFiles_Click(object sender, RoutedEventArgs e)
        {
            if (RvtFilesList.Items.Count == 0)
            {
                Log("No hay archivos para procesar");
                return;
            }
            
            Log("Iniciando actualización de archivos...");
            
            var selectedFiles = RvtFilesList.Items.Cast<FileInfo>()
                .Select(fi => fi.FullName).ToList();
            
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
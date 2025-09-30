using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BimManagement
{
    public partial class WeeklyReportView : Window
    {
        private ExternalEvent m_ExEvent;
        public static WeeklyReportView Instance { get; private set; }

        public WeeklyReportView(ExternalEvent exEvent)
        {
            InitializeComponent();
            m_ExEvent = exEvent;
            Instance = this;
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

        //private void IssueWeek_TextChanged(object sender, TextChangedEventArgs e)
        //{
        //    string input = IssueWeek.Text;

        //    if (int.TryParse(input, out int weekNumber))
        //    {
        //        int subWeek = weekNumber - 27;
        //        string basePath =
        //            @"G:\{0}\Proyectos\BIM\2408-EOPNP_Chorrillos\OB\Informes Semanales\SEMANA {1} - {2} SUP\Modelos\Supervisión";

        //        // Intenta con "Shared drives"
        //        string path1 = string.Format(basePath, "Shared drives", weekNumber, subWeek);
        //        // Intenta con "Unidades compartidas"
        //        string path2 = string.Format(basePath, "Unidades compartidas", weekNumber, subWeek);

        //        if (Directory.Exists(path1))
        //        {
        //            FolderPathBox.Text = path1;
        //        }
        //        else if (Directory.Exists(path2))
        //        {
        //            FolderPathBox.Text = path2;
        //        }
        //        else
        //        {
        //            FolderPathBox.Text = string.Empty;
        //        }

        //        // Semana 1 = Sábado 13/11/2023 al Viernes 19/11/2023
        //        // =====================================
        //        DateTime inicioSemana1 = new DateTime(2023, 11, 13);
        //        DateTime inicioSemanaActual = inicioSemana1.AddDays((weekNumber - 1) * 7);
        //        DateTime finSemanaActual = inicioSemanaActual.AddDays(6); // viernes

        //        string periodoTexto = $"{inicioSemanaActual:dd/MM} - {finSemanaActual:dd/MM}";
        //        PeriodoBox.Text = periodoTexto;
        //    }
        //    else
        //    {
        //        FolderPathBox.Text = string.Empty;
        //    }
        //}

        private void IssueWeek_TextChanged(object sender, TextChangedEventArgs e)
        {
            string input = IssueWeek.Text;

            if (int.TryParse(input, out int weekNumber))
            {
                int subWeek = weekNumber - 27;

                // Opciones de base: combina raíz de unidad con rutas relativas
                var folderPaths = new[]
                {
                    $@"G:\Shared drives\Proyectos\BIM\2408-EOPNP_Chorrillos\OB\Informes Semanales\SEMANA {weekNumber} - {subWeek} SUP\Modelos\Supervisión",
                    $@"G:\Unidades compartidas\Proyectos\BIM\2408-EOPNP_Chorrillos\OB\Informes Semanales\SEMANA {weekNumber} - {subWeek} SUP\Modelos\Supervisión",
                    $@"G:\Shared drives\06-24300-CSL-Cesel\24303-CSL-EOPNP Chorrillos\OB\Informes Semanales\SEMANA {weekNumber} - {subWeek} SUP\Modelos\Supervisión",
                    $@"G:\Unidades compartidas\06-24300-CSL-Cesel\24303-CSL-EOPNP Chorrillos\OB\Informes Semanales\SEMANA {weekNumber} - {subWeek} SUP\Modelos\Supervisión"
                };

                // Buscar el primer path que exista
                string existingPath = folderPaths.FirstOrDefault(Directory.Exists);
                FolderPathBox.Text = existingPath ?? string.Empty;

                // Semana 1 = Lunes 13/11/2023
                DateTime inicioSemana1 = new DateTime(2023, 11, 13);
                DateTime inicioSemanaActual = inicioSemana1.AddDays((weekNumber - 1) * 7);
                DateTime finSemanaActual = inicioSemanaActual.AddDays(6);

                PeriodoBox.Text = $"{inicioSemanaActual:dd/MM} - {finSemanaActual:dd/MM}";
            }
            else
            {
                FolderPathBox.Text = string.Empty;
                PeriodoBox.Text = string.Empty;
            }
        }



        private async void UpdateFiles_Click(object sender, RoutedEventArgs e)
        {
            string issueName = IssueWeek.Text.Trim();
            string issueMonth = TitleBlockBox.Text.Trim();
            string issueDate = PeriodoBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(issueName) || string.IsNullOrWhiteSpace(issueDate))
            {
                Log("Por favor completa ambos campos: Semana y Período.");
                return;
            }

            var archivos = RvtFilesList.Items.OfType<FileInfo>().Where(f => f.Exists).ToList();
            if (archivos.Count == 0)
            {
                Log("No hay archivos válidos para procesar.");
                return;
            }

            Log("Iniciando Actualización de Planos");

            WeeklyReportTools.IssueName = issueName;
            if ((bool)MonthCheck.IsChecked)
            {
                WeeklyReportTools.IssueDate = issueDate;
            }
            else
            {
                WeeklyReportTools.IssueDate = string.Empty;
            }

            WeeklyReportTools.IssueMonth = issueMonth;
            WeeklyReportTools.Files = archivos;

            ProgressBar.Value = 0;
            ProgressBar.Maximum = archivos.Count;

            m_ExEvent.Raise();
        }

        private void Log(string message)
        {
            LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            LogTextBox.ScrollToEnd();
        }

        private void MonthCheck_Checked(object sender, RoutedEventArgs e)
        {
            if (MonthCheck.IsChecked == true)
            {
                TitleBlockBox.Visibility = System.Windows.Visibility.Visible;
                TitleBlockLabel.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                TitleBlockBox.Visibility = System.Windows.Visibility.Collapsed;
                TitleBlockLabel.Visibility = System.Windows.Visibility.Collapsed;
            }
        }
    }
}
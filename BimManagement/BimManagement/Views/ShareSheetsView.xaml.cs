using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace BimManagement
{
    /// <summary>
    /// Lógica de interacción para ShareSheetsView.xaml
    /// </summary>
    public partial class ShareSheetsView : Window
    {
        private ExternalEvent m_ExEvent;

        public ShareSheetsView(ExternalEvent exEvent)
        {
            InitializeComponent();
            m_ExEvent = exEvent;

            //txtFilePath.Text = string.Empty;
            txtFilePath.Text = !string.IsNullOrEmpty(ViewTools.DirectoryPath) ? ViewTools.DirectoryPath : string.Empty;
            chkCopyShared.IsChecked = ViewTools.CopyShared;
            chkDivideFiles.IsChecked = ViewTools.DivideNativePDF;
            chkPDF.IsChecked = ViewTools.CreatePDFToo;


            //ViewTools.DirectoryPath = txtFilePath.Text;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ViewTools.Revision = txtRevision.Text.ToString();
            //ViewTools.RevisionCBS = txtRevisionCBS.Text.ToString();
            //ViewTools.SendDate = dtDateSend.SelectedDate;
            //ViewTools.IsSameDate = chkIsSameDate.IsChecked;
            //ViewTools.Description = txtDescription.Text.ToString();

            ViewTools.DivideNativePDF = (bool)chkDivideFiles.IsChecked;
            ViewTools.CopyShared = (bool)chkCopyShared.IsChecked;
            ViewTools.CreatePDFToo= (bool)chkPDF.IsChecked;


            if (Tools.ExistPath(txtFilePath.Text))
            {
                ViewTools.DirectoryPath = txtFilePath.Text;
            }

            //List<SheetDetail> sheets = new List<SheetDetail>();

            //foreach(SheetDetail sheetDetail in treeSheets.Items)
            //{
            //    if (!sheetDetail.IsSelected) continue;
            //    sheets.Add(sheetDetail);
            //}

            List<SheetDetail> sheets = treeSheets.Items
                                        .OfType<SheetDetail>()
                                        .Where(sheetDetail => sheetDetail.IsSelected)
                                        .ToList();

            btnOpenRevision.IsEnabled = true;
            btnOpenShare.IsEnabled = true;

            ViewTools.SelectedSheets = sheets;

            m_ExEvent.Raise();
            //ShareSheetsTools.Execute();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            txtRevision.Text = 0.ToString();
            //dtDateSend.SelectedDate = DateTime.Now;
        }

        private void btnOpenRevision_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(ShareSheetsTools.revisionPath);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("CASA", ex.Message);
            }            
        }

        private void btnOpenShare_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(ShareSheetsTools.sharePath);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("CASA", ex.Message);
            }            
        }

        private void btn_Directory_Click(object sender, RoutedEventArgs e)
        {
            string folderPath = string.Empty;

            using (OpenFileDialog openFile = new OpenFileDialog())
            {
                openFile.ValidateNames = false;
                openFile.CheckFileExists = false;
                openFile.CheckPathExists = true;

                openFile.FileName = "Folder Selection";

                if (openFile.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    folderPath = System.IO.Path.GetDirectoryName(openFile.FileName);
                }
            }

            if (folderPath == string.Empty) { return; }

            txtFilePath.Text = folderPath;

            //guardo el path
            ViewTools.DirectoryPath = folderPath;   
        }
    }
}

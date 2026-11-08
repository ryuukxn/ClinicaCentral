using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace ClinicaCentral
{
    public partial class receipt_preview : Window
    {
        private readonly ReceiptRecord _receipt;

        public bool WasPrinted { get; private set; }

        public receipt_preview(ReceiptRecord receipt)
        {
            InitializeComponent();

            _receipt = receipt;
            DataContext = _receipt;
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog
            {
                Title = "Save receipt as PDF",
                Filter = "PDF document (*.pdf)|*.pdf",
                DefaultExt = ".pdf",
                AddExtension = true,
                FileName = MakeFileName()
            };

            if (dialog.ShowDialog(this) != true) return;

            try
            {
                PdfExport.SaveElement(ReceiptCard, dialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show("The receipt PDF could not be saved.\n\n" + ex.Message, "Print Receipt",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            WasPrinted = true;

            try
            {
                Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
            }
            catch (Exception)
            {
                MessageBox.Show("The receipt was saved to:\n" + dialog.FileName, "Print Receipt",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            DialogResult = true;
        }

        private string MakeFileName()
        {
            string name = "Receipt " + _receipt.Code + " - " + _receipt.Patient;

            foreach (char bad in Path.GetInvalidFileNameChars())
                name = name.Replace(bad, '_');

            return name.Trim();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace ClinicaCentral
{
    public partial class visit_view : Window
    {
        public visit_view(VisitSummary summary)
        {
            InitializeComponent();
            DataContext = summary;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private DrawingVisual BuildPrintVisual(double printableWidth)
        {
            ActionPanel.Visibility = Visibility.Hidden;
            CloseButton.Visibility = Visibility.Hidden;
            SummaryCard.UpdateLayout();

            Size size = new Size(SummaryCard.ActualWidth, SummaryCard.ActualHeight);
            double scale = Math.Min(1.0, printableWidth / size.Width);

            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.PushTransform(new ScaleTransform(scale, scale));
                dc.DrawRectangle(new VisualBrush(SummaryCard), null, new Rect(size));
                dc.Pop();
            }
            return visual;
        }

        private void RestoreButtons()
        {
            ActionPanel.Visibility = Visibility.Visible;
            CloseButton.Visibility = Visibility.Visible;
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            PrintDialog dialog = new PrintDialog();
            if (dialog.ShowDialog() != true) return;

            try
            {
                double width = dialog.PrintableAreaWidth > 0 ? dialog.PrintableAreaWidth - 40 : 700;
                dialog.PrintVisual(BuildPrintVisual(width), "Visit Summary");
            }
            catch (Exception ex)
            {
                MessageBox.Show("The visit summary could not be printed.\n\n" + ex.Message, "Print",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                RestoreButtons();
            }
        }

        private void Pdf_Click(object sender, RoutedEventArgs e)
        {
            string date = (DataContext as VisitSummary)?.DateText ?? "";
            string name = ("Visit Summary " + date).Trim();
            foreach (char bad in Path.GetInvalidFileNameChars())
                name = name.Replace(bad, '_');

            SaveFileDialog dialog = new SaveFileDialog
            {
                Title = "Save visit summary as PDF",
                Filter = "PDF document (*.pdf)|*.pdf",
                DefaultExt = ".pdf",
                AddExtension = true,
                FileName = name
            };

            if (dialog.ShowDialog(this) != true) return;

            try
            {
                ActionPanel.Visibility = Visibility.Hidden;
                CloseButton.Visibility = Visibility.Hidden;
                SummaryCard.UpdateLayout();

                PdfExport.SaveElement(SummaryCard, dialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show("The visit summary PDF could not be saved.\n\n" + ex.Message, "Download PDF",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            finally
            {
                RestoreButtons();
            }

            try
            {
                Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
            }
            catch (Exception)
            {
                MessageBox.Show("The visit summary was saved to:\n" + dialog.FileName, "Download PDF",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
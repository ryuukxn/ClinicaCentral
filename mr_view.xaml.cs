using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ClinicaCentral
{
    public partial class mr_view : Window
    {
        private readonly MedicalRecordRow _record;
        private byte[]? _uploadBytes;

        public mr_view(MedicalRecordRow record, bool showPrescriptions = false)
        {
            InitializeComponent();

            _record = record;
            DataContext = _record;
            PrescriptionSection.Visibility = showPrescriptions ? Visibility.Visible : Visibility.Collapsed;

            Loaded += (s, e) => LoadUploadedImage();
        }

        private void LoadUploadedImage()
        {
            try
            {
                PatientUpload? upload = new PatientUploadRepository(new DatabaseConnection()).GetLatest(_record.PatientId);
                if (upload == null) return;

                _uploadBytes = upload.Data;
                AttachmentImage.Source = PhotoStore.FromBytes(upload.Data);
                AttachmentImage.Visibility = Visibility.Visible;
                ImagePlaceholder.Visibility = Visibility.Collapsed;
                ImageCaption.Text = upload.FileName;
                ImageBox.Cursor = Cursors.Hand;
                ImageBox.ToolTip = "Click to view full size";
            }
            catch (Exception ex)
            {
                ImageCaption.Text = "Image unavailable";
                MessageBox.Show(
                    "The patient image could not be loaded. It may have been removed or the database may be unavailable.\n\n" + ex.Message,
                    "Attachment unavailable", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ImageBox_Click(object sender, MouseButtonEventArgs e)
        {
            if (_uploadBytes == null) return;

            Image full = new Image
            {
                Source = PhotoStore.FromBytes(_uploadBytes),
                Stretch = Stretch.Uniform
            };

            Window viewer = new Window
            {
                Title = "Patient image - " + _record.PatientName,
                Content = full,
                Owner = this,
                Width = 900,
                Height = 700,
                Background = Brushes.Black,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            viewer.ShowDialog();
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            PrintDialog dialog = new PrintDialog();

            if (dialog.ShowDialog() != true) return;

            ButtonPanel.Visibility = Visibility.Hidden;
            RecordCard.UpdateLayout();

            try
            {
                dialog.PrintVisual(RecordCard, "Medical record " + _record.CodeText);
            }
            finally
            {
                ButtonPanel.Visibility = Visibility.Visible;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

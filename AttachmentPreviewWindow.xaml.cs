using System;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ClinicaCentral
{
    public partial class AttachmentPreviewWindow : Window
    {
        public AttachmentPreviewWindow(BitmapSource image, string fileName)
        {
            InitializeComponent();

            PreviewImage.Source = image ?? throw new ArgumentNullException(nameof(image));
            if (!string.IsNullOrWhiteSpace(fileName))
                Title = fileName;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

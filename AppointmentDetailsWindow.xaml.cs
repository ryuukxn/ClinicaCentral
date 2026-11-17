using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace ClinicaCentral
{
    public partial class AppointmentDetailsWindow : Window
    {
        private readonly AppointmentDetails _details;
        private readonly bool _canConfirm;
        private readonly Func<int, bool>? _confirmAppointment;
        private BitmapImage? _attachmentImage;

        public AppointmentDetailsWindow(
            AppointmentDetails details,
            bool canConfirm,
            Func<int, bool>? confirmAppointment)
        {
            InitializeComponent();

            _details = details ?? throw new ArgumentNullException(nameof(details));
            _canConfirm = canConfirm &&
                string.Equals(_details.Status, "Pending", StringComparison.OrdinalIgnoreCase);
            _confirmAppointment = confirmAppointment;
            if (_canConfirm && _confirmAppointment == null)
                throw new ArgumentNullException(nameof(confirmAppointment));

            PatientNameText.Text = _details.PatientName;
            PatientIdText.Text = _details.PatientCode;
            ContactText.Text = _details.Contact;
            EmailText.Text = _details.Email;
            AppointmentDateText.Text = _details.AppointmentDate.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture);
            AppointmentTimeText.Text = _details.AppointmentDate.ToString("h:mm tt", CultureInfo.InvariantCulture);
            NotesText.Text = string.IsNullOrWhiteSpace(_details.Notes) ? "No notes provided." : _details.Notes;
            StatusText.Text = _details.Status;
            ConfirmButton.Visibility = _canConfirm ? Visibility.Visible : Visibility.Collapsed;
            LoadAttachment();
        }

        private void LoadAttachment()
        {
            byte[]? attachmentData = _details.AttachmentData;
            if (attachmentData == null || attachmentData.Length == 0)
            {
                NoAttachmentText.Text = "No attachment";
                return;
            }

            try
            {
                _attachmentImage = PhotoStore.FromBytes(attachmentData);
                AttachmentImage.Source = _attachmentImage;
                AttachmentImage.Visibility = Visibility.Visible;
                NoAttachmentText.Visibility = Visibility.Collapsed;
                AttachmentFileNameText.Text = _details.AttachmentFileName;
                AttachmentUploadedAtText.Text = _details.AttachmentUploadedAtText;
                AttachmentBox.Cursor = Cursors.Hand;
                AttachmentBox.ToolTip = "Click to view full size";
            }
            catch (Exception)
            {
                NoAttachmentText.Text = "Attachment unavailable";
                MessageBox.Show(
                    "The uploaded image could not be displayed. It may have been removed or damaged.",
                    "Attachment unavailable", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Attachment_Click(object sender, MouseButtonEventArgs e)
        {
            if (_attachmentImage == null)
                return;

            var preview = new AttachmentPreviewWindow(_attachmentImage, _details.AttachmentFileName)
            {
                Owner = this
            };
            preview.ShowDialog();
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_canConfirm || _confirmAppointment == null)
                return;

            MessageBoxResult answer = MessageBox.Show(
                "Are you sure you want to confirm this appointment?",
                "Confirm appointment", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes)
                return;

            try
            {
                if (!_confirmAppointment(_details.Id))
                {
                    MessageBox.Show(
                        "This appointment could not be confirmed. It may have already been updated or may not be assigned to your doctor account.",
                        "Appointment unavailable", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not confirm this appointment. Please check your database connection and try again.\n\n" + ex.Message,
                    "Confirmation error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

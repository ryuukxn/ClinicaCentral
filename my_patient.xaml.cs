using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ClinicaCentral
{
    public partial class my_patient : Window
    {
        private readonly PatientUser _user;
        private readonly DatabaseConnection _db;
        private readonly MyAppointmentsRepository _repo;
        private List<MyAppointmentRow> _rows = new List<MyAppointmentRow>();

        public my_patient(PatientUser user)
        {
            InitializeComponent();

            _user = user;
            _db = new DatabaseConnection();
            _repo = new MyAppointmentsRepository(_db);

            WelcomeText.Text = $"Welcome back, {user.Username}!";
            Loaded += (s, e) => LoadAppointments();
        }

        private void LoadAppointments()
        {
            if (_user.PatientId <= 0)
            {
                MessageBox.Show(
                    "Your account is not linked to a patient record yet, so there are no appointments to show. Please ask the clinic to link it.",
                    "ClinicaCentral", MessageBoxButton.OK, MessageBoxImage.Information);
                EmptyText.Visibility = Visibility.Visible;
                return;
            }

            try
            {
                PatientProfile? profile = new PatientDashboardRepository(_db).GetProfile(_user.PatientId);
                if (profile != null && profile.FirstName.Length > 0)
                    WelcomeText.Text = $"Welcome back, {profile.FirstName}!";

                _rows = _repo.GetAll(_user.PatientId);
                AppointmentList.ItemsSource = _rows;
                EmptyText.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Your appointments could not be loaded. Make sure MySQL is running in Laragon.\n\n" + ex.Message,
                    "Connection problem", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is int id)) return;

            MyAppointmentRow? row = _rows.Find(r => r.Id == id);
            if (row == null || !row.CanCancel) return;

            MessageBoxResult answer = MessageBox.Show(
                "Cancel this appointment?",
                "Cancel appointment", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;

            try
            {
                if (!_repo.CancelAppointment(id, _user.PatientId))
                {
                    MessageBox.Show(
                        "This appointment is no longer available to cancel. Refresh your appointments and try again.",
                        "Cancel appointment", MessageBoxButton.OK, MessageBoxImage.Warning);
                    LoadAppointments();
                    return;
                }

                LoadAppointments();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "The appointment could not be cancelled. Make sure MySQL is running in Laragon.\n\n" + ex.Message,
                    "Database error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is int id)) return;

            MyAppointmentRow? row = _rows.Find(r => r.Id == id);
            if (row == null || !row.CanEdit) return;

            UpdateAppointmentWindow window = new UpdateAppointmentWindow(_user, id)
            {
                Owner = this
            };

            if (window.ShowDialog() == true)
                LoadAppointments();
        }

        private void View_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is int id)) return;

            MyAppointmentRow? row = _rows.Find(r => r.Id == id);
            if (row == null) return;

            MedicalRecordRow? record;
            try
            {
                record = _repo.GetCompletedMedicalRecord(row.Id, _user.PatientId);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "The medical record for this appointment could not be loaded.\n\n" + ex.Message,
                    "Connection problem", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (record == null)
            {
                MessageBox.Show(
                    "No medical record was found for this completed appointment.",
                    "Medical record unavailable", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            mr_view window = new mr_view(record, true);
            window.Owner = this;
            window.ShowDialog();
        }

        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            string page = (sender as Button)?.Tag as string ?? "";

            if (page == "My Appointment") return;

            if (page == "Dashboard")
            {
                new dashboard_patient(_user).Open();
                Close();
                return;
            }

            if (page == "Book Appointment")
            {
                new bk_patient(_user).Open();
                Close();
                return;
            }

            if (page == "Profile")
            {
                new pf_patient(_user).Open();
                Close();
                return;
            }

        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            var answer = MessageBox.Show("Do you want to log out?", "Log out",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes) return;

            new login_form().Open();
            Close();
        }
    }
}

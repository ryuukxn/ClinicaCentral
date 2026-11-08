using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ClinicaCentral
{
    public partial class my_patient : Window
    {
        private readonly PatientUser _user;
        private readonly MyAppointmentsRepository _repo;
        private readonly VisitRepository _visits;
        private List<MyAppointmentRow> _rows = new List<MyAppointmentRow>();

        public my_patient(PatientUser user)
        {
            InitializeComponent();

            _user = user;
            DatabaseConnection db = new DatabaseConnection();
            _repo = new MyAppointmentsRepository(db);
            _visits = new VisitRepository(db);

            WelcomeText.Text = $"Welcome back, {user.Username}!";
            Loaded += (s, e) => LoadAppointments(db);
        }

        private void LoadAppointments(DatabaseConnection db)
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
                PatientProfile? profile = new PatientDashboardRepository(db).GetProfile(_user.PatientId);
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
        }

        private void View_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is int id)) return;

            MyAppointmentRow? row = _rows.Find(r => r.Id == id);
            if (row == null) return;

            VisitSummary summary;
            try
            {
                summary = _visits.GetSummary(row, _user.PatientId);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "The visit summary could not be loaded. Make sure visit_summary.sql was run.\n\n" + ex.Message,
                    "Connection problem", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            visit_view window = new visit_view(summary);
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

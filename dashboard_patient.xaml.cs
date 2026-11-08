using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ClinicaCentral
{
    public partial class dashboard_patient : Window
    {
        private readonly PatientUser _user;
        private readonly PatientDashboardRepository _repo;
        private readonly MedicalRecordRepository _records;

        public dashboard_patient(PatientUser user)
        {
            InitializeComponent();

            _user = user;
            DatabaseConnection db = new DatabaseConnection();
            _repo = new PatientDashboardRepository(db);
            _records = new MedicalRecordRepository(db);

            WelcomeText.Text = $"Welcome back, {user.Username}!";
            Loaded += (s, e) => LoadDashboard();
        }

        private static SolidColorBrush Hex(string hex) =>
            (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;

        private void ShowCondition(ConditionInfo info)
        {
            ConditionBand.Background = Hex(info.BackColor);
            ConditionText.Foreground = Hex(info.ForeColor);
            ConditionText.Text = info.Label;
            ConditionMessage.Text = info.Message;
        }

        private void LoadDashboard()
        {
            if (_user.PatientId <= 0)
            {
                MessageBox.Show(
                    "Your account is not linked to a patient record yet, so there is nothing to show. Please ask the clinic to link it.",
                    "ClinicaCentral", MessageBoxButton.OK, MessageBoxImage.Information);

                ShowCondition(ConditionInfo.None);
                AppointmentsEmpty.Visibility = Visibility.Visible;
                PrescriptionEmpty.Visibility = Visibility.Visible;
                HistoryEmpty.Visibility = Visibility.Visible;
                return;
            }

            try
            {
                PatientProfile? profile = _repo.GetProfile(_user.PatientId);
                if (profile != null)
                {
                    string first = profile.FirstName.Length == 0 ? _user.Username : profile.FirstName;
                    WelcomeText.Text = $"Welcome back, {first}!";
                }

                var appointments = _repo.GetUpcomingAppointments(_user.PatientId);
                AppointmentsList.ItemsSource = appointments;
                AppointmentsEmpty.Visibility = appointments.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                if (appointments.Count > 0)
                {
                    NextDateText.Text = appointments[0].When.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);
                    NextDoctorText.Text = appointments[0].DoctorText;
                }
                else
                {
                    NextDateText.Text = "None";
                    NextDoctorText.Text = "No upcoming visit";
                }

                PrescriptionCountText.Text = _repo.CountActivePrescriptions(_user.PatientId).ToString();
                TotalAppointmentsText.Text = _repo.CountTotalAppointments(_user.PatientId).ToString();

                ShowCondition(_repo.GetLatestCondition(_user.PatientId));

                var prescriptions = _repo.GetActivePrescriptions(_user.PatientId);
                PrescriptionList.ItemsSource = prescriptions;
                PrescriptionEmpty.Visibility = prescriptions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                List<MedicalRecordRow> all = _records.GetForPatient(_user.PatientId);
                List<MedicalRecordRow> recent = all.Count > 3 ? all.GetRange(0, 3) : all;
                HistoryList.ItemsSource = recent;
                HistoryEmpty.Visibility = recent.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Your dashboard could not be loaded. Make sure MySQL is running in Laragon and that the SQL scripts were run.\n\n" + ex.Message,
                    "Connection problem", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void EditAppointment_Click(object sender, RoutedEventArgs e)
        {
        }

        private void DeleteAppointment_Click(object sender, RoutedEventArgs e)
        {
        }

        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            string page = (sender as Button)?.Tag as string ?? "";

            if (page == "Dashboard") return;

            if (page == "Book Appointment")
            {
                new bk_patient(_user).Open();
                Close();
                return;
            }

            if (page == "My Appointment")
            {
                new my_patient(_user).Open();
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

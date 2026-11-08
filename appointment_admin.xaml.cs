using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ClinicaCentral
{
    public partial class appointment_admin : Window
    {
        private readonly User _user;
        private readonly AppointmentRepository _appointments;
        private readonly List<AppointmentRecord> _all = new List<AppointmentRecord>();
        private string _activeFilter = "All";

        public appointment_admin(User user)
        {
            InitializeComponent();

            _user = user;
            _appointments = new AppointmentRepository(new DatabaseConnection());

            lblWelcome.Text = _user.GetWelcomeMessage();
            Loaded += (s, e) => LoadAppointments();
        }

        private void LoadAppointments()
        {
            try
            {
                _all.Clear();
                _all.AddRange(_appointments.GetAll());
                ShowAppointments();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not load the appointments. Make sure MySQL is running in Laragon.\n\n" + ex.Message,
                    "Database error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowAppointments()
        {
            string search = SearchBox.Text.Trim();
            var shown = new List<AppointmentRecord>();

            foreach (AppointmentRecord item in _all)
            {
                bool matchesFilter = _activeFilter == "All" || item.HasStatus(_activeFilter);
                bool matchesSearch = search.Length == 0 ||
                    item.Patient.Contains(search, StringComparison.OrdinalIgnoreCase);

                if (matchesFilter && matchesSearch)
                    shown.Add(item);
            }

            AppointmentList.ItemsSource = shown;
            EmptyText.Visibility = shown.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateFilterCounts();
        }

        private int CountWithStatus(string status)
        {
            int count = 0;

            foreach (AppointmentRecord item in _all)
            {
                if (item.HasStatus(status))
                    count++;
            }
            return count;
        }

        private void UpdateFilterCounts()
        {
            AllFilter.Content = "All " + _all.Count;
            PendingFilter.Content = "Pending " + CountWithStatus("Pending");
            ConfirmedFilter.Content = "Confirmed " + CountWithStatus("Confirmed");
            CompletedFilter.Content = "Completed " + CountWithStatus("Completed");
            CancelledFilter.Content = "Cancelled " + CountWithStatus("Cancelled");
        }

        private Style GetPillStyle(string filter)
        {
            string key = _activeFilter == filter ? "FilterPillActive" : "FilterPill";
            return (Style)FindResource(key);
        }

        private void Filter_Click(object sender, RoutedEventArgs e)
        {
            if (sender == PendingFilter)
                _activeFilter = "Pending";
            else if (sender == ConfirmedFilter)
                _activeFilter = "Confirmed";
            else if (sender == CompletedFilter)
                _activeFilter = "Completed";
            else if (sender == CancelledFilter)
                _activeFilter = "Cancelled";
            else
                _activeFilter = "All";

            AllFilter.Style = GetPillStyle("All");
            PendingFilter.Style = GetPillStyle("Pending");
            ConfirmedFilter.Style = GetPillStyle("Confirmed");
            CompletedFilter.Style = GetPillStyle("Completed");
            CancelledFilter.Style = GetPillStyle("Cancelled");

            ShowAppointments();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ShowAppointments();
        }

        private void View_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is AppointmentRecord item)) return;

            MessageBox.Show(
                "Patient:   " + item.Patient + "\n" +
                "Doctor:    " + item.DoctorText + "\n" +
                "When:      " + item.WhenText + "\n" +
                "Status:    " + item.Status,
                "Appointment details", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CancelAppointment_Click(object sender, RoutedEventArgs e)
        {
        }

        private void Dashboard_Click(object sender, RoutedEventArgs e)
        {
            new dashboard_admin(_user).Open();
            Close();
        }

        private void ManageAccount_Click(object sender, RoutedEventArgs e)
        {
            new ma_admin(_user).Open();
            Close();
        }

        private void Receipts_Click(object sender, RoutedEventArgs e)
        {
            new receipt_admin(_user).Open();
            Close();
        }

        private void NavigationPlaceholder_Click(object sender, RoutedEventArgs e)
        {
        }

        private void Backup_Click(object sender, RoutedEventArgs e)
        {
            new backup_admin(_user).Open();
            Close();
        }

        private void LogOut_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult answer = MessageBox.Show("Log out of ClinicaCentral?", "Log Out",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes) return;

            new login_form().Open();
            Close();
        }
    }
}

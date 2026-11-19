using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

// commentttt

namespace ClinicaCentral
{
    public partial class dashboard_staff : Window
    {
        private readonly PatientQueue _waitingQueue = new PatientQueue();
        private readonly StatsRepository _stats;
        private readonly DashboardRepository _dashboard;
        private readonly AppointmentDetailsRepository _appointmentDetails;
        private readonly string _displayName;

        // Appointments already called with "Call Next" this session, so the
        // auto-refresh does not put them back in the queue.
        private readonly HashSet<int> _called = new HashSet<int>();
        private readonly DispatcherTimer _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };

        public dashboard_staff() : this("Doctor") { }

        public dashboard_staff(string displayName)
        {
            InitializeComponent();

            DatabaseConnection db = new DatabaseConnection();
            _stats = new StatsRepository(db);
            _dashboard = new DashboardRepository(db);
            _appointmentDetails = new AppointmentDetailsRepository(db);

            _displayName = displayName;
            WelcomeText.Text = $"Welcome back, {displayName}!";
            Loaded += (s, e) => LoadDashboard();

            // Queue refreshes by itself: new confirmations show up and patients
            // whose visit was completed drop out without reopening the dashboard.
            _refreshTimer.Tick += (s, e) => LoadDashboard(true);
            Loaded += (s, e) => _refreshTimer.Start();
            Closed += (s, e) => _refreshTimer.Stop();
        }

        private void LoadDashboard(bool quiet = false)
        {
            try
            {
                TotalPatientsText.Text = _stats.CountPatients().ToString();

                var appointments = _dashboard.GetTodayAppointments(_displayName);
                AppointmentsList.ItemsSource = appointments;
                AppointmentsEmpty.Visibility = appointments.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                _waitingQueue.Clear();
                foreach (var entry in _dashboard.GetWaitingPatients(_displayName))
                {
                    if (_called.Contains(entry.AppointmentId)) continue;
                    _waitingQueue.Add(entry);
                }
                RefreshQueue();

                var recent = _dashboard.GetRecentPatients(5, _displayName);
                RecentList.ItemsSource = recent;
                RecentEmpty.Visibility = recent.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

               TotalAppointmentsText.Text = appointments.Count.ToString();

            }
            catch (Exception ex)
            {
                if (quiet) return; // don't pop up a message every 10 seconds
                MessageBox.Show(
                    "The dashboard could not load its data. Make sure MySQL is running in Laragon.\n\n" + ex.Message,
                    "Connection problem", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ViewAppointment_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.DataContext is RecentPatientRow row))
                return;

            try
            {
                AppointmentDetails? details = _appointmentDetails.GetForDoctor(row.AppointmentId, _displayName);
                if (details == null)
                {
                    MessageBox.Show(
                        "This appointment could not be found or is no longer assigned to your account.",
                        "Appointment unavailable", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadDashboard();
                    return;
                }

                var dialog = new AppointmentDetailsWindow(details, true,
                    id => _appointmentDetails.ConfirmForDoctor(id, _displayName))
                {
                    Owner = this
                };

                if (dialog.ShowDialog() == true)
                {
                    MessageBox.Show("Appointment confirmed successfully.", "Appointment confirmed",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadDashboard();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not load this appointment. Please check your database connection and try again.\n\n" + ex.Message,
                    "Appointment error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void RefreshQueue()
        {
            var rows = _waitingQueue.GetNumberedList();

            QueueList.ItemsSource = rows;
            QueueEmpty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            InQueueText.Text = _waitingQueue.Count.ToString();
        }

        private void CallNext_Click(object sender, RoutedEventArgs e)
        {
            if (_waitingQueue.IsEmpty())
            {
                MessageBox.Show("There are no patients waiting.", "Queue is empty",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            QueueEntry next = _waitingQueue.CallNext();
            _called.Add(next.AppointmentId);
            RefreshQueue();
            MessageBox.Show($"Now serving {next.Name} ({next.PatientCode}).", "Next patient",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            string page = (sender as Button)?.Tag as string ?? "";
            if (page == "Dashboard") return;

            if (page == "Patient Records")
            {
                OpenPatientRecords();
                return;
            }

            if (page == "Medical Reports")
            {
                new mr_staff(_displayName).Open();
                Close();
                return;
            }

            if (page == "Schedule")
            {
                new sc_staff(_displayName).Open();
                Close();
                return;
            }

        }

        private void OpenPatientRecords()
        {
            new pr_staff(_displayName).Open();
            Close();
        }

        private void ViewPatient_Click(object sender, RoutedEventArgs e)
        {
            OpenPatientRecords();
        }

        private void EditPatient_Click(object sender, RoutedEventArgs e)
        {
        }

        private void DeletePatient_Click(object sender, RoutedEventArgs e)
        {
        }

        private void PrintReceipt_Click(object sender, RoutedEventArgs e)
        {
            receipt_picker.ShowFor(this, new StaffUser(_displayName));
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
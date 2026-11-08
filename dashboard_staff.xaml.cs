using System;
using System.Windows;
using System.Windows.Controls;

namespace ClinicaCentral
{
    public partial class dashboard_staff : Window
    {
        private readonly PatientQueue _waitingQueue = new PatientQueue();
        private readonly StatsRepository _stats;
        private readonly DashboardRepository _dashboard;
        private readonly string _displayName;

        public dashboard_staff() : this("Doctor") { }

        public dashboard_staff(string displayName)
        {
            InitializeComponent();

            DatabaseConnection db = new DatabaseConnection();
            _stats = new StatsRepository(db);
            _dashboard = new DashboardRepository(db);

            _displayName = displayName;
            WelcomeText.Text = $"Welcome back, {displayName}!";
            Loaded += (s, e) => LoadDashboard();
        }

        private void LoadDashboard()
        {
            try
            {
                TotalPatientsText.Text = _stats.CountPatients().ToString();

                var appointments = _dashboard.GetTodayAppointments();
                AppointmentsList.ItemsSource = appointments;
                AppointmentsEmpty.Visibility = appointments.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                _waitingQueue.Clear();
                foreach (var entry in _dashboard.GetWaitingPatients())
                    _waitingQueue.Add(entry);
                RefreshQueue();

                var recent = _dashboard.GetRecentPatients(5);
                RecentList.ItemsSource = recent;
                RecentEmpty.Visibility = recent.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                PendingLabText.Text = _dashboard.GetPendingLabCount().ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "The dashboard could not load its data. Make sure MySQL is running in Laragon.\n\n" + ex.Message,
                    "Connection problem", MessageBoxButton.OK, MessageBoxImage.Warning);
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

            if (page == "Medical Records")
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
            receipt_picker.ShowFor(this, _displayName);
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
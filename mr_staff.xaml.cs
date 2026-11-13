using System;
using System.Windows;
using System.Windows.Controls;

namespace ClinicaCentral
{
    public partial class mr_staff : Window
    {
        private readonly string _displayName;
        private readonly AdminUser? _adminUser;
        private readonly MedicalRecordRepository _records;
        private readonly System.Collections.Generic.List<MedicalRecordRow> _all =
            new System.Collections.Generic.List<MedicalRecordRow>();

        public mr_staff() : this("Doctor", null) { }

        public mr_staff(string displayName) : this(displayName, null) { }

        public mr_staff(AdminUser user) : this(user.Username, user) { }

        private mr_staff(string displayName, AdminUser? adminUser)
        {
            InitializeComponent();

            _records = new MedicalRecordRepository(new DatabaseConnection());
            _displayName = displayName;
            _adminUser = adminUser;
            WelcomeText.Text = _adminUser == null
                ? $"Welcome back, {displayName}!"
                : _adminUser.GetWelcomeMessage();
            StaffNavigationPanel.Visibility = _adminUser == null ? Visibility.Visible : Visibility.Collapsed;
            AdminNavigationPanel.Visibility = _adminUser == null ? Visibility.Collapsed : Visibility.Visible;
            Loaded += (s, e) => LoadRecords();
        }

        private void LoadRecords()
        {
            try
            {
                _all.Clear();
                _all.AddRange(_adminUser == null
                    ? _records.GetForDoctor(_displayName)
                    : _records.GetAll());
                foreach (MedicalRecordRow record in _all)
                    record.CanDelete = _adminUser != null;

                RecordList.ItemsSource = _all;
                EmptyText.Visibility = _all.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "The medical records could not be loaded. Make sure MySQL is running in Laragon and that medical_records.sql and medical_records_details.sql were both run.\n\n" + ex.Message,
                    "Connection problem", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void View_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is int id)) return;

            MedicalRecordRow? record = _all.Find(r => r.Id == id);
            if (record == null) return;

            mr_view window = new mr_view(record);
            window.Owner = this;
            window.ShowDialog();
        }

        private void Delete_Click(object sender, RoutedEventArgs e) { }

        private void AdminNav_Click(object sender, RoutedEventArgs e)
        {
            if (_adminUser == null) return;

            string page = (sender as Button)?.Tag as string ?? "";
            if (page == "Dashboard")
                new dashboard_admin(_adminUser).Open();
            else if (page == "Manage Account")
                new ma_admin(_adminUser).Open();
            else if (page == "Appointments")
                new appointment_admin(_adminUser).Open();
            else if (page == "Patient Records")
                new pr_staff(_adminUser).Open();
            else if (page == "Receipts")
                new receipt_admin(_adminUser).Open();
            else if (page == "Backup")
                new backup_admin(_adminUser).Open();
            else
                return;

            Close();
        }

        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            string page = (sender as Button)?.Tag as string ?? "";

            if (page == "Medical Records") return;

            if (page == "Dashboard")
            {
                new dashboard_staff(_displayName).Open();
                Close();
                return;
            }

            if (page == "Patient Records")
            {
                new pr_staff(_displayName).Open();
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

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ClinicaCentral
{
    public partial class pr_staff : Window
    {
        private const string DefaultEmptyMessage = "No patient records yet.";

        private readonly string _displayName;
        private readonly AdminUser? _adminUser;
        private readonly PatientRepository _patients;
        private readonly PatientDirectory _directory = new PatientDirectory();

        public pr_staff() : this("Doctor", null) { }

        public pr_staff(string displayName) : this(displayName, null) { }

        public pr_staff(AdminUser user) : this(user.Username, user) { }

        private pr_staff(string displayName, AdminUser? adminUser)
        {
            InitializeComponent();

            _patients = new PatientRepository(new DatabaseConnection());
            _displayName = displayName;
            _adminUser = adminUser;
            WelcomeText.Text = _adminUser == null
                ? $"Welcome back, {displayName}!"
                : _adminUser.GetWelcomeMessage();
            StaffNavigationPanel.Visibility = _adminUser == null ? Visibility.Visible : Visibility.Collapsed;
            AdminNavigationPanel.Visibility = _adminUser == null ? Visibility.Collapsed : Visibility.Visible;
            Loaded += (s, e) => LoadPatients();
        }

        private void LoadPatients()
        {
            try
            {
                _directory.Clear();

                List<PatientRow> patients = _adminUser != null
                    ? _patients.GetAll()
                    : _patients.GetForDoctor(_displayName);
                foreach (var patient in patients)
                    _directory.Add(patient);

                ShowRows(_directory.GetAll(), DefaultEmptyMessage);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "The patient records could not be loaded. Make sure MySQL is running in Laragon.\n\n" + ex.Message,
                    "Connection problem", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ShowRows(List<PatientRow> rows, string emptyMessage)
        {
            PatientList.ItemsSource = rows;
            EmptyText.Text = emptyMessage;
            EmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchHint.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

            if (SearchBox.Text.Length == 0)
                ShowRows(_directory.GetAll(), DefaultEmptyMessage);
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) Search_Click(sender, e);
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            string text = SearchBox.Text.Trim();

            if (text.Length == 0)
            {
                ShowRows(_directory.GetAll(), DefaultEmptyMessage);
                return;
            }

            if (!TryParsePatientId(text, out int id))
            {
                MessageBox.Show("Enter a patient ID such as PCC-0012 (or just 12).",
                    "Search", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_directory.TryFind(id, out PatientRow? found) && found != null)
                ShowRows(new List<PatientRow> { found }, DefaultEmptyMessage);
            else
                ShowRows(new List<PatientRow>(), $"No patient found with ID {PatientRow.FormatId(id)}.");
        }

        private static bool TryParsePatientId(string input, out int id)
        {
            string digits = input.Trim();
            if (digits.StartsWith("PCC", StringComparison.OrdinalIgnoreCase))
                digits = digits.Substring(3);
            digits = digits.TrimStart('-', ' ');

            return int.TryParse(digits, out id) && id > 0;
        }

        private void View_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn) || !(btn.Tag is int id)) return;

            try
            {
                PatientProfileRow? profile = _adminUser != null
                    ? _patients.GetPatientProfile(id)
                    : _patients.GetPatientProfileForStaff(id, _displayName);
                if (profile == null)
                {
                    MessageBox.Show("The selected patient profile could not be found.",
                        "Patient profile", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                PatientProfilePanel.DataContext = profile;
                ShowPatientPhoto(profile.Photo);
                PatientProfilePanel.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "The selected patient profile could not be loaded. Make sure MySQL is running in Laragon.\n\n" + ex.Message,
                    "Patient profile", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ShowPatientPhoto(byte[]? photoData)
        {
            if (photoData == null || photoData.Length == 0)
            {
                PatientPhotoCircle.Fill = Brushes.Transparent;
                PatientPhotoCircle.Visibility = Visibility.Collapsed;
                PatientAvatarGlyph.Visibility = Visibility.Visible;
                return;
            }

            ImageBrush brush = new ImageBrush(PhotoStore.FromBytes(photoData))
            {
                Stretch = Stretch.UniformToFill
            };
            PatientPhotoCircle.Fill = brush;
            PatientPhotoCircle.Visibility = Visibility.Visible;
            PatientAvatarGlyph.Visibility = Visibility.Collapsed;
        }

        private void ClosePatientProfile_Click(object sender, RoutedEventArgs e)
        {
            PatientProfilePanel.Visibility = Visibility.Collapsed;
            PatientProfilePanel.DataContext = null;
            ShowPatientPhoto(null);
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && PatientProfilePanel.Visibility == Visibility.Visible)
            {
                ClosePatientProfile_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
        }

        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            string page = (sender as Button)?.Tag as string ?? "";

            if (page == "Patient Records") return;

            if (page == "Dashboard")
            {
                new dashboard_staff(_displayName).Open();
                Close();
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
            else if (page == "Medical Records")
                new mr_staff(_adminUser).Open();
            else if (page == "Receipts")
                new receipt_admin(_adminUser).Open();
            else if (page == "Backup")
                new backup_admin(_adminUser).Open();
            else
                return;

            Close();
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
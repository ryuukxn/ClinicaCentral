using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace ClinicaCentral
{
    public partial class backup_admin : Window
    {
        private readonly User _user;
        private readonly BackupRepository _backups;
        private readonly PatientArchiveRepository _archive;
        private bool _loading;

        public backup_admin(User user)
        {
            InitializeComponent();

            _user = user;
            DatabaseConnection db = new DatabaseConnection();
            _backups = new BackupRepository(db);
            _archive = new PatientArchiveRepository(db);

            lblWelcome.Text = _user.GetWelcomeMessage();

            FrequencyBox.ItemsSource = new List<string> { "Daily", "Weekly", "Monthly" };
            TimeBox.ItemsSource = BuildTimeChoices();

            Loaded += (s, e) => LoadAll();
        }

        private static List<string> BuildTimeChoices()
        {
            var times = new List<string>();

            for (int hour = 0; hour < 24; hour++)
                times.Add(DateTime.Today.AddHours(hour).ToString("h:mm tt", CultureInfo.InvariantCulture));

            return times;
        }

        private void LoadAll()
        {
            try
            {
                _loading = true;
                BackupSettings settings = _backups.GetSettings();
                FrequencyBox.SelectedItem = settings.Frequency;
                TimeBox.SelectedIndex = Math.Clamp(settings.Hour, 0, 23);
                _loading = false;

                RefreshView();
            }
            catch (Exception ex)
            {
                _loading = false;
                MessageBox.Show(
                    "Could not load the backups. Make sure MySQL is running in Laragon.\n\n" + ex.Message,
                    "Database error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshView()
        {
            List<BackupRecord> all = _backups.GetAll();
            HistoryList.ItemsSource = all;
            EmptyText.Visibility = all.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            List<DeletedPatientRecord> deleted = _archive.GetDeleted();
            DeletedList.ItemsSource = deleted;
            EmptyDeletedText.Visibility = deleted.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            BackupRecord? last = null;
            foreach (BackupRecord item in all)
            {
                if (item.IsSuccessful)
                {
                    last = item;
                    break;
                }
            }

            if (last == null)
            {
                LastBadge.Visibility = Visibility.Collapsed;
                LastDateText.Text = "No backups yet";
                LastSizeText.Text = "—";
            }
            else
            {
                LastBadge.Visibility = Visibility.Visible;
                LastDateText.Text = last.DateText;
                LastSizeText.Text = last.SizeText;
            }

            UpdateNextBackup();
        }

        private BackupSettings ReadSettingsFromScreen()
        {
            BackupSettings settings = new BackupSettings();
            settings.Frequency = FrequencyBox.SelectedItem as string ?? "Daily";
            settings.Hour = TimeBox.SelectedIndex < 0 ? 2 : TimeBox.SelectedIndex;
            return settings;
        }

        private void UpdateNextBackup()
        {
            DateTime now = DateTime.Now;
            DateTime next = ReadSettingsFromScreen().GetNextRun(now, _backups.GetLastAutoRun());
            string time = next.ToString("h:mm tt", CultureInfo.InvariantCulture);

            if (next <= now)
                NextBackupText.Text = "Due now";
            else if (next.Date == now.Date)
                NextBackupText.Text = "Today - " + time;
            else if (next.Date == now.Date.AddDays(1))
                NextBackupText.Text = "Tomorrow - " + time;
            else
                NextBackupText.Text = next.ToString("MMM d, yyyy", CultureInfo.InvariantCulture) + " - " + time;
        }

        private void Settings_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || !IsLoaded) return;

            try
            {
                UpdateNextBackup();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not read the backup schedule.\n\n" + ex.Message,
                    "Database error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BackupNow_Click(object sender, RoutedEventArgs e)
        {
        }

        private void Download_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is BackupRecord item)) return;

            if (item.FilePath.Length == 0 || !File.Exists(item.FilePath))
            {
                MessageBox.Show("The backup file could not be found.\n\n" + item.FilePath,
                    "ClinicaCentral", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveFileDialog dialog = new SaveFileDialog();
            dialog.FileName = Path.GetFileName(item.FilePath);
            dialog.Filter = "SQL backup (*.sql)|*.sql";

            if (dialog.ShowDialog(this) != true) return;

            try
            {
                File.Copy(item.FilePath, dialog.FileName, true);
                MessageBox.Show("Backup saved.", "ClinicaCentral",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not save the file.\n\n" + ex.Message, "ClinicaCentral",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Restore_Click(object sender, RoutedEventArgs e)
        {
        }

        private void RestorePatient_Click(object sender, RoutedEventArgs e)
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

        private void Appointments_Click(object sender, RoutedEventArgs e)
        {
            new appointment_admin(_user).Open();
            Close();
        }

        private void MedicalRecords_Click(object sender, RoutedEventArgs e)
        {
            if (!(_user is AdminUser admin))
            {
                MessageBox.Show("Only admins can open medical records.", "Access denied",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            new mr_staff(admin).Open();
            Close();
        }

        private void PatientRecords_Click(object sender, RoutedEventArgs e)
        {
            if (!(_user is AdminUser admin))
            {
                MessageBox.Show("Only admins can open patient records.", "Access denied",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            new pr_staff(admin).Open();
            Close();
        }

        private void Receipts_Click(object sender, RoutedEventArgs e)
        {
            new receipt_admin(_user).Open();
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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ClinicaCentral
{
    public partial class backup_admin : Window
    {
        private readonly User _user;
        private readonly BackupRepository _backups;
        private readonly DeletedMedicalRecordRepository _deletedMedicalRecords;
        private readonly DispatcherTimer _backupTimer;
        private BackupSettings? _settings;
        private bool _loading;
        private bool _backupInProgress;

        public backup_admin(User user)
        {
            InitializeComponent();

            _user = user;
            DatabaseConnection db = new DatabaseConnection();
            _backups = new BackupRepository(db);
            _deletedMedicalRecords = new DeletedMedicalRecordRepository(db);

            lblWelcome.Text = _user.GetWelcomeMessage();

            FrequencyBox.ItemsSource = new List<string> { "Daily", "Weekly", "Monthly" };
            TimeBox.ItemsSource = BuildTimeChoices();

            // Scheduled backups are checked only while this window is open.
            _backupTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            _backupTimer.Tick += BackupTimer_Tick;
            Loaded += (s, e) =>
            {
                LoadBackupPage();
                _backupTimer.Start();
            };
            Closed += (s, e) => _backupTimer.Stop();
        }

        private static List<string> BuildTimeChoices()
        {
            var times = new List<string>();

            for (int minute = 0; minute < 24 * 60; minute += 15)
                times.Add(DateTime.Today.AddMinutes(minute).ToString("h:mm tt", CultureInfo.InvariantCulture));

            return times;
        }

        private void LoadBackupPage()
        {
            try
            {
                _loading = true;
                _settings = _backups.GetSettings();
                FrequencyBox.SelectedItem = _settings.Frequency;
                string selectedTime = FormatTime(_settings.BackupTime);
                if (!TimeBox.Items.Contains(selectedTime))
                    TimeBox.Items.Add(selectedTime);
                TimeBox.SelectedItem = selectedTime;
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

        private static string FormatTime(TimeSpan time)
        {
            return DateTime.Today.Add(time).ToString("h:mm tt", CultureInfo.InvariantCulture);
        }

        private void RefreshView()
        {
            List<BackupRecord> all = _backups.GetAll();
            HistoryList.ItemsSource = all;
            EmptyText.Visibility = all.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            List<DeletedMedicalRecordRow> deleted = _deletedMedicalRecords.GetDeleted();
            DeletedList.ItemsSource = deleted;
            EmptyDeletedText.Visibility = deleted.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            BackupRecord? last = _backups.GetLatestSuccess();

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

            _settings = _backups.GetSettings();
            UpdateNextBackup();
        }

        private BackupSettings ReadSettingsFromScreen()
        {
            string selectedTime = TimeBox.SelectedItem as string ?? "2:00 AM";
            DateTime parsedTime = DateTime.ParseExact(
                selectedTime, "h:mm tt", CultureInfo.InvariantCulture, DateTimeStyles.None);

            return new BackupSettings
            {
                Frequency = FrequencyBox.SelectedItem as string ?? "Daily",
                BackupTime = parsedTime.TimeOfDay,
                LastRun = _settings?.LastRun
            };
        }

        private void UpdateNextBackup()
        {
            if (_settings == null) return;

            DateTime now = DateTime.Now;
            if (_settings.IsDue(now))
            {
                NextBackupText.Text = "Due now";
                return;
            }

            DateTime next = _settings.GetNextRun(now);
            string time = FormatTime(next.TimeOfDay);

            if (next.Date == now.Date)
                NextBackupText.Text = "Today - " + time;
            else if (next.Date == now.Date.AddDays(1))
                NextBackupText.Text = "Tomorrow - " + time;
            else
                NextBackupText.Text = next.ToString("ddd, MMM d", CultureInfo.InvariantCulture) + " - " + time;
        }

        private void Settings_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || !IsLoaded) return;

            try
            {
                _backups.SaveSettings(ReadSettingsFromScreen());
                LoadBackupPage();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not save the backup schedule.\n\n" + ex.Message,
                    "Database error", MessageBoxButton.OK, MessageBoxImage.Error);
                LoadBackupPage();
            }
        }

        private void BackupNow_Click(object sender, RoutedEventArgs e)
        {
            if (_backupInProgress) return;

            SetBackupBusy(true);
            try
            {
                if (CreateBackup("Manual", _user.Username, out string error))
                    MessageBox.Show("Backup completed successfully.", "Backup",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                else
                    MessageBox.Show("The backup failed.\n\n" + error, "Backup",
                        MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetBackupBusy(false);
                LoadBackupPage();
            }
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
            if (!(sender is Button button) || !(button.Tag is BackupRecord item)) return;

            if (!item.IsSuccessful)
            {
                MessageBox.Show("Only successful backups can be restored.", "Restore backup",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!File.Exists(item.FilePath))
            {
                MessageBox.Show("The backup file could not be found.\n\n" + item.FilePath,
                    "Restore backup", MessageBoxButton.OK, MessageBoxImage.Warning);
                LoadBackupPage();
                return;
            }

            MessageBoxResult answer = MessageBox.Show(
                "Restoring this backup will replace ALL current database data. " +
                "A Pre-restore backup will be created first. Continue?",
                "Replace all current data", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes || _backupInProgress) return;

            SetBackupBusy(true);
            try
            {
                if (!CreateBackup("Pre-restore", _user.Username, out string preRestoreError))
                {
                    MessageBox.Show("The safety backup could not be created, so the restore was cancelled.\n\n" +
                                    preRestoreError,
                        "Restore backup", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                _backups.ImportFromFile(item.FilePath);
                MessageBox.Show("The backup was restored successfully.", "Restore backup",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("The backup could not be restored.\n\n" + ex.Message,
                    "Restore backup", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetBackupBusy(false);
                LoadBackupPage();
            }
        }

        private void RestorePatient_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is DeletedMedicalRecordRow record)) return;

            try
            {
                _deletedMedicalRecords.Restore(record.Id);
                LoadBackupPage();
            }
            catch (Exception ex)
            {
                MessageBox.Show("The medical record could not be restored.\n\n" + ex.Message,
                    "Deleted Medical Records", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BackupTimer_Tick(object? sender, EventArgs e)
        {
            if (_backupInProgress) return;

            try
            {
                _settings = _backups.GetSettings();
                if (!_settings.IsDue(DateTime.Now))
                {
                    UpdateNextBackup();
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not check the backup schedule.\n\n" + ex.Message,
                    "Scheduled backup", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            SetBackupBusy(true);
            try
            {
                bool succeeded = CreateBackup("Scheduled", "system", out string error);
                _backups.UpdateLastRun();
                if (!succeeded)
                    MessageBox.Show("The scheduled backup failed.\n\n" + error,
                        "Scheduled backup", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show("The scheduled backup could not be completed.\n\n" + ex.Message,
                    "Scheduled backup", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetBackupBusy(false);
                LoadBackupPage();
            }
        }

        private bool CreateBackup(string backupType, string createdBy, out string error)
        {
            string fileName = "clinicacentral_" + DateTime.Now.ToString("yyyyMMdd_HHmmss",
                CultureInfo.InvariantCulture) + ".sql";
            string filePath = Path.Combine(BackupRepository.BackupFolder, fileName);

            try
            {
                Directory.CreateDirectory(BackupRepository.BackupFolder);
                _backups.ExportToFile(filePath);

                BackupRecord record = new BackupRecord
                {
                    FileName = fileName,
                    FilePath = filePath,
                    BackupType = backupType,
                    SizeBytes = new FileInfo(filePath).Length,
                    Status = "Success",
                    CreatedBy = createdBy
                };
                _backups.InsertBackup(record);
                error = "";
                return true;
            }
            catch (Exception ex)
            {
                List<string> errors = new List<string> { ex.Message };
                try
                {
                    if (File.Exists(filePath))
                        File.Delete(filePath);
                }
                catch (Exception deleteException)
                {
                    errors.Add("The partial backup file could not be removed: " + deleteException.Message);
                }

                try
                {
                    _backups.InsertBackup(new BackupRecord
                    {
                        FileName = fileName,
                        FilePath = filePath,
                        BackupType = backupType,
                        SizeBytes = 0,
                        Status = "Failed",
                        CreatedBy = createdBy
                    });
                }
                catch (Exception logException)
                {
                    errors.Add("The failed backup could not be recorded: " + logException.Message);
                }

                error = string.Join(Environment.NewLine, errors);
                return false;
            }
        }

        private void SetBackupBusy(bool busy)
        {
            _backupInProgress = busy;
            BackupNowButton.IsEnabled = !busy;
            Mouse.OverrideCursor = busy ? Cursors.Wait : null;
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MySqlConnector;


namespace ClinicaCentral
{
    public partial class bk_patient : Window
    {
        private readonly PatientUser _user;
        private readonly BookingRepository _repo;
        private readonly PatientUploadRepository _uploads;
        private bool _syncing;

        public bk_patient(PatientUser user)
        {
            InitializeComponent();

            _user = user;
            DatabaseConnection db = new DatabaseConnection();
            _repo = new BookingRepository(db);
            _uploads = new PatientUploadRepository(db);

            WelcomeText.Text = $"Welcome back, {user.Username}!";

            DateBox.DisplayDateStart = DateTime.Today;
            DateBox.DisplayDateEnd = DateTime.Today.AddDays(180);

            Loaded += (s, e) =>
            {
                ShowFirstName(db);
                LoadDoctors();
                RefreshSlots();
                ShowExistingUpload();
            };
        }

        private void ShowFirstName(DatabaseConnection db)
        {
            if (_user.PatientId <= 0) return;

            try
            {
                PatientProfile? profile = new PatientDashboardRepository(db).GetProfile(_user.PatientId);
                if (profile != null && profile.FirstName.Length > 0)
                    WelcomeText.Text = $"Welcome back, {profile.FirstName}!";
            }
            catch
            {
            }
        }

        private void LoadDoctors()
        {
            var doctors = new List<DoctorOption>
            {
                new DoctorOption { IsPlaceholder = true, Name = "Select Doctor" }
            };

            try
            {
                using var conn = Database.GetConnection();
                conn.Open();

                string query = "SELECT full_name FROM staff WHERE status = 'On Duty' AND position = 'Doctor' ORDER BY full_name ASC";
                
                using var cmd = new MySqlCommand(query, conn);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    string doctorName = reader.GetString(0);
                    doctors.Add(new DoctorOption { IsPlaceholder = false, Name = doctorName });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "The list of doctors could not be loaded. Make sure MySQL is running in Laragon.\n\n" + ex.Message,
                    "Connection problem", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            DoctorBox.ItemsSource = doctors;
            DoctorBox.DisplayMemberPath = "Name";
            DoctorBox.SelectedIndex = 0;
        }

        private TimeSlot? SelectedSlot()
        {
            RadioButton? chosen = SlotPanel.Children.OfType<RadioButton>().FirstOrDefault(r => r.IsChecked == true);
            return chosen?.Tag as TimeSlot;
        }

        private void SetTimeItems(IEnumerable<TimeSlot> available)
        {
            _syncing = true;

            var items = new List<TimeSlot>
            {
                new TimeSlot { IsPlaceholder = true, PlaceholderText = "Select Time" }
            };
            items.AddRange(available);

            TimeBox.ItemsSource = items;
            TimeBox.DisplayMemberPath = "Label";
            TimeBox.SelectedIndex = 0;

            _syncing = false;
        }

        private void ShowHint(string text)
        {
            SlotHint.Text = text;
            SlotHint.Visibility = Visibility.Visible;
        }

        private void RefreshSlots()
        {
            SlotPanel.Children.Clear();
            SetTimeItems(new List<TimeSlot>());

            DoctorOption? doctor = DoctorBox.SelectedItem as DoctorOption;
            DateTime? date = DateBox.SelectedDate;

            if (doctor == null || doctor.IsPlaceholder || date == null)
            {
                ShowHint("Choose a doctor and a date to see the available times.");
                return;
            }

            if (!ClinicHours.IsOpenOn(date.Value))
            {
                ShowHint("The clinic is closed on Sundays. Please pick another date.");
                return;
            }

            List<TimeSlot> slots;
            try
            {
                HashSet<TimeSpan> booked = _repo.GetBookedTimes(doctor.Name, date.Value);
                slots = ClinicHours.BuildSlots(date.Value, booked, DateTime.Now);
            }
            catch (Exception ex)
            {
                MessageBox.Show("The available times could not be loaded.\n\n" + ex.Message,
                    "Connection problem", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            foreach (TimeSlot slot in slots)
            {
                RadioButton button = new RadioButton();
                button.Content = slot.Label;
                button.Tag = slot;
                button.GroupName = "slots";
                button.IsEnabled = slot.IsAvailable;
                button.Style = (Style)FindResource("SlotButton");
                button.Checked += Slot_Checked;
                SlotPanel.Children.Add(button);
            }

            List<TimeSlot> free = slots.Where(s => s.IsAvailable).ToList();
            SetTimeItems(free);

            if (free.Count == 0)
                ShowHint("No free times on this date. Please pick another day.");
            else
                SlotHint.Visibility = Visibility.Collapsed;
        }

        private void DoctorBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshSlots();
        }

        private void DateBox_SelectedDateChanged(object? sender, SelectionChangedEventArgs e)
        {
            RefreshSlots();
        }

        private void Slot_Checked(object sender, RoutedEventArgs e)
        {
            if (!(sender is RadioButton button) || !(button.Tag is TimeSlot slot)) return;

            _syncing = true;
            TimeSlot? match = TimeBox.Items.Cast<TimeSlot>().FirstOrDefault(s => !s.IsPlaceholder && s.Time == slot.Time);
            if (match != null) TimeBox.SelectedItem = match;
            _syncing = false;
        }

        private void TimeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_syncing) return;
            if (!(TimeBox.SelectedItem is TimeSlot slot) || slot.IsPlaceholder) return;

            foreach (RadioButton button in SlotPanel.Children.OfType<RadioButton>())
            {
                if (button.Tag is TimeSlot t && t.Time == slot.Time)
                {
                    button.IsChecked = true;
                    break;
                }
            }
        }

        private void NotesBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            NotesHint.Visibility = NotesBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ShowUpload(byte[] data, string fileName)
        {
            UploadPreview.Source = PhotoStore.FromBytes(data);
            UploadPlaceholder.Visibility = Visibility.Collapsed;
            UploadStatus.Text = "Uploaded: " + fileName + ". Your doctor can see it in your medical record.";
        }

        private void ShowExistingUpload()
        {
            if (_user.PatientId <= 0) return;

            try
            {
                PatientUpload? latest = _uploads.GetLatest(_user.PatientId);
                if (latest != null)
                    ShowUpload(latest.Data, latest.FileName);
            }
            catch
            {
            }
        }

        private void UploadImage_Click(object sender, RoutedEventArgs e)
        {
            if (_user.PatientId <= 0)
            {
                MessageBox.Show("Your account is not linked to a patient record, so an image cannot be uploaded.",
                    "Upload image", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "Choose an image to upload";
            dialog.Filter = "Pictures (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp";
            dialog.CheckFileExists = true;

            if (dialog.ShowDialog(this) != true) return;

            byte[] data;
            try
            {
                data = UploadStore.Prepare(dialog.FileName);
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Upload image", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            catch (Exception)
            {
                MessageBox.Show("That file could not be read as a picture. Please choose a JPG, PNG or BMP image.",
                    "Upload image", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string fileName = Path.GetFileName(dialog.FileName);

            try
            {
                _uploads.Save(_user.PatientId, fileName, data);
                ShowUpload(data, fileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show("The image could not be uploaded. Make sure MySQL is running in Laragon.\n\n" + ex.Message,
                    "Upload image", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Book_Click(object sender, RoutedEventArgs e)
        {
            if (_user.PatientId <= 0)
            {
                MessageBox.Show("Your account is not linked to a patient record.", "Book Appointment", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DoctorOption? doctor = DoctorBox.SelectedItem as DoctorOption;
            if (doctor == null || doctor.IsPlaceholder)
            {
                MessageBox.Show("Please select a doctor.", "Book Appointment", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DateTime? date = DateBox.SelectedDate;
            if (date == null)
            {
                MessageBox.Show("Please select a date.", "Book Appointment", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TimeSlot? slot = TimeBox.SelectedItem as TimeSlot;
            if (slot == null || slot.IsPlaceholder)
            {
                slot = SelectedSlot();
            }

            if (slot == null)
            {
                MessageBox.Show("Please select a time slot.", "Book Appointment", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DateTime appointmentDateTime = date.Value.Date + slot.Time;
            string notes = NotesBox.Text.Trim();

            try
            {
                using var conn = Database.GetConnection();
                conn.Open();

                int staffId = 0;
                string getStaffIdQuery = "SELECT id FROM staff WHERE full_name = @docName LIMIT 1";
                using (var cmdStaff = new MySqlCommand(getStaffIdQuery, conn))
                {
                    cmdStaff.Parameters.AddWithValue("@docName", doctor.Name);
                    var result = cmdStaff.ExecuteScalar();
                    if (result != null)
                    {
                        staffId = Convert.ToInt32(result);
                    }
                }

                if (staffId <= 0)
                {
                    MessageBox.Show("Could not find the selected doctor in the database.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string query = "INSERT INTO appointments (patient_id, staff_id, appointment_date, status, notes, doctor) " +
                            "VALUES (@pid, @sid, @adate, 'Pending', @notes, @docName)";

                using var cmd = new MySqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@pid", _user.PatientId);
                cmd.Parameters.AddWithValue("@sid", staffId);
                cmd.Parameters.AddWithValue("@adate", appointmentDateTime);
                cmd.Parameters.AddWithValue("@notes", string.IsNullOrEmpty(notes) ? (object)DBNull.Value : notes);
                cmd.Parameters.AddWithValue("@docName", doctor.Name);

                cmd.ExecuteNonQuery();

                MessageBox.Show("Successfully booked your appointment!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                GoToDashboard();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to save the appointment. Please ensure Laragon MySQL is running.\n\n" + ex.Message,
                    "Server error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            GoToDashboard();
        }

        private void GoToDashboard()
        {
            new dashboard_patient(_user).Open();
            Close();
        }

        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            string page = (sender as Button)?.Tag as string ?? "";

            if (page == "Book Appointment") return;

            if (page == "Dashboard")
            {
                GoToDashboard();
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

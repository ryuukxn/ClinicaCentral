using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MySqlConnector;

namespace ClinicaCentral
{
    public partial class UpdateAppointmentWindow : Window
    {
        private readonly PatientUser _user;
        private readonly int _appointmentId;
        private readonly BookingRepository _appointments;
        private EditableAppointment? _appointment;
        private bool _syncing;

        public UpdateAppointmentWindow(PatientUser user, int appointmentId)
        {
            InitializeComponent();

            _user = user;
            _appointmentId = appointmentId;
            _appointments = new BookingRepository(new DatabaseConnection());
            PatientWelcome.Text = $"Welcome back, {user.Username}!";
            DateBox.DisplayDateStart = DateTime.Today;
            Loaded += (s, e) => 
            {
                LoadAppointment();
                RefreshSlots();
            };
        }

        private void LoadAppointment()
        {
            try
            {
                _appointment = _appointments.GetForEdit(_appointmentId, _user.PatientId);
                if (_appointment == null)
                {
                    MessageBox.Show(
                        "This appointment is no longer available to update.",
                        "Update appointment", MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                    return;
                }

                DateBox.DisplayDateEnd = _appointment.When.Date > DateTime.Today.AddDays(180)
                    ? _appointment.When.Date
                    : DateTime.Today.AddDays(180);

                List<DoctorOption> doctors = _appointments.GetDoctors();
                DoctorBox.ItemsSource = doctors;
                DoctorBox.DisplayMemberPath = "Name";
                DoctorBox.SelectedItem = doctors.Find(d => d.Id == _appointment.DoctorId);

                if (DoctorBox.SelectedItem == null && doctors.Count > 0)
                    DoctorBox.SelectedIndex = 0;

                DateBox.SelectedDate = _appointment.When.Date;
                NotesBox.Text = _appointment.Notes;

                RefreshSlots();
                SelectCurrentTime();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "The appointment could not be loaded. Make sure MySQL is running in Laragon.\n\n" + ex.Message,
                    "Connection problem", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
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
            return SlotPanel.Children.OfType<RadioButton>()
                .FirstOrDefault(button => button.IsChecked == true)?.Tag as TimeSlot;
        }

        private void RefreshSlots()
        {
            SlotPanel.Children.Clear();
            _syncing = true;
            TimeBox.ItemsSource = null;
            _syncing = false;

            DoctorOption? doctor = DoctorBox.SelectedItem as DoctorOption;
            DateTime? date = DateBox.SelectedDate;
            if (doctor == null || date == null)
            {
                ShowHint("Choose a doctor and date to see available times.");
                return;
            }

            if (!ClinicHours.IsOpenOn(date.Value))
            {
                ShowHint("The clinic is closed on Sundays. Please choose another date.");
                return;
            }

            try
            {
                var booked = _appointments.GetBookedTimes(doctor.Name, date.Value, _appointmentId);
                List<TimeSlot> slots = ClinicHours.BuildSlots(date.Value, booked, DateTime.Now);
                foreach (TimeSlot slot in slots)
                {
                    RadioButton button = new RadioButton
                    {
                        Content = slot.Label,
                        Tag = slot,
                        GroupName = "appointmentSlots",
                        IsEnabled = slot.IsAvailable,
                        Style = (Style)FindResource("SlotButton")
                    };
                    button.Checked += Slot_Checked;
                    SlotPanel.Children.Add(button);
                }

                List<TimeSlot> available = slots.FindAll(slot => slot.IsAvailable);
                _syncing = true;
                TimeBox.ItemsSource = available;
                TimeBox.DisplayMemberPath = "Label";
                TimeBox.SelectedIndex = -1;
                _syncing = false;

                ShowHint(available.Count == 0
                    ? "No available times for this doctor on the selected date."
                    : "Select one of the available times.");
            }
            catch (Exception ex)
            {
                ShowHint("Available times could not be loaded.");
                MessageBox.Show(
                    "Available times could not be loaded. Make sure MySQL is running in Laragon.\n\n" + ex.Message,
                    "Connection problem", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowHint(string text)
        {
            SlotHint.Text = text;
            SlotHint.Visibility = Visibility.Visible;
        }

        private void SelectCurrentTime()
        {
            if (_appointment == null) return;

            TimeSpan time = _appointment.When.TimeOfDay;
            foreach (RadioButton button in SlotPanel.Children.OfType<RadioButton>())
            {
                if (button.Tag is TimeSlot slot && slot.Time == time && slot.IsAvailable)
                {
                    button.IsChecked = true;
                    break;
                }
            }
        }

        private void DoctorBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_appointment != null) RefreshSlots();
        }

        private void DateBox_SelectedDateChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_appointment != null) RefreshSlots();
        }

        private void Slot_Checked(object sender, RoutedEventArgs e)
        {
            if (!(sender is RadioButton button) || !(button.Tag is TimeSlot slot)) return;

            _syncing = true;
            TimeBox.SelectedItem = TimeBox.Items.Cast<TimeSlot>()
                .FirstOrDefault(item => item.Time == slot.Time);
            _syncing = false;
        }

        private void TimeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_syncing || !(TimeBox.SelectedItem is TimeSlot selected)) return;

            foreach (RadioButton button in SlotPanel.Children.OfType<RadioButton>())
            {
                if (button.Tag is TimeSlot slot && slot.Time == selected.Time)
                {
                    button.IsChecked = true;
                    break;
                }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            DoctorOption? doctor = DoctorBox.SelectedItem as DoctorOption;
            if (doctor == null)
            {
                MessageBox.Show("Please select a doctor.", "Update appointment",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (DateBox.SelectedDate == null)
            {
                MessageBox.Show("Please select a date.", "Update appointment",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TimeSlot? slot = TimeBox.SelectedItem as TimeSlot ?? SelectedSlot();
            if (slot == null)
            {
                MessageBox.Show("Please select an available time.", "Update appointment",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_user.PatientId <= 0)
            {
                MessageBox.Show("Your account is not linked to a patient record.",
                    "Update appointment", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                bool updated = _appointments.UpdateAppointment(
                    _appointmentId,
                    _user.PatientId,
                    doctor.Id,
                    DateBox.SelectedDate.Value.Date + slot.Time,
                    NotesBox.Text.Trim());

                if (!updated)
                {
                    MessageBox.Show(
                        "This appointment is no longer available to update. Refresh your appointments and try again.",
                        "Update appointment", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                MessageBox.Show("Your appointment has been updated.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Update appointment",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                RefreshSlots();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "The appointment could not be updated. Make sure MySQL is running in Laragon.\n\n" + ex.Message,
                    "Database error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}

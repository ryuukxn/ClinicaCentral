using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ClinicaCentral
{
    public partial class sc_staff : Window
    {
        private readonly string _displayName;
        private readonly ScheduleRepository _schedule;
        private readonly System.Collections.Generic.List<UIElement> _cells =
            new System.Collections.Generic.List<UIElement>();
        private AppointmentCompletionContext? _completionContext;

        public sc_staff() : this("Doctor") { }

        public sc_staff(string displayName)
        {
            InitializeComponent();

            _schedule = new ScheduleRepository(new DatabaseConnection());
            _displayName = displayName;

            WelcomeText.Text = $"Welcome back, {displayName}!";
            HealthyRadio.Checked += CompletionType_Changed;
            NeedsPrescriptionRadio.Checked += CompletionType_Changed;
            SetCompletionMode();
            Loaded += (s, e) => LoadSchedule();
        }

        private void LoadSchedule()
        {
            try
            {
                DrawAvailability(_schedule.GetAvailability(_displayName));

                var timeOff = _schedule.GetTimeOff(_displayName);
                TimeOffList.ItemsSource = timeOff;
                TimeOffEmpty.Visibility = timeOff.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                var requests = _schedule.GetBookingRequests();
                RequestList.ItemsSource = requests;
                PendingText.Text = requests.Count + " pending";
                RequestsEmpty.Visibility = requests.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                var confirmed = _schedule.GetConfirmed(_displayName);
                ConfirmedList.ItemsSource = confirmed;
                ConfirmedCount.Text = confirmed.Count.ToString();
                ConfirmedEmpty.Visibility = confirmed.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "The schedule could not be loaded. Make sure MySQL is running in Laragon and that schedule.sql was run.\n\n" + ex.Message,
                    "Connection problem", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static SolidColorBrush Hex(string hex) =>
            (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;

        private void DrawAvailability(WeeklyAvailability week)
        {
            foreach (UIElement old in _cells)
            AvailabilityGrid.Children.Remove(old);
            _cells.Clear();

            for (int slot = 0; slot < 2; slot++)
            {
                for (int day = 0; day < 6; day++)
                {
                    string status = week.Get(slot, day);

                    string back = "#E3E8F0", fore = "#6B7A90";
                    if (string.Equals(status, "Open", StringComparison.OrdinalIgnoreCase))
                    {
                        back = "#D9F7E3"; fore = "#1E9E4B";
                    }
                    else if (string.Equals(status, "Busy", StringComparison.OrdinalIgnoreCase))
                    {
                        back = "#FFF0C2"; fore = "#B7791F";
                    }

                    Button cellButton = new Button
                    {
                        Tag = new Tuple<int, int, string>(slot, day, status),
                        Cursor = System.Windows.Input.Cursors.Hand,
                        Margin = new Thickness(3),
                        Template = (ControlTemplate)FindResource("AvailabilityCellTemplate"),
                        Content = status
                    };

                    cellButton.Background = Hex(back);
                    cellButton.Foreground = Hex(fore);
                    cellButton.Click += Cell_Click;

                    Grid.SetRow(cellButton, slot + 1);
                    Grid.SetColumn(cellButton, day + 1);
                    AvailabilityGrid.Children.Add(cellButton);
                    _cells.Add(cellButton);
                }
            }
        }

        private void Cell_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Tuple<int, int, string> info)
            {
                int slot = info.Item1;
                int day = info.Item2;
                string currentStatus = info.Item3;

                string newStatus = string.Equals(currentStatus, "Open", StringComparison.OrdinalIgnoreCase) ? "Off" : "Open";

                try
                {
                    _schedule.UpdateAvailability(_displayName, slot, day, newStatus);
                    LoadSchedule();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Hindi na-update ang availability:\n\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void AddTimeOff_Click(object sender, RoutedEventArgs e)
        {
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int appointmentId)
            {
                try
                {
                    _schedule.UpdateAppointmentStatus(appointmentId, "Confirmed");
                    LoadSchedule();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Can't confirm appointment:\n\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Decline_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int appointmentId)
            {
                try
                {
                    _schedule.UpdateAppointmentStatus(appointmentId, "Cancelled");
                    LoadSchedule();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Hindi na-decline ang appointment:\n\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
                }

        private void Complete_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn) ||
                !(btn.DataContext is ConfirmedRow appointment) ||
                appointment.PatientId <= 0)
            {
                MessageBox.Show("The selected appointment does not have a valid patient record.",
                    "Unable to complete", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _completionContext = new AppointmentCompletionContext
            {
                AppointmentId = appointment.Id,
                PatientId = appointment.PatientId,
                PatientName = appointment.Patient,
                DoctorName = _displayName
            };
            CompletionPanel.DataContext = _completionContext;
            ClearCompletionInputs();
            HealthyRadio.IsChecked = true;
            SetCompletionMode();
            CompletionOverlay.Visibility = Visibility.Visible;
        }

        private void CompletionType_Changed(object sender, RoutedEventArgs e)
        {
            if (NeedsPrescriptionRadio.IsChecked == true &&
                string.Equals(DiagnosisInput.Text.Trim(), "Normal / Healthy", StringComparison.OrdinalIgnoreCase))
                DiagnosisInput.Clear();
            else if (HealthyRadio.IsChecked == true && string.IsNullOrWhiteSpace(DiagnosisInput.Text))
                DiagnosisInput.Text = "Normal / Healthy";

            SetCompletionMode();
        }

        private void SetCompletionMode()
        {
            bool healthy = HealthyRadio.IsChecked == true;
            PrescriptionFields.IsEnabled = !healthy;
        }

        private void SaveComplete_Click(object sender, RoutedEventArgs e)
        {
            if (_completionContext == null) return;

            bool needsPrescription = NeedsPrescriptionRadio.IsChecked == true;
            string diagnosis = DiagnosisInput.Text.Trim();
            if (diagnosis.Length == 0)
            {
                ShowCompletionValidation("Enter a diagnosis.");
                DiagnosisInput.Focus();
                return;
            }

            string medicine = MedicineInput.Text.Trim();
            string dosage = DosageInput.Text.Trim();
            string frequency = FrequencyInput.Text.Trim();
            string duration = DurationInput.Text.Trim();
            if (needsPrescription)
            {
                if (medicine.Length == 0 || dosage.Length == 0 ||
                    frequency.Length == 0 || duration.Length == 0)
                {
                    ShowCompletionValidation("Complete the medicine, dosage, frequency, and duration fields.");
                    return;
                }
            }

            decimal? temperature = null;
            decimal? weightKg = null;
            int? pulse = null;
            if (!TryReadDecimal(TemperatureInput, "Temperature", 999.9m, out temperature) ||
                !TryReadDecimal(WeightInput, "Weight", 9999.9m, out weightKg) ||
                !TryReadPulse(out pulse))
                return;

            AppointmentCompletion completion = new AppointmentCompletion
            {
                AppointmentId = _completionContext.AppointmentId,
                PatientId = _completionContext.PatientId,
                Doctor = _completionContext.DoctorName,
                Diagnosis = diagnosis,
                Notes = OptionalText(NotesInput),
                Bp = OptionalText(BpInput),
                Temperature = temperature,
                Pulse = pulse,
                WeightKg = weightKg,
                Treatment = OptionalText(TreatmentInput),
                NeedsPrescription = needsPrescription,
                MedicineName = medicine,
                Dosage = dosage,
                Frequency = frequency,
                Duration = duration,
                Description = needsPrescription ? OptionalText(PrescriptionDescriptionInput) : null
            };

            try
            {
                _schedule.CompleteAppointment(completion);
                CloseCompletionPanel();
                LoadSchedule();
            }
            catch (Exception ex)
            {
                MessageBox.Show("The appointment could not be completed:\n\n" + ex.Message,
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool TryReadDecimal(TextBox input, string label, decimal maximum, out decimal? value)
        {
            value = null;
            string text = input.Text.Trim();
            if (text.Length == 0) return true;

            if (!decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out decimal parsed) ||
                parsed < 0 || parsed > maximum || decimal.Round(parsed, 1) != parsed)
            {
                ShowCompletionValidation(label + " must be a non-negative number with at most one decimal place.");
                input.Focus();
                return false;
            }

            value = parsed;
            return true;
        }

        private bool TryReadPulse(out int? pulse)
        {
            pulse = null;
            string text = PulseInput.Text.Trim();
            if (text.Length == 0) return true;

            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) || parsed < 0)
            {
                ShowCompletionValidation("Pulse must be a non-negative whole number.");
                PulseInput.Focus();
                return false;
            }

            pulse = parsed;
            return true;
        }

        private static string? OptionalText(TextBox input)
        {
            string text = input.Text.Trim();
            return text.Length == 0 ? null : text;
        }

        private static void ShowCompletionValidation(string message)
        {
            MessageBox.Show(message, "Check the form", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void CancelCompletion_Click(object sender, RoutedEventArgs e)
        {
            CloseCompletionPanel();
        }

        private void CloseCompletionPanel()
        {
            CompletionOverlay.Visibility = Visibility.Collapsed;
            CompletionPanel.DataContext = null;
            _completionContext = null;
            ClearCompletionInputs();
            HealthyRadio.IsChecked = true;
            SetCompletionMode();
        }

        private void ClearCompletionInputs()
        {
            DiagnosisInput.Text = "Normal / Healthy";
            NotesInput.Clear();
            BpInput.Clear();
            TemperatureInput.Clear();
            PulseInput.Clear();
            WeightInput.Clear();
            TreatmentInput.Clear();
            MedicineInput.Clear();
            DosageInput.Clear();
            FrequencyInput.Clear();
            DurationInput.Clear();
            PrescriptionDescriptionInput.Clear();
        }

        private sealed class AppointmentCompletionContext
        {
            public int AppointmentId { get; set; }
            public int PatientId { get; set; }
            public string PatientName { get; set; } = "";
            public string DoctorName { get; set; } = "";
        }

        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            string page = (sender as Button)?.Tag as string ?? "";

            if (page == "Schedule") return;

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

            if (page == "Medical Records")
            {
                new mr_staff(_displayName).Open();
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

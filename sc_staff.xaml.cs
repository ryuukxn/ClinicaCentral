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

        public sc_staff() : this("Doctor") { }

        public sc_staff(string displayName)
        {
            InitializeComponent();

            _schedule = new ScheduleRepository(new DatabaseConnection());
            _displayName = displayName;

            WelcomeText.Text = $"Welcome back, {displayName}!";
            TodayTitle.Text = "Today \u00B7 " + DateTime.Today.ToString("ddd, MMM d", CultureInfo.InvariantCulture);
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

                var today = _schedule.GetToday();
                TodayList.ItemsSource = today;
                TodayEmpty.Visibility = today.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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
                int slot = info.Item1; // 0 o 1
                int day = info.Item2;  // 0 hanggang 5 (Mon-Sat)
                string currentStatus = info.Item3;

                // Toggle: Kung Open, gawing Off. Kung Off o Busy, gawing Open.
                string newStatus = string.Equals(currentStatus, "Open", StringComparison.OrdinalIgnoreCase) ? "Off" : "Open";

                try
                {
                    _schedule.UpdateAvailability(_displayName, slot, day, newStatus);
                    LoadSchedule(); // I-reload ang UI para makita ang pagbabago ng kulay
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
        }

        private void Decline_Click(object sender, RoutedEventArgs e)
        {
        }

        private void Complete_Click(object sender, RoutedEventArgs e)
        {
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

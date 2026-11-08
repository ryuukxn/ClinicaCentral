using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ClinicaCentral
{
    public partial class dashboard_admin : Window
    {
        private readonly User _user;
        private readonly StatsRepository _stats;
        private readonly StaffRepository _staff;
        private int[] _week = new int[7];

        public dashboard_admin(User user)
        {
            InitializeComponent();

            DatabaseConnection db = new DatabaseConnection();
            _user = user;
            _stats = new StatsRepository(db);
            _staff = new StaffRepository(db);

            lblWelcome.Text = _user.GetWelcomeMessage();
            Loaded += (s, e) => LoadData();
        }

        private void LoadData()
        {
            try
            {
                txtPatients.Text = _stats.CountPatients().ToString("N0");
                txtStaff.Text = _stats.CountStaff().ToString("N0");
                txtAppointments.Text = _stats.CountAppointments().ToString("N0");

                _week = _stats.GetWeeklyAppointments();

                var staff = _staff.GetStaffList();
                StaffList.ItemsSource = staff;
                lblNoStaff.Visibility = staff.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                DrawChart();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not load dashboard data:\n\n" + ex.Message,
                    "Database error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static SolidColorBrush Hex(string hex) =>
            (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;

        private void ChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawChart();

        private void DrawChart()
        {
            ChartCanvas.Children.Clear();
            double w = ChartCanvas.ActualWidth, h = ChartCanvas.ActualHeight;
            if (w < 100 || h < 80) return;

            const double leftPad = 42, bottomPad = 26, topPad = 10;
            double plotH = h - bottomPad - topPad;
            double plotW = w - leftPad;

            int max = 0;
            foreach (int v in _week) if (v > max) max = v;

            int step = max <= 10 ? 2 : max <= 50 ? 10 : max <= 100 ? 20 : max <= 500 ? 50 : 100;
            int axisMax = Math.Max(step * 5, (int)Math.Ceiling(max / (double)step) * step);

            for (int v = 0; v <= axisMax; v += step)
            {
                double y = topPad + plotH - (v / (double)axisMax) * plotH;

                ChartCanvas.Children.Add(new Line
                {
                    X1 = leftPad, X2 = w, Y1 = y, Y2 = y,
                    Stroke = Hex("#E3E3E3"), StrokeThickness = 1
                });

                var label = new TextBlock
                {
                    Text = v.ToString(), Width = leftPad - 8,
                    TextAlignment = TextAlignment.Right, FontSize = 12, Foreground = Hex("#222222")
                };
                Canvas.SetLeft(label, 0);
                Canvas.SetTop(label, y - 9);
                ChartCanvas.Children.Add(label);
            }

            string[] days = { "M", "T", "W", "T", "F", "S", "S" };
            double slot = plotW / 7;
            double barW = slot * 0.7;

            for (int i = 0; i < 7; i++)
            {
                double barH = _week[i] / (double)axisMax * plotH;

                var bar = new Rectangle
                {
                    Width = barW, Height = Math.Max(barH, 0),
                    Fill = Hex("#4A7DF5"), RadiusX = 3, RadiusY = 3
                };
                Canvas.SetLeft(bar, leftPad + i * slot + (slot - barW) / 2);
                Canvas.SetTop(bar, topPad + plotH - barH);
                ChartCanvas.Children.Add(bar);

                var day = new TextBlock
                {
                    Text = days[i], Width = slot, TextAlignment = TextAlignment.Center, FontSize = 12
                };
                Canvas.SetLeft(day, leftPad + i * slot);
                Canvas.SetTop(day, topPad + plotH + 4);
                ChartCanvas.Children.Add(day);
            }
        }

        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            string page = (sender as Button)?.Content?.ToString() ?? "This";

            if (page == "Manage Account")
            {
                new ma_admin(_user).Open();
                Close();
                return;
            }

            if (page == "Appointments")
            {
                new appointment_admin(_user).Open();
                Close();
                return;
            }

            if (page == "Receipts")
            {
                new receipt_admin(_user).Open();
                Close();
                return;
            }

        }

        private void PrintReceipt_Click(object sender, RoutedEventArgs e)
        {
            receipt_picker.ShowFor(this);
        }

        private void Backup_Click(object sender, RoutedEventArgs e)
        {
            new backup_admin(_user).Open();
            Close();
        }

        private void LogOut_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Log out of ClinicaCentral?", "Log Out",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            new login_form().Open();
            Close();
        }
    }
}
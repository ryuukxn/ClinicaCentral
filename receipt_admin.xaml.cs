using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ClinicaCentral
{
    public partial class receipt_admin : Window
    {
        private readonly User _user;
        private readonly ReceiptRepository _receipts;
        private readonly List<ReceiptRecord> _all = new List<ReceiptRecord>();
        private string _activeFilter = "All";

        public receipt_admin(User user)
        {
            InitializeComponent();

            _user = user;
            if (!(_user is AdminUser))
                throw new UnauthorizedAccessException("Only admins can open the receipts management page.");

            _receipts = new ReceiptRepository(new DatabaseConnection());

            lblWelcome.Text = _user.GetWelcomeMessage();
            Loaded += (s, e) => LoadReceipts();
        }

        private void LoadReceipts()
        {
            try
            {
                _all.Clear();
                _all.AddRange(_receipts.GetAll(_user));
                ShowReceipts();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not load the receipts. Make sure MySQL is running in Laragon.\n\n" + ex.Message,
                    "Database error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowReceipts()
        {
            string search = SearchBox.Text.Trim();
            var shown = new List<ReceiptRecord>();

            foreach (ReceiptRecord item in _all)
            {
                bool matchesFilter = _activeFilter == "All" || item.HasStatus(_activeFilter);
                bool matchesSearch = search.Length == 0 ||
                    item.Patient.Contains(search, StringComparison.OrdinalIgnoreCase);

                if (matchesFilter && matchesSearch)
                    shown.Add(item);
            }

            ReceiptList.ItemsSource = shown;
            EmptyText.Visibility = shown.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateFilterCounts();
        }

        private int CountWithStatus(string status)
        {
            int count = 0;

            foreach (ReceiptRecord item in _all)
            {
                if (item.HasStatus(status))
                    count++;
            }
            return count;
        }

        private void UpdateFilterCounts()
        {
            AllFilter.Content = "All " + _all.Count;
            IssuedFilter.Content = "Issued " + CountWithStatus("Issued");
            PrintedFilter.Content = "Printed " + CountWithStatus("Printed");
        }

        private Style GetPillStyle(string filter)
        {
            string key = _activeFilter == filter ? "FilterPillActive" : "FilterPill";
            return (Style)FindResource(key);
        }

        private void Filter_Click(object sender, RoutedEventArgs e)
        {
            if (sender == IssuedFilter)
                _activeFilter = "Issued";
            else if (sender == PrintedFilter)
                _activeFilter = "Printed";
            else
                _activeFilter = "All";

            AllFilter.Style = GetPillStyle("All");
            IssuedFilter.Style = GetPillStyle("Issued");
            PrintedFilter.Style = GetPillStyle("Printed");

            ShowReceipts();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ShowReceipts();
        }

        private void View_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is ReceiptRecord item)) return;

            try
            {
                receipt_preview preview = new receipt_preview(item.Id, _user);
                preview.Owner = this;
                if (preview.ShowDialog() == true)
                    LoadReceipts();
            }
            catch (Exception ex)
            {
                MessageBox.Show("The receipt could not be opened.\n\n" + ex.Message,
                    "Receipt access", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

        private void NavigationPlaceholder_Click(object sender, RoutedEventArgs e)
        {
        }

        private void Backup_Click(object sender, RoutedEventArgs e)
        {
            new backup_admin(_user).Open();
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

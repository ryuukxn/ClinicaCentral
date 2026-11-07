using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ClinicaCentral
{
    public partial class ma_admin : Window
    {
        private readonly User _user;
        private readonly UserAccountRepository _accounts;
        private string _activeFilter = "All";

        public ma_admin(User user)
        {
            InitializeComponent();

            _user = user;
            _accounts = new UserAccountRepository(new DatabaseConnection());

            try
            {
                RefreshAccounts();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not connect to the database.\n\n" + ex.Message,
                    "Database error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void RefreshAccounts()
        {
            try
            {
                var accounts = _accounts.GetAccounts();

                if (_activeFilter != "All")
                    accounts = accounts
                        .Where(x => x.Role.Equals(_activeFilter, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                string search = SearchBox.Text.Trim();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    accounts = accounts.Where(x =>
                        x.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        x.Role.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        x.Email.Contains(search, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                AccountList.ItemsSource = accounts;
                UpdateFilterCounts();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not load accounts from the database.\n\n" + ex.Message,
                    "Database error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void UpdateFilterCounts()
        {
            try
            {
                var all = _accounts.GetAccounts();

                AllFilter.Content = $"All {all.Count}";
                DoctorFilter.Content =
                    $"Doctor {all.Count(x => x.Role.Equals("Doctor", StringComparison.OrdinalIgnoreCase))}";
                PatientFilter.Content =
                    $"Patient {all.Count(x => x.Role.Equals("Patient", StringComparison.OrdinalIgnoreCase))}";
            }
            catch
            {
            }
        }

        private void Filter_Click(object sender, RoutedEventArgs e)
        {
            _activeFilter =
                sender == DoctorFilter ? "Doctor" :
                sender == PatientFilter ? "Patient" : "All";

            AllFilter.Style = (Style)FindResource(
                _activeFilter == "All" ? "FilterPillActive" : "FilterPill");
            DoctorFilter.Style = (Style)FindResource(
                _activeFilter == "Doctor" ? "FilterPillActive" : "FilterPill");
            PatientFilter.Style = (Style)FindResource(
                _activeFilter == "Patient" ? "FilterPillActive" : "FilterPill");

            RefreshAccounts();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshAccounts();
        }

        private void AddAccount_Click(object sender, RoutedEventArgs e)
        {
            AddAccountPanel.Visibility =
                AddAccountPanel.Visibility == Visibility.Visible
                    ? Visibility.Collapsed
                    : Visibility.Visible;
        }

        private void CancelAdd_Click(object sender, RoutedEventArgs e)
        {
            NameBox.Clear();
            EmailBox.Clear();
            PasswordBox.Clear();
            RoleBox.SelectedIndex = 0;
            AddAccountPanel.Visibility = Visibility.Collapsed;
        }

        private void SaveAccount_Click(object sender, RoutedEventArgs e)
        {
            string name = NameBox.Text.Trim();
            string email = EmailBox.Text.Trim();
            string password = PasswordBox.Password;
            string role = (RoleBox.SelectedItem as ComboBoxItem)?.Content?.ToString()
                          ?? "Patient";

            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show(
                    "Full name, email, role, and password are required.",
                    "Add Account",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                _accounts.CreateAccount(name, email, role, password);

                MessageBox.Show(
                    "The account was saved to the database successfully.",
                    "Account created",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                CancelAdd_Click(sender, e);
                RefreshAccounts();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not create the account.\n\n" + ex.Message,
                    "Database error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not UserAccountRow account)
                return;

            var dialog = new EditAccountWindow(account)
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                _accounts.UpdateAccount(
                    account.Id,
                    dialog.AccountName,
                    dialog.AccountEmail,
                    dialog.AccountRole);

                RefreshAccounts();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not update the account.\n\n" + ex.Message,
                    "Database error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ToggleStatus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not UserAccountRow account)
                return;

            try
            {
                _accounts.SetStatus(account.Id, account.Status != "Active");
                RefreshAccounts();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not change the account status.\n\n" + ex.Message,
                    "Database error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void Dashboard_Click(object sender, RoutedEventArgs e)
        {
            new dashboard_admin(_user).Show();
            Close();
        }

        private void Appointments_Click(object sender, RoutedEventArgs e)
        {
            new appointment_admin(_user).Show();
            Close();
        }

        private void Receipts_Click(object sender, RoutedEventArgs e)
        {
            new receipt_admin(_user).Show();
            Close();
        }

        private void NavigationPlaceholder_Click(object sender, RoutedEventArgs e)
        {
            string page = (sender as Button)?.Content?.ToString() ?? "Page";
            MessageBox.Show(
                $"Connect the {page} button to its existing page.",
                "ClinicaCentral");
        }

        private void LogOut_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(
                    "Log out of ClinicaCentral?",
                    "Log Out",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            new login_form().Show();
            Close();
        }
    }

    public sealed class EditAccountWindow : Window
    {
        private readonly TextBox _name = new();
        private readonly TextBox _email = new();
        private readonly ComboBox _role = new();

        public string AccountName => _name.Text.Trim();
        public string AccountEmail => _email.Text.Trim();
        public string AccountRole =>
            (_role.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Patient";

        public EditAccountWindow(UserAccountRow account)
        {
            Title = "Edit Account";
            Width = 420;
            Height = 300;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;

            var panel = new StackPanel { Margin = new Thickness(24) };

            panel.Children.Add(new TextBlock
            {
                Text = "Edit Account",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 18)
            });

            panel.Children.Add(new TextBlock { Text = "Full name" });
            _name.Text = account.Name;
            _name.Margin = new Thickness(0, 4, 0, 12);
            panel.Children.Add(_name);

            panel.Children.Add(new TextBlock { Text = "Email" });
            _email.Text = account.Email;
            _email.Margin = new Thickness(0, 4, 0, 12);
            panel.Children.Add(_email);

            panel.Children.Add(new TextBlock { Text = "Role" });
            _role.Items.Add(new ComboBoxItem { Content = "Doctor" });
            _role.Items.Add(new ComboBoxItem { Content = "Patient" });
            _role.SelectedIndex =
                account.Role.Equals("Doctor", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
            _role.Margin = new Thickness(0, 4, 0, 18);
            panel.Children.Add(_role);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var cancel = new Button
            {
                Content = "Cancel",
                Width = 85,
                Margin = new Thickness(0, 0, 8, 0)
            };
            cancel.Click += (_, _) => DialogResult = false;

            var save = new Button
            {
                Content = "Save",
                Width = 85
            };
            save.Click += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(AccountName) ||
                    string.IsNullOrWhiteSpace(AccountEmail))
                {
                    MessageBox.Show(
                        "Name and email are required.",
                        "Edit Account",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                DialogResult = true;
            };

            buttons.Children.Add(cancel);
            buttons.Children.Add(save);
            panel.Children.Add(buttons);

            Content = panel;
        }
    }
}

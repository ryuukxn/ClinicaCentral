using System;
using System.Linq;
using System.Net.Mail;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MySqlConnector;

namespace ClinicaCentral
{
    public partial class ma_admin : Window
    {
        private readonly User _user;
        private readonly UserAccountRepository _accounts;
        private List<UserAccountRow> _allAccounts = new();
        private UserAccountRow? _editingAccount;
        private byte[]? _selectedPatientPhoto;
        private bool _changingRole;
        private string _activeFilter = "All";

        public ma_admin(User user)
        {
            InitializeComponent();

            _user = user;
            _accounts = new UserAccountRepository(new DatabaseConnection());

            Loaded += (s, e) =>
            {
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
            };
        }

        private void RefreshAccounts()
        {
            try
            {
                _allAccounts = _accounts.GetAccounts();
                UpdateFilterCounts();
                var accounts = _allAccounts;

                if (_activeFilter != "All")
                    accounts = accounts
                        .Where(x => _activeFilter == "Doctor" ? x.IsDoctor : x.IsPatient)
                        .ToList();

                string search = SearchBox.Text.Trim();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    accounts = accounts.Where(x =>
                        x.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        x.Username.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        x.Email.Contains(search, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                AccountList.ItemsSource = accounts;
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
            AllFilter.Content = $"All {_allAccounts.Count}";
            DoctorFilter.Content = $"Doctor {_allAccounts.Count(x => x.IsDoctor)}";
            PatientFilter.Content = $"Patient {_allAccounts.Count(x => x.IsPatient)}";
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
            ResetAccountForm();
            AccountPanelTitle.Text = "Add Account";
            SaveAccountButton.Content = "Save Account";
            ShowAccountForm();
        }

        private void CancelAdd_Click(object sender, RoutedEventArgs e)
        {
            ResetAccountForm();
        }

        private void AccountRole_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_changingRole) return;

            string role = (RoleBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            AccountFormFields.Visibility = role.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            StaffFields.Visibility = role == "Staff" ? Visibility.Visible : Visibility.Collapsed;
            PatientFields.Visibility = role == "Patient" ? Visibility.Visible : Visibility.Collapsed;
            UpdateAccountPanelHeight();
        }

        private void SaveAccount_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameBox.Text.Trim();
            string email = EmailBox.Text.Trim();
            string password = PasswordBox.Password;
            string confirmPassword = ConfirmPasswordBox.Password;
            string role = _editingAccount?.UserRole ??
                (RoleBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

            if (role.Length == 0)
            {
                WarnAccount("Choose whether this account is for Staff or a Patient.");
                RoleBox.Focus();
                return;
            }

            if (username.Length is < 3 or > 50 || username.Contains(' '))
            {
                WarnAccount("Username must be 3-50 characters with no spaces.");
                UsernameBox.Focus();
                return;
            }

            if (!IsValidEmail(email) || email.Length > 100)
            {
                WarnAccount("Enter a valid email address (maximum 100 characters).");
                EmailBox.Focus();
                return;
            }

            if (_editingAccount == null && password.Length < 8)
            {
                WarnAccount("Password must be at least 8 characters.");
                PasswordBox.Focus();
                return;
            }

            if (password.Length > 0 &&
                (password.Length < 8 || password != confirmPassword))
            {
                WarnAccount(password.Length < 8
                    ? "Password must be at least 8 characters."
                    : "Password and confirmation do not match.");
                PasswordBox.Focus();
                return;
            }

            if (_editingAccount == null && password != confirmPassword)
            {
                WarnAccount("Password and confirmation do not match.");
                ConfirmPasswordBox.Focus();
                return;
            }

            UserAccountRow account = new UserAccountRow
            {
                Id = _editingAccount?.Id ?? 0,
                Username = username,
                Name = username,
                UserRole = role,
                Role = role,
                Email = email,
                Status = _editingAccount?.Status ?? "Active",
                Position = GetSelectedContent(StaffPositionBox),
                StaffFullName = StaffFullNameBox.Text.Trim(),
                StaffContact = StaffContactBox.Text.Trim(),
                FirstName = PatientFirstNameBox.Text.Trim(),
                LastName = PatientLastNameBox.Text.Trim(),
                BirthDate = PatientBirthDatePicker.SelectedDate,
                Gender = GetSelectedContent(PatientGenderBox),
                Contact = PatientContactBox.Text.Trim(),
                Address = PatientAddressBox.Text.Trim(),
                Allergies = PatientAllergiesBox.Text.Trim()
            };

            if (role == "Staff" &&
                (string.IsNullOrWhiteSpace(account.StaffFullName) ||
                 string.IsNullOrWhiteSpace(account.Position)))
            {
                WarnAccount("Full name and position are required for staff accounts.");
                return;
            }

            if (role == "Patient" &&
                (string.IsNullOrWhiteSpace(account.FirstName) ||
                 string.IsNullOrWhiteSpace(account.LastName) ||
                 string.IsNullOrWhiteSpace(account.Gender) ||
                 string.IsNullOrWhiteSpace(account.Contact) ||
                 string.IsNullOrWhiteSpace(account.Address)))
            {
                WarnAccount("First name, last name, gender, contact, and address are required for patient accounts.");
                return;
            }

            if (account.StaffFullName.Length > 100 || account.StaffContact.Length > 20 ||
                account.FirstName.Length > 50 || account.LastName.Length > 50 ||
                account.Contact.Length > 30 || account.Address.Length > 200 ||
                account.Allergies.Length > 255)
            {
                WarnAccount("One or more profile fields exceed the maximum length allowed.");
                return;
            }

            try
            {
                bool isEditing = _editingAccount != null;
                bool isCurrentAdmin = isEditing &&
                    string.Equals(_editingAccount!.Username, _user.Username, StringComparison.OrdinalIgnoreCase);
                string previousUsername = _user.Username;
                if (!isEditing)
                    _accounts.CreateAccount(account, password, _selectedPatientPhoto);
                else
                    _accounts.UpdateAccount(account, password, _selectedPatientPhoto);

                if (isCurrentAdmin && !string.Equals(previousUsername, account.Username, StringComparison.OrdinalIgnoreCase))
                {
                    _user.UpdateUsername(account.Username);
                    UserSessionMonitor.UpdateUsername(previousUsername, account.Username);
                }

                ResetAccountForm();
                RefreshAccounts();
                MessageBox.Show(isEditing ? "Account updated successfully." : "Account created successfully.",
                    "Manage Account", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                MessageBox.Show("Username/Email already exists.", "Manage Account",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("The account could not be saved.\n\n" + ex.Message,
                    "Manage Account", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is UserAccountRow account)) return;

            _editingAccount = account;
            _selectedPatientPhoto = null;
            AccountPanelTitle.Text = "Edit Account";
            SaveAccountButton.Content = "Save Changes";

            _changingRole = true;
            if (account.UserRole == "Staff")
            {
                RoleBox.SelectedIndex = 0;
                RoleBox.IsEnabled = false;
                StaffFields.Visibility = Visibility.Visible;
                PatientFields.Visibility = Visibility.Collapsed;
            }
            else if (account.UserRole == "Patient")
            {
                RoleBox.SelectedIndex = 1;
                RoleBox.IsEnabled = false;
                StaffFields.Visibility = Visibility.Collapsed;
                PatientFields.Visibility = Visibility.Visible;
            }
            else
            {
                RoleBox.SelectedIndex = -1;
                RoleBox.IsEnabled = false;
                StaffFields.Visibility = Visibility.Collapsed;
                PatientFields.Visibility = Visibility.Collapsed;
            }
            _changingRole = false;
            AccountFormFields.Visibility = Visibility.Visible;

            UsernameBox.Text = account.Username;
            EmailBox.Text = account.Email;
            PasswordBox.Clear();
            ConfirmPasswordBox.Clear();
            StaffFullNameBox.Text = account.StaffFullName;
            SelectContent(StaffPositionBox, account.Position);
            StaffContactBox.Text = account.StaffContact;
            PatientFirstNameBox.Text = account.FirstName;
            PatientLastNameBox.Text = account.LastName;
            PatientBirthDatePicker.SelectedDate = account.BirthDate;
            SelectContent(PatientGenderBox, account.Gender);
            PatientContactBox.Text = account.Contact;
            PatientAddressBox.Text = account.Address;
            PatientAllergiesBox.Text = account.Allergies;
            PatientPhotoText.Text = account.Photo == null ? "No photo selected" : "Current photo will be kept";
            ShowAccountForm();
        }

        private void ToggleStatus_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is UserAccountRow account)) return;

            string newStatus = account.Status.Equals("Active", StringComparison.OrdinalIgnoreCase)
                ? "Inactive" : "Active";
            string action = newStatus == "Inactive" ? "deactivate" : "reactivate";
            if (MessageBox.Show($"Are you sure you want to {action} {account.Name}'s account?",
                    "Confirm account status", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                _accounts.SetStatus(account.Id, newStatus, _user.Username);
                RefreshAccounts();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Manage Account",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void PatientPhoto_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "Choose a patient photo",
                Filter = "Pictures (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp",
                CheckFileExists = true
            };
            if (dialog.ShowDialog(this) != true) return;

            try
            {
                _selectedPatientPhoto = PhotoStore.Prepare(dialog.FileName);
                PatientPhotoText.Text = System.IO.Path.GetFileName(dialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show("The selected photo could not be loaded.\n\n" + ex.Message,
                    "Patient Photo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ResetAccountForm()
        {
            _editingAccount = null;
            _selectedPatientPhoto = null;
            _changingRole = true;
            RoleBox.SelectedIndex = -1;
            RoleBox.IsEnabled = true;
            _changingRole = false;
            AccountFormFields.Visibility = Visibility.Collapsed;
            StaffFields.Visibility = Visibility.Collapsed;
            PatientFields.Visibility = Visibility.Collapsed;
            UsernameBox.Clear();
            EmailBox.Clear();
            PasswordBox.Clear();
            ConfirmPasswordBox.Clear();
            StaffFullNameBox.Clear();
            StaffPositionBox.SelectedIndex = -1;
            StaffContactBox.Clear();
            PatientFirstNameBox.Clear();
            PatientLastNameBox.Clear();
            PatientBirthDatePicker.SelectedDate = null;
            PatientGenderBox.SelectedIndex = -1;
            PatientContactBox.Clear();
            PatientAddressBox.Clear();
            PatientAllergiesBox.Clear();
            PatientPhotoText.Text = "Optional";
            AccountPanelTitle.Text = "Add Account";
            SaveAccountButton.Content = "Save Account";
            AddAccountPanel.Visibility = Visibility.Collapsed;
            SetAccountBrowseVisibility(Visibility.Visible);
        }

        private void ShowAccountForm()
        {
            AddAccountPanel.Visibility = Visibility.Visible;
            SetAccountBrowseVisibility(Visibility.Collapsed);
            UpdateAccountPanelHeight();
        }

        private void SetAccountBrowseVisibility(Visibility visibility)
        {
            AccountFiltersPanel.Visibility = visibility;
            AccountColumnHeaderPanel.Visibility = visibility;
            AccountListPanel.Visibility = visibility;
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateAccountPanelHeight();
        }

        private void UpdateAccountPanelHeight()
        {
            double availableHeight = Math.Max(260, ActualHeight - 320);
            string role = (RoleBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            double desiredHeight = role == "Patient" ? 500 : role == "Staff" ? 390 : 230;

            AddAccountPanel.MaxHeight = availableHeight;
            AddAccountPanel.Height = Math.Min(availableHeight, desiredHeight);
        }

        private static string GetSelectedContent(ComboBox comboBox)
        {
            return (comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        }

        private static void SelectContent(ComboBox comboBox, string content)
        {
            comboBox.SelectedItem = comboBox.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(item => string.Equals(
                    item.Content?.ToString(), content, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsValidEmail(string email)
        {
            try { return new MailAddress(email).Address == email; }
            catch (FormatException) { return false; }
        }

        private static void WarnAccount(string message)
        {
            MessageBox.Show(message, "Manage Account", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void Dashboard_Click(object sender, RoutedEventArgs e)
        {
            new dashboard_admin(_user).Open();
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
            if (MessageBox.Show(
                    "Log out of ClinicaCentral?",
                    "Log Out",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            new login_form().Open();
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

using System;
using System.Windows;
using System.Windows.Controls;

namespace ClinicaCentral
{
    public partial class login_form : Window
    {
        private readonly DatabaseConnection _db;
        private readonly UserRepository _users;

        public login_form()
        {
            InitializeComponent();

            _db = new DatabaseConnection();
            _users = new UserRepository(_db);

            Loaded += (s, e) =>
            {
                if (!_db.TestConnection(out string err))
                {
                    MessageBox.Show("Cannot reach the database. Is MySQL running in Laragon?\n\n" + err,
                        "Database error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
        }

        private bool _syncingPassword;

        private void TxtPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (!_syncingPassword)
            {
                _syncingPassword = true;
                txtPasswordVisible.Text = txtPassword.Password;
                _syncingPassword = false;
            }

            UpdatePasswordHint();
        }

        private void TxtPasswordVisible_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_syncingPassword)
            {
                _syncingPassword = true;
                txtPassword.Password = txtPasswordVisible.Text;
                _syncingPassword = false;
            }

            UpdatePasswordHint();
        }

        private void UpdatePasswordHint()
        {
            lblPasswordHint.Visibility = txtPassword.Password.Length == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void BtnShowPassword_Changed(object sender, RoutedEventArgs e)
        {
            bool show = btnShowPassword.IsChecked == true;

            if (show)
            {
                txtPasswordVisible.Text = txtPassword.Password;
                txtPassword.Visibility = Visibility.Collapsed;
                txtPasswordVisible.Visibility = Visibility.Visible;
                txtPasswordVisible.Focus();
                txtPasswordVisible.CaretIndex = txtPasswordVisible.Text.Length;
            }
            else
            {
                txtPassword.Password = txtPasswordVisible.Text;
                txtPasswordVisible.Visibility = Visibility.Collapsed;
                txtPassword.Visibility = Visibility.Visible;
                txtPassword.Focus();
            }
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string login = txtUsername.Text.Trim();
            string pass = txtPassword.Password;

            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(pass))
            {
                MessageBox.Show("Please enter your username and password.",
                    "Login", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                User? user = _users.Login(login, pass, out bool deactivated);

                if (user == null)
                {
                    if (deactivated)
                    {
                        MessageBox.Show(
                            "Your account is deactivated. Please contact the administrator.",
                            "Login", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    MessageBox.Show("Invalid username or password.", "Login",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                UserSessionMonitor.Start(user.Username);

                Window dashboard;

                if (user is PatientUser patient)
                    dashboard = new dashboard_patient(patient);
                else if (user is StaffUser)
                    dashboard = new dashboard_staff(user.Username);
                else
                    dashboard = new dashboard_admin(user);

                dashboard.Open();
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Login failed:\n\n" + ex.Message, "Login",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Register_Click(object sender, RoutedEventArgs e)
        {
            new register_form().Open();
            Close();
        }
    }
}
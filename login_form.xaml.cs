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

            if (!_db.TestConnection(out string err))
            {
                MessageBox.Show("Cannot reach the database. Is MySQL running in Laragon?\n\n" + err,
                    "Database error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            try
            {
                _users.EnsureDefaultAdmin();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not prepare the users table:\n\n" + ex.Message,
                    "Database error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TxtPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            lblPasswordHint.Visibility = txtPassword.Password.Length == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
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
                User? user = _users.Login(login, pass);

                if (user == null)
                {
                    MessageBox.Show("Invalid username or password.", "Login",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                Window dashboard;

                if (user is StaffUser)
                    dashboard = new dashboard_staff(user.Username);
                else
                    dashboard = new dashboard_admin(user);

                dashboard.Show();
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
            new register_form().Show();
            Close();
        }
    }
}
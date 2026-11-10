using System;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using MySqlConnector;

namespace ClinicaCentral
{
    public partial class register_form : Window
    {
        public register_form()
        {
            InitializeComponent();
        }

        private void Password_Changed(object sender, RoutedEventArgs e)
        {
            hintPassword.Visibility = txtPassword.Password.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            new login_form().Open();
            Close();
        }

        private void Register_Click(object sender, RoutedEventArgs e)
        {
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Password;
            string fullName = Regex.Replace(txtFullName.Text.Trim(), @"\s+", " ");
            string email    = txtEmail.Text.Trim();
            string contact  = txtContact.Text.Trim();
            string address  = txtAddress.Text.Trim();
            string allergies = txtAllergies.Text.Trim();
            string gender   = (cmbGender.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

            if (username.Length < 3 || username.Length > 50 || username.Contains(' '))
            { Warn("Username must be 3-50 characters with no spaces."); txtUsername.Focus(); return; }

            if (password.Length < 6)
            { Warn("Password must be at least 6 characters."); txtPassword.Focus(); return; }

            int space = fullName.LastIndexOf(' ');
            if (space < 1)
            { Warn("Please enter your full name (first and last name)."); txtFullName.Focus(); return; }
            string firstName = fullName.Substring(0, space);
            string lastName  = fullName.Substring(space + 1);
            if (firstName.Length > 50 || lastName.Length > 50)
            { Warn("First or last name is too long (max 50 characters each)."); txtFullName.Focus(); return; }

            if (dpBirth.SelectedDate == null)
            { Warn("Please select your birthdate."); dpBirth.Focus(); return; }
            if (dpBirth.SelectedDate.Value.Date > DateTime.Today)
            { Warn("Birthdate cannot be in the future."); dpBirth.Focus(); return; }

            if (gender.Length == 0)
            { Warn("Please select your sex."); cmbGender.Focus(); return; }

            if (!IsValidEmail(email) || email.Length > 100)
            { Warn("Please enter a valid email address."); txtEmail.Focus(); return; }

            if (!Regex.IsMatch(contact, @"^[0-9+\-\s]{7,30}$"))
            { Warn("Please enter a valid contact number."); txtContact.Focus(); return; }

            if (address.Length == 0 || address.Length > 200)
            { Warn("Please enter your complete address (max 200 characters)."); txtAddress.Focus(); return; }

            if (allergies.Length > 255)
            { Warn("Allergies must be 255 characters or fewer."); txtAllergies.Focus(); return; }

            var data = new Database.RegistrationData
            {
                Username  = username,
                Password  = password,
                FirstName = firstName,
                LastName  = lastName,
                BirthDate = dpBirth.SelectedDate.Value,
                Gender    = gender,
                Email     = email,
                Contact   = contact,
                Address   = address,
                Allergies = allergies.Length == 0 ? null : allergies
            };

            try
            {
                Database.RegisterPatient(data);

                MessageBox.Show("Registration successful! You can now log in.",
                    "Registration!", MessageBoxButton.OK, MessageBoxImage.Information);

                new login_form().Open();
                Close();
            }
            catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.DuplicateKeyEntry)
            {
                if (ex.Message.Contains("username", StringComparison.OrdinalIgnoreCase))
                { Warn("That username is already taken."); txtUsername.Focus(); }
                else if (ex.Message.Contains("email", StringComparison.OrdinalIgnoreCase))
                { Warn("That email is already registered."); txtEmail.Focus(); }
                else
                    Warn("This account already exists.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Registration failed:\n\n" + ex.Message, "Registration",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static bool IsValidEmail(string email)
        {
            try { return new MailAddress(email).Address == email; }
            catch { return false; }
        }

        private static void Warn(string message)
        {
            MessageBox.Show(message, "Registration", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
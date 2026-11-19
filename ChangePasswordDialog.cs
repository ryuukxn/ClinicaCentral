using System.Windows;
using System.Windows.Controls;

namespace ClinicaCentral
{
    public class ChangePasswordDialog : Window
    {
        private readonly PasswordBox _current, _new, _confirm;

        public string CurrentPassword => _current.Password;
        public string NewPassword => _new.Password;

        public ChangePasswordDialog()
        {
            Title = "Change Password";
            Width = 380;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var panel = new StackPanel { Margin = new Thickness(18) };

            PasswordBox AddField(string label)
            {
                panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 8, 0, 3) });
                var box = new PasswordBox { Padding = new Thickness(6) };
                panel.Children.Add(box);
                return box;
            }

            _current = AddField("Current password");
            _new = AddField("New password (at least 6 characters)");
            _confirm = AddField("Confirm new password");

            var save = new Button
            {
                Content = "Change Password",
                IsDefault = true,
                Padding = new Thickness(0, 8, 0, 8),
                Margin = new Thickness(0, 18, 0, 0)
            };
            var cancel = new Button
            {
                Content = "Cancel",
                IsCancel = true,
                Padding = new Thickness(0, 8, 0, 8),
                Margin = new Thickness(0, 6, 0, 0)
            };

            save.Click += (s, e) =>
            {
                if (CurrentPassword.Length == 0) { Warn("Please enter your current password."); return; }
                if (NewPassword.Length < 6) { Warn("The new password must be at least 6 characters."); return; }
                if (NewPassword != _confirm.Password) { Warn("The new passwords do not match."); return; }
                if (NewPassword == CurrentPassword) { Warn("The new password must be different from the current one."); return; }
                DialogResult = true;
            };

            panel.Children.Add(save);
            panel.Children.Add(cancel);
            Content = panel;
        }

        private void Warn(string message) =>
            MessageBox.Show(this, message, "Change Password", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}

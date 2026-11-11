using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ClinicaCentral
{
    public class EditProfileDialog : Window
    {
        private readonly TextBox _email, _contact, _address, _allergies;

        public string Email => _email.Text.Trim();
        public string Contact => _contact.Text.Trim();
        public string Address => _address.Text.Trim();
        public string Allergies => _allergies.Text.Trim();

        public EditProfileDialog(string email, string contact, string address, string allergies)
        {
            Title = "Edit Profile";
            Width = 380;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var panel = new StackPanel { Margin = new Thickness(18) };

            TextBox AddField(string label, string value, bool multiline = false)
            {
                panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 8, 0, 3) });
                var box = new TextBox
                {
                    Text = value,
                    Padding = new Thickness(6),
                    AcceptsReturn = multiline,
                    TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
                    MinHeight = multiline ? 60 : 0
                };
                panel.Children.Add(box);
                return box;
            }

            _email = AddField("Email", email);
            _contact = AddField("Contact Number", contact);
            _address = AddField("Complete Address", address, true);
            _allergies = AddField("Allergies (leave blank if none)", allergies, true);

            var save = new Button
            {
                Content = "Save Changes",
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
                if (Email.Length == 0 || !Email.Contains('@') || !Email.Contains('.'))
                {
                    Warn("Please enter a valid email address.");
                    return;
                }
                if (Contact.Length == 0 || !Contact.All(c => char.IsDigit(c) || c == '+' || c == ' ' || c == '-'))
                {
                    Warn("Contact number can only have digits, spaces, + and -.");
                    return;
                }
                if (Address.Length == 0)
                {
                    Warn("Please enter your address.");
                    return;
                }
                DialogResult = true;
            };

            panel.Children.Add(save);
            panel.Children.Add(cancel);
            Content = panel;
        }

        private void Warn(string message) =>
            MessageBox.Show(this, message, "Edit Profile", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
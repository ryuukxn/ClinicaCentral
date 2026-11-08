using System.Windows;
using System.Windows.Controls;

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
    }
}

using System.Windows;

namespace ClinicaCentral
{
    public partial class register_form : Window
    {
        public register_form()
        {
            InitializeComponent();
        }

        private void BackToLogin_Click(object sender, RoutedEventArgs e)
        {
            new login_form().Show();
            Close();
        }
    }
}
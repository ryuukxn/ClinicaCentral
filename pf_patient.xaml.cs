using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace ClinicaCentral
{
    public partial class pf_patient : Window
    {
        private readonly PatientUser _user;
        private readonly PhotoRepository _photos;

        public pf_patient(PatientUser user)
        {
            InitializeComponent();

            _user = user;
            _photos = new PhotoRepository(new DatabaseConnection());
            WelcomeText.Text = $"Welcome back, {user.Username}!";
            Loaded += (s, e) =>
            {
                LoadProfile();
                ShowPhoto();
            };
        }

        private void LoadProfile()
        {
            if (_user.PatientId <= 0)
            {
                MessageBox.Show(
                    "Your account is not linked to a patient record yet, so there is no profile to show. Please ask the clinic to link it.",
                    "ClinicaCentral", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                ProfileRepository repo = new ProfileRepository(new DatabaseConnection());
                ProfileInfo? info = repo.GetProfile(_user.Username);

                if (info == null)
                {
                    MessageBox.Show("Your profile could not be found.", "ClinicaCentral",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                DataContext = info;

                string first = info.FullName.Split(' ')[0];
                if (first.Length > 0) WelcomeText.Text = $"Welcome back, {first}!";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Your profile could not be loaded. Make sure MySQL is running in Laragon and that profile_fields.sql was run.\n\n" + ex.Message,
                    "Connection problem", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ShowPhoto()
        {
            if (_user.PatientId <= 0) return;

            try
            {
                byte[]? data = _photos.GetPhoto(_user.PatientId);
                BitmapImage? photo = data == null ? null : PhotoStore.FromBytes(data);

                if (photo == null)
                {
                    PhotoCircle.Visibility = Visibility.Collapsed;
                    AvatarGlyph.Visibility = Visibility.Visible;
                    UploadButton.Content = "Upload Photo";
                    return;
                }

                ImageBrush brush = new ImageBrush(photo);
                brush.Stretch = Stretch.UniformToFill;
                PhotoCircle.Fill = brush;
                PhotoCircle.Visibility = Visibility.Visible;
                AvatarGlyph.Visibility = Visibility.Collapsed;
                UploadButton.Content = "Change Photo";
            }
            catch (Exception)
            {
            }
        }

        private void UploadPhoto_Click(object sender, RoutedEventArgs e)
        {
            if (_user.PatientId <= 0)
            {
                MessageBox.Show("Your account is not linked to a patient record yet, so a photo can't be added.",
                    "ClinicaCentral", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "Choose a profile photo";
            dialog.Filter = "Pictures (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp";
            dialog.CheckFileExists = true;

            if (dialog.ShowDialog(this) != true) return;

            byte[] data;
            try
            {
                data = PhotoStore.Prepare(dialog.FileName);
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Profile photo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            catch (Exception)
            {
                MessageBox.Show("That file could not be read as a picture. Please choose a JPG, PNG or BMP image.",
                    "Profile photo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _photos.SavePhoto(_user.PatientId, data);
                ShowPhoto();
                MessageBox.Show("Your profile photo was updated.", "Profile photo",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Your photo could not be saved. Make sure MySQL is running in Laragon and that photo_column.sql was run.\n\n" + ex.Message,
                    "Profile photo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ChangePassword_Click(object sender, RoutedEventArgs e)
        {
        }

        private void EditProfile_Click(object sender, RoutedEventArgs e)
{
    if (_user.PatientId <= 0)
    {
        MessageBox.Show("Your account is not linked to a patient record yet.",
            "ClinicaCentral", MessageBoxButton.OK, MessageBoxImage.Information);
        return;
    }

    try
    {
        ProfileEdit.ContactInfo current = ProfileEdit.GetContactInfo(_user.PatientId);

        var dlg = new EditProfileDialog(current.Email, current.Contact, current.Address, current.Allergies)
        {
            Owner = this
        };
        if (dlg.ShowDialog() != true) return;

        ProfileEdit.UpdateProfile(_user.PatientId, dlg.Email, dlg.Contact, dlg.Address, dlg.Allergies);

        LoadProfile();
        MessageBox.Show("Your profile was updated.", "Edit Profile",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }
    catch (Exception ex)
    {
        MessageBox.Show("Your profile could not be saved.\n\n" + ex.Message, "Edit Profile",
            MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}

        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            string page = (sender as Button)?.Tag as string ?? "";

            if (page == "Profile") return;

            Window? next = null;
            if (page == "Dashboard") next = new dashboard_patient(_user);
            else if (page == "Book Appointment") next = new bk_patient(_user);
            else if (page == "My Appointment") next = new my_patient(_user);

            if (next == null)
            {
                return;
            }

            next.Open();
            Close();
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            var answer = MessageBox.Show("Do you want to log out?", "Log out",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes) return;

            new login_form().Open();
            Close();
        }
    }
}

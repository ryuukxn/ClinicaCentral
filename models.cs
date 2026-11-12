using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace ClinicaCentral
{
    public abstract class Person
    {
        private string _name = "";

        public string Name
        {
            get { return _name; }
            set
            {
                if (value == null)
                    _name = "";
                else
                    _name = value.Trim();
            }
        }

        public abstract string GetRoleTitle();
    }

    public class StaffMember : Person
    {
        public string Position { get; set; } = "";
        public string Status { get; set; } = "";

        public override string GetRoleTitle()
        {
            return Position;
        }
    }

    public abstract class User
    {
        public string Username { get; private set; }

        protected User(string username)
        {
            Username = username;
        }

        public void UpdateUsername(string username)
        {
            Username = username;
        }

        public abstract string Role { get; }

        public abstract bool CanManageStaff();

        public virtual string GetWelcomeMessage()
        {
            return "Welcome back, " + Username + "!";
        }
    }

    public class AdminUser : User
    {
        public AdminUser(string username) : base(username) { }

        public override string Role => "Admin";

        public override bool CanManageStaff() => true;

        public override string GetWelcomeMessage()
        {
            return "Welcome back, Admin!";
        }
    }

    public class StaffUser : User
    {
        public StaffUser(string username) : base(username) { }

        public override string Role => "Staff";

        public override bool CanManageStaff() => false;
    }

    public class PatientUser : User
    {
        public int PatientId { get; }

        public PatientUser(string username, int patientId) : base(username)
        {
            PatientId = patientId;
        }

        public override string Role => "Patient";

        public override bool CanManageStaff() => false;
    }

    public sealed class UserAccountRow
    {
        public int Id { get; init; }
        public string Username { get; init; } = "";
        public string Name { get; init; } = "";
        public string Role { get; init; } = "";
        public string UserRole { get; init; } = "";
        public string Email { get; init; } = "";
        public string Status { get; init; } = "Active";
        public string Position { get; init; } = "";
        public string StaffFullName { get; init; } = "";
        public string StaffContact { get; init; } = "";
        public string FirstName { get; init; } = "";
        public string LastName { get; init; } = "";
        public DateTime? BirthDate { get; init; }
        public string Gender { get; init; } = "";
        public string Contact { get; init; } = "";
        public string Address { get; init; } = "";
        public string Allergies { get; init; } = "";
        public byte[]? Photo { get; init; }
        public bool IsDoctor => UserRole == "Staff" && Position == "Doctor";
        public bool IsPatient => UserRole == "Patient";
    }

    public static class UserSessionMonitor
    {
        private static DispatcherTimer? _timer;
        private static string _username = "";

        public static void Start(string username)
        {
            Stop();
            _username = username;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            _timer.Tick += CheckStatus;
            _timer.Start();
        }

        public static void Stop()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Tick -= CheckStatus;
                _timer = null;
            }
            _username = "";
        }

        public static void UpdateUsername(string oldUsername, string newUsername)
        {
            if (string.Equals(_username, oldUsername, StringComparison.OrdinalIgnoreCase))
                _username = newUsername;
        }

        private static void CheckStatus(object? sender, EventArgs e)
        {
            if (Application.Current == null) return;

            var windows = Application.Current.Windows
                .OfType<Window>()
                .Where(window => window is not login_form)
                .ToList();

            if (windows.Count == 0)
            {
                Stop();
                return;
            }

            try
            {
                if (Database.IsAccountActive(_username)) return;

                LogOut(windows,
                    "Your account is deactivated. Please contact the administrator.");
            }
            catch
            {
                LogOut(windows,
                    "Your session could not be verified. Please log in again.");
            }
        }

        private static void LogOut(System.Collections.Generic.List<Window> windows, string message)
        {
            Stop();
            new login_form().Open();
            foreach (Window window in windows)
                window.Close();
            MessageBox.Show(message, "Account access", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}

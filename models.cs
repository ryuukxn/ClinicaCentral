using System;

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
        public string Username { get; }

        protected User(string username)
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

        public override string Role
        {
            get { return "Admin"; }
        }

        public override bool CanManageStaff()
        {
            return true;
        }

        public override string GetWelcomeMessage()
        {
            return "Welcome back, Admin!";
        }
    }

    public class StaffUser : User
    {
        public StaffUser(string username) : base(username) { }

        public override string Role
        {
            get { return "Staff"; }
        }

        public override bool CanManageStaff()
        {
            return false;
        }
    }
}

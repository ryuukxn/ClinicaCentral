
using System;
using System.Collections.Generic;
using MySqlConnector;

namespace ClinicaCentral
{
    public class DatabaseConnection
    {
        public bool TestConnection(out string error)
        {
            return Database.TestConnection(out error);
        }

        public void EnsureDefaultAdmin()
        {
            Database.EnsureDefaultAdmin();
        }

        public MySqlConnection OpenConnection()
        {
            MySqlConnection conn = Database.GetConnection();
            conn.Open();
            return conn;
        }
    }

    public abstract class Repository
    {
        protected readonly DatabaseConnection Db;

        protected Repository(DatabaseConnection db)
        {
            Db = db ?? throw new ArgumentNullException(nameof(db));
        }

        protected MySqlConnection OpenConnection()
        {
            return Db.OpenConnection();
        }
    }

    public class UserRepository : Repository
    {
        public UserRepository(DatabaseConnection db) : base(db) { }

        public void EnsureDefaultAdmin()
        {
            Db.EnsureDefaultAdmin();
        }

        public User? Login(string login, string password)
        {
            if (!Database.Authenticate(
                login, password, out string username, out string role))
            {
                return null;
            }

            if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                return new AdminUser(username);

            if (role.Equals("Staff", StringComparison.OrdinalIgnoreCase))
                return new StaffUser(username);

            return null;
        }
    }

    public class StatsRepository : Repository
    {
        public StatsRepository(DatabaseConnection db) : base(db) { }

        public int CountPatients()
        {
            return Database.CountRows("patients");
        }

        public int CountStaff()
        {
            return Database.CountRows("staff");
        }

        public int CountAppointments()
        {
            return Database.CountRows("appointments");
        }

        public int[] GetWeeklyAppointments()
        {
            return Database.GetWeeklyAppointments();
        }
    }

    public class StaffRepository : Repository
    {
        public StaffRepository(DatabaseConnection db) : base(db) { }

        public List<StaffRow> GetStaffList()
        {
            return Database.GetStaffList();
        }
    }
}

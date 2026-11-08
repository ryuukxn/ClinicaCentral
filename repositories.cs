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

        public User? Login(string login, string password)
        {
            if (!Database.Authenticate(login, password, out string username, out string role))
                return null;

            if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                return new AdminUser(username);

            if (role.Equals("Staff", StringComparison.OrdinalIgnoreCase))
                return new StaffUser(username);

            if (role.Equals("Patient", StringComparison.OrdinalIgnoreCase))
                return new PatientUser(username, GetPatientId(username));

            return null;
        }

        private int GetPatientId(string username)
        {
            using var conn = OpenConnection();
            using var cmd = new MySqlCommand("SELECT patient_id FROM users WHERE username = @u LIMIT 1", conn);
            cmd.Parameters.AddWithValue("@u", username);

            object? result = cmd.ExecuteScalar();
            return result == null || result is DBNull ? 0 : Convert.ToInt32(result);
        }
    }

    public class UserAccountRepository : Repository
    {
        public UserAccountRepository(DatabaseConnection db) : base(db) { }

        public List<UserAccountRow> GetAccounts()
        {
            var list = new List<UserAccountRow>();

            using var conn = OpenConnection();

            using var cmd = new MySqlCommand(
                @"SELECT id, username, email, role, status
                  FROM users
                  ORDER BY id", conn);

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                list.Add(new UserAccountRow
                {
                    Id = Convert.ToInt32(reader["id"]),
                    Name = reader["username"]?.ToString() ?? "",
                    Email = reader["email"]?.ToString() ?? "",
                    Role = reader["role"]?.ToString() ?? "",
                    Status = reader["status"]?.ToString() ?? "Active"
                });
            }

            return list;
        }
    }

    public class StatsRepository : Repository
    {
        public StatsRepository(DatabaseConnection db) : base(db) { }

        public int CountPatients() => Database.CountRows("patients");

        public int CountStaff() => Database.CountRows("staff");

        public int CountAppointments() => Database.CountRows("appointments");

        public int[] GetWeeklyAppointments() => Database.GetWeeklyAppointments();
    }

    public class StaffRepository : Repository
    {
        public StaffRepository(DatabaseConnection db) : base(db) { }

        public List<StaffRow> GetStaffList() => Database.GetStaffList();
    }
}

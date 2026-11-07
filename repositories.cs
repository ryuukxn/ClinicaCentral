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
            if (!Database.Authenticate(login, password, out string username, out string role))
                return null;

            if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                return new AdminUser(username);

            if (role.Equals("Staff", StringComparison.OrdinalIgnoreCase))
                return new StaffUser(username);

            return null;
        }
    }

    public class UserAccountRepository : Repository
    {
        public UserAccountRepository(DatabaseConnection db) : base(db) { }

        public List<UserAccountRow> GetAccounts()
        {
            var list = new List<UserAccountRow>();

            using var conn = OpenConnection();
            EnsureStatusColumn(conn);

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

        public void CreateAccount(string name, string email, string role, string password)
        {
            using var conn = OpenConnection();
            EnsureStatusColumn(conn);

            using (var check = new MySqlCommand(
                "SELECT COUNT(*) FROM users WHERE email = @email", conn))
            {
                check.Parameters.AddWithValue("@email", email);

                if (Convert.ToInt32(check.ExecuteScalar()) > 0)
                    throw new InvalidOperationException(
                        "An account with that email already exists.");
            }

            string username = BuildUsername(conn, name, email);
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

            using var cmd = new MySqlCommand(
                @"INSERT INTO users
                    (username, email, password_hash, role, status)
                  VALUES
                    (@username, @email, @password_hash, @role, 'Active')", conn);

            cmd.Parameters.AddWithValue("@username", username);
            cmd.Parameters.AddWithValue("@email", email);
            cmd.Parameters.AddWithValue("@password_hash", passwordHash);
            cmd.Parameters.AddWithValue("@role", role);
            cmd.ExecuteNonQuery();
        }

        public void UpdateAccount(int id, string name, string email, string role)
        {
            using var conn = OpenConnection();

            using var check = new MySqlCommand(
                @"SELECT COUNT(*) FROM users
                  WHERE email = @email AND id <> @id", conn);

            check.Parameters.AddWithValue("@email", email);
            check.Parameters.AddWithValue("@id", id);

            if (Convert.ToInt32(check.ExecuteScalar()) > 0)
                throw new InvalidOperationException(
                    "Another account already uses that email.");

            using var cmd = new MySqlCommand(
                @"UPDATE users
                  SET username = @name,
                      email = @email,
                      role = @role
                  WHERE id = @id", conn);

            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@email", email);
            cmd.Parameters.AddWithValue("@role", role);

            if (cmd.ExecuteNonQuery() == 0)
                throw new InvalidOperationException("The account was not found.");
        }

        public void SetStatus(int id, bool active)
        {
            using var conn = OpenConnection();
            EnsureStatusColumn(conn);

            using var cmd = new MySqlCommand(
                @"UPDATE users
                  SET status = @status
                  WHERE id = @id", conn);

            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@status", active ? "Active" : "Inactive");

            if (cmd.ExecuteNonQuery() == 0)
                throw new InvalidOperationException("The account was not found.");
        }

        private static string BuildUsername(
            MySqlConnection conn, string name, string email)
        {
            string baseName = email.Split('@')[0].Trim();

            if (string.IsNullOrWhiteSpace(baseName))
                baseName = name.Trim().Replace(" ", ".");

            string username = baseName;
            int suffix = 1;

            while (UsernameExists(conn, username))
            {
                username = baseName + suffix;
                suffix++;
            }

            return username;
        }

        private static bool UsernameExists(MySqlConnection conn, string username)
        {
            using var cmd = new MySqlCommand(
                "SELECT COUNT(*) FROM users WHERE username = @username", conn);

            cmd.Parameters.AddWithValue("@username", username);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        private static void EnsureStatusColumn(MySqlConnection conn)
        {
            using var check = new MySqlCommand(
                @"SELECT COUNT(*)
                  FROM INFORMATION_SCHEMA.COLUMNS
                  WHERE TABLE_SCHEMA = DATABASE()
                    AND TABLE_NAME = 'users'
                    AND COLUMN_NAME = 'status'", conn);

            bool exists = Convert.ToInt32(check.ExecuteScalar()) > 0;

            if (!exists)
            {
                using var alter = new MySqlCommand(
                    @"ALTER TABLE users
                      ADD COLUMN status VARCHAR(20) NOT NULL DEFAULT 'Active'",
                    conn);

                alter.ExecuteNonQuery();
            }
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

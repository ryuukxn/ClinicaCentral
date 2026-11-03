using System;
using System.Collections.Generic;
using MySqlConnector;
    namespace ClinicaCentral
{
    public class StaffRow
    {
        public string Name { get; set; } = "";
        public string Position { get; set; } = "";
        public string Status { get; set; } = "";
    }

    public static class Database
    {
        public const string ConnectionString =
            "Server=127.0.0.1;Port=3306;Database=clinicacentral;User ID=root;Password=;";

        public static MySqlConnection GetConnection() => new MySqlConnection(ConnectionString);

        public static bool TestConnection(out string error)
        {
            try
            {
                using var conn = GetConnection();
                conn.Open();
                error = string.Empty;
                return true;
            }
            catch (MySqlException ex)
            {
                error = ex.Message;
                return false;
            }
        }
        public static void EnsureDefaultAdmin()
        {
            using var conn = GetConnection();
            conn.Open();

            using var check = new MySqlCommand("SELECT COUNT(*) FROM users", conn);
            if (Convert.ToInt32(check.ExecuteScalar()) > 0) return;

            using var insert = new MySqlCommand(
                "INSERT INTO users (username, email, password_hash, role) VALUES (@u, @e, @p, 'Admin')", conn);
            insert.Parameters.AddWithValue("@u", "admin");
            insert.Parameters.AddWithValue("@e", "admin@clinicacentral.local");
            insert.Parameters.AddWithValue("@p", BCrypt.Net.BCrypt.HashPassword("admin123"));
            insert.ExecuteNonQuery();
        }

        public static bool Authenticate(string login, string password, out string username, out string role)
        {
            username = "";
            role = "";

            using var conn = GetConnection();
            conn.Open();

            using var cmd = new MySqlCommand(
                "SELECT username, password_hash, role FROM users WHERE username = @l OR email = @l LIMIT 1", conn);
            cmd.Parameters.AddWithValue("@l", login);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return false;

            string hash = reader.GetString(1);
            if (!BCrypt.Net.BCrypt.Verify(password, hash)) return false;

            username = reader.GetString(0);
            role = reader.GetString(2);
            return true;
        }

        public static int CountRows(string table)
        {
            if (table != "patients" && table != "staff" && table != "appointments")
                throw new ArgumentException("Unknown table: " + table);

            using var conn = GetConnection();
            conn.Open();
            using var cmd = new MySqlCommand($"SELECT COUNT(*) FROM {table}", conn);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public static int[] GetWeeklyAppointments()
        {
            var counts = new int[7];

            using var conn = GetConnection();
            conn.Open();
            using var cmd = new MySqlCommand(
                "SELECT WEEKDAY(appointment_date), COUNT(*) FROM appointments " +
                "WHERE YEARWEEK(appointment_date, 1) = YEARWEEK(CURDATE(), 1) GROUP BY 1", conn);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                int day = Convert.ToInt32(reader.GetValue(0));
                if (day >= 0 && day < 7)
                    counts[day] = Convert.ToInt32(reader.GetValue(1));
            }
            return counts;
        }

        public static List<StaffRow> GetStaffList()
        {
            var list = new List<StaffRow>();

            using var conn = GetConnection();
            conn.Open();
            using var cmd = new MySqlCommand(
                "SELECT full_name, position, status FROM staff ORDER BY id LIMIT 10", conn);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new StaffRow
                {
                    Name = reader.GetString(0),
                    Position = reader.GetString(1),
                    Status = reader.GetString(2)
                });
            }
            return list;
        }
    }
}
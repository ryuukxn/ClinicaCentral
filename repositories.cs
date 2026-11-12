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

        public User? Login(string login, string password, out bool deactivated)
        {
            if (!Database.Authenticate(login, password, out string username, out string role, out deactivated))
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

        private const string AccountSelect =
            @"SELECT u.id, u.username, u.email, u.role, u.status,
                     COALESCE(NULLIF(s.full_name, ''), NULLIF(TRIM(CONCAT_WS(' ', p.first_name, p.last_name)), ''), u.username) AS display_name,
                     COALESCE(s.position, '') AS position, COALESCE(s.full_name, '') AS staff_full_name,
                     COALESCE(s.contact, '') AS staff_contact, COALESCE(p.first_name, '') AS first_name,
                     COALESCE(p.last_name, '') AS last_name, p.birthdate, COALESCE(p.gender, '') AS gender,
                     COALESCE(p.contact, '') AS patient_contact, COALESCE(p.address, '') AS address,
                     COALESCE(p.allergies, '') AS allergies, p.photo,
                     CASE WHEN u.role = 'Staff' THEN COALESCE(s.position, 'Staff') ELSE u.role END AS display_role
              FROM users u
              LEFT JOIN staff s ON s.user_id = u.id
              LEFT JOIN patients p ON p.user_id = u.id OR p.id = u.patient_id";

        public List<UserAccountRow> GetAccounts()
        {
            var list = new List<UserAccountRow>();

            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(AccountSelect + " ORDER BY u.id", conn);

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
                list.Add(ReadAccount(reader));

            return list;
        }

        public void CreateAccount(UserAccountRow account, string password, byte[]? photo)
        {
            using var conn = OpenConnection();
            using var transaction = conn.BeginTransaction();

            try
            {
                long userId;
                using (var cmd = new MySqlCommand(
                    "INSERT INTO users (username, email, password_hash, role, status) " +
                    "VALUES (@username, @email, @passwordHash, @role, 'Active')", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@username", account.Username);
                    cmd.Parameters.AddWithValue("@email", account.Email);
                    cmd.Parameters.AddWithValue("@passwordHash", BCrypt.Net.BCrypt.HashPassword(password));
                    cmd.Parameters.AddWithValue("@role", account.UserRole);
                    cmd.ExecuteNonQuery();
                    userId = cmd.LastInsertedId;
                }

                if (account.UserRole == "Staff")
                {
                    using var cmd = new MySqlCommand(
                        "INSERT INTO staff (user_id, full_name, position, contact) " +
                        "VALUES (@userId, @fullName, @position, @contact)", conn, transaction);
                    cmd.Parameters.AddWithValue("@userId", userId);
                    cmd.Parameters.AddWithValue("@fullName", account.StaffFullName);
                    cmd.Parameters.AddWithValue("@position", account.Position);
                    cmd.Parameters.AddWithValue("@contact", DbValue(account.StaffContact));
                    cmd.ExecuteNonQuery();
                }
                else if (account.UserRole == "Patient")
                {
                    long patientId;
                    using (var cmd = new MySqlCommand(
                        "INSERT INTO patients (user_id, first_name, last_name, birthdate, gender, contact, address, allergies, photo) " +
                        "VALUES (@userId, @firstName, @lastName, @birthdate, @gender, @contact, @address, @allergies, @photo)",
                        conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        cmd.Parameters.AddWithValue("@firstName", account.FirstName);
                        cmd.Parameters.AddWithValue("@lastName", account.LastName);
                        cmd.Parameters.AddWithValue("@birthdate", DbValue(account.BirthDate?.Date));
                        cmd.Parameters.AddWithValue("@gender", DbValue(account.Gender));
                        cmd.Parameters.AddWithValue("@contact", DbValue(account.Contact));
                        cmd.Parameters.AddWithValue("@address", DbValue(account.Address));
                        cmd.Parameters.AddWithValue("@allergies", DbValue(account.Allergies));
                        cmd.Parameters.Add("@photo", MySqlDbType.MediumBlob).Value = DbValue(photo);
                        cmd.ExecuteNonQuery();
                        patientId = cmd.LastInsertedId;
                    }

                    using var updateUser = new MySqlCommand(
                        "UPDATE users SET patient_id = @patientId WHERE id = @userId", conn, transaction);
                    updateUser.Parameters.AddWithValue("@patientId", patientId);
                    updateUser.Parameters.AddWithValue("@userId", userId);
                    updateUser.ExecuteNonQuery();
                }
                else
                {
                    throw new InvalidOperationException("Choose Staff or Patient for the account.");
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public void UpdateAccount(UserAccountRow account, string? password, byte[]? newPhoto)
        {
            using var conn = OpenConnection();
            using var transaction = conn.BeginTransaction();

            try
            {
                string userSql = string.IsNullOrEmpty(password)
                    ? "UPDATE users SET username = @username, email = @email WHERE id = @id"
                    : "UPDATE users SET username = @username, email = @email, password_hash = @passwordHash WHERE id = @id";
                using (var cmd = new MySqlCommand(userSql, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@username", account.Username);
                    cmd.Parameters.AddWithValue("@email", account.Email);
                    cmd.Parameters.AddWithValue("@id", account.Id);
                    if (!string.IsNullOrEmpty(password))
                        cmd.Parameters.AddWithValue("@passwordHash", BCrypt.Net.BCrypt.HashPassword(password));
                    cmd.ExecuteNonQuery();
                }

                if (account.UserRole == "Staff")
                {
                    using var cmd = new MySqlCommand(
                        "UPDATE staff SET full_name = @fullName, position = @position, contact = @contact " +
                        "WHERE user_id = @id", conn, transaction);
                    cmd.Parameters.AddWithValue("@fullName", account.StaffFullName);
                    cmd.Parameters.AddWithValue("@position", account.Position);
                    cmd.Parameters.AddWithValue("@contact", DbValue(account.StaffContact));
                    cmd.Parameters.AddWithValue("@id", account.Id);
                    cmd.ExecuteNonQuery();
                }
                else if (account.UserRole == "Patient")
                {
                    string patientSql = newPhoto == null
                        ? "UPDATE patients SET first_name = @firstName, last_name = @lastName, birthdate = @birthdate, " +
                          "gender = @gender, contact = @contact, address = @address, allergies = @allergies " +
                          "WHERE user_id = @id OR id = (SELECT patient_id FROM users WHERE id = @id)"
                        : "UPDATE patients SET first_name = @firstName, last_name = @lastName, birthdate = @birthdate, " +
                          "gender = @gender, contact = @contact, address = @address, allergies = @allergies, photo = @photo " +
                          "WHERE user_id = @id OR id = (SELECT patient_id FROM users WHERE id = @id)";
                    using var cmd = new MySqlCommand(patientSql, conn, transaction);
                    cmd.Parameters.AddWithValue("@firstName", account.FirstName);
                    cmd.Parameters.AddWithValue("@lastName", account.LastName);
                    cmd.Parameters.AddWithValue("@birthdate", DbValue(account.BirthDate?.Date));
                    cmd.Parameters.AddWithValue("@gender", DbValue(account.Gender));
                    cmd.Parameters.AddWithValue("@contact", DbValue(account.Contact));
                    cmd.Parameters.AddWithValue("@address", DbValue(account.Address));
                    cmd.Parameters.AddWithValue("@allergies", DbValue(account.Allergies));
                    cmd.Parameters.AddWithValue("@id", account.Id);
                    if (newPhoto != null)
                        cmd.Parameters.Add("@photo", MySqlDbType.MediumBlob).Value = newPhoto;
                    cmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public void SetStatus(int accountId, string status, string currentUsername)
        {
            using var conn = OpenConnection();
            using var transaction = conn.BeginTransaction();

            try
            {
                string username;
                string role;
                using (var cmd = new MySqlCommand(
                    "SELECT username, role FROM users WHERE id = @id FOR UPDATE", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@id", accountId);
                    using var reader = cmd.ExecuteReader();
                    if (!reader.Read())
                        throw new InvalidOperationException("The selected account no longer exists.");
                    username = reader.GetString(0);
                    role = reader.GetString(1);
                }

                if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Admin accounts cannot be deactivated.");

                if (status == "Inactive" &&
                    string.Equals(username, currentUsername, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("You cannot deactivate your own account.");

                using (var cmd = new MySqlCommand(
                    "UPDATE users SET status = @status WHERE id = @id AND role <> 'Admin'",
                    conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@status", status);
                    cmd.Parameters.AddWithValue("@id", accountId);
                    if (cmd.ExecuteNonQuery() == 0)
                        throw new InvalidOperationException("Admin accounts cannot be deactivated.");
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private static UserAccountRow ReadAccount(MySqlDataReader reader)
        {
            return new UserAccountRow
            {
                Id = Convert.ToInt32(reader["id"]),
                Username = Text(reader, "username"),
                Name = Text(reader, "display_name"),
                Role = Text(reader, "display_role"),
                UserRole = Text(reader, "role"),
                Email = Text(reader, "email"),
                Status = Text(reader, "status"),
                Position = Text(reader, "position"),
                StaffFullName = Text(reader, "staff_full_name"),
                StaffContact = Text(reader, "staff_contact"),
                FirstName = Text(reader, "first_name"),
                LastName = Text(reader, "last_name"),
                BirthDate = reader.IsDBNull(reader.GetOrdinal("birthdate"))
                    ? null : Convert.ToDateTime(reader["birthdate"]),
                Gender = Text(reader, "gender"),
                Contact = Text(reader, "patient_contact"),
                Address = Text(reader, "address"),
                Allergies = Text(reader, "allergies"),
                Photo = reader.IsDBNull(reader.GetOrdinal("photo")) ? null : (byte[])reader["photo"]
            };
        }

        private static string Text(MySqlDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? "" : Convert.ToString(reader.GetValue(ordinal)) ?? "";
        }

        private static object DbValue(object? value)
        {
            return value == null || value is string text && string.IsNullOrWhiteSpace(text)
                ? DBNull.Value
                : value;
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

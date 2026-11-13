using System;
using System.Collections.Generic;
using System.Globalization;
using MySqlConnector;

namespace ClinicaCentral
{
    public class ReceiptRecord
    {
        public int Id { get; set; }
        public DateTime ReceiptDate { get; set; }
        public string Patient { get; set; } = "";
        public string Doctor { get; set; } = "";
        public string Purpose { get; set; } = "";
        public string Status { get; set; } = "";
        public string IssuedBy { get; set; } = "";

        public string Code
        {
            get { return $"R-{Id:D4}"; }
        }

        public string CodeAndDate
        {
            get { return Code + " · " + ReceiptDate.ToString("MMM d", CultureInfo.InvariantCulture); }
        }

        public string FullDateText
        {
            get { return ReceiptDate.ToString("MMM d, yyyy", CultureInfo.InvariantCulture); }
        }

        public string DoctorText
        {
            get { return Doctor.Length == 0 ? "Unassigned" : Doctor; }
        }

        public bool HasStatus(string status)
        {
            return string.Equals(Status, status, StringComparison.OrdinalIgnoreCase);
        }
    }

    public class ReceiptRepository : Repository
    {
        public ReceiptRepository(DatabaseConnection db) : base(db) { }

        public List<ReceiptRecord> GetAll(User user)
        {
            var list = new List<ReceiptRecord>();

            string scope = GetDoctorScope(user);
            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "SELECT r.id, r.receipt_date, CONCAT(p.first_name, ' ', p.last_name), " +
                "r.doctor, r.purpose, r.status, r.issued_by " +
                "FROM receipts r JOIN patients p ON p.id = r.patient_id " +
                "WHERE 1 = 1" + scope + " ORDER BY r.id DESC", conn);
            AddScopeParameters(cmd, user);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(ReadReceipt(reader));

            return list;
        }

        public ReceiptRecord? GetById(int receiptId, User user)
        {
            string scope = GetDoctorScope(user);
            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "SELECT r.id, r.receipt_date, CONCAT(p.first_name, ' ', p.last_name), " +
                "r.doctor, r.purpose, r.status, r.issued_by " +
                "FROM receipts r JOIN patients p ON p.id = r.patient_id " +
                "WHERE r.id = @id" + scope, conn);
            cmd.Parameters.AddWithValue("@id", receiptId);
            AddScopeParameters(cmd, user);

            using var reader = cmd.ExecuteReader();
            return reader.Read() ? ReadReceipt(reader) : null;
        }

        public void MarkPrinted(int receiptId, User user)
        {
            string scope = GetDoctorScope(user);
            using var conn = OpenConnection();
            using var transaction = conn.BeginTransaction();

            try
            {
                using (var check = new MySqlCommand(
                    "SELECT r.id FROM receipts r WHERE r.id = @id" + scope + " FOR UPDATE",
                    conn, transaction))
                {
                    check.Parameters.AddWithValue("@id", receiptId);
                    AddScopeParameters(check, user);
                    if (check.ExecuteScalar() == null)
                        throw new UnauthorizedAccessException("You cannot access this receipt.");
                }

                using (var update = new MySqlCommand(
                    "UPDATE receipts SET status = 'Printed' WHERE id = @id", conn, transaction))
                {
                    update.Parameters.AddWithValue("@id", receiptId);
                    update.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private static string GetDoctorScope(User user)
        {
            if (user is AdminUser)
                return "";

            if (user is StaffUser)
                return " AND r.doctor = (" +
                    "SELECT s.full_name FROM staff s JOIN users u ON u.id = s.user_id " +
                    "WHERE u.username = @username LIMIT 1)";

            throw new UnauthorizedAccessException("Only admins and staff can access receipts.");
        }

        private static void AddScopeParameters(MySqlCommand command, User user)
        {
            if (user is StaffUser)
                command.Parameters.AddWithValue("@username", user.Username);
        }

        private static ReceiptRecord ReadReceipt(MySqlDataReader reader)
        {
            return new ReceiptRecord
            {
                Id = Convert.ToInt32(reader.GetValue(0)),
                ReceiptDate = Convert.ToDateTime(reader.GetValue(1)),
                Patient = Convert.ToString(reader.GetValue(2)) ?? "",
                Doctor = reader.IsDBNull(3) ? "" : Convert.ToString(reader.GetValue(3)) ?? "",
                Purpose = reader.IsDBNull(4) ? "" : Convert.ToString(reader.GetValue(4)) ?? "",
                Status = reader.IsDBNull(5) ? "" : Convert.ToString(reader.GetValue(5)) ?? "",
                IssuedBy = reader.IsDBNull(6) ? "" : Convert.ToString(reader.GetValue(6)) ?? ""
            };
        }
    }
}

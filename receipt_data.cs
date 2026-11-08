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
            get { return "R-" + Id.ToString("0000"); }
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

        public List<ReceiptRecord> GetAll()
        {
            var list = new List<ReceiptRecord>();

            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "SELECT r.id, r.receipt_date, CONCAT(p.first_name, ' ', p.last_name), " +
                "r.doctor, r.purpose, r.status, r.issued_by " +
                "FROM receipts r JOIN patients p ON p.id = r.patient_id " +
                "ORDER BY r.id DESC", conn);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                ReceiptRecord item = new ReceiptRecord();
                item.Id = Convert.ToInt32(reader.GetValue(0));
                item.ReceiptDate = Convert.ToDateTime(reader.GetValue(1));
                item.Patient = Convert.ToString(reader.GetValue(2)) ?? "";
                item.Doctor = reader.IsDBNull(3) ? "" : Convert.ToString(reader.GetValue(3)) ?? "";
                item.Purpose = reader.IsDBNull(4) ? "" : Convert.ToString(reader.GetValue(4)) ?? "";
                item.Status = reader.IsDBNull(5) ? "" : Convert.ToString(reader.GetValue(5)) ?? "";
                item.IssuedBy = reader.IsDBNull(6) ? "" : Convert.ToString(reader.GetValue(6)) ?? "";
                list.Add(item);
            }
            return list;
        }
    }
}

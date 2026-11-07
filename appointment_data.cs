using System;
using System.Collections.Generic;
using System.Globalization;
using MySqlConnector;

namespace ClinicaCentral
{
    public class AppointmentRecord
    {
        public int Id { get; set; }
        public DateTime When { get; set; }
        public string Patient { get; set; } = "";
        public string Doctor { get; set; } = "";
        public string Status { get; set; } = "";

        public string WhenText
        {
            get { return When.ToString("MMM d · h:mm tt", CultureInfo.InvariantCulture); }
        }

        public string DoctorText
        {
            get { return Doctor.Length == 0 ? "Unassigned" : Doctor; }
        }

        public bool IsFinished
        {
            get { return HasStatus("Completed") || HasStatus("Cancelled"); }
        }

        public bool CanCancel
        {
            get { return !IsFinished; }
        }

        public bool HasStatus(string status)
        {
            return string.Equals(Status, status, StringComparison.OrdinalIgnoreCase);
        }
    }

    public class AppointmentRepository : Repository
    {
        public AppointmentRepository(DatabaseConnection db) : base(db) { }

        public List<AppointmentRecord> GetAll()
        {
            var upcoming = new List<AppointmentRecord>();
            var finished = new List<AppointmentRecord>();

            using var conn = OpenConnection();
            EnsureDoctorColumn(conn);

            using var cmd = new MySqlCommand(
                "SELECT a.id, a.appointment_date, CONCAT(p.first_name, ' ', p.last_name), a.doctor, a.status " +
                "FROM appointments a JOIN patients p ON p.id = a.patient_id " +
                "ORDER BY a.appointment_date", conn);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                AppointmentRecord item = new AppointmentRecord();
                item.Id = Convert.ToInt32(reader.GetValue(0));
                item.When = Convert.ToDateTime(reader.GetValue(1));
                item.Patient = Convert.ToString(reader.GetValue(2)) ?? "";
                item.Doctor = reader.IsDBNull(3) ? "" : Convert.ToString(reader.GetValue(3)) ?? "";
                item.Status = reader.IsDBNull(4) ? "" : Convert.ToString(reader.GetValue(4)) ?? "";

                if (item.IsFinished)
                    finished.Insert(0, item);
                else
                    upcoming.Add(item);
            }

            upcoming.AddRange(finished);
            return upcoming;
        }

        public void Cancel(int id)
        {
            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "UPDATE appointments SET status = 'Cancelled' " +
                "WHERE id = @id AND status NOT IN ('Completed', 'Cancelled')", conn);
            cmd.Parameters.AddWithValue("@id", id);

            if (cmd.ExecuteNonQuery() == 0)
                throw new InvalidOperationException("This appointment can no longer be cancelled.");
        }

        private void EnsureDoctorColumn(MySqlConnection conn)
        {
            using var check = new MySqlCommand(
                "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS " +
                "WHERE TABLE_SCHEMA = DATABASE() " +
                "AND TABLE_NAME = 'appointments' " +
                "AND COLUMN_NAME = 'doctor'", conn);

            bool exists = Convert.ToInt32(check.ExecuteScalar()) > 0;
            if (exists) return;

            using var alter = new MySqlCommand(
                "ALTER TABLE appointments ADD COLUMN doctor VARCHAR(100) NULL", conn);
            alter.ExecuteNonQuery();
        }
    }
}

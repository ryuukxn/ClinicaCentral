using System;
using System.Collections.Generic;
using System.Globalization;
using MySqlConnector;

namespace ClinicaCentral
{
    public class MyAppointmentRow
    {
        public int Id { get; set; }
        public DateTime When { get; set; }
        public string Doctor { get; set; } = "";
        public string Status { get; set; } = "";

        public string DateText
        {
            get { return When.ToString("MM-dd-yyyy", CultureInfo.InvariantCulture); }
        }

        public string TimeText
        {
            get { return When.ToString("hh:mm tt", CultureInfo.InvariantCulture); }
        }

        public string DoctorText
        {
            get { return Doctor.Length == 0 ? "Unassigned" : Doctor; }
        }

        private bool HasStatus(string status)
        {
            return string.Equals(Status, status, StringComparison.OrdinalIgnoreCase);
        }

        public bool CanCancel
        {
            get
            {
                bool open = HasStatus("Pending") || HasStatus("Confirmed") || HasStatus("Scheduled");
                return open && When > DateTime.Now;
            }
        }

        public bool ShowView
        {
            get { return !CanCancel; }
        }
    }

    public class MyAppointmentsRepository : Repository
    {
        public MyAppointmentsRepository(DatabaseConnection db) : base(db) { }

        public List<MyAppointmentRow> GetAll(int patientId)
        {
            var list = new List<MyAppointmentRow>();

            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "SELECT id, appointment_date, doctor, status FROM appointments " +
                "WHERE patient_id = @id ORDER BY appointment_date DESC", conn);
            cmd.Parameters.AddWithValue("@id", patientId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new MyAppointmentRow
                {
                    Id = Convert.ToInt32(reader.GetValue(0)),
                    When = Convert.ToDateTime(reader.GetValue(1)),
                    Doctor = reader.IsDBNull(2) ? "" : Convert.ToString(reader.GetValue(2)) ?? "",
                    Status = reader.IsDBNull(3) ? "" : Convert.ToString(reader.GetValue(3)) ?? ""
                });
            }
            return list;
        }
    }
}

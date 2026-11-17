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

        public bool HasStatus(string status)
        {
            return string.Equals(Status, status, StringComparison.OrdinalIgnoreCase);
        }
    }

    public class AppointmentDetails
    {
        public int Id { get; set; }
        public int PatientId { get; set; }
        public string PatientName { get; set; } = "";
        public string PatientCode => PatientRow.FormatId(PatientId);
        public string Contact { get; set; } = "";
        public string Email { get; set; } = "";
        public DateTime AppointmentDate { get; set; }
        public string Notes { get; set; } = "";
        public string Status { get; set; } = "";
        public string AttachmentFileName { get; set; } = "";
        public byte[]? AttachmentData { get; set; }
        public DateTime? AttachmentUploadedAt { get; set; }
        public bool HasAttachment => AttachmentData != null && AttachmentData.Length > 0;
        public string AttachmentUploadedAtText =>
            AttachmentUploadedAt?.ToString("MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture) ?? "";
    }

    public class AppointmentRepository : Repository
    {
        public AppointmentRepository(DatabaseConnection db) : base(db) { }

        public List<AppointmentRecord> GetAll()
        {
            var upcoming = new List<AppointmentRecord>();
            var finished = new List<AppointmentRecord>();

            using var conn = OpenConnection();
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
                    finished.Add(item);
                else
                    upcoming.Add(item);
            }

            finished.Reverse();
            upcoming.AddRange(finished);
            return upcoming;
        }
    }

    public class AppointmentDetailsRepository : Repository
    {
        private const string DoctorDetailsQuery =
            @"SELECT a.id, a.patient_id, CONCAT(p.first_name, ' ', p.last_name),
                     p.contact, patient_user.email, a.appointment_date, a.notes, a.status
              FROM appointments a
              JOIN patients p ON p.id = a.patient_id
              LEFT JOIN users patient_user ON patient_user.id = p.user_id
              JOIN users requester ON requester.username = @username AND requester.role = 'Staff'
              JOIN staff current_doctor ON current_doctor.user_id = requester.id
                   AND current_doctor.position = 'Doctor'
              WHERE a.id = @appointmentId AND a.staff_id = current_doctor.id
              LIMIT 1";

        private const string AdminDetailsQuery =
            @"SELECT a.id, a.patient_id, CONCAT(p.first_name, ' ', p.last_name),
                     p.contact, patient_user.email, a.appointment_date, a.notes, a.status
              FROM appointments a
              JOIN patients p ON p.id = a.patient_id
              LEFT JOIN users patient_user ON patient_user.id = p.user_id
              JOIN users requester ON requester.username = @username AND requester.role = 'Admin'
              WHERE a.id = @appointmentId
              LIMIT 1";

        public AppointmentDetailsRepository(DatabaseConnection db) : base(db) { }

        public AppointmentDetails? GetForDoctor(int appointmentId, string username)
        {
            return GetDetails(DoctorDetailsQuery, appointmentId, username);
        }

        public AppointmentDetails? GetForAdmin(int appointmentId, string username)
        {
            return GetDetails(AdminDetailsQuery, appointmentId, username);
        }

        public bool ConfirmForDoctor(int appointmentId, string username)
        {
            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                @"UPDATE appointments a
                  JOIN users requester ON requester.username = @username AND requester.role = 'Staff'
                  JOIN staff current_doctor ON current_doctor.user_id = requester.id
                       AND current_doctor.position = 'Doctor'
                  SET a.status = @status
                  WHERE a.id = @appointmentId
                    AND a.staff_id = current_doctor.id
                    AND a.status = 'Pending'", conn);
            cmd.Parameters.AddWithValue("@status", "Confirmed");
            cmd.Parameters.AddWithValue("@username", username);
            cmd.Parameters.AddWithValue("@appointmentId", appointmentId);
            return cmd.ExecuteNonQuery() == 1;
        }

        private AppointmentDetails? GetDetails(string query, int appointmentId, string username)
        {
            AppointmentDetails details;

            using (var conn = OpenConnection())
            using (var cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@username", username);
                cmd.Parameters.AddWithValue("@appointmentId", appointmentId);

                using var reader = cmd.ExecuteReader();
                if (!reader.Read())
                    return null;

                details = new AppointmentDetails
                {
                    Id = Convert.ToInt32(reader.GetValue(0)),
                    PatientId = Convert.ToInt32(reader.GetValue(1)),
                    PatientName = Convert.ToString(reader.GetValue(2)) ?? "",
                    Contact = reader.IsDBNull(3) ? "" : Convert.ToString(reader.GetValue(3)) ?? "",
                    Email = reader.IsDBNull(4) ? "" : Convert.ToString(reader.GetValue(4)) ?? "",
                    AppointmentDate = Convert.ToDateTime(reader.GetValue(5)),
                    Notes = reader.IsDBNull(6) ? "" : Convert.ToString(reader.GetValue(6)) ?? "",
                    Status = reader.IsDBNull(7) ? "" : Convert.ToString(reader.GetValue(7)) ?? ""
                };
            }

            PatientUpload? upload = new PatientUploadRepository(Db).GetLatest(details.PatientId);
            if (upload != null)
            {
                details.AttachmentFileName = upload.FileName;
                details.AttachmentData = upload.Data;
                details.AttachmentUploadedAt = upload.UploadedAt;
            }

            return details;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using MySqlConnector;

namespace ClinicaCentral
{
    public class WeeklyAvailability
    {
        public static readonly string[] Slots = { "Morning", "Afternoon" };

        private readonly string[,] _status = new string[2, 6];

        public WeeklyAvailability()
        {
            for (int slot = 0; slot < 2; slot++)
                for (int day = 0; day < 6; day++)
                    _status[slot, day] = "Off";
        }

        public void Set(string slot, int weekday, string status)
        {
            int row = Array.FindIndex(Slots, s => string.Equals(s, slot, StringComparison.OrdinalIgnoreCase));
            int col = weekday;

            if (row < 0 || col < 0 || col > 5) return;
            _status[row, col] = string.IsNullOrEmpty(status) ? "Off" : status;
        }

        public string Get(int slotIndex, int dayIndex)
        {
            return _status[slotIndex, dayIndex];
        }
    }

    public class TimeOffRow
    {
        public int Id { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Reason { get; set; } = "";
        public string Type { get; set; } = "";
        public string Scope { get; set; } = "Both";

        public string TypeCategory
        {
            get { return Type; }
        }

        public string ScopeText
        {
            get { return Scope == "Both" ? "AM & PM" : Scope; }
        }

        public string DateText
        {
            get
            {
                if (StartDate.Date == EndDate.Date)
                    return StartDate.ToString("ddd, MMM d", CultureInfo.InvariantCulture);

                if (StartDate.Year == EndDate.Year && StartDate.Month == EndDate.Month)
                    return StartDate.ToString("MMM d", CultureInfo.InvariantCulture) + " \u2013 " + EndDate.Day;

                return StartDate.ToString("MMM d", CultureInfo.InvariantCulture) + " \u2013 " +
                       EndDate.ToString("MMM d", CultureInfo.InvariantCulture);
            }
        }

        public string TypeText
        {
            get { return TypeCategory.Length == 0 ? "Busy" : TypeCategory + " · " + ScopeText; }
        }
    }

    public class BookingRequestRow
    {
        public int Id { get; set; }
        public DateTime When { get; set; }
        public string Patient { get; set; } = "";

        public string WhenText
        {
            get { return When.ToString("ddd, MMM d \u00B7 h:mm tt", CultureInfo.InvariantCulture); }
        }
    }

    public class TodayRow
    {
        public int Id { get; set; }
        public DateTime When { get; set; }
        public string Patient { get; set; } = "";
        public string Status { get; set; } = "";

        public string TimeText
        {
            get { return When.ToString("h:mm tt", CultureInfo.InvariantCulture); }
        }

        public bool HasStatus(string status)
        {
            return string.Equals(Status, status, StringComparison.OrdinalIgnoreCase);
        }

        public bool CanComplete
        {
            get { return HasStatus("Scheduled") || HasStatus("Confirmed"); }
        }

        public bool ShowBadge
        {
            get { return !CanComplete; }
        }
    }

    public class ConfirmedRow
    {
        public int Id { get; set; }
        public int PatientId { get; set; }
        public DateTime When { get; set; }
        public string Patient { get; set; } = "";
        public string DoctorName { get; set; } = "";

        public string WhenText
        {
            get { return When.ToString("ddd, MMM d \u00B7 h:mm tt", CultureInfo.InvariantCulture); }
        }

        public string DateText
        {
            get { return When.ToString("ddd, MMM d", CultureInfo.InvariantCulture); }
        }

        public string TimeText
        {
            get { return When.ToString("h:mm tt", CultureInfo.InvariantCulture); }
        }
    }

    public class ScheduleRepository : Repository
    {
        public ScheduleRepository(DatabaseConnection db) : base(db) { }

        public List<ConfirmedRow> GetConfirmed(string username)
        {
            var list = new List<ConfirmedRow>();

            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                @"SELECT a.id, a.patient_id, a.appointment_date, CONCAT(p.first_name, ' ', p.last_name), s.full_name
                FROM appointments a
                JOIN patients p ON p.id = a.patient_id
                JOIN staff s ON s.id = a.staff_id
                JOIN users u ON u.id = s.user_id
                WHERE u.username = @u
                    AND a.status = 'Confirmed'
                    AND a.appointment_date >= CURDATE()
                ORDER BY a.appointment_date", conn);
            cmd.Parameters.AddWithValue("@u", username);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new ConfirmedRow
                {
                    Id = Convert.ToInt32(reader.GetValue(0)),
                    PatientId = Convert.ToInt32(reader.GetValue(1)),
                    When = Convert.ToDateTime(reader.GetValue(2)),
                    Patient = Convert.ToString(reader.GetValue(3)) ?? "",
                    DoctorName = reader.IsDBNull(4) ? "" : Convert.ToString(reader.GetValue(4)) ?? ""
                });
            }
            return list;
        }

        public void CompleteAppointment(AppointmentCompletion completion)
        {
            using var conn = OpenConnection();
            using var transaction = conn.BeginTransaction();

            try
            {
                long medicalRecordId;
                using (var cmd = new MySqlCommand(
                    @"INSERT INTO medical_records
                        (appointment_id, patient_id, record_date, diagnosis, doctor, notes, bp, temperature, pulse, weight_kg, height, treatment)
                      VALUES
                        (@appointmentId, @patientId, CURDATE(), @diagnosis, @doctor, @notes, @bp, @temperature, @pulse, @weightKg, @heightCm, @treatment)",
                    conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@appointmentId", completion.AppointmentId);
                    cmd.Parameters.AddWithValue("@patientId", completion.PatientId);
                    cmd.Parameters.AddWithValue("@diagnosis", completion.Diagnosis);
                    cmd.Parameters.AddWithValue("@doctor", completion.Doctor);
                    cmd.Parameters.AddWithValue("@notes", DbValue(completion.Notes));
                    cmd.Parameters.AddWithValue("@bp", DbValue(completion.Bp));
                    cmd.Parameters.AddWithValue("@temperature", DbValue(completion.Temperature));
                    cmd.Parameters.AddWithValue("@pulse", DbValue(completion.Pulse));
                    cmd.Parameters.AddWithValue("@weightKg", DbValue(completion.WeightKg));
                    cmd.Parameters.AddWithValue("@heightCm", DbValue(completion.HeightCm));
                    cmd.Parameters.AddWithValue("@treatment", DbValue(completion.Treatment));
                    cmd.ExecuteNonQuery();
                    medicalRecordId = cmd.LastInsertedId;
                }

                if (completion.NeedsPrescription)
                {
                    if (completion.Prescriptions.Count == 0)
                        throw new InvalidOperationException("At least one prescription is required.");

                    foreach (PrescriptionInput prescription in completion.Prescriptions)
                    {
                        if (string.IsNullOrWhiteSpace(prescription.MedicineName) ||
                            string.IsNullOrWhiteSpace(prescription.Dosage) ||
                            string.IsNullOrWhiteSpace(prescription.Frequency) ||
                            string.IsNullOrWhiteSpace(prescription.Duration))
                            throw new InvalidOperationException("Prescription name, dosage, frequency, and duration are required.");

                        using var cmd = new MySqlCommand(
                        @"INSERT INTO prescriptions
                            (medical_record_id, medicine_name, dosage, frequency, duration, description)
                          VALUES
                            (@medicalRecordId, @medicineName, @dosage, @frequency, @duration, @description)",
                            conn, transaction);
                        cmd.Parameters.AddWithValue("@medicalRecordId", medicalRecordId);
                        cmd.Parameters.AddWithValue("@medicineName", prescription.MedicineName);
                        cmd.Parameters.AddWithValue("@dosage", prescription.Dosage);
                        cmd.Parameters.AddWithValue("@frequency", prescription.Frequency);
                        cmd.Parameters.AddWithValue("@duration", prescription.Duration);
                        cmd.Parameters.AddWithValue("@description", DbValue(prescription.Description));
                        cmd.ExecuteNonQuery();
                    }
                }

                using (var cmd = new MySqlCommand(
                    @"UPDATE appointments a
                      JOIN staff s ON s.id = a.staff_id
                      JOIN users u ON u.id = s.user_id
                      SET a.status = 'Completed'
                      WHERE a.id = @appointmentId
                        AND a.patient_id = @patientId
                        AND u.username = @doctor
                        AND a.status = 'Confirmed'",
                    conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@appointmentId", completion.AppointmentId);
                    cmd.Parameters.AddWithValue("@patientId", completion.PatientId);
                    cmd.Parameters.AddWithValue("@doctor", completion.Doctor);
                    if (cmd.ExecuteNonQuery() != 1)
                        throw new InvalidOperationException("This appointment is no longer confirmed or assigned to your account.");
                }

                using (var cmd = new MySqlCommand(
                    @"INSERT INTO receipts
                        (patient_id, appointment_id, doctor, purpose, status, issued_by, receipt_date)
                      SELECT a.patient_id, a.id, s.full_name, 'Consultation', 'Issued', @issuedBy, CURDATE()
                      FROM appointments a
                      JOIN staff s ON s.id = a.staff_id
                      JOIN users u ON u.id = s.user_id
                      WHERE a.id = @appointmentId
                        AND a.patient_id = @patientId
                        AND u.username = @doctor
                        AND a.status = 'Completed'",
                    conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@appointmentId", completion.AppointmentId);
                    cmd.Parameters.AddWithValue("@patientId", completion.PatientId);
                    cmd.Parameters.AddWithValue("@doctor", completion.Doctor);
                    cmd.Parameters.AddWithValue("@issuedBy", completion.Doctor);
                    if (cmd.ExecuteNonQuery() != 1)
                        throw new InvalidOperationException("The receipt could not be created for this appointment.");
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private static object DbValue(object? value)
        {
            return value ?? DBNull.Value;
        }


        public void UpdateAppointmentStatus(int appointmentId, string status)
        {
            using var conn = OpenConnection();
            string query = "UPDATE appointments SET status = @status WHERE id = @id";
            using var cmd = new MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@status", status);
            cmd.Parameters.AddWithValue("@id", appointmentId);
            cmd.ExecuteNonQuery();
        }

        public WeeklyAvailability GetAvailability(string username)
        {
            var week = new WeeklyAvailability();

            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "SELECT weekday, slot, status FROM staff_availability WHERE username = @u", conn);
            cmd.Parameters.AddWithValue("@u", username);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                int weekday = Convert.ToInt32(reader.GetValue(0));
                string slot = Convert.ToString(reader.GetValue(1)) ?? "";
                string status = Convert.ToString(reader.GetValue(2)) ?? "Off";
                week.Set(slot, weekday, status);
            }
            return week;
        }

        public List<TimeOffRow> GetTimeOff(string username)
        {
            var list = new List<TimeOffRow>();

            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "SELECT id, start_date, end_date, reason, type FROM time_off " +
                "WHERE username = @u AND end_date >= CURDATE() ORDER BY start_date LIMIT 5", conn);
            cmd.Parameters.AddWithValue("@u", username);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new TimeOffRow
                {
                    Id = Convert.ToInt32(reader.GetValue(0)),
                    StartDate = Convert.ToDateTime(reader.GetValue(1)),
                    EndDate = Convert.ToDateTime(reader.GetValue(2)),
                    Reason = ParseTimeOffReason(reader.IsDBNull(3) ? "" : Convert.ToString(reader.GetValue(3)) ?? "", out string scope),
                    Type = reader.IsDBNull(4) ? "" : Convert.ToString(reader.GetValue(4)) ?? "",
                    Scope = scope
                });
            }
            return list;
        }

        
        public List<TimeOffRow> GetTimeOffBetween(string username, DateTime from, DateTime to)
        {
            var list = new List<TimeOffRow>();

            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "SELECT id, start_date, end_date, reason, type FROM time_off " +
                "WHERE username = @u AND start_date <= @to AND end_date >= @from ORDER BY start_date", conn);
            cmd.Parameters.AddWithValue("@u", username);
            cmd.Parameters.AddWithValue("@from", from.Date);
            cmd.Parameters.AddWithValue("@to", to.Date);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new TimeOffRow
                {
                    Id = Convert.ToInt32(reader.GetValue(0)),
                    StartDate = Convert.ToDateTime(reader.GetValue(1)),
                    EndDate = Convert.ToDateTime(reader.GetValue(2)),
                    Reason = ParseTimeOffReason(reader.IsDBNull(3) ? "" : Convert.ToString(reader.GetValue(3)) ?? "", out string scope),
                    Type = reader.IsDBNull(4) ? "" : Convert.ToString(reader.GetValue(4)) ?? "",
                    Scope = scope
                });
            }
            return list;
        }

        public void AddTimeOff(string username, DateTime startDate, DateTime endDate, string type, string scope, string? reason)
        {
            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "INSERT INTO time_off (username, start_date, end_date, reason, type) " +
                "VALUES (@username, @startDate, @endDate, @reason, @type)", conn);
            cmd.Parameters.AddWithValue("@username", username);
            cmd.Parameters.AddWithValue("@startDate", startDate.Date);
            cmd.Parameters.AddWithValue("@endDate", endDate.Date);
            cmd.Parameters.AddWithValue("@reason", "[" + scope + "] " + (reason ?? ""));
            cmd.Parameters.AddWithValue("@type", type);
            cmd.ExecuteNonQuery();
        }

        private static string ParseTimeOffReason(string storedReason, out string scope)
        {
            scope = "Both";
            foreach (string value in new[] { "AM", "PM", "Both" })
            {
                string prefix = "[" + value + "]";
                if (!storedReason.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                scope = value;
                return storedReason.Substring(prefix.Length).Trim();
            }

            return storedReason;
        }

        public Dictionary<string, string> GetDoctorDayAvailability(string doctorName, DateTime date)
        {
            int weekday = ((int)date.DayOfWeek + 6) % 7;

            var availability = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Morning", "Off" },
                { "Afternoon", "Off" }
            };

            using var conn = OpenConnection();
            using (var cmd = new MySqlCommand(
                @"SELECT sa.slot, sa.status
                  FROM staff_availability sa
                  JOIN users u ON u.username = sa.username
                  JOIN staff s ON s.user_id = u.id
                  WHERE s.full_name = @docName AND sa.weekday = @weekday", conn))
            {
                cmd.Parameters.AddWithValue("@docName", doctorName);
                cmd.Parameters.AddWithValue("@weekday", weekday);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string slot = Convert.ToString(reader.GetValue(0)) ?? "";
                    string status = Convert.ToString(reader.GetValue(1)) ?? "Off";
                    if (availability.ContainsKey(slot))
                        availability[slot] = status;
                }
            }

            using (var cmd = new MySqlCommand(
                @"SELECT t.reason
                  FROM time_off t
                  JOIN users u ON u.username = t.username
                  JOIN staff s ON s.user_id = u.id
                  WHERE s.full_name = @docName
                    AND t.start_date <= @date
                    AND t.end_date >= @date", conn))
            {
                cmd.Parameters.AddWithValue("@docName", doctorName);
                cmd.Parameters.AddWithValue("@date", date.Date);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string reason = reader.IsDBNull(0) ? "" : Convert.ToString(reader.GetValue(0)) ?? "";
                    ParseTimeOffReason(reason, out string scope);
                    if (scope == "AM" || scope == "Both")
                        availability["Morning"] = "Off";
                    if (scope == "PM" || scope == "Both")
                        availability["Afternoon"] = "Off";
                }
            }

            return availability;
        }

        public List<BookingRequestRow> GetBookingRequests()
        {
            var list = new List<BookingRequestRow>();

            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "SELECT a.id, a.appointment_date, CONCAT(p.first_name, ' ', p.last_name) " +
                "FROM appointments a JOIN patients p ON p.id = a.patient_id " +
                "WHERE a.status = 'Pending'" +
                "ORDER BY a.appointment_date LIMIT 20", conn);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new BookingRequestRow
                {
                    Id = Convert.ToInt32(reader.GetValue(0)),
                    When = Convert.ToDateTime(reader.GetValue(1)),
                    Patient = Convert.ToString(reader.GetValue(2)) ?? ""
                });
            }
            return list;
        }

        public List<TodayRow> GetToday()
        {
            var list = new List<TodayRow>();

            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "SELECT a.id, a.appointment_date, CONCAT(p.first_name, ' ', p.last_name), a.status " +
                "FROM appointments a JOIN patients p ON p.id = a.patient_id " +
                "WHERE DATE(a.appointment_date) = CURDATE() AND a.status <> 'Cancelled' " +
                "ORDER BY a.appointment_date", conn);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new TodayRow
                {
                    Id = Convert.ToInt32(reader.GetValue(0)),
                    When = Convert.ToDateTime(reader.GetValue(1)),
                    Patient = Convert.ToString(reader.GetValue(2)) ?? "",
                    Status = reader.IsDBNull(3) ? "" : Convert.ToString(reader.GetValue(3)) ?? ""
                });
            }
            return list;
        }

        public void UpdateAvailability(string doctorName, int slot, int day, string newStatus)
        {
            using var conn = Database.GetConnection();
            conn.Open();

            string slotName = slot == 0 ? "Morning" : "Afternoon";

            string query = @"INSERT INTO staff_availability (username, weekday, slot, status)
                            VALUES (@user, @day, @slot, @status)
                            ON DUPLICATE KEY UPDATE status = @status;";

            using var cmd = new MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@user", doctorName);
            cmd.Parameters.AddWithValue("@day", day); 
            cmd.Parameters.AddWithValue("@slot", slotName);
            cmd.Parameters.AddWithValue("@status", newStatus);

            cmd.ExecuteNonQuery();
        }
    }

    public sealed class AppointmentCompletion
    {
        public int AppointmentId { get; set; }
        public int PatientId { get; set; }
        public string Doctor { get; set; } = "";
        public string Diagnosis { get; set; } = "";
        public string? Notes { get; set; }
        public string? Bp { get; set; }
        public decimal? Temperature { get; set; }
        public int? Pulse { get; set; }
        public decimal? WeightKg { get; set; }
        public decimal? HeightCm { get; set; }
        public string? Treatment { get; set; }
        public bool NeedsPrescription { get; set; }
        public List<PrescriptionInput> Prescriptions { get; set; } = new List<PrescriptionInput>();
    }

    public sealed class PrescriptionInput
    {
        public string MedicineName { get; set; } = "";
        public string Dosage { get; set; } = "";
        public string Frequency { get; set; } = "";
        public string Duration { get; set; } = "";
        public string? Description { get; set; }
    }
}

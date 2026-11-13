using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using MySqlConnector;

namespace ClinicaCentral
{
    public class PatientProfile : Person
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = "";
        public DateTime? BirthDate { get; set; }
        public string Contact { get; set; } = "";

        public override string GetRoleTitle()
        {
            return "Patient";
        }

        public string Code
        {
            get { return PatientRow.FormatId(Id); }
        }
    }

    public class PatientAppointmentRow
    {
        public int Id { get; set; }
        public DateTime When { get; set; }
        public string Doctor { get; set; } = "";
        public string Status { get; set; } = "";

        public string DoctorText
        {
            get { return Doctor.Length == 0 ? "Unassigned" : Doctor; }
        }
    }

    public class PrescriptionRow
    {
        public int Id { get; set; }
        public string Medicine { get; set; } = "";
        public string Intake { get; set; } = "";
        public string Duration { get; set; } = "";
        public DateTime PrescribedOn { get; set; }
    }

    public class ConditionInfo
    {
        public string Label { get; }
        public string Message { get; }
        public string BackColor { get; }
        public string ForeColor { get; }

        private ConditionInfo(string diagnosis, string message, string back, string fore)
        {
            Label = diagnosis;
            Message = message;
            BackColor = back;
            ForeColor = fore;
        }

        public static ConditionInfo None
        {
            get { return new ConditionInfo("No result yet", "Your doctor has not recorded a result yet.", "#E8E8E8", "#777777"); }
        }

        public static ConditionInfo FromLabel(string label)
        {
            string diagnosis = string.IsNullOrWhiteSpace(label) ? "No result yet" : label.Trim();
            if (string.Equals(diagnosis, "Normal", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(diagnosis, "Normal / Healthy", StringComparison.OrdinalIgnoreCase))
                return new ConditionInfo(diagnosis, "You are in Good Health!", "#D4F3C4", "#2E9E2E");

            if (string.Equals(diagnosis, "Needs Attention", StringComparison.OrdinalIgnoreCase))
                return new ConditionInfo(diagnosis, "Please follow up with your doctor.", "#FFF0C2", "#B7791F");

            if (string.Equals(diagnosis, "Critical", StringComparison.OrdinalIgnoreCase))
                return new ConditionInfo(diagnosis, "Please contact your doctor right away.", "#FDE2E2", "#D93025");

            return new ConditionInfo(diagnosis, "", "#D9E8FB", "#2F5FCB");
        }

        public static ConditionInfo FromRecord(string diagnosis, string? notes)
        {
            ConditionInfo condition = FromLabel(diagnosis);
            string noteText = string.IsNullOrWhiteSpace(notes)
                ? "No additional notes."
                : "Doctor's Note: " + notes.Trim();
            return new ConditionInfo(condition.Label, noteText, condition.BackColor, condition.ForeColor);
        }
    }

    public class PatientDashboardRepository : Repository
    {
        public PatientDashboardRepository(DatabaseConnection db) : base(db) { }

        public PatientProfile? GetProfile(int patientId)
        {
            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "SELECT first_name, last_name, birthdate, contact FROM patients WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("@id", patientId);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;

            string first = Convert.ToString(reader.GetValue(0)) ?? "";
            string last = Convert.ToString(reader.GetValue(1)) ?? "";

            PatientProfile profile = new PatientProfile();
            profile.Id = patientId;
            profile.FirstName = first.Trim();
            profile.Name = first + " " + last;
            profile.BirthDate = reader.IsDBNull(2) ? (DateTime?)null : Convert.ToDateTime(reader.GetValue(2));
            profile.Contact = reader.IsDBNull(3) ? "" : Convert.ToString(reader.GetValue(3)) ?? "";
            return profile;
        }

        public int CountTotalAppointments(int patientId)
        {
            return Scalar("SELECT COUNT(*) FROM appointments WHERE patient_id = @id", patientId);
        }

        public int CountActivePrescriptions(int patientId)
        {
            return GetActivePrescriptions(patientId).Count;
        }

        public List<PatientAppointmentRow> GetUpcomingAppointments(int patientId)
        {
            var list = new List<PatientAppointmentRow>();

            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "SELECT a.id, a.appointment_date, COALESCE(s.full_name, a.doctor), a.status, a.patient_id " +
                "FROM appointments a LEFT JOIN staff s ON s.id = a.staff_id " +
                "WHERE a.patient_id = @id AND a.appointment_date >= CURDATE() AND a.status NOT IN ('Cancelled', 'Completed') " +
                "ORDER BY appointment_date LIMIT 5", conn);
            cmd.Parameters.AddWithValue("@id", patientId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new PatientAppointmentRow
                {
                    Id = Convert.ToInt32(reader.GetValue(0)),
                    When = Convert.ToDateTime(reader.GetValue(1)),
                    Doctor = reader.IsDBNull(2) ? "" : Convert.ToString(reader.GetValue(2)) ?? "",
                    Status = reader.IsDBNull(3) ? "" : Convert.ToString(reader.GetValue(3)) ?? ""
                });
            }
            return list;
        }

        public List<PrescriptionRow> GetActivePrescriptions(int patientId)
        {
            var list = new List<PrescriptionRow>();

            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "SELECT pr.prescription_id, pr.medicine_name, pr.dosage, pr.frequency, pr.duration, mr.record_date " +
                "FROM prescriptions pr JOIN medical_records mr ON mr.id = pr.medical_record_id " +
                "WHERE mr.patient_id = @id AND mr.is_deleted = 0 " +
                "ORDER BY mr.record_date DESC, pr.prescription_id DESC", conn);
            cmd.Parameters.AddWithValue("@id", patientId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string duration = reader.IsDBNull(4) ? "" : Convert.ToString(reader.GetValue(4)) ?? "";
                DateTime prescribedOn = Convert.ToDateTime(reader.GetValue(5));
                if (!IsPrescriptionActive(duration, prescribedOn, DateTime.Today))
                    continue;

                list.Add(new PrescriptionRow
                {
                    Id = Convert.ToInt32(reader.GetValue(0)),
                    Medicine = PrescriptionMedicine(
                        Convert.ToString(reader.GetValue(1)) ?? "",
                        reader.IsDBNull(2) ? "" : Convert.ToString(reader.GetValue(2)) ?? ""),
                    Intake = PrescriptionIntake(
                        reader.IsDBNull(3) ? "" : Convert.ToString(reader.GetValue(3)) ?? "",
                        duration),
                    Duration = duration,
                    PrescribedOn = prescribedOn
                });
            }
            return list;
        }

        public ConditionInfo GetLatestCondition(int patientId)
        {
            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "SELECT diagnosis, notes FROM medical_records " +
                "WHERE patient_id = @id AND is_deleted = 0 " +
                "ORDER BY record_date DESC, id DESC LIMIT 1", conn);
            cmd.Parameters.AddWithValue("@id", patientId);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return ConditionInfo.None;

            string diagnosis = reader.IsDBNull(0) ? "" : Convert.ToString(reader.GetValue(0)) ?? "";
            string notes = reader.IsDBNull(1) ? "" : Convert.ToString(reader.GetValue(1)) ?? "";
            return ConditionInfo.FromRecord(diagnosis, notes);
        }

        private static string PrescriptionMedicine(string medicine, string dosage)
        {
            return dosage.Length == 0 ? medicine : medicine + " · " + dosage;
        }

        private static string PrescriptionIntake(string frequency, string duration)
        {
            if (frequency.Length == 0) return duration;
            return duration.Length == 0 ? frequency : frequency + " · " + duration;
        }

        private static bool IsPrescriptionActive(string duration, DateTime prescribedOn, DateTime today)
        {
            MatchCollection matches = Regex.Matches(
                duration,
                @"(?<amount>\d+)\s*(?<unit>days?|d|weeks?|w|months?|mos?|years?|yrs?)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (matches.Count == 0)
                return string.Equals(duration.Trim(), "ongoing", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(duration.Trim(), "continuous", StringComparison.OrdinalIgnoreCase);

            Match match = matches[matches.Count - 1];
            if (!int.TryParse(match.Groups["amount"].Value, NumberStyles.None,
                    CultureInfo.InvariantCulture, out int amount) || amount <= 0)
                return false;

            string unit = match.Groups["unit"].Value.ToLowerInvariant();
            DateTime expiresOn;
            try
            {
                if (unit.StartsWith("d", StringComparison.Ordinal))
                    expiresOn = prescribedOn.Date.AddDays(amount);
                else if (unit.StartsWith("w", StringComparison.Ordinal))
                    expiresOn = prescribedOn.Date.AddDays(amount * 7d);
                else if (unit.StartsWith("mo", StringComparison.Ordinal))
                    expiresOn = prescribedOn.Date.AddMonths(amount);
                else
                    expiresOn = prescribedOn.Date.AddYears(amount);
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }

            return today < expiresOn;
        }

        private int Scalar(string sql, int patientId)
        {
            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", patientId);

            object? result = cmd.ExecuteScalar();
            return result == null || result is DBNull ? 0 : Convert.ToInt32(result);
        }
    }
}

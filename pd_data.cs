using System;
using System.Collections.Generic;
using System.Globalization;
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
    }

    public class ConditionInfo
    {
        public string Label { get; }
        public string Message { get; }
        public string BackColor { get; }
        public string ForeColor { get; }

        private ConditionInfo(string label, string message, string back, string fore)
        {
            Label = label;
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
            if (string.Equals(label, "Normal", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(label, "Normal / Healthy", StringComparison.OrdinalIgnoreCase))
                return new ConditionInfo("Normal", "You are in Good Health!", "#D4F3C4", "#2E9E2E");

            if (string.Equals(label, "Needs Attention", StringComparison.OrdinalIgnoreCase))
                return new ConditionInfo("Needs Attention", "Please follow up with your doctor.", "#FFF0C2", "#B7791F");

            if (string.Equals(label, "Critical", StringComparison.OrdinalIgnoreCase))
                return new ConditionInfo("Critical", "Please contact your doctor right away.", "#FDE2E2", "#D93025");

            return None;
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

        public int CountPrescriptions(int patientId)
        {
            return Scalar(
                "SELECT COUNT(*) FROM prescriptions pr " +
                "JOIN medical_records mr ON mr.id = pr.id WHERE mr.patient_id = @id",
                patientId);
        }

        public List<PatientAppointmentRow> GetUpcomingAppointments(int patientId)
        {
            var list = new List<PatientAppointmentRow>();

            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "SELECT id, appointment_date, doctor, status FROM appointments " +
                "WHERE patient_id = @id AND appointment_date >= CURDATE() AND status NOT IN ('Cancelled', 'Completed') " +
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

        public List<PrescriptionRow> GetPrescriptions(int patientId)
        {
            var list = new List<PrescriptionRow>();

            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "SELECT pr.id, pr.medicine_name, pr.dosage " +
                "FROM prescriptions pr JOIN medical_records mr ON mr.id = pr.id " +
                "WHERE mr.patient_id = @id ORDER BY mr.record_date DESC, pr.id DESC LIMIT 6", conn);
            cmd.Parameters.AddWithValue("@id", patientId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new PrescriptionRow
                {
                    Id = Convert.ToInt32(reader.GetValue(0)),
                    Medicine = Convert.ToString(reader.GetValue(1)) ?? "",
                    Intake = reader.IsDBNull(2) ? "" : Convert.ToString(reader.GetValue(2)) ?? ""
                });
            }
            return list;
        }

        public ConditionInfo GetLatestCondition(int patientId)
        {
            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "SELECT COALESCE(result_condition, diagnosis) FROM medical_records " +
                "WHERE patient_id = @id AND (result_condition IS NOT NULL OR diagnosis IS NOT NULL) " +
                "ORDER BY record_date DESC, id DESC LIMIT 1", conn);
            cmd.Parameters.AddWithValue("@id", patientId);

            object? result = cmd.ExecuteScalar();
            if (result == null || result is DBNull) return ConditionInfo.None;

            string diagnosis = Convert.ToString(result) ?? "";
            if (string.Equals(diagnosis, "Normal / Healthy", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(diagnosis, "Normal", StringComparison.OrdinalIgnoreCase))
                return ConditionInfo.FromLabel("Normal");
            if (string.Equals(diagnosis, "Critical", StringComparison.OrdinalIgnoreCase))
                return ConditionInfo.FromLabel("Critical");
            return ConditionInfo.FromLabel("Needs Attention");
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

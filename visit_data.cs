using System;
using System.Collections.Generic;
using System.Globalization;
using MySqlConnector;

namespace ClinicaCentral
{
    public class PrescriptionLine
    {
        public string Medicine { get; set; } = "";
        public string Dosage { get; set; } = "";
        public string Frequency { get; set; } = "";
        public string Duration { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public class VisitSummary
    {
        public string DateText { get; set; } = "";
        public string TimeText { get; set; } = "";
        public string Doctor { get; set; } = "";
        public string Status { get; set; } = "";
        public string Diagnosis { get; set; } = "";
        public string Notes { get; set; } = "";
        public DateTime? FollowUp { get; set; }
        public List<PrescriptionLine> Prescriptions { get; } = new List<PrescriptionLine>();

        public string DateLine
        {
            get { return "Date: " + DateText; }
        }

        public string TimeLine
        {
            get { return "Time: " + TimeText; }
        }

        public string DoctorLine
        {
            get { return "Doctor: " + Doctor; }
        }

        public string DiagnosisLine
        {
            get { return "Diagnosis: " + (Diagnosis.Length == 0 ? "None recorded for this visit" : Diagnosis); }
        }

        public string NotesLine
        {
            get { return "Notes: " + (Notes.Length == 0 ? "None" : Notes); }
        }

        public bool HasFollowUp
        {
            get { return FollowUp != null; }
        }

        public string FollowUpText
        {
            get
            {
                return FollowUp == null
                    ? ""
                    : "Follow up: Next visit on " + FollowUp.Value.ToString("MM-dd-yyyy", CultureInfo.InvariantCulture);
            }
        }

        public bool HasPrescriptions
        {
            get { return Prescriptions.Count > 0; }
        }

        public bool NoPrescriptions
        {
            get { return Prescriptions.Count == 0; }
        }
    }

    public class VisitRepository : Repository
    {
        public VisitRepository(DatabaseConnection db) : base(db) { }

        public VisitSummary GetSummary(MyAppointmentRow appointment, int patientId)
        {
            VisitSummary summary = new VisitSummary();
            summary.DateText = appointment.DateText;
            summary.TimeText = appointment.TimeText;
            summary.Doctor = appointment.DoctorText;
            summary.Status = appointment.Status;

            using var conn = OpenConnection();

            using (var cmd = new MySqlCommand(
                "SELECT diagnosis, notes, follow_up_date FROM medical_records " +
                "WHERE patient_id = @id AND record_date = @day ORDER BY id DESC LIMIT 1", conn))
            {
                cmd.Parameters.AddWithValue("@id", patientId);
                cmd.Parameters.AddWithValue("@day", appointment.When.Date);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    summary.Diagnosis = reader.IsDBNull(0) ? "" : Convert.ToString(reader.GetValue(0)) ?? "";
                    summary.Notes = reader.IsDBNull(1) ? "" : Convert.ToString(reader.GetValue(1)) ?? "";
                    summary.FollowUp = reader.IsDBNull(2) ? (DateTime?)null : Convert.ToDateTime(reader.GetValue(2));
                }
            }

            using (var cmd = new MySqlCommand(
                "SELECT pr.medicine_name, pr.dosage, pr.frequency, pr.duration, pr.description " +
                "FROM prescriptions pr JOIN medical_records mr ON mr.id = pr.medical_record_id " +
                "WHERE mr.patient_id = @id AND mr.record_date = @day ORDER BY pr.prescription_id", conn))
            {
                cmd.Parameters.AddWithValue("@id", patientId);
                cmd.Parameters.AddWithValue("@day", appointment.When.Date);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    summary.Prescriptions.Add(new PrescriptionLine
                    {
                        Medicine = Convert.ToString(reader.GetValue(0)) ?? "",
                        Dosage = reader.IsDBNull(1) ? "" : Convert.ToString(reader.GetValue(1)) ?? "",
                        Frequency = reader.IsDBNull(2) ? "" : Convert.ToString(reader.GetValue(2)) ?? "",
                        Duration = reader.IsDBNull(3) ? "" : Convert.ToString(reader.GetValue(3)) ?? "",
                        Description = reader.IsDBNull(4) ? "" : Convert.ToString(reader.GetValue(4)) ?? ""
                    });
                }
            }

            return summary;
        }
    }
}

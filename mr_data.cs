using System;
using System.Collections.Generic;
using System.Globalization;
using MySqlConnector;

namespace ClinicaCentral
{
    public class MedicalRecordRow
    {
        private const string Dash = "\u2014";

        public int Id { get; set; }
        public int PatientId { get; set; }
        public DateTime RecordDate { get; set; }
        public DateTime? BirthDate { get; set; }
        public string PatientName { get; set; } = "";
        public string Diagnosis { get; set; } = "";
        public string Doctor { get; set; } = "";
        public string Notes { get; set; } = "";
        public string Treatment { get; set; } = "";
        public string Bp { get; set; } = "";
        public decimal? Temperature { get; set; }
        public int? Pulse { get; set; }
        public decimal? WeightKg { get; set; }
        public decimal? HeightCm { get; set; }
        public bool CanDelete { get; set; }
        public string AttachmentImage { get; set; } = "";
        public string AttachmentPdf { get; set; } = "";
        public List<PrescriptionLine> Prescriptions { get; } = new List<PrescriptionLine>();

        public bool HasPrescriptions
        {
            get { return Prescriptions.Count > 0; }
        }

        public bool NoPrescriptions
        {
            get { return Prescriptions.Count == 0; }
        }

        public string NoPrescriptionMessage
        {
            get
            {
                return string.Equals(Diagnosis, "Normal / Healthy", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(Diagnosis, "Normal", StringComparison.OrdinalIgnoreCase)
                    ? "No prescription issued (Normal / Healthy check-up)"
                    : "No prescription was issued for this check-up.";
            }
        }

        public string PatientCode
        {
            get { return PatientRow.FormatId(PatientId); }
        }

        public string CodeText
        {
            get { return "MR-" + Id.ToString("0000"); }
        }

        public string DateText
        {
            get { return RecordDate.ToString("MM-dd-yyyy", CultureInfo.InvariantCulture); }
        }

        public string DateLabel
        {
            get { return "Date: " + DateText; }
        }

        public string DoctorText
        {
            get { return Doctor.Length == 0 ? "Unassigned" : Doctor; }
        }

        public string DiagnosisText
        {
            get { return Diagnosis.Length == 0 ? Dash : Diagnosis; }
        }

        public string NotesText
        {
            get { return Notes.Length == 0 ? "No notes." : Notes; }
        }

        public string TreatmentText
        {
            get { return Treatment.Length == 0 ? "No treatment recorded." : Treatment; }
        }

        public int? Age
        {
            get
            {
                if (BirthDate == null) return null;

                DateTime today = DateTime.Today;
                int years = today.Year - BirthDate.Value.Year;
                if (BirthDate.Value.Date > today.AddYears(-years)) years--;
                return Math.Max(0, years);
            }
        }

        public string PatientLine
        {
            get
            {
                return Age == null
                    ? PatientCode
                    : PatientCode + "   " + Age.Value + "yrs";
            }
        }

        public string BpText
        {
            get { return Bp.Length == 0 ? Dash : Bp; }
        }

        public string TempText
        {
            get
            {
                return Temperature == null
                    ? Dash
                    : Temperature.Value.ToString("0.0", CultureInfo.InvariantCulture) + "\u00B0C";
            }
        }

        public string PulseText
        {
            get { return Pulse == null ? Dash : Pulse.Value + " bpm"; }
        }

        public string WeightText
        {
            get
            {
                return WeightKg == null
                    ? Dash
                    : WeightKg.Value.ToString("0.#", CultureInfo.InvariantCulture) + " kg";
            }
        }

        public string HeightText
        {
            get
            {
                return HeightCm == null
                    ? Dash
                    : HeightCm.Value.ToString("0.#", CultureInfo.InvariantCulture) + " cm";
            }
        }

        public string ImageFileText
        {
            get { return AttachmentImage.Length == 0 ? "No image" : AttachmentImage; }
        }

    }

    public class MedicalRecordRepository : Repository
    {
        public MedicalRecordRepository(DatabaseConnection db) : base(db) { }

        public List<MedicalRecordRow> GetAll()
        {
            return Query("", null, null);
        }

        public List<MedicalRecordRow> GetForDoctor(string doctorName)
        {
            return Query("AND m.doctor = @doctor ", null, doctorName);
        }

        public List<MedicalRecordRow> GetForPatient(int patientId)
        {
            return Query("AND m.patient_id = @pid ", patientId, null);
        }

        public void SoftDelete(int recordId, string deletedBy)
        {
            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "UPDATE medical_records " +
                "SET is_deleted = 1, deleted_at = NOW(), deleted_by = @by " +
                "WHERE id = @id AND is_deleted = 0", conn);
            cmd.Parameters.AddWithValue("@by", deletedBy);
            cmd.Parameters.AddWithValue("@id", recordId);

            if (cmd.ExecuteNonQuery() != 1)
                throw new InvalidOperationException("The medical record was already deleted or no longer exists.");
        }

        private List<MedicalRecordRow> Query(string where, int? patientId, string? doctorName)
        {
            var list = new List<MedicalRecordRow>();

            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "SELECT m.id, m.patient_id, m.record_date, CONCAT(p.first_name, ' ', p.last_name), " +
                "m.diagnosis, COALESCE(staff_name.full_name, staff_user.full_name, m.doctor), m.notes, " +
                "p.birthdate, m.bp, m.temperature, m.pulse, m.weight_kg, m.height, " +
                "m.treatment, m.attachment_image " +
                "FROM medical_records m JOIN patients p ON p.id = m.patient_id " +
                "LEFT JOIN staff staff_name ON staff_name.full_name = m.doctor " +
                "LEFT JOIN users u ON u.username = m.doctor " +
                "LEFT JOIN staff staff_user ON staff_user.user_id = u.id " +
                "WHERE m.is_deleted = 0 " + where +
                "ORDER BY m.record_date DESC, m.id DESC", conn);
            if (patientId.HasValue) cmd.Parameters.AddWithValue("@pid", patientId.Value);
            if (doctorName != null) cmd.Parameters.AddWithValue("@doctor", doctorName);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                MedicalRecordRow item = new MedicalRecordRow();
                item.Id = Convert.ToInt32(reader.GetValue(0));
                item.PatientId = Convert.ToInt32(reader.GetValue(1));
                item.RecordDate = Convert.ToDateTime(reader.GetValue(2));
                item.PatientName = Convert.ToString(reader.GetValue(3)) ?? "";
                item.Diagnosis = Text(reader, 4);
                item.Doctor = Text(reader, 5);
                item.Notes = Text(reader, 6);
                item.BirthDate = reader.IsDBNull(7) ? (DateTime?)null : Convert.ToDateTime(reader.GetValue(7));
                item.Bp = Text(reader, 8);
                item.Temperature = reader.IsDBNull(9) ? (decimal?)null : Convert.ToDecimal(reader.GetValue(9));
                item.Pulse = reader.IsDBNull(10) ? (int?)null : Convert.ToInt32(reader.GetValue(10));
                item.WeightKg = reader.IsDBNull(11) ? (decimal?)null : Convert.ToDecimal(reader.GetValue(11));
                item.HeightCm = reader.IsDBNull(12) ? (decimal?)null : Convert.ToDecimal(reader.GetValue(12));
                item.Treatment = Text(reader, 13);
                item.AttachmentImage = Text(reader, 14);
                list.Add(item);
            }
            return list;
        }

        private static string Text(MySqlDataReader reader, int index)
        {
            return reader.IsDBNull(index) ? "" : Convert.ToString(reader.GetValue(index)) ?? "";
        }
    }
}

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
                return CanManage && When > DateTime.Now;
            }
        }

        public bool CanEdit
        {
            get { return CanManage && When > DateTime.Now; }
        }

        private bool CanManage
        {
            get { return HasStatus("Pending") || HasStatus("Confirmed") || HasStatus("Scheduled"); }
        }

        public bool ShowView
        {
            get { return HasStatus("Completed"); }
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
                "SELECT a.id, a.appointment_date, COALESCE(s.full_name, a.doctor), a.status " +
                "FROM appointments a LEFT JOIN staff s ON s.id = a.staff_id " +
                "WHERE a.patient_id = @id ORDER BY a.appointment_date DESC", conn);
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

        public bool CancelAppointment(int appointmentId, int patientId)
        {
            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "UPDATE appointments SET status = 'Cancelled' " +
                "WHERE id = @appointmentId AND patient_id = @patientId " +
                "AND appointment_date > NOW() " +
                "AND status IN ('Pending', 'Confirmed', 'Scheduled')", conn);
            cmd.Parameters.AddWithValue("@appointmentId", appointmentId);
            cmd.Parameters.AddWithValue("@patientId", patientId);

            return cmd.ExecuteNonQuery() == 1;
        }

        public MedicalRecordRow? GetCompletedMedicalRecord(int appointmentId, int patientId)
        {
            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                @"SELECT mr.id, mr.patient_id, mr.record_date, CONCAT(p.first_name, ' ', p.last_name),
                         mr.diagnosis, COALESCE(s.full_name, mr.doctor), mr.notes, p.birthdate, mr.bp, mr.temperature,
                         mr.pulse, mr.weight_kg, mr.height, mr.treatment, mr.attachment_image, mr.attachment_pdf,
                         pr.prescription_id, pr.medicine_name, pr.dosage, pr.frequency, pr.duration, pr.description
                  FROM appointments a
                  JOIN patients p ON p.id = a.patient_id
                  LEFT JOIN staff s ON s.id = a.staff_id
                  LEFT JOIN users u ON u.id = s.user_id
                  JOIN medical_records mr
                    ON mr.patient_id = a.patient_id
                   AND (
                      mr.appointment_id = a.id
                      OR (
                          mr.appointment_id IS NULL
                          AND mr.record_date = DATE(a.appointment_date)
                          AND (mr.doctor = a.doctor OR mr.doctor = u.username OR mr.doctor = s.full_name)
                      )
                   )
                   AND mr.id = (
                      SELECT mr2.id
                      FROM medical_records mr2
                      WHERE mr2.patient_id = a.patient_id
                        AND mr2.is_deleted = 0
                        AND (
                            mr2.appointment_id = a.id
                            OR (
                                mr2.appointment_id IS NULL
                                AND mr2.record_date = DATE(a.appointment_date)
                                AND (mr2.doctor = a.doctor OR mr2.doctor = u.username OR mr2.doctor = s.full_name)
                            )
                        )
                      ORDER BY mr2.id DESC
                      LIMIT 1
                   )
                   AND mr.is_deleted = 0
                  LEFT JOIN prescriptions pr ON pr.medical_record_id = mr.id
                  WHERE a.id = @appointmentId
                   AND a.patient_id = @patientId
                   AND a.status = 'Completed'
                  ORDER BY pr.prescription_id", conn);
            cmd.Parameters.AddWithValue("@appointmentId", appointmentId);
            cmd.Parameters.AddWithValue("@patientId", patientId);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;

            MedicalRecordRow record = new MedicalRecordRow
            {
                Id = Convert.ToInt32(reader.GetValue(0)),
                PatientId = Convert.ToInt32(reader.GetValue(1)),
                RecordDate = Convert.ToDateTime(reader.GetValue(2)),
                PatientName = Convert.ToString(reader.GetValue(3)) ?? "",
                Diagnosis = reader.IsDBNull(4) ? "" : Convert.ToString(reader.GetValue(4)) ?? "",
                Doctor = reader.IsDBNull(5) ? "" : Convert.ToString(reader.GetValue(5)) ?? "",
                Notes = reader.IsDBNull(6) ? "" : Convert.ToString(reader.GetValue(6)) ?? "",
                BirthDate = reader.IsDBNull(7) ? (DateTime?)null : Convert.ToDateTime(reader.GetValue(7)),
                Bp = reader.IsDBNull(8) ? "" : Convert.ToString(reader.GetValue(8)) ?? "",
                Temperature = reader.IsDBNull(9) ? (decimal?)null : Convert.ToDecimal(reader.GetValue(9)),
                Pulse = reader.IsDBNull(10) ? (int?)null : Convert.ToInt32(reader.GetValue(10)),
                WeightKg = reader.IsDBNull(11) ? (decimal?)null : Convert.ToDecimal(reader.GetValue(11)),
                HeightCm = reader.IsDBNull(12) ? (decimal?)null : Convert.ToDecimal(reader.GetValue(12)),
                Treatment = reader.IsDBNull(13) ? "" : Convert.ToString(reader.GetValue(13)) ?? "",
                AttachmentImage = reader.IsDBNull(14) ? "" : Convert.ToString(reader.GetValue(14)) ?? "",
                AttachmentPdf = reader.IsDBNull(15) ? "" : Convert.ToString(reader.GetValue(15)) ?? ""
            };

            do
            {
                if (!reader.IsDBNull(16))
                {
                    record.Prescriptions.Add(new PrescriptionLine
                    {
                        Medicine = Convert.ToString(reader.GetValue(17)) ?? "",
                        Dosage = reader.IsDBNull(18) ? "" : Convert.ToString(reader.GetValue(18)) ?? "",
                        Frequency = reader.IsDBNull(19) ? "" : Convert.ToString(reader.GetValue(19)) ?? "",
                        Duration = reader.IsDBNull(20) ? "" : Convert.ToString(reader.GetValue(20)) ?? "",
                        Description = reader.IsDBNull(21) ? "" : Convert.ToString(reader.GetValue(21)) ?? ""
                    });
                }
            }
            while (reader.Read());

            return record;
        }
    }
}

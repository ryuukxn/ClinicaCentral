using System;
using System.Collections.Generic;
using System.Globalization;
using MySqlConnector;

namespace ClinicaCentral
{
    public class DeletedPatientRecord
    {
        public int Id { get; set; }
        public int PatientId { get; set; }
        public string PatientName { get; set; } = "";
        public string DeletedBy { get; set; } = "";
        public DateTime DeletedAt { get; set; }

        public string Code
        {
            get { return PatientRow.FormatId(PatientId); }
        }

        public string PatientText
        {
            get { return Code + " · " + PatientName; }
        }

        public string DeletedText
        {
            get { return DeletedAt.ToString("MMM d, yyyy - h:mm tt", CultureInfo.InvariantCulture); }
        }
    }

    public class PatientArchiveRepository : Repository
    {
        public PatientArchiveRepository(DatabaseConnection db) : base(db) { }

        public List<DeletedPatientRecord> GetDeleted()
        {
            var list = new List<DeletedPatientRecord>();

            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "SELECT id, patient_id, patient_name, deleted_by, deleted_at " +
                "FROM deleted_patients WHERE restored_at IS NULL " +
                "ORDER BY id DESC LIMIT 100", conn);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                DeletedPatientRecord item = new DeletedPatientRecord();
                item.Id = Convert.ToInt32(reader.GetValue(0));
                item.PatientId = Convert.ToInt32(reader.GetValue(1));
                item.PatientName = Convert.ToString(reader.GetValue(2)) ?? "";
                item.DeletedBy = Convert.ToString(reader.GetValue(3)) ?? "";
                item.DeletedAt = Convert.ToDateTime(reader.GetValue(4));
                list.Add(item);
            }
            return list;
        }
    }

    public class DeletedMedicalRecordRow
    {
        public int Id { get; set; }
        public string Patient { get; set; } = "";
        public string DeletedBy { get; set; } = "";
        public DateTime? DeletedAt { get; set; }

        public string DeletedText
        {
            get
            {
                return DeletedAt == null
                    ? "\u2014"
                    : DeletedAt.Value.ToString("MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture);
            }
        }
    }

    public class DeletedMedicalRecordRepository : Repository
    {
        public DeletedMedicalRecordRepository(DatabaseConnection db) : base(db) { }

        public List<DeletedMedicalRecordRow> GetDeleted()
        {
            var list = new List<DeletedMedicalRecordRow>();

            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "SELECT mr.id, CONCAT_WS(' ', p.first_name, p.last_name), mr.deleted_by, mr.deleted_at " +
                "FROM medical_records mr JOIN patients p ON p.id = mr.patient_id " +
                "WHERE mr.is_deleted = 1 ORDER BY mr.deleted_at DESC, mr.id DESC", conn);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new DeletedMedicalRecordRow
                {
                    Id = Convert.ToInt32(reader.GetValue(0)),
                    Patient = Convert.ToString(reader.GetValue(1)) ?? "",
                    DeletedBy = reader.IsDBNull(2) ? "" : Convert.ToString(reader.GetValue(2)) ?? "",
                    DeletedAt = reader.IsDBNull(3) ? (DateTime?)null : Convert.ToDateTime(reader.GetValue(3))
                });
            }
            return list;
        }

        public void Restore(int recordId)
        {
            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "UPDATE medical_records " +
                "SET is_deleted = 0, deleted_at = NULL, deleted_by = NULL " +
                "WHERE id = @id AND is_deleted = 1", conn);
            cmd.Parameters.AddWithValue("@id", recordId);

            if (cmd.ExecuteNonQuery() != 1)
                throw new InvalidOperationException("The medical record was already restored or no longer exists.");
        }
    }
}

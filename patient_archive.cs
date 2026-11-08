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
}

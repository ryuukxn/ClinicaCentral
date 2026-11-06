using System;
using System.Collections.Generic;

namespace ClinicaCentral
{
    public class PatientRow : Person
    {
        public int Id { get; set; }
        public string Code => FormatId(Id);
        public string DateOfBirth { get; set; } = "-";
        public string LastVisit { get; set; } = "-";
        public string Contact { get; set; } = "-";

        public override string GetRoleTitle()
        {
            return "Patient";
        }

        public static string FormatId(int id) => "PCC-" + id.ToString("0000");
    }

    public class PatientDirectory
    {
        private readonly List<PatientRow> _all = new List<PatientRow>();
        private readonly Dictionary<int, PatientRow> _byId = new Dictionary<int, PatientRow>();

        public void Clear()
        {
            _all.Clear();
            _byId.Clear();
        }

        public void Add(PatientRow patient)
        {
            _all.Add(patient);
            _byId[patient.Id] = patient;
        }

        public List<PatientRow> GetAll()
        {
            return _all;
        }

        public bool TryFind(int id, out PatientRow? patient)
        {
            return _byId.TryGetValue(id, out patient);
        }
    }

    public class PatientRepository : Repository
    {
        public PatientRepository(DatabaseConnection db) : base(db) { }

        public List<PatientRow> GetAll()
        {
            var list = new List<PatientRow>();

            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT p.id, CONCAT(p.first_name, ' ', p.last_name), p.birthdate, p.contact, " +
                "       (SELECT MAX(a.appointment_date) FROM appointments a WHERE a.patient_id = p.id AND a.appointment_date < CURDATE()) " +
                "FROM patients p " +
                "ORDER BY p.id";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                PatientRow patient = new PatientRow();
                patient.Id = Convert.ToInt32(reader.GetValue(0));
                patient.Name = Convert.ToString(reader.GetValue(1)) ?? "";
                patient.DateOfBirth = FormatDate(reader.GetValue(2));
                patient.Contact = reader.IsDBNull(3) ? "-" : Convert.ToString(reader.GetValue(3)) ?? "-";
                patient.LastVisit = FormatDate(reader.GetValue(4));
                list.Add(patient);
            }
            return list;
        }

        private string FormatDate(object value)
        {
            if (value == null || value is DBNull) return "-";
            try { return Convert.ToDateTime(value).ToString("yyyy - MM - dd"); }
            catch { return "-"; }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Globalization;
using MySqlConnector;

namespace ClinicaCentral
{
    public class PatientProfileRow
    {
        public int Id { get; set; }
        public string FullName { get; set; } = "-";
        public string Gender { get; set; } = "-";
        public DateTime? BirthDate { get; set; }
        public string ContactNumber { get; set; } = "-";
        public string Email { get; set; } = "-";
        public string Allergies { get; set; } = "-";
        public string Address { get; set; } = "-";
        public string Status { get; set; } = "-";
        public DateTime? MemberSince { get; set; }
        public byte[]? Photo { get; set; }
        public decimal? WeightKg { get; set; }
        public decimal? HeightCm { get; set; }
        public DateTime? VitalsRecordDate { get; set; }

        public string PatientCode => "Patient Code: " + PatientRow.FormatId(Id);
        public string BirthDateText => FormatDate(BirthDate);
        public string MemberSinceText => "Member since: " + FormatDate(MemberSince);
        public string WeightText => WeightKg.HasValue
            ? WeightKg.Value.ToString("0.#", CultureInfo.InvariantCulture) + " kg"
            : "-";
        public string HeightText => HeightCm.HasValue
            ? HeightCm.Value.ToString("0.#", CultureInfo.InvariantCulture) + " cm"
            : "-";
        public string VitalsAsOfText => VitalsRecordDate.HasValue
            ? "As of " + VitalsRecordDate.Value.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)
            : "No recorded vitals yet";

        private static string FormatDate(DateTime? date)
        {
            return date?.ToString("MM-dd-yyyy", CultureInfo.InvariantCulture) ?? "-";
        }
    }

    public class PatientRow : Person
    {
        public int Id { get; set; }
        public string Code => FormatId(Id);
        public DateTime? DateOfBirthValue { get; set; }
        public DateTime? LastVisitValue { get; set; }
        public string DateOfBirth => FormatDate(DateOfBirthValue);
        public string LastVisit => FormatDate(LastVisitValue);
        public string Contact { get; set; } = "-";

        public override string GetRoleTitle()
        {
            return "Patient";
        }

        public static string FormatId(int id) => "PCC-" + id.ToString("0000");

        private static string FormatDate(DateTime? value)
        {
            return value?.ToString("yyyy - MM - dd", CultureInfo.InvariantCulture) ?? "-";
        }
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

        public PatientProfileRow? GetPatientProfile(int patientId)
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT p.id, p.first_name, p.last_name, p.gender, p.birthdate, p.contact, " +
                "p.address, p.allergies, u.email, u.status, u.created_at, p.photo, " +
                "latest.weight_kg, latest.height, latest.record_date " +
                "FROM patients p LEFT JOIN users u ON u.patient_id = p.id " +
                "LEFT JOIN medical_records latest ON latest.id = (" +
                "SELECT mr.id FROM medical_records mr WHERE mr.patient_id = p.id " +
                "ORDER BY mr.record_date DESC, mr.id DESC LIMIT 1) " +
                "WHERE p.id = @patientId LIMIT 1";
            cmd.Parameters.AddWithValue("@patientId", patientId);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;

            string firstName = Text(reader, 1);
            string lastName = Text(reader, 2);
            byte[]? photo = reader.IsDBNull(11) ? null : (byte[])reader.GetValue(11);

            return new PatientProfileRow
            {
                Id = Convert.ToInt32(reader.GetValue(0)),
                FullName = DisplayValue((firstName + " " + lastName).Trim()),
                Gender = DisplayValue(Text(reader, 3)),
                BirthDate = reader.IsDBNull(4) ? null : Convert.ToDateTime(reader.GetValue(4)),
                ContactNumber = DisplayValue(Text(reader, 5)),
                Address = DisplayValue(Text(reader, 6)),
                Allergies = DisplayValue(Text(reader, 7)),
                Email = DisplayValue(Text(reader, 8)),
                Status = DisplayValue(Text(reader, 9)),
                MemberSince = reader.IsDBNull(10) ? null : Convert.ToDateTime(reader.GetValue(10)),
                Photo = photo,
                WeightKg = reader.IsDBNull(12) ? (decimal?)null : Convert.ToDecimal(reader.GetValue(12)),
                HeightCm = reader.IsDBNull(13) ? (decimal?)null : Convert.ToDecimal(reader.GetValue(13)),
                VitalsRecordDate = reader.IsDBNull(14) ? (DateTime?)null : Convert.ToDateTime(reader.GetValue(14))
            };
        }

        public List<PatientRow> GetAll()
        {
            var list = new List<PatientRow>();

            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
                        cmd.CommandText =
                "SELECT p.id, CONCAT(p.first_name, ' ', p.last_name), p.birthdate, p.contact, " +
                "       lv.last_visit " +
                "FROM patients p " +
                "LEFT JOIN ( " +
                "    SELECT patient_id, MAX(appointment_date) AS last_visit " +
                "    FROM appointments " +
                "    WHERE status = 'Completed' " +
                "    GROUP BY patient_id " +
                ") lv ON lv.patient_id = p.id " +
                "ORDER BY p.id";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                PatientRow patient = new PatientRow();
                patient.Id = Convert.ToInt32(reader.GetValue(0));
                patient.Name = Convert.ToString(reader.GetValue(1)) ?? "";
                patient.DateOfBirthValue = reader.IsDBNull(2) ? null : Convert.ToDateTime(reader.GetValue(2));
                patient.Contact = reader.IsDBNull(3) ? "-" : Convert.ToString(reader.GetValue(3)) ?? "-";
                patient.LastVisitValue = reader.IsDBNull(4) ? null : Convert.ToDateTime(reader.GetValue(4));
                list.Add(patient);
            }
            return list;
        }

        private static string Text(MySqlDataReader reader, int index)
        {
            return reader.IsDBNull(index) ? "" : Convert.ToString(reader.GetValue(index)) ?? "";
        }

        private static string DisplayValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }
    }
}
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
        public string AttachmentImage { get; set; } = "";
        public string AttachmentPdf { get; set; } = "";

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

        public string ImageFileText
        {
            get { return AttachmentImage.Length == 0 ? "No image" : AttachmentImage; }
        }

        public string PdfFileText
        {
            get { return AttachmentPdf.Length == 0 ? "No file" : AttachmentPdf; }
        }
    }

    public class PagedList<T>
    {
        private readonly List<T> _items = new List<T>();

        public int PageSize { get; }
        public int PageNumber { get; private set; } = 1;

        public PagedList(int pageSize)
        {
            PageSize = Math.Max(1, pageSize);
        }

        public int TotalItems
        {
            get { return _items.Count; }
        }

        public int TotalPages
        {
            get { return Math.Max(1, (int)Math.Ceiling(_items.Count / (double)PageSize)); }
        }

        public bool HasPrevious
        {
            get { return PageNumber > 1; }
        }

        public bool HasNext
        {
            get { return PageNumber < TotalPages; }
        }

        public void Load(IEnumerable<T> items)
        {
            _items.Clear();
            _items.AddRange(items);
            PageNumber = 1;
        }

        public void GoTo(int page)
        {
            PageNumber = Math.Min(Math.Max(1, page), TotalPages);
        }

        public void Next()
        {
            GoTo(PageNumber + 1);
        }

        public void Previous()
        {
            GoTo(PageNumber - 1);
        }

        public List<T> GetCurrentPage()
        {
            int start = (PageNumber - 1) * PageSize;
            int count = Math.Min(PageSize, _items.Count - start);

            if (count <= 0) return new List<T>();
            return _items.GetRange(start, count);
        }
    }

    public class MedicalRecordRepository : Repository
    {
        public MedicalRecordRepository(DatabaseConnection db) : base(db) { }

        public List<MedicalRecordRow> GetAll()
        {
            return Query("", null);
        }

        public List<MedicalRecordRow> GetForPatient(int patientId)
        {
            return Query("WHERE m.patient_id = @pid ", patientId);
        }

        private List<MedicalRecordRow> Query(string where, int? patientId)
        {
            var list = new List<MedicalRecordRow>();

            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "SELECT m.id, m.patient_id, m.record_date, CONCAT(p.first_name, ' ', p.last_name), " +
                "m.diagnosis, m.doctor, m.notes, " +
                "p.birthdate, m.bp, m.temperature, m.pulse, m.weight_kg, " +
                "m.treatment, m.attachment_image, m.attachment_pdf " +
                "FROM medical_records m JOIN patients p ON p.id = m.patient_id " + where +
                "ORDER BY m.record_date DESC, m.id DESC", conn);
            if (patientId.HasValue) cmd.Parameters.AddWithValue("@pid", patientId.Value);

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
                item.Treatment = Text(reader, 12);
                item.AttachmentImage = Text(reader, 13);
                item.AttachmentPdf = Text(reader, 14);
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

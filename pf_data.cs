using System;
using System.Globalization;
using MySqlConnector;

namespace ClinicaCentral
{
    public class ProfileInfo
    {
        private const string Dash = "\u2014";

        public int PatientId { get; set; }
        public string FullName { get; set; } = "";
        public string Gender { get; set; } = "";
        public DateTime? BirthDate { get; set; }
        public string Contact { get; set; } = "";
        public string Email { get; set; } = "";
        public string Allergies { get; set; } = "";
        public string Address { get; set; } = "";
        public string Username { get; set; } = "";
        public string Status { get; set; } = "Active";
        public DateTime? MemberSince { get; set; }

        public string PatientCode
        {
            get { return "Patient Code: " + PatientRow.FormatId(PatientId); }
        }

        public string GenderText
        {
            get { return Gender.Length == 0 ? Dash : Gender; }
        }

        public string BirthdateText
        {
            get
            {
                return BirthDate == null
                    ? Dash
                    : BirthDate.Value.ToString("MM-dd-yyyy", CultureInfo.InvariantCulture);
            }
        }

        public string ContactText
        {
            get { return Contact.Length == 0 ? Dash : Contact; }
        }

        public string EmailText
        {
            get { return Email.Length == 0 ? Dash : Email; }
        }

        public string AllergiesText
        {
            get { return Allergies.Length == 0 ? "None recorded" : Allergies; }
        }

        public string AddressText
        {
            get { return Address.Length == 0 ? Dash : Address; }
        }

        public string PasswordText
        {
            get { return "\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022"; }
        }

        public string MemberSinceText
        {
            get
            {
                return MemberSince == null
                    ? "Member since: " + Dash
                    : "Member since: " + MemberSince.Value.ToString("MM-dd-yyyy", CultureInfo.InvariantCulture);
            }
        }
    }

    public class ProfileRepository : Repository
    {
        public ProfileRepository(DatabaseConnection db) : base(db) { }

        public ProfileInfo? GetProfile(string username)
        {
            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "SELECT p.id, p.first_name, p.last_name, p.birthdate, p.contact, p.gender, p.allergies, p.address, " +
                "u.username, u.email, u.status, u.created_at " +
                "FROM users u JOIN patients p ON p.id = u.patient_id WHERE u.username = @u LIMIT 1", conn);
            cmd.Parameters.AddWithValue("@u", username);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;

            ProfileInfo info = new ProfileInfo();
            info.PatientId = Convert.ToInt32(reader.GetValue(0));
            info.FullName = ((Convert.ToString(reader.GetValue(1)) ?? "") + " " + (Convert.ToString(reader.GetValue(2)) ?? "")).Trim();
            info.BirthDate = reader.IsDBNull(3) ? (DateTime?)null : Convert.ToDateTime(reader.GetValue(3));
            info.Contact = Text(reader, 4);
            info.Gender = Text(reader, 5);
            info.Allergies = Text(reader, 6);
            info.Address = Text(reader, 7);
            info.Username = Text(reader, 8);
            info.Email = Text(reader, 9);
            info.Status = Text(reader, 10).Length == 0 ? "Active" : Text(reader, 10);
            info.MemberSince = reader.IsDBNull(11) ? (DateTime?)null : Convert.ToDateTime(reader.GetValue(11));
            return info;
        }

        private static string Text(MySqlDataReader reader, int index)
        {
            return reader.IsDBNull(index) ? "" : Convert.ToString(reader.GetValue(index)) ?? "";
        }
    }
}

using System;
using MySqlConnector;

namespace ClinicaCentral
{
    public static class ProfileEdit
    {
        public class ContactInfo
        {
            public string Email { get; set; } = "";
            public string Contact { get; set; } = "";
            public string Address { get; set; } = "";
            public string Allergies { get; set; } = "";
        }

        public static ContactInfo GetContactInfo(int patientId)
        {
            using var conn = Database.GetConnection();
            conn.Open();

            using var cmd = new MySqlCommand(
                "SELECT u.email, p.contact, p.address, p.allergies " +
                "FROM patients p LEFT JOIN users u ON u.patient_id = p.id " +
                "WHERE p.id = @pid LIMIT 1", conn);
            cmd.Parameters.AddWithValue("@pid", patientId);

            using var reader = cmd.ExecuteReader();
            var info = new ContactInfo();
            if (reader.Read())
            {
                info.Email = reader.IsDBNull(0) ? "" : Convert.ToString(reader.GetValue(0)) ?? "";
                info.Contact = reader.IsDBNull(1) ? "" : Convert.ToString(reader.GetValue(1)) ?? "";
                info.Address = reader.IsDBNull(2) ? "" : Convert.ToString(reader.GetValue(2)) ?? "";
                info.Allergies = reader.IsDBNull(3) ? "" : Convert.ToString(reader.GetValue(3)) ?? "";
            }
            return info;
        }

        public static void UpdateProfile(int patientId, string email, string contact, string address, string allergies)
        {
            using var conn = Database.GetConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                using (var cmd = new MySqlCommand(
                    "UPDATE patients SET contact = @c, address = @a, allergies = @al WHERE id = @pid", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@c", contact);
                    cmd.Parameters.AddWithValue("@a", address);
                    cmd.Parameters.AddWithValue("@al", string.IsNullOrWhiteSpace(allergies) ? DBNull.Value : allergies);
                    cmd.Parameters.AddWithValue("@pid", patientId);
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = new MySqlCommand(
                    "UPDATE users SET email = @e WHERE patient_id = @pid", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@e", email);
                    cmd.Parameters.AddWithValue("@pid", patientId);
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
    }
}
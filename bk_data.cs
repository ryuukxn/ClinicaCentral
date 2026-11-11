using System;
using System.Collections.Generic;
using System.Globalization;
using MySqlConnector;

namespace ClinicaCentral
{
    public class DoctorOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Position { get; set; } = "";
        public bool IsPlaceholder { get; set; }

        public string Display
        {
            get
            {
                if (IsPlaceholder || Position.Length == 0) return Name;
                return Name + " - " + Position;
            }
        }
    }

    public class TimeSlot
    {
        public TimeSpan Time { get; set; }
        public bool IsAvailable { get; set; } = true;
        public bool IsPlaceholder { get; set; }
        public string PlaceholderText { get; set; } = "";

        public string Label
        {
            get
            {
                if (IsPlaceholder) return PlaceholderText;

                string clock = DateTime.Today.Add(Time).ToString("hh:mm", CultureInfo.InvariantCulture);
                return clock + (Time.Hours < 12 ? " A.M." : " P.M.");
            }
        }

        public DateTime On(DateTime date)
        {
            return date.Date + Time;
        }
    }

    public class EditableAppointment
    {
        public int Id { get; set; }
        public int DoctorId { get; set; }
        public DateTime When { get; set; }
        public string Notes { get; set; } = "";
    }

    public static class ClinicHours
    {
        private static readonly int[] Hours = { 8, 9, 10, 11, 13, 14, 15, 16 };

        public static bool IsOpenOn(DateTime date)
        {
            return date.DayOfWeek != DayOfWeek.Sunday;
        }

        public static bool IsValidAppointmentTime(DateTime appointmentDate, DateTime now)
        {
            return appointmentDate > now &&
                IsOpenOn(appointmentDate) &&
                Array.IndexOf(Hours, appointmentDate.Hour) >= 0 &&
                appointmentDate.Minute == 0 &&
                appointmentDate.Second == 0;
        }

        public static List<TimeSlot> BuildSlots(DateTime date, ICollection<TimeSpan> booked, DateTime now)
        {
            var slots = new List<TimeSlot>();
            if (!IsOpenOn(date)) return slots;

            foreach (int hour in Hours)
            {
                TimeSlot slot = new TimeSlot { Time = TimeSpan.FromHours(hour) };

                slot.IsAvailable = !booked.Contains(slot.Time) && slot.On(date) > now;
                slots.Add(slot);
            }
            return slots;
        }
    }

    public class BookingRepository : Repository
    {
        public BookingRepository(DatabaseConnection db) : base(db) { }

        public List<DoctorOption> GetDoctors()
        {
            var list = new List<DoctorOption>();

            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "SELECT id, full_name, position FROM staff " +
                "WHERE status = 'On Duty' AND position = 'Doctor' ORDER BY full_name", conn);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new DoctorOption
                {
                    Id = Convert.ToInt32(reader.GetValue(0)),
                    Name = Convert.ToString(reader.GetValue(1)) ?? "",
                    Position = reader.IsDBNull(2) ? "" : Convert.ToString(reader.GetValue(2)) ?? ""
                });
            }
            return list;
        }

        public HashSet<TimeSpan> GetBookedTimes(string doctorName, DateTime date, int? excludeAppointmentId = null)
        {
            var booked = new HashSet<TimeSpan>();

            using var conn = OpenConnection();
            string excludeCondition = excludeAppointmentId.HasValue ? " AND id <> @excludeId" : "";
            using var cmd = new MySqlCommand(
                "SELECT appointment_date FROM appointments " +
                "WHERE doctor = @doctor AND appointment_date >= @start AND appointment_date < @end " +
                "AND status <> 'Cancelled'" + excludeCondition, conn);
            cmd.Parameters.AddWithValue("@doctor", doctorName);
            cmd.Parameters.AddWithValue("@start", date.Date);
            cmd.Parameters.AddWithValue("@end", date.Date.AddDays(1));
            if (excludeAppointmentId.HasValue)
                cmd.Parameters.AddWithValue("@excludeId", excludeAppointmentId.Value);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                booked.Add(Convert.ToDateTime(reader.GetValue(0)).TimeOfDay);

            return booked;
        }

        public EditableAppointment? GetForEdit(int appointmentId, int patientId)
        {
            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "SELECT id, staff_id, appointment_date, notes FROM appointments " +
                "WHERE id = @id AND patient_id = @patientId AND appointment_date > NOW() " +
                "AND status IN ('Pending', 'Confirmed', 'Scheduled') LIMIT 1", conn);
            cmd.Parameters.AddWithValue("@id", appointmentId);
            cmd.Parameters.AddWithValue("@patientId", patientId);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;

            return new EditableAppointment
            {
                Id = Convert.ToInt32(reader.GetValue(0)),
                DoctorId = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1)),
                When = Convert.ToDateTime(reader.GetValue(2)),
                Notes = reader.IsDBNull(3) ? "" : Convert.ToString(reader.GetValue(3)) ?? ""
            };
        }

        public bool UpdateAppointment(
            int appointmentId, int patientId, int doctorId, DateTime appointmentDate, string notes)
        {
            using var conn = OpenConnection();
            using var transaction = conn.BeginTransaction();

            using (var appointmentCommand = new MySqlCommand(
                "SELECT id FROM appointments WHERE id = @id AND patient_id = @patientId " +
                "AND appointment_date > NOW() AND status IN ('Pending', 'Confirmed', 'Scheduled') " +
                "FOR UPDATE", conn, transaction))
            {
                appointmentCommand.Parameters.AddWithValue("@id", appointmentId);
                appointmentCommand.Parameters.AddWithValue("@patientId", patientId);
                if (appointmentCommand.ExecuteScalar() == null)
                    return false;
            }

            if (!ClinicHours.IsValidAppointmentTime(appointmentDate, DateTime.Now))
                throw new InvalidOperationException("Please choose a future date and a valid clinic time.");

            string doctorName;
            using (var doctorCommand = new MySqlCommand(
                "SELECT full_name FROM staff WHERE id = @doctorId " +
                "AND status = 'On Duty' AND position = 'Doctor' LIMIT 1", conn, transaction))
            {
                doctorCommand.Parameters.AddWithValue("@doctorId", doctorId);
                object? result = doctorCommand.ExecuteScalar();
                if (result == null || result is DBNull)
                    throw new InvalidOperationException("The selected doctor is no longer available. Please choose another doctor.");

                doctorName = Convert.ToString(result) ?? "";
            }

            using (var conflictCommand = new MySqlCommand(
                "SELECT COUNT(*) FROM appointments WHERE doctor = @doctor " +
                "AND appointment_date = @appointmentDate AND status <> 'Cancelled' AND id <> @id",
                conn, transaction))
            {
                conflictCommand.Parameters.AddWithValue("@doctor", doctorName);
                conflictCommand.Parameters.AddWithValue("@appointmentDate", appointmentDate);
                conflictCommand.Parameters.AddWithValue("@id", appointmentId);
                if (Convert.ToInt32(conflictCommand.ExecuteScalar()) > 0)
                    throw new InvalidOperationException("That time was just booked. Please choose another available time.");
            }

            using (var updateCommand = new MySqlCommand(
                "UPDATE appointments SET staff_id = @staffId, doctor = @doctor, " +
                "appointment_date = @appointmentDate, notes = @notes " +
                "WHERE id = @id AND patient_id = @patientId " +
                "AND appointment_date > NOW() AND status IN ('Pending', 'Confirmed', 'Scheduled')",
                conn, transaction))
            {
                updateCommand.Parameters.AddWithValue("@staffId", doctorId);
                updateCommand.Parameters.AddWithValue("@doctor", doctorName);
                updateCommand.Parameters.AddWithValue("@appointmentDate", appointmentDate);
                updateCommand.Parameters.AddWithValue("@notes",
                    string.IsNullOrWhiteSpace(notes) ? (object)DBNull.Value : notes.Trim());
                updateCommand.Parameters.AddWithValue("@id", appointmentId);
                updateCommand.Parameters.AddWithValue("@patientId", patientId);

                if (updateCommand.ExecuteNonQuery() == 0)
                    return false;
            }

            transaction.Commit();
            return true;
        }
    }
}

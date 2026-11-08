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

    public static class ClinicHours
    {
        private static readonly int[] Hours = { 8, 9, 10, 11, 13, 14, 15, 16 };

        public static bool IsOpenOn(DateTime date)
        {
            return date.DayOfWeek != DayOfWeek.Sunday;
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
                "SELECT id, full_name, position FROM staff WHERE status = 'Active' ORDER BY full_name", conn);

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

        public HashSet<TimeSpan> GetBookedTimes(string doctorName, DateTime date)
        {
            var booked = new HashSet<TimeSpan>();

            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "SELECT appointment_date FROM appointments " +
                "WHERE doctor = @doctor AND appointment_date >= @start AND appointment_date < @end " +
                "AND status <> 'Cancelled'", conn);
            cmd.Parameters.AddWithValue("@doctor", doctorName);
            cmd.Parameters.AddWithValue("@start", date.Date);
            cmd.Parameters.AddWithValue("@end", date.Date.AddDays(1));

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                booked.Add(Convert.ToDateTime(reader.GetValue(0)).TimeOfDay);

            return booked;
        }
    }
}

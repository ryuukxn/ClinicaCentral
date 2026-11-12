using System;
using System.Collections.Generic;
using System.Globalization;
using MySqlConnector;

namespace ClinicaCentral
{
    public class WeeklyAvailability
    {
        public static readonly string[] Slots = { "Morning", "Afternoon" };

        private readonly string[,] _status = new string[2, 6];

        public WeeklyAvailability()
        {
            for (int slot = 0; slot < 2; slot++)
                for (int day = 0; day < 6; day++)
                    _status[slot, day] = "Off";
        }

        public void Set(string slot, int weekday, string status)
        {
            int row = Array.FindIndex(Slots, s => string.Equals(s, slot, StringComparison.OrdinalIgnoreCase));
            int col = weekday; // 0 = Mon, 5 = Sat

            if (row < 0 || col < 0 || col > 5) return;
            _status[row, col] = string.IsNullOrEmpty(status) ? "Off" : status;
        }

        public string Get(int slotIndex, int dayIndex)
        {
            return _status[slotIndex, dayIndex];
        }
    }

    public class TimeOffRow
    {
        public int Id { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Reason { get; set; } = "";
        public string Type { get; set; } = "";

        public string DateText
        {
            get
            {
                if (StartDate.Date == EndDate.Date)
                    return StartDate.ToString("ddd, MMM d", CultureInfo.InvariantCulture);

                if (StartDate.Year == EndDate.Year && StartDate.Month == EndDate.Month)
                    return StartDate.ToString("MMM d", CultureInfo.InvariantCulture) + " \u2013 " + EndDate.Day;

                return StartDate.ToString("MMM d", CultureInfo.InvariantCulture) + " \u2013 " +
                       EndDate.ToString("MMM d", CultureInfo.InvariantCulture);
            }
        }

        public string TypeText
        {
            get { return Type.Length == 0 ? "Busy" : Type; }
        }
    }

    public class BookingRequestRow
    {
        public int Id { get; set; }
        public DateTime When { get; set; }
        public string Patient { get; set; } = "";

        public string WhenText
        {
            get { return When.ToString("ddd, MMM d \u00B7 h:mm tt", CultureInfo.InvariantCulture); }
        }
    }

    public class TodayRow
    {
        public int Id { get; set; }
        public DateTime When { get; set; }
        public string Patient { get; set; } = "";
        public string Status { get; set; } = "";

        public string TimeText
        {
            get { return When.ToString("h:mm tt", CultureInfo.InvariantCulture); }
        }

        public bool HasStatus(string status)
        {
            return string.Equals(Status, status, StringComparison.OrdinalIgnoreCase);
        }

        public bool CanComplete
        {
            get { return HasStatus("Scheduled") || HasStatus("Confirmed"); }
        }

        public bool ShowBadge
        {
            get { return !CanComplete; }
        }
    }

    public class ScheduleRepository : Repository
    {
        public ScheduleRepository(DatabaseConnection db) : base(db) { }

        public WeeklyAvailability GetAvailability(string username)
        {
            var week = new WeeklyAvailability();

            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "SELECT weekday, slot, status FROM staff_availability WHERE username = @u", conn);
            cmd.Parameters.AddWithValue("@u", username);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                int weekday = Convert.ToInt32(reader.GetValue(0));
                string slot = Convert.ToString(reader.GetValue(1)) ?? "";
                string status = Convert.ToString(reader.GetValue(2)) ?? "Off";
                week.Set(slot, weekday, status);
            }
            return week;
        }

        public List<TimeOffRow> GetTimeOff(string username)
        {
            var list = new List<TimeOffRow>();

            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "SELECT id, start_date, end_date, reason, type FROM time_off " +
                "WHERE username = @u AND end_date >= CURDATE() ORDER BY start_date LIMIT 5", conn);
            cmd.Parameters.AddWithValue("@u", username);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new TimeOffRow
                {
                    Id = Convert.ToInt32(reader.GetValue(0)),
                    StartDate = Convert.ToDateTime(reader.GetValue(1)),
                    EndDate = Convert.ToDateTime(reader.GetValue(2)),
                    Reason = reader.IsDBNull(3) ? "" : Convert.ToString(reader.GetValue(3)) ?? "",
                    Type = reader.IsDBNull(4) ? "" : Convert.ToString(reader.GetValue(4)) ?? ""
                });
            }
            return list;
        }

        public List<BookingRequestRow> GetBookingRequests()
        {
            var list = new List<BookingRequestRow>();

            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "SELECT a.id, a.appointment_date, CONCAT(p.first_name, ' ', p.last_name) " +
                "FROM appointments a JOIN patients p ON p.id = a.patient_id " +
                "WHERE a.status = 'Pending' AND a.appointment_date >= CURDATE() " +
                "ORDER BY a.appointment_date LIMIT 20", conn);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new BookingRequestRow
                {
                    Id = Convert.ToInt32(reader.GetValue(0)),
                    When = Convert.ToDateTime(reader.GetValue(1)),
                    Patient = Convert.ToString(reader.GetValue(2)) ?? ""
                });
            }
            return list;
        }

        public List<TodayRow> GetToday()
        {
            var list = new List<TodayRow>();

            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "SELECT a.id, a.appointment_date, CONCAT(p.first_name, ' ', p.last_name), a.status " +
                "FROM appointments a JOIN patients p ON p.id = a.patient_id " +
                "WHERE DATE(a.appointment_date) = CURDATE() AND a.status <> 'Cancelled' " +
                "ORDER BY a.appointment_date", conn);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new TodayRow
                {
                    Id = Convert.ToInt32(reader.GetValue(0)),
                    When = Convert.ToDateTime(reader.GetValue(1)),
                    Patient = Convert.ToString(reader.GetValue(2)) ?? "",
                    Status = reader.IsDBNull(3) ? "" : Convert.ToString(reader.GetValue(3)) ?? ""
                });
            }
            return list;
        }

        public void UpdateAvailability(string doctorName, int slot, int day, string newStatus)
        {
            using var conn = Database.GetConnection();
            conn.Open();

            // slot index 0 = "Morning", 1 = "Afternoon"
            string slotName = slot == 0 ? "Morning" : "Afternoon";

            // Kung ang status ay "Off", pwede nating i-update o i-delete para maging Off ang default
            string query = @"INSERT INTO staff_availability (username, weekday, slot, status)
                            VALUES (@user, @day, @slot, @status)
                            ON DUPLICATE KEY UPDATE status = @status;";

            using var cmd = new MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@user", doctorName);
            cmd.Parameters.AddWithValue("@day", day); // 0 hanggang 5 (Mon hanggang Sat)
            cmd.Parameters.AddWithValue("@slot", slotName);
            cmd.Parameters.AddWithValue("@status", newStatus);

            cmd.ExecuteNonQuery();
        }
    }
}

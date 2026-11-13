using System;
using System.Collections.Generic;

namespace ClinicaCentral
{
    public class AppointmentRow
    {
        public string Time { get; set; } = "";
        public string PatientName { get; set; } = "";
        public string Status { get; set; } = "";
    }

    public class QueueEntry
    {
        public int No { get; set; }
        public int PatientId { get; set; }
        public string PatientCode => PatientRow.FormatId(PatientId);
        public string Name { get; set; } = "";
    }

    public class RecentPatientRow
    {
        public int Id { get; set; }
        public string Time { get; set; } = "";
        public string PatientName { get; set; } = "";
        public string Status { get; set; } = "";
    }

    public class PatientQueue
    {
        private readonly Queue<QueueEntry> _line = new Queue<QueueEntry>();

        public int Count
        {
            get { return _line.Count; }
        }

        public bool IsEmpty()
        {
            return _line.Count == 0;
        }

        public void Clear()
        {
            _line.Clear();
        }

        public void Add(QueueEntry entry)
        {
            _line.Enqueue(entry);
        }

        public QueueEntry CallNext()
        {
            return _line.Dequeue();
        }

        public List<QueueEntry> GetNumberedList()
        {
            var rows = new List<QueueEntry>();
            int number = 1;

            foreach (var entry in _line)
            {
                entry.No = number++;
                rows.Add(entry);
            }
            return rows;
        }
    }

    public class DashboardRepository : Repository
    {
        public DashboardRepository(DatabaseConnection db) : base(db) { }

        public List<AppointmentRow> GetTodayAppointments()
        {
            var list = new List<AppointmentRow>();

            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT TIME_FORMAT(a.appointment_date, '%h:%i %p'), CONCAT(p.first_name, ' ', p.last_name), a.status " +
                "FROM appointments a JOIN patients p ON p.id = a.patient_id " +
                "WHERE DATE(a.appointment_date) = CURDATE() " +
                "ORDER BY a.appointment_date";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new AppointmentRow
                {
                    Time = Convert.ToString(reader.GetValue(0)) ?? "",
                    PatientName = Convert.ToString(reader.GetValue(1)) ?? "",
                    Status = Convert.ToString(reader.GetValue(2)) ?? ""
                });
            }
            return list;
        }

        public List<QueueEntry> GetWaitingPatients()
        {
            var list = new List<QueueEntry>();

            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT p.id, CONCAT(p.first_name, ' ', p.last_name) " +
                "FROM appointments a JOIN patients p ON p.id = a.patient_id " +
                "WHERE DATE(a.appointment_date) = CURDATE() AND a.status = 'Confirmed' " +
                "ORDER BY a.appointment_date";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new QueueEntry
                {
                    PatientId = Convert.ToInt32(reader.GetValue(0)),
                    Name = Convert.ToString(reader.GetValue(1)) ?? ""
                });
            }
            return list;
        }

        public List<RecentPatientRow> GetRecentPatients(int count)
        {
            var list = new List<RecentPatientRow>();

            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT p.id, TIME_FORMAT(a.appointment_date, '%h:%i %p'), CONCAT(p.first_name, ' ', p.last_name), a.status " +
                "FROM patients p " +
                "JOIN appointments a ON a.id = (SELECT a2.id FROM appointments a2 " +
                "                               WHERE a2.patient_id = p.id " +
                "                               ORDER BY a2.appointment_date DESC LIMIT 1) " +
                "ORDER BY a.appointment_date DESC " +
                $"LIMIT {Math.Max(1, count)}";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new RecentPatientRow
                {
                    Id = Convert.ToInt32(reader.GetValue(0)),
                    Time = Convert.ToString(reader.GetValue(1)) ?? "",
                    PatientName = Convert.ToString(reader.GetValue(2)) ?? "",
                    Status = Convert.ToString(reader.GetValue(3)) ?? ""
                });
            }
            return list;
        }

        public int GetPendingLabCount()
        {
            return 0;
        }
    }
}
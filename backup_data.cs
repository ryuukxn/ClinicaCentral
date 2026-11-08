using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MySqlConnector;

namespace ClinicaCentral
{
    public class BackupRecord
    {
        public int Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public string BackupType { get; set; } = "";
        public long SizeBytes { get; set; }
        public string Status { get; set; } = "";
        public string FilePath { get; set; } = "";

        public bool IsSuccessful
        {
            get { return string.Equals(Status, "Successful", StringComparison.OrdinalIgnoreCase); }
        }

        public string DateText
        {
            get { return CreatedAt.ToString("MMM d, yyyy - h:mm tt", CultureInfo.InvariantCulture); }
        }

        public string SizeText
        {
            get { return IsSuccessful ? FormatSize(SizeBytes) : "—"; }
        }

        public static string FormatSize(long bytes)
        {
            if (bytes >= 1024 * 1024)
                return (bytes / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture) + " MB";

            return Math.Max(1, (long)Math.Round(bytes / 1024.0)).ToString(CultureInfo.InvariantCulture) + " KB";
        }
    }

    public class BackupSettings
    {
        public string Frequency { get; set; } = "Daily";
        public int Hour { get; set; } = 2;

        public DateTime GetNextRun(DateTime now, DateTime? lastAutoRun)
        {
            TimeSpan time = TimeSpan.FromHours(Hour);
            DateTime todayAt = now.Date + time;

            if (lastAutoRun == null)
                return todayAt > now ? todayAt : todayAt.AddDays(1);

            DateTime lastDay = lastAutoRun.Value.Date;

            if (Frequency == "Weekly")
                return lastDay.AddDays(7) + time;
            if (Frequency == "Monthly")
                return lastDay.AddMonths(1) + time;

            return lastDay.AddDays(1) + time;
        }
    }

    public class BackupRepository : Repository
    {
        public BackupRepository(DatabaseConnection db) : base(db) { }

        public static string BackupFolder
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "ClinicaCentral", "Backups");
            }
        }

        public List<BackupRecord> GetAll()
        {
            var list = new List<BackupRecord>();

            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "SELECT id, created_at, backup_type, size_bytes, status, file_path " +
                "FROM backups ORDER BY id DESC LIMIT 50", conn);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                BackupRecord item = new BackupRecord();
                item.Id = Convert.ToInt32(reader.GetValue(0));
                item.CreatedAt = Convert.ToDateTime(reader.GetValue(1));
                item.BackupType = Convert.ToString(reader.GetValue(2)) ?? "";
                item.SizeBytes = Convert.ToInt64(reader.GetValue(3));
                item.Status = Convert.ToString(reader.GetValue(4)) ?? "";
                item.FilePath = reader.IsDBNull(5) ? "" : Convert.ToString(reader.GetValue(5)) ?? "";
                list.Add(item);
            }
            return list;
        }

        public DateTime? GetLastAutoRun()
        {
            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "SELECT MAX(created_at) FROM backups WHERE backup_type = 'Auto'", conn);

            object? value = cmd.ExecuteScalar();
            if (value == null || value == DBNull.Value) return null;
            return Convert.ToDateTime(value);
        }

        public BackupSettings GetSettings()
        {
            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "SELECT frequency, run_hour FROM backup_settings WHERE id = 1", conn);

            using var reader = cmd.ExecuteReader();
            BackupSettings settings = new BackupSettings();

            if (reader.Read())
            {
                settings.Frequency = Convert.ToString(reader.GetValue(0)) ?? "Daily";
                settings.Hour = Convert.ToInt32(reader.GetValue(1));
            }
            return settings;
        }
    }
}

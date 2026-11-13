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
        public string FileName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string BackupType { get; set; } = "";
        public long SizeBytes { get; set; }
        public string Status { get; set; } = "";
        public string CreatedBy { get; set; } = "";

        public bool IsSuccessful
        {
            get { return string.Equals(Status, "Success", StringComparison.OrdinalIgnoreCase); }
        }

        public bool CanRestore
        {
            get { return IsSuccessful && File.Exists(FilePath); }
        }

        public string DateText
        {
            get { return CreatedAt.ToString("MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture); }
        }

        public string SizeText
        {
            get { return IsSuccessful ? FormatSize(SizeBytes) : "\u2014"; }
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
        public TimeSpan BackupTime { get; set; } = TimeSpan.FromHours(2);
        public DateTime? LastRun { get; set; }

        public bool IsDue(DateTime now)
        {
            DateTime dueToday = now.Date + BackupTime;
            if (now < dueToday) return false;
            if (LastRun == null) return true;

            DateTime nextDue = GetNextAfter(LastRun.Value);
            return now >= nextDue;
        }

        public DateTime GetNextRun(DateTime now)
        {
            if (LastRun == null)
            {
                DateTime firstRun = now.Date + BackupTime;
                return firstRun > now ? firstRun : firstRun.AddDays(1);
            }

            DateTime nextRun = GetNextAfter(LastRun.Value);
            while (nextRun <= now)
                nextRun = GetNextAfter(nextRun);

            return nextRun;
        }

        private DateTime GetNextAfter(DateTime run)
        {
            DateTime nextDate;
            if (string.Equals(Frequency, "Weekly", StringComparison.OrdinalIgnoreCase))
                nextDate = run.Date.AddDays(7);
            else if (string.Equals(Frequency, "Monthly", StringComparison.OrdinalIgnoreCase))
                nextDate = run.Date.AddMonths(1);
            else
                nextDate = run.Date.AddDays(1);

            return nextDate + BackupTime;
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
                "SELECT id, file_name, file_path, created_at, backup_type, size_bytes, status, created_by " +
                "FROM backups ORDER BY created_at DESC, id DESC LIMIT 50", conn);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(ReadRecord(reader));

            return list;
        }

        public BackupRecord? GetLatestSuccess()
        {
            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "SELECT id, file_name, file_path, created_at, backup_type, size_bytes, status, created_by " +
                "FROM backups WHERE status = 'Success' ORDER BY created_at DESC, id DESC LIMIT 1", conn);

            using var reader = cmd.ExecuteReader();
            return reader.Read() ? ReadRecord(reader) : null;
        }

        public void InsertBackup(BackupRecord record)
        {
            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "INSERT INTO backups " +
                "(file_name, file_path, backup_type, size_bytes, status, created_by, created_at) " +
                "VALUES (@fileName, @filePath, @backupType, @sizeBytes, @status, @createdBy, NOW())", conn);
            cmd.Parameters.AddWithValue("@fileName", record.FileName);
            cmd.Parameters.AddWithValue("@filePath", record.FilePath);
            cmd.Parameters.AddWithValue("@backupType", record.BackupType);
            cmd.Parameters.AddWithValue("@sizeBytes", record.SizeBytes);
            cmd.Parameters.AddWithValue("@status", record.Status);
            cmd.Parameters.AddWithValue("@createdBy", record.CreatedBy);
            cmd.ExecuteNonQuery();
        }

        public void ExportToFile(string filePath)
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            using var backup = new MySqlBackup(cmd);
            backup.ExportInfo.AddDropTable = true;
            backup.ExportInfo.ExportTableStructure = true;
            backup.ExportInfo.ExportRows = true;
            backup.ExportInfo.ExcludeTables.Add("backups");
            backup.ExportInfo.ExcludeTables.Add("backup_settings");
            backup.ExportToFile(filePath);
        }

        public void ImportFromFile(string filePath)
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            using var backup = new MySqlBackup(cmd);
            backup.ImportFromFile(filePath);
        }

        public BackupSettings GetSettings()
        {
            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "SELECT frequency, backup_time, last_run FROM backup_settings WHERE id = 1", conn);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                throw new InvalidOperationException("The backup_settings row with id = 1 was not found.");

            object timeValue = reader.GetValue(1);
            TimeSpan backupTime = timeValue is TimeSpan time
                ? time
                : TimeSpan.Parse(Convert.ToString(timeValue, CultureInfo.InvariantCulture) ?? "02:00:00",
                    CultureInfo.InvariantCulture);

            return new BackupSettings
            {
                Frequency = Convert.ToString(reader.GetValue(0)) ?? "Daily",
                BackupTime = backupTime,
                LastRun = reader.IsDBNull(2) ? (DateTime?)null : Convert.ToDateTime(reader.GetValue(2))
            };
        }

        public void SaveSettings(BackupSettings settings)
        {
            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "UPDATE backup_settings SET frequency = @frequency, backup_time = @backupTime WHERE id = 1", conn);
            cmd.Parameters.AddWithValue("@frequency", settings.Frequency);
            cmd.Parameters.AddWithValue("@backupTime", settings.BackupTime);
            cmd.ExecuteNonQuery();
        }

        public void UpdateLastRun()
        {
            using var conn = OpenConnection();
            using var cmd = new MySqlCommand(
                "UPDATE backup_settings SET last_run = NOW() WHERE id = 1", conn);
            if (cmd.ExecuteNonQuery() != 1)
                throw new InvalidOperationException("The backup schedule could not be updated.");
        }

        private static BackupRecord ReadRecord(MySqlDataReader reader)
        {
            return new BackupRecord
            {
                Id = Convert.ToInt32(reader.GetValue(0)),
                FileName = Convert.ToString(reader.GetValue(1)) ?? "",
                FilePath = reader.IsDBNull(2) ? "" : Convert.ToString(reader.GetValue(2)) ?? "",
                CreatedAt = Convert.ToDateTime(reader.GetValue(3)),
                BackupType = Convert.ToString(reader.GetValue(4)) ?? "",
                SizeBytes = Convert.ToInt64(reader.GetValue(5)),
                Status = Convert.ToString(reader.GetValue(6)) ?? "",
                CreatedBy = reader.IsDBNull(7) ? "" : Convert.ToString(reader.GetValue(7)) ?? ""
            };
        }
    }
}

using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MySqlConnector;

namespace ClinicaCentral
{
    public class PatientUpload
    {
        public string FileName { get; set; } = "";
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public DateTime UploadedAt { get; set; }
    }

    public static class UploadStore
    {
        private const int MaxSide = 1600;
        private const long MaxFileBytes = 8L * 1024 * 1024;

        public static byte[] Prepare(string sourceFile)
        {
            if (new FileInfo(sourceFile).Length > MaxFileBytes)
                throw new InvalidOperationException("That picture is too large. Please choose one under 8 MB.");

            BitmapImage source = new BitmapImage();
            source.BeginInit();
            source.CacheOption = BitmapCacheOption.OnLoad;
            source.UriSource = new Uri(sourceFile);
            source.EndInit();
            source.Freeze();

            double scale = Math.Min(1.0, (double)MaxSide / Math.Max(source.PixelWidth, source.PixelHeight));
            int width = Math.Max(1, (int)Math.Round(source.PixelWidth * scale));
            int height = Math.Max(1, (int)Math.Round(source.PixelHeight * scale));

            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
                dc.DrawImage(source, new Rect(0, 0, width, height));
            }

            RenderTargetBitmap bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);

            JpegBitmapEncoder encoder = new JpegBitmapEncoder { QualityLevel = 88 };
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using (MemoryStream stream = new MemoryStream())
            {
                encoder.Save(stream);
                return stream.ToArray();
            }
        }
    }

    public class PatientUploadRepository : Repository
    {
        public PatientUploadRepository(DatabaseConnection db) : base(db) { }

        private static void EnsureTable(MySqlConnection conn)
        {
            using var cmd = new MySqlCommand(
                "CREATE TABLE IF NOT EXISTS patient_uploads (" +
                "id INT AUTO_INCREMENT PRIMARY KEY, " +
                "patient_id INT NOT NULL, " +
                "file_name VARCHAR(255) NOT NULL, " +
                "image MEDIUMBLOB NOT NULL, " +
                "uploaded_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP, " +
                "INDEX idx_patient_uploads_patient (patient_id))", conn);
            cmd.ExecuteNonQuery();
        }

        public void Save(int patientId, string fileName, byte[] jpeg)
        {
            if (fileName.Length > 255) fileName = fileName.Substring(0, 255);

            using var conn = OpenConnection();
            EnsureTable(conn);

            using var cmd = new MySqlCommand(
                "INSERT INTO patient_uploads (patient_id, file_name, image) VALUES (@pid, @name, @image)", conn);
            cmd.Parameters.AddWithValue("@pid", patientId);
            cmd.Parameters.AddWithValue("@name", fileName);
            cmd.Parameters.Add("@image", MySqlDbType.MediumBlob).Value = jpeg;
            cmd.ExecuteNonQuery();
        }

        public PatientUpload? GetLatest(int patientId)
        {
            using var conn = OpenConnection();
            EnsureTable(conn);

            using var cmd = new MySqlCommand(
                "SELECT file_name, image, uploaded_at FROM patient_uploads " +
                "WHERE patient_id = @pid ORDER BY id DESC LIMIT 1", conn);
            cmd.Parameters.AddWithValue("@pid", patientId);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;

            return new PatientUpload
            {
                FileName = Convert.ToString(reader.GetValue(0)) ?? "",
                Data = (byte[])reader.GetValue(1),
                UploadedAt = Convert.ToDateTime(reader.GetValue(2))
            };
        }
    }
}

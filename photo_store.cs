using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MySqlConnector;

namespace ClinicaCentral
{
    public static class PhotoStore
    {
        private const int MaxSide = 512;
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
            BitmapSource result = scale < 1.0
                ? new TransformedBitmap(source, new ScaleTransform(scale, scale))
                : source;

            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(result));

            using (MemoryStream stream = new MemoryStream())
            {
                encoder.Save(stream);
                return stream.ToArray();
            }
        }

        public static BitmapImage FromBytes(byte[] data)
        {
            BitmapImage image = new BitmapImage();
            using (MemoryStream stream = new MemoryStream(data))
            {
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
            }
            image.Freeze();
            return image;
        }
    }

    public class PhotoRepository : Repository
    {
        public PhotoRepository(DatabaseConnection db) : base(db) { }

        public byte[]? GetPhoto(int patientId)
        {
            using var conn = OpenConnection();
            using var cmd = new MySqlCommand("SELECT photo FROM patients WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("@id", patientId);

            return cmd.ExecuteScalar() as byte[];
        }

        public void SavePhoto(int patientId, byte[] png)
        {
            using var conn = OpenConnection();
            using var cmd = new MySqlCommand("UPDATE patients SET photo = @photo WHERE id = @id", conn);
            cmd.Parameters.Add("@photo", MySqlDbType.MediumBlob).Value = png;
            cmd.Parameters.AddWithValue("@id", patientId);
            cmd.ExecuteNonQuery();
        }
    }
}

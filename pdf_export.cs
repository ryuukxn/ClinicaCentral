using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ClinicaCentral
{
    public static class PdfExport
    {
        private const double PageWidth = 595.28;
        private const double PageHeight = 841.89;
        private const double PageMargin = 40;
        private const double RenderDpi = 300;

        public static void SaveElement(FrameworkElement element, string filePath)
        {
            byte[] jpeg = RenderToJpeg(element, out int pixelWidth, out int pixelHeight);
            File.WriteAllBytes(filePath, BuildPdf(jpeg, pixelWidth, pixelHeight));
        }

        private static byte[] RenderToJpeg(FrameworkElement element, out int pixelWidth, out int pixelHeight)
        {
            element.UpdateLayout();

            double width = element.ActualWidth;
            double height = element.ActualHeight;
            double scale = RenderDpi / 96.0;

            pixelWidth = (int)Math.Ceiling(width * scale);
            pixelHeight = (int)Math.Ceiling(height * scale);

            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                Rect area = new Rect(0, 0, width, height);
                dc.DrawRectangle(Brushes.White, null, area);
                dc.DrawRectangle(new VisualBrush(element), null, area);
            }

            RenderTargetBitmap bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, RenderDpi, RenderDpi, PixelFormats.Pbgra32);
            bitmap.Render(visual);

            JpegBitmapEncoder encoder = new JpegBitmapEncoder { QualityLevel = 95 };
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using MemoryStream stream = new MemoryStream();
            encoder.Save(stream);
            return stream.ToArray();
        }

        private static byte[] BuildPdf(byte[] jpeg, int pixelWidth, int pixelHeight)
        {
            double drawWidth = pixelWidth * 72.0 / RenderDpi;
            double drawHeight = pixelHeight * 72.0 / RenderDpi;
            double fit = Math.Min(1.0, Math.Min((PageWidth - 2 * PageMargin) / drawWidth,
                                                (PageHeight - 2 * PageMargin) / drawHeight));
            drawWidth *= fit;
            drawHeight *= fit;

            double x = (PageWidth - drawWidth) / 2;
            double y = PageHeight - PageMargin - drawHeight;

            string content = string.Format(CultureInfo.InvariantCulture,
                "q {0:0.##} 0 0 {1:0.##} {2:0.##} {3:0.##} cm /Im0 Do Q", drawWidth, drawHeight, x, y);

            using MemoryStream pdf = new MemoryStream();
            int[] offsets = new int[6];

            Write(pdf, "%PDF-1.4\n");

            offsets[1] = (int)pdf.Position;
            Write(pdf, "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

            offsets[2] = (int)pdf.Position;
            Write(pdf, "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");

            offsets[3] = (int)pdf.Position;
            Write(pdf, string.Format(CultureInfo.InvariantCulture,
                "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {0} {1}] " +
                "/Resources << /XObject << /Im0 5 0 R >> >> /Contents 4 0 R >>\nendobj\n", PageWidth, PageHeight));

            offsets[4] = (int)pdf.Position;
            Write(pdf, "4 0 obj\n<< /Length " + content.Length + " >>\nstream\n" + content + "\nendstream\nendobj\n");

            offsets[5] = (int)pdf.Position;
            Write(pdf, string.Format(CultureInfo.InvariantCulture,
                "5 0 obj\n<< /Type /XObject /Subtype /Image /Width {0} /Height {1} /ColorSpace /DeviceRGB " +
                "/BitsPerComponent 8 /Filter /DCTDecode /Length {2} >>\nstream\n", pixelWidth, pixelHeight, jpeg.Length));
            pdf.Write(jpeg, 0, jpeg.Length);
            Write(pdf, "\nendstream\nendobj\n");

            int xrefPosition = (int)pdf.Position;
            StringBuilder xref = new StringBuilder();
            xref.Append("xref\n0 6\n0000000000 65535 f \n");
            for (int i = 1; i <= 5; i++)
                xref.Append(offsets[i].ToString("0000000000", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
            xref.Append("trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n").Append(xrefPosition).Append("\n%%EOF\n");
            Write(pdf, xref.ToString());

            return pdf.ToArray();
        }

        private static void Write(Stream stream, string text)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}

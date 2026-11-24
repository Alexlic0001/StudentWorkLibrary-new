using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace StudentWorkLibrary.Services
{
    public interface IPdfTrimmer
    {
        Task<string?> TrimToFirstPagesCopyAsync(string sourcePdfPath, string outputPdfPath, int maxPages);
    }

    public class PdfTrimmer : IPdfTrimmer
    {
        private readonly ILogger<PdfTrimmer> _logger;

        public PdfTrimmer(ILogger<PdfTrimmer> logger)
        {
            _logger = logger;
        }

        public Task<string?> TrimToFirstPagesCopyAsync(string sourcePdfPath, string outputPdfPath, int maxPages)
        {
            if (!File.Exists(sourcePdfPath))
            {
                _logger.LogWarning("PDF не найден: {Path}", sourcePdfPath);
                return Task.FromResult<string?>(null);
            }

            try
            {
                if (maxPages <= 0)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPdfPath)!);
                    File.Copy(sourcePdfPath, outputPdfPath, overwrite: true);
                    return Task.FromResult<string?>(outputPdfPath);
                }

                using var input = PdfReader.Open(sourcePdfPath, PdfDocumentOpenMode.Import);
                using var output = new PdfDocument();

                if (input.PageCount <= maxPages)
                {
                    for (int i = 0; i < input.PageCount; i++)
                        output.AddPage(input.Pages[i]);
                }
                else
                {
                    for (int i = 0; i < maxPages; i++)
                        output.AddPage(input.Pages[i]);
                }

                // ✅ НАНЕСЕНИЕ ВОДЯНОГО ЗНАКА
                ApplyWatermark(output, "Образец");

                Directory.CreateDirectory(Path.GetDirectoryName(outputPdfPath)!);
                output.Save(outputPdfPath);

                _logger.LogInformation("PDF обрезан до {Max} стр. и помечен водяным знаком: {Path}", maxPages, outputPdfPath);
                return Task.FromResult<string?>(outputPdfPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка обрезки PDF: {Path}", sourcePdfPath);
                return Task.FromResult<string?>(null);
            }
        }

        /// <summary>
        /// Наносит диагональный водяной знак на каждую страницу документа.
        /// </summary>
        private void ApplyWatermark(PdfDocument document, string text)
        {
            // ✅ Используем системный Arial — на Windows он точно поддерживает кириллицу
            XFont font = new XFont("Arial", 100, XFontStyle.Bold);

            XBrush brush = XBrushes.LightGray; // Светло-серый цвет

            XStringFormat format = new XStringFormat
            {
                Alignment = XStringAlignment.Center,
                LineAlignment = XLineAlignment.Center
            };

            foreach (PdfPage page in document.Pages)
            {
                // Append означает, что мы рисуем ПОВЕРХ существующего контента
                using XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);

                // Перемещаем точку отсчета (0,0) в центр страницы
                gfx.TranslateTransform(page.Width / 2, page.Height / 2);

                // Поворачиваем систему координат на -45 градусов (по диагонали)
                gfx.RotateTransform(-45);

                // Рисуем текст в центре
                gfx.DrawString(text, font, brush, 0, 0, format);
            }
        }
    }
}
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace StudentWorkLibrary.Services
{
    public interface IPdfTrimmer
    {
        /// <summary>
        /// Создаёт обрезанную копию PDF (первые maxPages страниц) в отдельном файле.
        /// Оригинал не изменяется. Возвращает путь к обрезанной копии.
        /// Если страниц <= maxPages — возвращает путь к копии (полной, но в кэше).
        /// </summary>
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
                // Если maxPages <= 0 — просто копируем целиком (без обрезки)
                if (maxPages <= 0)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPdfPath)!);
                    File.Copy(sourcePdfPath, outputPdfPath, overwrite: true);
                    _logger.LogInformation("PDF скопирован без обрезки: {Path}", outputPdfPath);
                    return Task.FromResult<string?>(outputPdfPath);
                }

                using var input = PdfReader.Open(sourcePdfPath, PdfDocumentOpenMode.Import);

                using var output = new PdfDocument();

                if (input.PageCount <= maxPages)
                {
                    // Страниц меньше лимита — копируем все
                    for (int i = 0; i < input.PageCount; i++)
                        output.AddPage(input.Pages[i]);

                    _logger.LogInformation("PDF содержит {Count} стр. (лимит {Max}), копируем целиком",
                        input.PageCount, maxPages);
                }
                else
                {
                    for (int i = 0; i < maxPages; i++)
                        output.AddPage(input.Pages[i]);

                    _logger.LogInformation("PDF обрезан до {Max} стр.: {Path}", maxPages, outputPdfPath);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(outputPdfPath)!);
                output.Save(outputPdfPath);
                return Task.FromResult<string?>(outputPdfPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка обрезки PDF: {Path}", sourcePdfPath);
                return Task.FromResult<string?>(null);
            }
        }
    }
}
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace StudentWorkLibrary.Services
{
    public interface IDocumentConverter
    {
        /// <summary>
        /// Конвертирует Office-файл в PDF (полный, без обрезки) и кладёт рядом с исходником.
        /// Возвращает путь к полному PDF.
        /// </summary>
        Task<string?> ConvertToPdfAsync(string inputPath);
    }

    public class DocumentConverter : IDocumentConverter
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<DocumentConverter> _logger;

        public DocumentConverter(IWebHostEnvironment env, ILogger<DocumentConverter> logger)
        {
            _env = env;
            _logger = logger;
        }

        public async Task<string?> ConvertToPdfAsync(string inputPath)
        {
            // Полный PDF кладём рядом с исходником (кэш конвертации)
            var pdfPath = Path.ChangeExtension(inputPath, ".pdf");

            // Кэш: PDF уже есть и новее исходника — возвращаем
            if (File.Exists(pdfPath))
            {
                var inputTime = File.GetLastWriteTimeUtc(inputPath);
                var pdfTime = File.GetLastWriteTimeUtc(pdfPath);
                if (pdfTime >= inputTime)
                {
                    _logger.LogInformation("PDF уже существует, кэш: {Path}", pdfPath);
                    return pdfPath;
                }

                _logger.LogInformation("PDF устарел, переконвертация: {Path}", pdfPath);
                try { File.Delete(pdfPath); } catch { }
            }

            var tempDir = Path.Combine(_env.WebRootPath, "temp-preview");
            Directory.CreateDirectory(tempDir);

            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            var soffice = isWindows
                ? @"C:\Program Files\LibreOffice\program\soffice.exe"
                : "soffice";

            var userProfile = Path.Combine(Path.GetTempPath(), "lo-profile-" + Guid.NewGuid().ToString("N"));
            var args = $"--headless --invisible --norestore " +
                       $"-env:UserInstallation=file:///{userProfile.Replace('\\', '/')} " +
                       $"--convert-to pdf --outdir \"{tempDir}\" \"{inputPath}\"";

            var psi = new ProcessStartInfo
            {
                FileName = soffice,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                _logger.LogInformation("Запуск LibreOffice для {Path}", inputPath);

                using var process = Process.Start(psi);
                if (process == null) return null;

                var completed = await Task.Run(() => process.WaitForExit(30000));
                if (!completed)
                {
                    process.Kill();
                    _logger.LogWarning("LibreOffice timeout для {Path}", inputPath);
                    return null;
                }

                var tempPdf = Path.Combine(tempDir,
                    Path.GetFileNameWithoutExtension(inputPath) + ".pdf");

                if (!File.Exists(tempPdf))
                {
                    _logger.LogWarning("PDF не создан для {Path}", inputPath);
                    return null;
                }

                File.Move(tempPdf, pdfPath, overwrite: true);
                _logger.LogInformation("PDF сохранён: {Path}", pdfPath);
                return pdfPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка конвертации для {Path}", inputPath);
                return null;
            }
            finally
            {
                try { if (Directory.Exists(userProfile)) Directory.Delete(userProfile, true); } catch { }
            }
        }
    }
}
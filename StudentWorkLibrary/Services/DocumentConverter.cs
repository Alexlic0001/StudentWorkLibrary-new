using System.Diagnostics;
using System.Runtime.InteropServices;

namespace StudentWorkLibrary.Services
{
    public interface IDocumentConverter
    {
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
            // PDF кладём рядом с исходным файлом: то же имя, но расширение .pdf
            var pdfPath = Path.ChangeExtension(inputPath, ".pdf");

            // Если PDF уже сконвертирован — возвращаем сразу, без запуска LibreOffice
            if (File.Exists(pdfPath))
            {
                // Проверяем, что PDF новее исходного файла (не устарел)
                var inputTime = File.GetLastWriteTimeUtc(inputPath);
                var pdfTime = File.GetLastWriteTimeUtc(pdfPath);
                if (pdfTime >= inputTime)
                {
                    _logger.LogInformation("PDF уже существует, кэш: {Path}", pdfPath);
                    return pdfPath;
                }

                // PDF старее исходника — удаляем и конвертируем заново
                _logger.LogInformation("PDF устарел, переконвертация: {Path}", pdfPath);
                try { File.Delete(pdfPath); } catch { }
            }

            // Временная папка для вывода LibreOffice (он не умеет писать сразу в целевую папку с нужным именем)
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
                _logger.LogInformation("Запуск LibreOffice: {File}", soffice);

                using var process = Process.Start(psi);
                if (process == null) return null;

                var completed = await Task.Run(() => process.WaitForExit(30000));
                if (!completed)
                {
                    process.Kill();
                    _logger.LogWarning("LibreOffice timeout for {Path}", inputPath);
                    return null;
                }

                // LibreOffice сохраняет PDF в tempDir с именем исходного файла
                var tempPdf = Path.Combine(tempDir,
                    Path.GetFileNameWithoutExtension(inputPath) + ".pdf");

                if (!File.Exists(tempPdf))
                {
                    _logger.LogWarning("PDF not created for {Path}", inputPath);
                    return null;
                }

                // Перемещаем PDF рядом с исходным файлом
                File.Move(tempPdf, pdfPath, overwrite: true);
                _logger.LogInformation("PDF сохранён: {Path}", pdfPath);
                return pdfPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Conversion failed for {Path}", inputPath);
                return null;
            }
            finally
            {
                try { if (Directory.Exists(userProfile)) Directory.Delete(userProfile, true); } catch { }
            }
        }
    }
}
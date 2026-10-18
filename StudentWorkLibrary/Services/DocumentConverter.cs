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
            var tempDir = Path.Combine(_env.WebRootPath, "temp-preview");
            Directory.CreateDirectory(tempDir);

            var outputName = $"{Guid.NewGuid():N}.pdf";
            var outputPath = Path.Combine(tempDir, outputName);

            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            var soffice = isWindows ? "soffice.exe" : "soffice";

            // Уникальный профиль для каждого вызова — предотвращает блокировки при параллельных конвертациях
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
                using var process = Process.Start(psi);
                if (process == null) return null;

                var completed = await Task.Run(() => process.WaitForExit(30000));
                if (!completed)
                {
                    process.Kill();
                    _logger.LogWarning("LibreOffice timeout for {Path}", inputPath);
                    return null;
                }

                // LibreOffice сохраняет PDF с тем же именем, что и входной файл
                var expectedPdf = Path.Combine(tempDir,
                    Path.GetFileNameWithoutExtension(inputPath) + ".pdf");

                if (!File.Exists(expectedPdf))
                {
                    _logger.LogWarning("PDF not created for {Path}", inputPath);
                    return null;
                }

                File.Move(expectedPdf, outputPath, overwrite: true);
                return outputPath;
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
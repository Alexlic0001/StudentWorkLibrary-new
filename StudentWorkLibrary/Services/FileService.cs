using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Hosting;

namespace StudentWorkLibrary.Services
{
    public interface IFileService
    {
        Task<string> SaveFileAsync(IBrowserFile file, string workTypeName, string institutionName, int? year);
        Task<bool> DeleteFileAsync(string filePath);
        string GetFileUrl(string filePath);
    }

    public class FileService : IFileService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<FileService> _logger;
        private static readonly string[] AllowedExtensions = { ".pdf", ".doc", ".docx", ".zip", ".rar" };
        private const long MaxFileSize = 104857600; // 100 MB

        public FileService(IWebHostEnvironment environment, ILogger<FileService> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        public async Task<string> SaveFileAsync(IBrowserFile file, string workTypeName, string institutionName, int? year)
        {
            var ext = Path.GetExtension(file.Name).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
                throw new InvalidOperationException($"Недопустимый формат файла. Разрешены: {string.Join(", ", AllowedExtensions)}");

            if (file.Size > MaxFileSize)
                throw new InvalidOperationException($"Файл превышает допустимый размер ({MaxFileSize / 1048576} MB).");

            // Санитизация имён папок
            var safeWorkType = SanitizeFolderName(workTypeName);
            var safeInstitution = SanitizeFolderName(institutionName);
            var yearFolder = year?.ToString() ?? DateTime.Now.Year.ToString();

            var uploadDir = Path.Combine(_environment.WebRootPath, "uploads", safeWorkType, safeInstitution, yearFolder);
            Directory.CreateDirectory(uploadDir);

            var uniqueName = $"{Guid.NewGuid():N}_{DateTime.Now:yyyyMMddHHmmss}_{file.Name}";
            var filePath = Path.Combine(uploadDir, uniqueName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.OpenReadStream(MaxFileSize).CopyToAsync(stream);

            // Возвращаем путь относительно wwwroot
            return $"uploads/{safeWorkType}/{safeInstitution}/{yearFolder}/{uniqueName}";
        }

        public async Task<bool> DeleteFileAsync(string filePath)
        {
            try
            {
                var fullPath = Path.Combine(_environment.WebRootPath, filePath);
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка удаления файла: {FilePath}", filePath);
                return false;
            }
        }

        public string GetFileUrl(string filePath) => $"/{filePath.Replace('\\', '/')}";

        private string SanitizeFolderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Unknown";
            return string.Join("_", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries))
                .Replace(" ", "_").Replace("/", "_");
        }
    }
}
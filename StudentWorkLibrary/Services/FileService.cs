using Microsoft.AspNetCore.Hosting;
using System.Text;

namespace StudentWorkLibrary.Services
{
    public interface IFileService
    {
        /// <summary>
        /// Сохраняет файл из переданного потока на диск.
        /// Поток должен быть уже прочитан (чтобы можно было посчитать хэш до сохранения).
        /// </summary>
        Task<string> SaveFileAsync(Stream fileStream, string originalFileName, string workTypeName, string institutionName, int? year);
    }

    public class FileService : IFileService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<FileService> _logger;
        private static readonly string[] AllowedExtensions =
            { ".pdf", ".doc", ".docx", ".zip", ".rar", ".ppt", ".pptx" };
        private const long MaxFileSize = 104857600; // 100 MB

        public FileService(IWebHostEnvironment environment, ILogger<FileService> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        public async Task<string> SaveFileAsync(Stream fileStream, string originalFileName, string workTypeName, string institutionName, int? year)
        {
            var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
                throw new InvalidOperationException($"Недопустимый формат файла. Разрешены: {string.Join(", ", AllowedExtensions)}");

            // ASCII-slug для папок: транслитерация кириллицы + замена недопустимых символов
            var safeWorkType = ToAsciiSlug(workTypeName);
            var safeInstitution = ToAsciiSlug(institutionName);
            var yearFolder = year?.ToString() ?? DateTime.Now.Year.ToString();
            var uploadDir = Path.Combine(_environment.WebRootPath, "uploads", safeWorkType, safeInstitution, yearFolder);

            Directory.CreateDirectory(uploadDir);

            // Безопасное имя файла: только GUID + timestamp + расширение
            var uniqueName = $"{Guid.NewGuid():N}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
            var filePath = Path.Combine(uploadDir, uniqueName);

            // ✅ Сохраняем переданный поток (уже прочитанный в Upload.razor)
            await using var stream = new FileStream(filePath, FileMode.Create);
            await fileStream.CopyToAsync(stream);

            _logger.LogInformation("Файл сохранён: {Path}", filePath);

            // В БД храним относительный путь с прямыми слэшами
            return $"uploads/{safeWorkType}/{safeInstitution}/{yearFolder}/{uniqueName}";
        }

        private static string ToAsciiSlug(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "unknown";
            var map = new Dictionary<char, string>
            {
                ['а'] = "a",
                ['б'] = "b",
                ['в'] = "v",
                ['г'] = "g",
                ['д'] = "d",
                ['е'] = "e",
                ['ё'] = "e",
                ['ж'] = "zh",
                ['з'] = "z",
                ['и'] = "i",
                ['й'] = "y",
                ['к'] = "k",
                ['л'] = "l",
                ['м'] = "m",
                ['н'] = "n",
                ['о'] = "o",
                ['п'] = "p",
                ['р'] = "r",
                ['с'] = "s",
                ['т'] = "t",
                ['у'] = "u",
                ['ф'] = "f",
                ['х'] = "h",
                ['ц'] = "ts",
                ['ч'] = "ch",
                ['ш'] = "sh",
                ['щ'] = "sch",
                ['ъ'] = "",
                ['ы'] = "y",
                ['ь'] = "",
                ['э'] = "e",
                ['ю'] = "yu",
                ['я'] = "ya",
                ['А'] = "A",
                ['Б'] = "B",
                ['В'] = "V",
                ['Г'] = "G",
                ['Д'] = "D",
                ['Е'] = "E",
                ['Ё'] = "E",
                ['Ж'] = "Zh",
                ['З'] = "Z",
                ['И'] = "I",
                ['Й'] = "Y",
                ['К'] = "K",
                ['Л'] = "L",
                ['М'] = "M",
                ['Н'] = "N",
                ['О'] = "O",
                ['П'] = "P",
                ['Р'] = "R",
                ['С'] = "S",
                ['Т'] = "T",
                ['У'] = "U",
                ['Ф'] = "F",
                ['Х'] = "H",
                ['Ц'] = "Ts",
                ['Ч'] = "Ch",
                ['Ш'] = "Sh",
                ['Щ'] = "Sch",
                ['Ъ'] = "",
                ['Ы'] = "Y",
                ['Ь'] = "",
                ['Э'] = "E",
                ['Ю'] = "Yu",
                ['Я'] = "Ya",
            };
            var sb = new StringBuilder();
            foreach (var ch in name)
            {
                if (map.TryGetValue(ch, out var tr))
                    sb.Append(tr);
                else if (char.IsLetterOrDigit(ch))
                    sb.Append(ch);
                else
                    sb.Append('_');
            }
            return sb.ToString().Trim('_');
        }
    }
}
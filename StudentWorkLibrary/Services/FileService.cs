using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Hosting;
using System.Text;

namespace StudentWorkLibrary.Services
{
    public interface IFileService
    {
        Task<string> SaveFileAsync(IBrowserFile file, string workTypeName, string institutionName, int? year);
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

        public async Task<string> SaveFileAsync(IBrowserFile file, string workTypeName, string institutionName, int? year)
        {
            var ext = Path.GetExtension(file.Name).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
                throw new InvalidOperationException($"Недопустимый формат файла. Разрешены: {string.Join(", ", AllowedExtensions)}");

            if (file.Size > MaxFileSize)
                throw new InvalidOperationException($"Файл превышает допустимый размер ({MaxFileSize / 1048576} MB).");

            // ASCII-slug для папок: транслитерация кириллицы + замена недопустимых символов
            var safeWorkType = ToAsciiSlug(workTypeName);
            var safeInstitution = ToAsciiSlug(institutionName);
            var yearFolder = year?.ToString() ?? DateTime.Now.Year.ToString();

            var uploadDir = Path.Combine(_environment.WebRootPath, "uploads", safeWorkType, safeInstitution, yearFolder);
            Directory.CreateDirectory(uploadDir);

            // Безопасное имя файла: только GUID + timestamp + расширение
            var uniqueName = $"{Guid.NewGuid():N}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
            var filePath = Path.Combine(uploadDir, uniqueName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.OpenReadStream(MaxFileSize).CopyToAsync(stream);

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
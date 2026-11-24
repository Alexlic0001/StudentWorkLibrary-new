using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentWorkLibrary.Data;
using StudentWorkLibrary.Services;
using System.IO.Compression;

namespace StudentWorkLibrary.Controllers;

[ApiController]
[Route("api/export")]
public class ExportController : ControllerBase
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;
    private readonly IWebHostEnvironment _env;
    private readonly IDocumentConverter _converter;
    private readonly IPdfTrimmer _pdfTrimmer;
    private readonly int _maxPreviewPages;
    private readonly ILogger<ExportController> _logger;

    public ExportController(
        IDbContextFactory<ApplicationDbContext> factory,
        IWebHostEnvironment env,
        IDocumentConverter converter,
        IPdfTrimmer pdfTrimmer,
        IConfiguration config,
        ILogger<ExportController> logger)
    {
        _factory = factory;
        _env = env;
        _converter = converter;
        _pdfTrimmer = pdfTrimmer;
        _maxPreviewPages = config.GetValue<int>("PdfPreview:MaxPages", 5);
        _logger = logger;
    }

    /// <summary>
    /// Групповой экспорт работ выбранного типа в ZIP-архив.
    /// </summary>
    /// <param name="workTypeId">ID типа работы (обязательно)</param>
    /// <param name="includeOriginals">Включить оригиналы файлов</param>
    /// <param name="includePreviews">Включить PDF-превью (обрезанные)</param>
    [HttpGet("zip")]
    public async Task<IActionResult> ExportZip(
        [FromQuery] int workTypeId,
        [FromQuery] bool includeOriginals = true,
        [FromQuery] bool includePreviews = true)
    {
        if (!includeOriginals && !includePreviews)
            return BadRequest(new { message = "Выберите хотя бы один вариант экспорта (оригиналы или PDF)" });

        try
        {
            await using var context = await _factory.CreateDbContextAsync();

            // Проверяем существование типа работ
            var workType = await context.WorkTypes.FirstOrDefaultAsync(t => t.Id == workTypeId);
            if (workType == null)
                return NotFound(new { message = "Тип работы не найден", workTypeId });

            // Загружаем все работы этого типа
            var works = await context.StudentWorks
                .Where(w => w.WorkTypeId == workTypeId)
                .OrderBy(w => w.Id)
                .ToListAsync();

            if (!works.Any())
                return NotFound(new { message = "Работ данного типа не найдено", workTypeId });

            _logger.LogInformation(
                "Начало экспорта: тип «{Type}», работ: {Count}, оригиналы: {Orig}, превью: {Prev}",
                workType.Name, works.Count, includeOriginals, includePreviews);

            // Формируем ZIP в памяти
            await using var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                var safeTypeName = SanitizeFileName(workType.Name);
                var rootFolder = $"Export_{safeTypeName}_{DateTime.Now:yyyy-MM-dd}";

                int originalsAdded = 0;
                int previewsAdded = 0;
                int errorsCount = 0;

                foreach (var work in works)
                {
                    var fullPath = Path.Combine(_env.WebRootPath,
                        work.FilePath.Replace('/', Path.DirectorySeparatorChar));

                    if (!System.IO.File.Exists(fullPath))
                    {
                        _logger.LogWarning("Файл не найден на диске: {Path}", fullPath);
                        errorsCount++;
                        continue;
                    }

                    var ext = Path.GetExtension(work.FilePath).ToLowerInvariant();
                    var originalName = $"{work.Id}_{work.OriginalFileName}";
                    var previewName = $"{work.Id}_{Path.GetFileNameWithoutExtension(work.OriginalFileName)}_first{_maxPreviewPages}.pdf";

                    // ===== ОРИГИНАЛ =====
                    if (includeOriginals)
                    {
                        try
                        {
                            var entry = archive.CreateEntry($"{rootFolder}/originals/{originalName}", CompressionLevel.Optimal);
                            await using var entryStream = entry.Open();
                            await using var fileStream = System.IO.File.OpenRead(fullPath);
                            await fileStream.CopyToAsync(entryStream);
                            originalsAdded++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Ошибка добавления оригинала: {File}", originalName);
                            errorsCount++;
                        }
                    }

                    // ===== PDF-ПРЕВЬЮ =====
                    if (includePreviews)
                    {
                        try
                        {
                            string? pdfPath = null;

                            if (ext == ".pdf")
                            {
                                pdfPath = fullPath;
                            }
                            else if (ext is ".doc" or ".docx" or ".ppt" or ".pptx")
                            {
                                pdfPath = await _converter.ConvertToPdfAsync(fullPath);
                            }

                            if (pdfPath == null || !System.IO.File.Exists(pdfPath))
                            {
                                _logger.LogWarning("Не удалось получить PDF для {File}", work.OriginalFileName);
                                errorsCount++;
                                continue;
                            }

                            // Обрезаем PDF во временный файл
                            var tempDir = Path.Combine(_env.WebRootPath, "temp-export");
                            Directory.CreateDirectory(tempDir);
                            var tempTrimmed = Path.Combine(tempDir, $"{Guid.NewGuid():N}.pdf");

                            var trimmed = await _pdfTrimmer.TrimToFirstPagesCopyAsync(pdfPath, tempTrimmed, _maxPreviewPages);
                            if (trimmed == null || !System.IO.File.Exists(trimmed))
                            {
                                _logger.LogWarning("Не удалось обрезать PDF для {File}", work.OriginalFileName);
                                errorsCount++;
                                continue;
                            }

                            var entry = archive.CreateEntry($"{rootFolder}/previews/{previewName}", CompressionLevel.Optimal);
                            await using var entryStream = entry.Open();
                            await using var fileStream = System.IO.File.OpenRead(trimmed);
                            await fileStream.CopyToAsync(entryStream);
                            previewsAdded++;

                            // Удаляем временный файл
                            try { System.IO.File.Delete(trimmed); } catch { }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Ошибка создания превью для {File}", work.OriginalFileName);
                            errorsCount++;
                        }
                    }
                }

                // Добавляем README с информацией об экспорте
                var readmeEntry = archive.CreateEntry($"{rootFolder}/README.txt");
                await using (var readmeStream = readmeEntry.Open())
                using (var writer = new StreamWriter(readmeStream, System.Text.Encoding.UTF8))
                {
                    await writer.WriteLineAsync($"Экспорт работ типа: {workType.Name}");
                    await writer.WriteLineAsync($"Дата экспорта: {DateTime.Now:dd.MM.yyyy HH:mm}");
                    await writer.WriteLineAsync($"Всего работ в выборке: {works.Count}");
                    await writer.WriteLineAsync($"Добавлено оригиналов: {originalsAdded}");
                    await writer.WriteLineAsync($"Добавлено PDF-превью: {previewsAdded}");
                    if (errorsCount > 0)
                        await writer.WriteLineAsync($"Ошибок при обработке: {errorsCount}");
                    await writer.WriteLineAsync();
                    await writer.WriteLineAsync("Список работ:");
                    foreach (var w in works)
                        await writer.WriteLineAsync($"  #{w.Id} — {w.Title} ({w.Author ?? "автор не указан"})");
                }

                _logger.LogInformation(
                    "Экспорт завершён: оригиналов={Orig}, превью={Prev}, ошибок={Err}",
                    originalsAdded, previewsAdded, errorsCount);
            }

            memoryStream.Position = 0;
            var zipFileName = $"Export_{SanitizeFileName(workType.Name)}_{DateTime.Now:yyyy-MM-dd}.zip";
            return File(memoryStream.ToArray(), "application/zip", zipFileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Критическая ошибка при экспорте");
            return StatusCode(500, new { message = "Ошибка при формировании архива", error = ex.Message });
        }
    }

    /// <summary>
    /// Возвращает количество работ указанного типа (для UI).
    /// </summary>
    [HttpGet("count")]
    public async Task<IActionResult> GetCount([FromQuery] int workTypeId)
    {
        await using var context = await _factory.CreateDbContextAsync();
        var count = await context.StudentWorks.CountAsync(w => w.WorkTypeId == workTypeId);
        return Ok(new { workTypeId, count });
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "unknown";
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return sanitized.Trim();
    }
}
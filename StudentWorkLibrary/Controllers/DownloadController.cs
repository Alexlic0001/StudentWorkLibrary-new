using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentWorkLibrary.Data;
using StudentWorkLibrary.Services;
using System.IO;

namespace StudentWorkLibrary.Controllers;

[ApiController]
[Route("api/download")]
public class DownloadController : ControllerBase
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;
    private readonly IWebHostEnvironment _env;
    private readonly IDocumentConverter _converter;
    private readonly IPdfTrimmer _pdfTrimmer;
    private readonly int _maxPreviewPages;

    public DownloadController(
        IDbContextFactory<ApplicationDbContext> factory,
        IWebHostEnvironment env,
        IDocumentConverter converter,
        IPdfTrimmer pdfTrimmer,
        IConfiguration config)
    {
        _factory = factory;
        _env = env;
        _converter = converter;
        _pdfTrimmer = pdfTrimmer;
        _maxPreviewPages = config.GetValue<int>("PdfPreview:MaxPages", 5);
    }

    /// <summary>
    /// Скачивание оригинала: .docx, .pdf, .pptx — что было загружено. Без обрезки.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> DownloadFile(int id)
    {
        try
        {
            await using var context = await _factory.CreateDbContextAsync();
            var work = await context.StudentWorks.FirstOrDefaultAsync(w => w.Id == id);

            if (work == null)
                return NotFound(new { message = "Работа не найдена", id });

            var fullPath = Path.Combine(_env.WebRootPath,
                work.FilePath.Replace('/', Path.DirectorySeparatorChar));

            if (!System.IO.File.Exists(fullPath))
                return NotFound(new { message = "Файл не найден на сервере", path = fullPath });

            work.DownloadCount++;
            await context.SaveChangesAsync();

            var contentType = GetContentType(Path.GetExtension(work.FilePath));

            return PhysicalFile(fullPath, contentType, work.OriginalFileName, enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Ошибка при скачивании файла", error = ex.Message });
        }
    }

    /// <summary>
    /// Скачивание обрезанного PDF (первые _maxPreviewPages страниц).
    /// Оригинал не изменяется.
    /// </summary>
    [HttpGet("trimmed/{id:int}")]
    public async Task<IActionResult> DownloadTrimmedPdf(int id)
    {
        try
        {
            await using var context = await _factory.CreateDbContextAsync();
            var work = await context.StudentWorks.FirstOrDefaultAsync(w => w.Id == id);

            if (work == null)
                return NotFound(new { message = "Работа не найдена", id });

            var fullPath = Path.Combine(_env.WebRootPath,
                work.FilePath.Replace('/', Path.DirectorySeparatorChar));

            if (!System.IO.File.Exists(fullPath))
                return NotFound(new { message = "Файл не найден на сервере", path = fullPath });

            work.DownloadCount++;
            await context.SaveChangesAsync();

            var ext = Path.GetExtension(work.FilePath).ToLowerInvariant();

            string? fullPdfPath;
            if (ext == ".pdf")
            {
                fullPdfPath = fullPath;
            }
            else if (ext is ".doc" or ".docx" or ".ppt" or ".pptx")
            {
                fullPdfPath = await _converter.ConvertToPdfAsync(fullPath);
                if (fullPdfPath == null || !System.IO.File.Exists(fullPdfPath))
                    return StatusCode(500, new { message = "Не удалось конвертировать файл в PDF" });
            }
            else
            {
                return BadRequest(new { message = "Обрезка PDF поддерживается только для PDF, DOC, DOCX, PPT, PPTX", extension = ext });
            }

            var previewDir = Path.Combine(_env.WebRootPath, "preview-cache");
            Directory.CreateDirectory(previewDir);

            var cacheKey = $"{work.Id}_{_maxPreviewPages}_{Path.GetFileNameWithoutExtension(fullPdfPath)}.pdf";
            var trimmedPdfPath = Path.Combine(previewDir, cacheKey);

            if (System.IO.File.Exists(trimmedPdfPath))
            {
                var fullTime = System.IO.File.GetLastWriteTimeUtc(fullPdfPath);
                var trimmedTime = System.IO.File.GetLastWriteTimeUtc(trimmedPdfPath);
                if (trimmedTime >= fullTime)
                {
                    var downloadName = $"{Path.GetFileNameWithoutExtension(work.OriginalFileName)}_first{_maxPreviewPages}.pdf";
                    return PhysicalFile(trimmedPdfPath, "application/pdf", downloadName, enableRangeProcessing: true);
                }
                try { System.IO.File.Delete(trimmedPdfPath); } catch { }
            }

            var trimmed = await _pdfTrimmer.TrimToFirstPagesCopyAsync(fullPdfPath, trimmedPdfPath, _maxPreviewPages);
            if (trimmed == null)
                return StatusCode(500, new { message = "Не удалось подготовить PDF" });

            var downloadFileName = $"{Path.GetFileNameWithoutExtension(work.OriginalFileName)}_first{_maxPreviewPages}.pdf";
            return PhysicalFile(trimmed, "application/pdf", downloadFileName, enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Ошибка при скачивании обрезанного PDF", error = ex.Message });
        }
    }

    /// <summary>
    /// Предпросмотр: ПОЛНЫЙ PDF (без обрезки), inline в iframe.
    /// Для Office-форматов — конвертирует в полный PDF.
    /// </summary>
    [HttpGet("preview/{id:int}")]
    public async Task<IActionResult> PreviewFile(int id)
    {
        try
        {
            await using var context = await _factory.CreateDbContextAsync();
            var work = await context.StudentWorks.FirstOrDefaultAsync(w => w.Id == id);

            if (work == null)
                return NotFound(new { message = "Работа не найдена", id });

            var fullPath = Path.Combine(_env.WebRootPath,
                work.FilePath.Replace('/', Path.DirectorySeparatorChar));

            if (!System.IO.File.Exists(fullPath))
                return NotFound(new { message = "Файл не найден на сервере", path = fullPath });

            var ext = Path.GetExtension(work.FilePath).ToLowerInvariant();

            // PDF — отдаём как есть (полный)
            if (ext == ".pdf")
                return PhysicalFile(fullPath, "application/pdf", enableRangeProcessing: true);

            // Office-форматы — конвертируем в ПОЛНЫЙ PDF (без обрезки)
            if (ext is ".doc" or ".docx" or ".ppt" or ".pptx")
            {
                var pdfPath = await _converter.ConvertToPdfAsync(fullPath);
                if (pdfPath == null || !System.IO.File.Exists(pdfPath))
                    return StatusCode(500, new { message = "Не удалось конвертировать файл для предпросмотра" });

                return PhysicalFile(pdfPath, "application/pdf", enableRangeProcessing: true);
            }

            return BadRequest(new { message = "Предпросмотр для этого формата не поддерживается", extension = ext });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Ошибка при предпросмотре файла", error = ex.Message });
        }
    }

    [HttpGet("ping")]
    public IActionResult Ping() => Ok("DownloadController works");

    private string GetContentType(string extension)
    {
        return extension?.ToLower() switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".zip" => "application/zip",
            ".rar" => "application/x-rar-compressed",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => "application/octet-stream"
        };
    }
}
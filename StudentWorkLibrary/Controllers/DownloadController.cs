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

    public DownloadController(
        IDbContextFactory<ApplicationDbContext> factory,
        IWebHostEnvironment env,
        IDocumentConverter converter)
    {
        _factory = factory;
        _env = env;
        _converter = converter;
    }

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

            // Счётчик увеличиваем до отдачи файла — если стриминг упадёт на середине,
            // пользователь всё равно «начал» скачивание.
            work.DownloadCount++;
            await context.SaveChangesAsync();

            var contentType = GetContentType(Path.GetExtension(work.FilePath));

            // PhysicalFile отдаёт файл потоком с диска, не загружая его в память целиком.
            // enableRangeProcessing: true — поддержка докачки (Range-запросы).
            return PhysicalFile(fullPath, contentType, work.OriginalFileName, enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Ошибка при скачивании файла", error = ex.Message });
        }
    }

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

            // PDF — отдаём как есть, браузер отобразит inline
            if (ext == ".pdf")
                return PhysicalFile(fullPath, "application/pdf", enableRangeProcessing: true);

            // Office-форматы — конвертируем в PDF через LibreOffice
            if (ext is ".doc" or ".docx" or ".ppt" or ".pptx")
            {
                var pdfPath = await _converter.ConvertToPdfAsync(fullPath);
                if (pdfPath == null)
                    return StatusCode(500, new { message = "Не удалось конвертировать файл для предпросмотра" });

                return PhysicalFile(pdfPath, "application/pdf", enableRangeProcessing: true);
            }

            // Остальные форматы (zip, rar, изображения) — предпросмотр не поддерживается
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
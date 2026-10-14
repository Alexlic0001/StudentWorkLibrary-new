using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentWorkLibrary.Data;

namespace StudentWorkLibrary.Controllers;

[ApiController]
[Route("api/download")]
public class DownloadController : ControllerBase
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;
    private readonly IWebHostEnvironment _env;

    public DownloadController(IDbContextFactory<ApplicationDbContext> factory, IWebHostEnvironment env)
    {
        _factory = factory;
        _env = env;
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

            work.DownloadCount++;
            await context.SaveChangesAsync();

            var fullPath = Path.Combine(_env.WebRootPath,
                work.FilePath.Replace('/', Path.DirectorySeparatorChar));

            if (!System.IO.File.Exists(fullPath))
                return NotFound(new { message = "Файл не найден на сервере", path = fullPath });

            var fileBytes = await System.IO.File.ReadAllBytesAsync(fullPath);
            var contentType = GetContentType(Path.GetExtension(work.FilePath));

            return File(fileBytes, contentType, work.OriginalFileName);
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

            var fileBytes = await System.IO.File.ReadAllBytesAsync(fullPath);
            var contentType = GetContentType(Path.GetExtension(work.FilePath));

            return File(fileBytes, contentType);
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
            ".zip" => "application/zip",
            ".rar" => "application/x-rar-compressed",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => "application/octet-stream"
        };
    }
}
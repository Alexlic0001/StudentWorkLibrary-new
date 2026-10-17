// StudentWorkLibrary\Services\WorkSearchService.cs
using Microsoft.EntityFrameworkCore;
using StudentWorkLibrary.Data;
using StudentWorkLibrary.Models;

namespace StudentWorkLibrary.Services
{
    public interface IWorkSearchService
    {
        Task<List<StudentWork>> SearchAsync(string query, int? workTypeId = null, int? institutionId = null, int? specialtyId = null, int? year = null);
        Task<List<StudentWork>> GetRecentWorksAsync(int count = 10);
        Task<Dictionary<string, int>> GetStatisticsAsync();
    }

    public class WorkSearchService : IWorkSearchService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _factory;

        public WorkSearchService(IDbContextFactory<ApplicationDbContext> factory)
            => _factory = factory;

        public async Task<List<StudentWork>> SearchAsync(string query, int? workTypeId = null, int? institutionId = null, int? specialtyId = null, int? year = null)
        {
            await using var context = await _factory.CreateDbContextAsync();

            var works = context.StudentWorks
                .Include(w => w.WorkType)
                .Include(w => w.Specialty)
                    .ThenInclude(s => s!.Institution)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(query))
            {
                works = works.Where(w =>
                    (w.Title != null && w.Title.Contains(query)) ||
                    (w.Author != null && w.Author.Contains(query)) ||
                    (w.Keywords != null && w.Keywords.Contains(query)) ||
                    (w.Abstract != null && w.Abstract.Contains(query)));
            }

            if (workTypeId.HasValue) works = works.Where(w => w.WorkTypeId == workTypeId.Value);
            if (specialtyId.HasValue) works = works.Where(w => w.SpecialtyId == specialtyId.Value);
            if (institutionId.HasValue) works = works.Where(w => w.Specialty!.InstitutionId == institutionId.Value);
            if (year.HasValue) works = works.Where(w => w.Year == year.Value);

            return await works
                .OrderByDescending(w => w.UploadDate)
                .Take(100)
                .ToListAsync();
        }

        public async Task<List<StudentWork>> GetRecentWorksAsync(int count = 10)
        {
            await using var context = await _factory.CreateDbContextAsync();
            return await context.StudentWorks
                .Include(w => w.WorkType)
                .Include(w => w.Specialty).ThenInclude(s => s!.Institution)
                .OrderByDescending(w => w.UploadDate)
                .Take(count)
                .ToListAsync();
        }

        public async Task<Dictionary<string, int>> GetStatisticsAsync()
        {
            await using var context = await _factory.CreateDbContextAsync();
            return new Dictionary<string, int>
            {
                ["TotalWorks"] = await context.StudentWorks.CountAsync(),
                ["TotalInstitutions"] = await context.Institutions.CountAsync(),
                ["TotalSpecialties"] = await context.Specialties.CountAsync(),
                ["TotalDownloads"] = await context.StudentWorks.SumAsync(w => w.DownloadCount)
            };
        }
    }
}
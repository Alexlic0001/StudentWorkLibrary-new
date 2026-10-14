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
        private readonly ApplicationDbContext _context;
        public WorkSearchService(ApplicationDbContext context) => _context = context;

        public async Task<List<StudentWork>> SearchAsync(string query, int? workTypeId = null, int? institutionId = null, int? specialtyId = null, int? year = null)
        {
            var works = _context.StudentWorks
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
            return await _context.StudentWorks
                .Include(w => w.WorkType)
                .Include(w => w.Specialty).ThenInclude(s => s!.Institution)
                .OrderByDescending(w => w.UploadDate)
                .Take(count)
                .ToListAsync();
        }

        public async Task<Dictionary<string, int>> GetStatisticsAsync()
        {
            return new Dictionary<string, int>
            {
                ["TotalWorks"] = await _context.StudentWorks.CountAsync(),
                ["TotalInstitutions"] = await _context.Institutions.CountAsync(),
                ["TotalSpecialties"] = await _context.Specialties.CountAsync(),
                ["TotalDownloads"] = await _context.StudentWorks.SumAsync(w => w.DownloadCount)
            };
        }
    }
}
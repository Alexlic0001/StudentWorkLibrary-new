using Microsoft.EntityFrameworkCore;
using StudentWorkLibrary.Data;
using StudentWorkLibrary.Models;

namespace StudentWorkLibrary.Services
{
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
        public bool HasPrevious => Page > 1;
        public bool HasNext => Page < TotalPages;
    }

    public interface IWorkSearchService
    {
        Task<PagedResult<StudentWork>> SearchAsync(
            string query,
            int? workTypeId = null,
            int? institutionId = null,
            int? specialtyId = null,
            int? year = null,
            int page = 1,
            int pageSize = 15,
            string sortBy = "UploadDate",
            bool sortAscending = false);

        Task<List<StudentWork>> GetRecentWorksAsync(int count = 10);
        Task<Dictionary<string, int>> GetStatisticsAsync();
    }

    public class WorkSearchService : IWorkSearchService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _factory;

        public WorkSearchService(IDbContextFactory<ApplicationDbContext> factory)
            => _factory = factory;

        public async Task<PagedResult<StudentWork>> SearchAsync(
            string query,
            int? workTypeId = null,
            int? institutionId = null,
            int? specialtyId = null,
            int? year = null,
            int page = 1,
            int pageSize = 15,
            string sortBy = "UploadDate",
            bool sortAscending = false)
        {
            await using var context = await _factory.CreateDbContextAsync();

            var works = context.StudentWorks
                .Include(w => w.WorkType)
                .Include(w => w.Specialty)
                .ThenInclude(s => s!.Institution)
                .AsQueryable();

            // ===== Фильтры =====
            if (!string.IsNullOrWhiteSpace(query))
            {
                var q = query.Trim().ToLower();
                works = works.Where(w =>
                    (w.Title != null && w.Title.ToLower().Contains(q)) ||
                    (w.Author != null && w.Author.ToLower().Contains(q)) ||
                    (w.Keywords != null && w.Keywords.ToLower().Contains(q)) ||
                    (w.Abstract != null && w.Abstract.ToLower().Contains(q)));
            }

            if (workTypeId.HasValue) works = works.Where(w => w.WorkTypeId == workTypeId.Value);
            if (specialtyId.HasValue) works = works.Where(w => w.SpecialtyId == specialtyId.Value);
            if (institutionId.HasValue) works = works.Where(w => w.Specialty!.InstitutionId == institutionId.Value);
            if (year.HasValue) works = works.Where(w => w.Year == year.Value);

            // ===== Сортировка =====
            works = sortBy switch
            {
                "Id" => sortAscending ? works.OrderBy(w => w.Id) : works.OrderByDescending(w => w.Id),
                "Title" => sortAscending ? works.OrderBy(w => w.Title) : works.OrderByDescending(w => w.Title),
                "Author" => sortAscending ? works.OrderBy(w => w.Author) : works.OrderByDescending(w => w.Author),
                "WorkType" => sortAscending ? works.OrderBy(w => w.WorkType!.Name) : works.OrderByDescending(w => w.WorkType!.Name),
                "Year" => sortAscending ? works.OrderBy(w => w.Year) : works.OrderByDescending(w => w.Year),
                "UploadDate" => sortAscending ? works.OrderBy(w => w.UploadDate) : works.OrderByDescending(w => w.UploadDate),
                "DownloadCount" => sortAscending ? works.OrderBy(w => w.DownloadCount) : works.OrderByDescending(w => w.DownloadCount),
                _ => works.OrderByDescending(w => w.UploadDate)
            };

            // ===== Пагинация =====
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 15;

            var totalCount = await works.CountAsync();

            var items = await works
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // ОТЛАДКА: выводим в консоль, сколько записей реально вернулось
            Console.WriteLine($"[DEBUG] SearchAsync: page={page}, pageSize={pageSize}, totalCount={totalCount}, items.Count={items.Count}");

            return new PagedResult<StudentWork>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
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
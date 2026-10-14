using System.ComponentModel.DataAnnotations;

namespace StudentWorkLibrary.Models
{
    public class StudentWork
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Укажите название работы")]
        public string Title { get; set; } = string.Empty;

        public string? Author { get; set; }
        public int? Year { get; set; }
        public string? Abstract { get; set; }
        public string? Keywords { get; set; }

        public string FilePath { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public int DownloadCount { get; set; }

        public DateTime UploadDate { get; set; } = DateTime.UtcNow;
        public DateTime? ModifiedDate { get; set; }

        [Required(ErrorMessage = "Выберите тип работы")]
        public int? WorkTypeId { get; set; }
        public WorkType? WorkType { get; set; }

        [Required(ErrorMessage = "Выберите специальность")]
        public int? SpecialtyId { get; set; }
        public Specialty? Specialty { get; set; }
    }
}
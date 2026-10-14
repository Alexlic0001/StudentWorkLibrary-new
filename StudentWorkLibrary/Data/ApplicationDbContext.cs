using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StudentWorkLibrary.Models;

namespace StudentWorkLibrary.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<StudentWork> StudentWorks { get; set; }
        public DbSet<WorkType> WorkTypes { get; set; }
        public DbSet<Institution> Institutions { get; set; }
        public DbSet<Specialty> Specialties { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Полнотекстовый поиск будет добавлен позже
            // modelBuilder.Entity<StudentWork>()
            //     .HasIndex(e => e.SearchVector)
            //     .HasMethod("FTS5");

            // Уникальные индексы
            modelBuilder.Entity<WorkType>()
                .HasIndex(e => e.Name)
                .IsUnique();

            modelBuilder.Entity<Institution>()
                .HasIndex(e => e.Name)
                .IsUnique();

            modelBuilder.Entity<Specialty>()
                .HasIndex(e => new { e.Code, e.InstitutionId })
                .IsUnique();

            // Каскадное удаление
            modelBuilder.Entity<Specialty>()
                .HasOne(s => s.Institution)
                .WithMany()
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StudentWork>()
                .HasOne(w => w.WorkType)
                .WithMany()
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
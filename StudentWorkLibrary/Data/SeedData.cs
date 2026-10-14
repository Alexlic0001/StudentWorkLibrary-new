using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudentWorkLibrary.Models;

namespace StudentWorkLibrary.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            using var context = new ApplicationDbContext(
                serviceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>());

            var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // Создаем базу данных, если её нет
            await context.Database.EnsureCreatedAsync();

            // Создаем роли
            string[] roles = { "Admin", "User" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // Создаем администратора
            var adminEmail = "admin@library.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new IdentityUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true
                };
                await userManager.CreateAsync(adminUser, "Admin123!");
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }

            // Добавляем типы работ, если их нет
            if (!context.WorkTypes.Any())
            {
                context.WorkTypes.AddRange(
                    new WorkType { Name = "Курсовая работа", Description = "Курсовой проект/работа" },
                    new WorkType { Name = "Дипломная работа", Description = "Бакалаврская работа" },
                    new WorkType { Name = "Магистерская диссертация", Description = "Магистерская работа" },
                    new WorkType { Name = "Реферат", Description = "Научный реферат" },
                    new WorkType { Name = "Отчет по практике", Description = "Отчет о прохождении практики" },
                    new WorkType { Name = "Научная статья", Description = "Статья для публикации" }
                );
                await context.SaveChangesAsync();
            }

            // Добавляем пример ВУЗа
            if (!context.Institutions.Any())
            {
                context.Institutions.AddRange(
                    new Institution { Name = "Московский Государственный Университет", City = "Москва" },
                    new Institution { Name = "Санкт-Петербургский Государственный Университет", City = "Санкт-Петербург" }
                );
                await context.SaveChangesAsync();
            }

            // Добавляем пример специальности
            if (!context.Specialties.Any())
            {
                var msu = await context.Institutions.FirstOrDefaultAsync(i => i.Name.Contains("Московский"));
                if (msu != null)
                {
                    context.Specialties.AddRange(
                        new Specialty { Name = "Программная инженерия", Code = "09.03.04", InstitutionId = msu.Id },
                        new Specialty { Name = "Прикладная математика", Code = "01.03.02", InstitutionId = msu.Id }
                    );
                    await context.SaveChangesAsync();
                }
            }
        }
    }
}
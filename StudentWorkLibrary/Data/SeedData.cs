using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudentWorkLibrary.Models;

namespace StudentWorkLibrary.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            var factory = serviceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await factory.CreateDbContextAsync();

            var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var logger = serviceProvider.GetRequiredService<ILoggerFactory>()
                                         .CreateLogger(nameof(SeedData));

            // Применяем миграции (создаёт БД, если её нет)
            await context.Database.MigrateAsync();

            // ---------- Роли ----------
            string[] roles = { "Admin", "User" };
            foreach (var role in roles)
            {
                if (await roleManager.RoleExistsAsync(role))
                    continue;

                var result = await roleManager.CreateAsync(new IdentityRole(role));
                if (!result.Succeeded)
                {
                    var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                    throw new InvalidOperationException($"Не удалось создать роль «{role}»: {errors}");
                }
                logger.LogInformation("Роль «{Role}» создана.", role);
            }

            // ---------- Администратор ----------
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

                var createResult = await userManager.CreateAsync(adminUser, "Admin123!");
                if (!createResult.Succeeded)
                {
                    var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
                    throw new InvalidOperationException($"Не удалось создать администратора: {errors}");
                }
                logger.LogInformation("Администратор {Email} создан.", adminEmail);
            }

            // Роль Admin назначаем идемпотентно — даже если пользователь уже был
            if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
            {
                var roleResult = await userManager.AddToRoleAsync(adminUser, "Admin");
                if (!roleResult.Succeeded)
                {
                    var errors = string.Join("; ", roleResult.Errors.Select(e => e.Description));
                    throw new InvalidOperationException($"Не удалось назначить роль Admin пользователю {adminEmail}: {errors}");
                }
                logger.LogInformation("Пользователю {Email} назначена роль Admin.", adminEmail);
            }

            // ---------- Типы работ ----------
            if (!await context.WorkTypes.AnyAsync())
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
                logger.LogInformation("Добавлены типы работ.");
            }

            // ---------- Вузы ----------
            if (!await context.Institutions.AnyAsync())
            {
                context.Institutions.AddRange(
                    new Institution { Name = "Московский Государственный Университет", City = "Москва" },
                    new Institution { Name = "Санкт-Петербургский Государственный Университет", City = "Санкт-Петербург" }
                );
                await context.SaveChangesAsync();
                logger.LogInformation("Добавлены вузы.");
            }

            // ---------- Специальности ----------
            if (!await context.Specialties.AnyAsync())
            {
                var msu = await context.Institutions.FirstOrDefaultAsync(i => i.Name.Contains("Московский"));
                if (msu != null)
                {
                    context.Specialties.AddRange(
                        new Specialty { Name = "Программная инженерия", Code = "09.03.04", InstitutionId = msu.Id },
                        new Specialty { Name = "Прикладная математика", Code = "01.03.02", InstitutionId = msu.Id }
                    );
                    await context.SaveChangesAsync();
                    logger.LogInformation("Добавлены специальности.");
                }
                else
                {
                    logger.LogWarning("Вуз «Московский...» не найден — специальности не добавлены.");
                }
            }
        }
    }
}
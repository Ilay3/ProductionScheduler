// File: Data/DatabaseInitializer.cs
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;

namespace ProductionScheduler.Data
{
    public static class DatabaseInitializer
    {
        public static void Initialize()
        {
            var dbPath = "productionscheduler.db";

            try
            {
                // Удаляем существующую базу данных
                if (File.Exists(dbPath))
                {
                    File.Delete(dbPath);
                }

                // Создаем новый контекст
                using var context = new ApplicationDbContext();

                // Создаем базу данных без миграций (для отладки)
                context.Database.EnsureCreated();

                // Альтернативно, можно использовать миграции:
                // context.Database.Migrate();

                // Проверяем создание таблиц
                var tables = context.Database.ExecuteSqlRaw(@"
                    SELECT name FROM sqlite_master 
                    WHERE type='table' AND name NOT LIKE 'sqlite_%'
                ");

                Console.WriteLine("База данных создана успешно");
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка создания базы данных: {ex.Message}", ex);
            }
        }

        public static void InitializeWithMigrations()
        {
            try
            {
                using var context = new ApplicationDbContext();

                // Применяем миграции
                context.Database.Migrate();

                Console.WriteLine("Миграции применены успешно");
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка применения миграций: {ex.Message}", ex);
            }
        }
    }
}
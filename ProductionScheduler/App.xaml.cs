// File: App.xaml.cs
using ProductionScheduler.Data;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System;

namespace ProductionScheduler
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // Путь к файлу базы данных
                string dbPath = "productionscheduler.db";
                bool isNewDatabase = !File.Exists(dbPath);

                System.Diagnostics.Debug.WriteLine($"Database exists: {!isNewDatabase}");

                // Инициализация базы данных
                using (var dbContext = new ApplicationDbContext())
                {
                    if (isNewDatabase)
                    {
                        // Создаем новую базу данных только если её нет
                        bool created = dbContext.Database.EnsureCreated();
                        System.Diagnostics.Debug.WriteLine($"Database created: {created}");

                        // Добавляем тестовые данные только в новую базу
                        TestDataSeeder.SeedTestData(dbContext);
                        System.Diagnostics.Debug.WriteLine("Test data seeded");

                        MessageBox.Show("База данных создана и заполнена тестовыми данными!", "Первый запуск",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        // Просто проверяем подключение к существующей базе
                        bool canConnect = dbContext.Database.CanConnect();
                        System.Diagnostics.Debug.WriteLine($"Can connect to existing database: {canConnect}");

                        if (!canConnect)
                        {
                            throw new Exception("Cannot connect to existing database");
                        }

                        System.Diagnostics.Debug.WriteLine("Using existing database");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Database initialization error: {ex}");
                MessageBox.Show($"Ошибка инициализации базы данных: {ex.Message}\n\nДетали: {ex.InnerException?.Message}",
                    "Критическая ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
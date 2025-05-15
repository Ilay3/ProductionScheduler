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

                // УДАЛЯЕМ СТАРУЮ БАЗУ ПОЛНОСТЬЮ (если существует)
                if (File.Exists(dbPath))
                {
                    File.Delete(dbPath);
                    System.Threading.Thread.Sleep(100); // Даем время на удаление файла
                }

                System.Diagnostics.Debug.WriteLine("Database file deleted, creating new one...");

                // Инициализация базы данных
                using (var dbContext = new ApplicationDbContext())
                {
                    // Простое создание базы данных без миграций
                    bool created = dbContext.Database.EnsureCreated();
                    System.Diagnostics.Debug.WriteLine($"Database created: {created}");

                    // Проверяем подключение
                    bool canConnect = dbContext.Database.CanConnect();
                    System.Diagnostics.Debug.WriteLine($"Can connect: {canConnect}");

                    if (!canConnect)
                    {
                        throw new Exception("Cannot connect to database after creation");
                    }

                    // Добавляем тестовые данные
                    TestDataSeeder.SeedTestData(dbContext);
                    System.Diagnostics.Debug.WriteLine("Test data seeded");
                }

                MessageBox.Show("База данных успешно создана и заполнена тестовыми данными!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Database initialization error: {ex}");
                MessageBox.Show($"Ошибка инициализации базы данных: {ex.Message}\n\nДетали: {ex.InnerException?.Message}",
                    "Критическая ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                // Не завершаем приложение, даем пользователю шанс
                // this.Shutdown();
                // return;
            }
        }
    }
}
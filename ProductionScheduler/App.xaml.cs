// File: App.xaml.cs
using ProductionScheduler.Data;
using System.Windows;

namespace ProductionScheduler
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Инициализация базы данных
            using (var dbContext = new ApplicationDbContext())
            {
                // Этот метод создаст базу данных и схему, если они еще не существуют.
                // Он не использует миграции, поэтому если вы измените модели позже,
                // вам, возможно, придется удалить файл БД или использовать миграции.
                dbContext.Database.EnsureCreated();
            }

            // Можно здесь же открыть главное окно или окно администратора
            // MainWindow mainWindow = new MainWindow(); // Пока оставим стандартное
            // mainWindow.Show();
        }
    }
}
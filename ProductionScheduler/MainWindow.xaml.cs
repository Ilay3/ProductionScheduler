using ProductionScheduler.Views;
using System.Windows;

namespace ProductionScheduler
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnOpenAdminPanel_Click(object sender, RoutedEventArgs e)
        {
            AdminWindow adminWindow = new AdminWindow();
            adminWindow.ShowDialog(); // Открываем как модальное окно
        }

        private void BtnOpenEmployeeForm_Click(object sender, RoutedEventArgs e)
        {
            EmployeeWindow employeeWindow = new EmployeeWindow();
            employeeWindow.Show(); // Или ShowDialog(), если нужно модальное
        }

    }
}
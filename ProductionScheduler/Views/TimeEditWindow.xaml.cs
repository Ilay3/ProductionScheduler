// File: Views/TimeEditWindow.xaml.cs
using ProductionScheduler.ViewModels;
using System.Windows;

namespace ProductionScheduler.Views
{
    public partial class TimeEditWindow : Window
    {
        public TimeEditWindow(TimeEditViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.RequestClose += (success) =>
            {
                DialogResult = success;
                Close();
            };
        }
    }
}
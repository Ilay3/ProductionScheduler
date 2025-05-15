// File: Views/SplitOperationWindow.xaml.cs
using ProductionScheduler.ViewModels;
using System.Windows;

namespace ProductionScheduler.Views
{
    public partial class SplitOperationWindow : Window
    {
        public SplitOperationWindow(SplitOperationViewModel viewModel)
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
// File: Views/EmployeeWindow.xaml.cs
using System.Windows;
using System.Windows.Controls;
using ProductionScheduler.ViewModels;
using ProductionScheduler.Views.Controls;

namespace ProductionScheduler.Views
{
    public partial class EmployeeWindow : Window
    {
        private EmployeeWorkViewModel ViewModel => DataContext as EmployeeWorkViewModel;

        public EmployeeWindow()
        {
            InitializeComponent();
            // DataContext устанавливается в XAML
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            ProductionGanttChart.ZoomIn();
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            ProductionGanttChart.ZoomOut();
        }

        private void GanttChart_TaskClicked(object sender, TaskEventArgs e)
        {
            // Выбираем задание в ViewModel
            if (e.TaskViewModel.Task != null && ViewModel != null)
            {
                ViewModel.SelectedTask = e.TaskViewModel.Task;
            }
        }

        private void GanttChart_TaskDoubleClicked(object sender, TaskEventArgs e)
        {
            // При двойном клике открываем детали задания
            if (e.TaskViewModel.Task != null && ViewModel != null)
            {
                ViewModel.SelectedTask = e.TaskViewModel.Task;

                // Переключаемся на вкладку с таблицами
                var tabControl = this.FindName("MainTabControl") as TabControl;
                if (tabControl != null && tabControl.Items.Count > 0)
                {
                    tabControl.SelectedIndex = 0; // Переключаемся на первую вкладку
                }
            }
        }

        private void GanttChart_TaskContextMenuOpening(object sender, TaskEventArgs e)
        {
            // Обрабатываем контекстное меню диаграммы
            if (ViewModel == null) return;

            switch (e.Action)
            {
                case "Start":
                    if (e.TaskViewModel.Type == GanttItemType.Task)
                    {
                        ViewModel.StartTaskCommand.Execute(null);
                    }
                    else if (e.TaskViewModel.Type == GanttItemType.Stage)
                    {
                        var stageVM = ViewModel.SelectedTaskStages
                            .FirstOrDefault(s => s.TaskStage.Id == e.TaskViewModel.TaskStage.Id);
                        if (stageVM != null)
                        {
                            ViewModel.ExecuteStartStage(stageVM);
                        }
                    }
                    break;

                case "Pause":
                    if (e.TaskViewModel.Type == GanttItemType.Task)
                    {
                        ViewModel.PauseTaskCommand.Execute(null);
                    }
                    else if (e.TaskViewModel.Type == GanttItemType.Stage)
                    {
                        var stageVM = ViewModel.SelectedTaskStages
                            .FirstOrDefault(s => s.TaskStage.Id == e.TaskViewModel.TaskStage.Id);
                        if (stageVM != null)
                        {
                            ViewModel.ExecutePauseStage(stageVM);
                        }
                    }
                    break;

                case "Complete":
                    if (e.TaskViewModel.Type == GanttItemType.Task)
                    {
                        ViewModel.CompleteTaskCommand.Execute(null);
                    }
                    else if (e.TaskViewModel.Type == GanttItemType.Stage)
                    {
                        var stageVM = ViewModel.SelectedTaskStages
                            .FirstOrDefault(s => s.TaskStage.Id == e.TaskViewModel.TaskStage.Id);
                        if (stageVM != null)
                        {
                            ViewModel.ExecuteCompleteStage(stageVM);
                        }
                    }
                    break;

                case "Split":
                    if (e.TaskViewModel.Type == GanttItemType.Stage)
                    {
                        var stageVM = ViewModel.SelectedTaskStages
                            .FirstOrDefault(s => s.TaskStage.Id == e.TaskViewModel.TaskStage.Id);
                        if (stageVM != null)
                        {
                            ViewModel.ExecuteSplitStage(stageVM);
                        }
                    }
                    break;

                case "ShowDetails":
                    // Переключаемся на вкладку с деталями
                    var tabControl = this.FindName("MainTabControl") as TabControl;
                    if (tabControl != null && tabControl.Items.Count > 0)
                    {
                        tabControl.SelectedIndex = 0;
                    }
                    break;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Обновляем масштаб времени при изменении ComboBox
            TimeScaleComboBox.SelectionChanged += (s, args) =>
            {
                if (ProductionGanttChart != null)
                {
                    ProductionGanttChart.TimeScale = (TimeScale)TimeScaleComboBox.SelectedIndex;
                }
            };
        }
    }
}
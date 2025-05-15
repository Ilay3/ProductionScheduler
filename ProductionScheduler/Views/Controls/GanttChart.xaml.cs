// File: Views/Controls/GanttChart.xaml.cs (полная реализация)
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ProductionScheduler.Models;
using TaskStatus = ProductionScheduler.Models.TaskStatus;

namespace ProductionScheduler.Views.Controls
{
    public partial class GanttChart : UserControl
    {
        #region Constants
        private const double PIXELS_PER_HOUR = 60;
        private const double ROW_HEIGHT = 40;
        private const double HOURS_PER_DAY = 24;
        private const double MIN_TASK_WIDTH = 10;
        #endregion

        #region Private Fields
        private DateTime _startDate;
        private DateTime _endDate;
        private double _totalDuration;
        private ObservableCollection<GanttTaskViewModel> _ganttItems;
        private ObservableCollection<GanttTaskLabelViewModel> _taskLabels;
        private GanttTaskViewModel _selectedTask;
        private bool _isDragging = false;
        private Point _dragStartPoint;
        #endregion

        #region Dependency Properties
        public static readonly DependencyProperty TasksProperty = DependencyProperty.Register(
            "Tasks", typeof(ObservableCollection<ProductionTask>), typeof(GanttChart),
            new PropertyMetadata(null, OnTasksChanged));

        public static readonly DependencyProperty ShowCurrentTimeLineProperty = DependencyProperty.Register(
            "ShowCurrentTimeLine", typeof(bool), typeof(GanttChart),
            new PropertyMetadata(true, OnShowCurrentTimeLineChanged));

        public static readonly DependencyProperty TimeScaleProperty = DependencyProperty.Register(
            "TimeScale", typeof(TimeScale), typeof(GanttChart),
            new PropertyMetadata(TimeScale.Hours, OnTimeScaleChanged));

        public ObservableCollection<ProductionTask> Tasks
        {
            get => (ObservableCollection<ProductionTask>)GetValue(TasksProperty);
            set => SetValue(TasksProperty, value);
        }

        public bool ShowCurrentTimeLine
        {
            get => (bool)GetValue(ShowCurrentTimeLineProperty);
            set => SetValue(ShowCurrentTimeLineProperty, value);
        }

        public TimeScale TimeScale
        {
            get => (TimeScale)GetValue(TimeScaleProperty);
            set => SetValue(TimeScaleProperty, value);
        }
        #endregion

        #region Events
        public event EventHandler<TaskEventArgs> TaskClicked;
        public event EventHandler<TaskEventArgs> TaskDoubleClicked;
        public event EventHandler<TaskDragEventArgs> TaskMoved;
        public event EventHandler<TaskEventArgs> TaskContextMenuOpening;
        #endregion

        #region Constructor
        public GanttChart()
        {
            InitializeComponent();
            _ganttItems = new ObservableCollection<GanttTaskViewModel>();
            _taskLabels = new ObservableCollection<GanttTaskLabelViewModel>();

            GanttItemsControl.ItemsSource = _ganttItems;
            TaskLabelsItemsControl.ItemsSource = _taskLabels;

            Loaded += GanttChart_Loaded;
        }
        #endregion

        #region Event Handlers
        private void GanttChart_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateGanttChart();
            if (ShowCurrentTimeLine)
            {
                UpdateCurrentTimeLine();
            }
        }

        private static void OnTasksChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GanttChart gantt)
            {
                if (e.OldValue is ObservableCollection<ProductionTask> oldTasks)
                {
                    oldTasks.CollectionChanged -= gantt.Tasks_CollectionChanged;
                }

                if (e.NewValue is ObservableCollection<ProductionTask> newTasks)
                {
                    newTasks.CollectionChanged += gantt.Tasks_CollectionChanged;
                }

                gantt.UpdateGanttChart();
            }
        }

        private static void OnShowCurrentTimeLineChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GanttChart gantt)
            {
                gantt.CurrentTimeLine.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
                if ((bool)e.NewValue)
                {
                    gantt.UpdateCurrentTimeLine();
                }
            }
        }

        private static void OnTimeScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GanttChart gantt)
            {
                gantt.UpdateGanttChart();
            }
        }

        private void Tasks_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateGanttChart();
        }

        private void MainScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // Синхронизация скроллинга между областями
            TimeAxisScrollViewer.ScrollToHorizontalOffset(e.HorizontalOffset);
            TaskLabelsScrollViewer.ScrollToVerticalOffset(e.VerticalOffset);
        }

        private void TaskBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var taskBar = sender as Border;
            var taskViewModel = taskBar?.DataContext as GanttTaskViewModel;

            if (taskViewModel != null)
            {
                _selectedTask = taskViewModel;
                TaskClicked?.Invoke(this, new TaskEventArgs(taskViewModel));

                if (e.ClickCount == 2)
                {
                    TaskDoubleClicked?.Invoke(this, new TaskEventArgs(taskViewModel));
                }
                else
                {
                    // Начинаем перетаскивание
                    _isDragging = true;
                    _dragStartPoint = e.GetPosition(GanttItemsControl);
                    taskBar.CaptureMouse();
                }
            }
        }

        private void TaskBar_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var taskBar = sender as Border;
            var taskViewModel = taskBar?.DataContext as GanttTaskViewModel;

            if (taskViewModel != null)
            {
                _selectedTask = taskViewModel;
                TaskContextMenuOpening?.Invoke(this, new TaskEventArgs(taskViewModel));
                TaskContextMenu.PlacementTarget = taskBar;
                TaskContextMenu.IsOpen = true;
            }
        }
        #endregion

        #region Public Methods
        public void RefreshChart()
        {
            UpdateGanttChart();
        }

        public void ZoomIn()
        {
            TimeScale = TimeScale switch
            {
                TimeScale.Days => TimeScale.Hours,
                TimeScale.Hours => TimeScale.Minutes,
                _ => TimeScale
            };
        }

        public void ZoomOut()
        {
            TimeScale = TimeScale switch
            {
                TimeScale.Minutes => TimeScale.Hours,
                TimeScale.Hours => TimeScale.Days,
                _ => TimeScale
            };
        }

        public void ScrollToTask(ProductionTask task)
        {
            var taskViewModel = _ganttItems.FirstOrDefault(gi => gi.Task == task);
            if (taskViewModel != null)
            {
                MainScrollViewer.ScrollToHorizontalOffset(taskViewModel.XPosition - 50);
                MainScrollViewer.ScrollToVerticalOffset(taskViewModel.YPosition - 50);
            }
        }
        #endregion

        #region Private Methods
        private void UpdateGanttChart()
        {
            if (Tasks == null || !Tasks.Any())
            {
                _ganttItems.Clear();
                _taskLabels.Clear();
                return;
            }

            CalculateTimeRange();
            CreateGridLines();
            CreateTimeAxis();
            CreateTaskItems();
            CreateTaskLabels();
            UpdateCurrentTimeLine();
        }

        private void CalculateTimeRange()
        {
            var allTimes = new List<DateTime>();

            // Собираем все времена из заданий и этапов
            foreach (var task in Tasks)
            {
                if (task.PlannedStartTime.HasValue)
                    allTimes.Add(task.PlannedStartTime.Value);
                if (task.PlannedEndTime.HasValue)
                    allTimes.Add(task.PlannedEndTime.Value);
                if (task.ActualStartTime.HasValue)
                    allTimes.Add(task.ActualStartTime.Value);
                if (task.ActualEndTime.HasValue)
                    allTimes.Add(task.ActualEndTime.Value);

                if (task.TaskStages != null)
                {
                    foreach (var stage in task.TaskStages)
                    {
                        if (stage.PlannedStartTime.HasValue)
                            allTimes.Add(stage.PlannedStartTime.Value);
                        if (stage.PlannedEndTime.HasValue)
                            allTimes.Add(stage.PlannedEndTime.Value);
                        if (stage.ActualStartTime.HasValue)
                            allTimes.Add(stage.ActualStartTime.Value);
                        if (stage.ActualEndTime.HasValue)
                            allTimes.Add(stage.ActualEndTime.Value);
                    }
                }
            }

            if (allTimes.Any())
            {
                _startDate = allTimes.Min();
                _endDate = allTimes.Max();
            }
            else
            {
                _startDate = DateTime.Today;
                _endDate = DateTime.Today.AddDays(1);
            }

            // Добавляем отступы
            var padding = TimeSpan.FromHours(2);
            _startDate = _startDate.Add(-padding);
            _endDate = _endDate.Add(padding);

            _totalDuration = (_endDate - _startDate).TotalHours;
        }

        private void CreateGridLines()
        {
            GridLinesCanvas.Children.Clear();

            var canvasWidth = _totalDuration * PIXELS_PER_HOUR;
            var canvasHeight = Tasks.Count * ROW_HEIGHT;

            GridLinesCanvas.Width = canvasWidth;
            GridLinesCanvas.Height = canvasHeight;

            // Горизонтальные линии (между заданиями)
            for (int i = 0; i <= Tasks.Count; i++)
            {
                var y = i * ROW_HEIGHT;
                var line = new Line
                {
                    X1 = 0,
                    Y1 = y,
                    X2 = canvasWidth,
                    Y2 = y,
                    Style = this.FindResource("GridLineStyle") as Style
                };
                GridLinesCanvas.Children.Add(line);
            }

            // Вертикальные линии (временная сетка)
            var timeIncrement = GetTimeIncrement();
            var current = _startDate;

            while (current <= _endDate)
            {
                var x = (current - _startDate).TotalHours * PIXELS_PER_HOUR;
                var isMajorLine = IsMajorGridLine(current);

                var line = new Line
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = canvasHeight,
                    Style = this.FindResource(isMajorLine ? "MajorGridLineStyle" : "GridLineStyle") as Style
                };
                GridLinesCanvas.Children.Add(line);

                current = current.Add(timeIncrement);
            }
        }

        private void CreateTimeAxis()
        {
            TimeAxisCanvas.Children.Clear();

            var canvasWidth = _totalDuration * PIXELS_PER_HOUR;
            TimeAxisCanvas.Width = canvasWidth;

            var timeIncrement = GetTimeIncrement();
            var labelFormat = GetTimeLabelFormat();
            var current = _startDate;

            while (current <= _endDate)
            {
                var x = (current - _startDate).TotalHours * PIXELS_PER_HOUR;

                if (ShouldShowTimeLabel(current))
                {
                    var textBlock = new TextBlock
                    {
                        Text = current.ToString(labelFormat),
                        Style = this.FindResource("TimeAxisStyle") as Style
                    };

                    Canvas.SetLeft(textBlock, x + 5);
                    Canvas.SetTop(textBlock, 5);
                    TimeAxisCanvas.Children.Add(textBlock);
                }

                current = current.Add(timeIncrement);
            }
        }

        private void CreateTaskItems()
        {
            _ganttItems.Clear();

            for (int taskIndex = 0; taskIndex < Tasks.Count; taskIndex++)
            {
                var task = Tasks[taskIndex];
                CreateTaskMainBar(task, taskIndex);
                CreateTaskStages(task, taskIndex);
            }
        }

        private void CreateTaskMainBar(ProductionTask task, int taskIndex)
        {
            if (!task.PlannedStartTime.HasValue || !task.PlannedEndTime.HasValue)
                return;

            var startX = CalculateXPosition(task.PlannedStartTime.Value);
            var endX = CalculateXPosition(task.PlannedEndTime.Value);
            var width = Math.Max(endX - startX, MIN_TASK_WIDTH);
            var y = taskIndex * ROW_HEIGHT + 2;

            var taskViewModel = new GanttTaskViewModel
            {
                Task = task,
                TaskStage = null,
                Type = GanttItemType.Task,
                XPosition = startX,
                YPosition = y,
                BarWidth = width,
                DisplayText = $"{task.Detail?.Name} ({task.Quantity})",
                StatusBrush = GetStatusBrush(task.Status),
                TextBrush = GetTextBrush(task.Status),
                ToolTipText = CreateTaskToolTip(task)
            };

            _ganttItems.Add(taskViewModel);
        }

        private void CreateTaskStages(ProductionTask task, int taskIndex)
        {
            if (task.TaskStages == null || !task.TaskStages.Any())
                return;

            var stageHeight = 15;
            var stageOffsetY = 20;

            foreach (var stage in task.TaskStages.OrderBy(s => s.OrderInTask))
            {
                if (!stage.PlannedStartTime.HasValue || !stage.PlannedEndTime.HasValue)
                    continue;

                var startX = CalculateXPosition(stage.PlannedStartTime.Value);
                var endX = CalculateXPosition(stage.PlannedEndTime.Value);
                var width = Math.Max(endX - startX, MIN_TASK_WIDTH);
                var y = taskIndex * ROW_HEIGHT + stageOffsetY;

                var stageViewModel = new GanttTaskViewModel
                {
                    Task = task,
                    TaskStage = stage,
                    Type = GanttItemType.Stage,
                    XPosition = startX,
                    YPosition = y,
                    BarWidth = width,
                    DisplayText = stage.RouteStage?.OperationName ?? $"Этап {stage.OrderInTask}",
                    StatusBrush = GetStatusBrush(stage.Status),
                    TextBrush = GetTextBrush(stage.Status),
                    ToolTipText = CreateStageToolTip(stage)
                };

                _ganttItems.Add(stageViewModel);
            }
        }

        private void CreateTaskLabels()
        {
            _taskLabels.Clear();

            for (int i = 0; i < Tasks.Count; i++)
            {
                var task = Tasks[i];
                var labelViewModel = new GanttTaskLabelViewModel
                {
                    Task = task,
                    TaskName = $"Задание #{task.Id}",
                    DetailInfo = $"{task.Detail?.Name} - {task.Quantity} шт."
                };
                _taskLabels.Add(labelViewModel);
            }
        }

        private void UpdateCurrentTimeLine()
        {
            if (!ShowCurrentTimeLine)
                return;

            var currentTime = DateTime.Now;
            if (currentTime >= _startDate && currentTime <= _endDate)
            {
                var x = CalculateXPosition(currentTime);
                CurrentTimeLine.X1 = x;
                CurrentTimeLine.X2 = x;
                CurrentTimeLine.Visibility = Visibility.Visible;
            }
            else
            {
                CurrentTimeLine.Visibility = Visibility.Collapsed;
            }
        }

        private double CalculateXPosition(DateTime time)
        {
            return (time - _startDate).TotalHours * PIXELS_PER_HOUR;
        }

        private TimeSpan GetTimeIncrement()
        {
            return TimeScale switch
            {
                TimeScale.Minutes => TimeSpan.FromMinutes(30),
                TimeScale.Hours => TimeSpan.FromHours(1),
                TimeScale.Days => TimeSpan.FromHours(6),
                _ => TimeSpan.FromHours(1)
            };
        }

        private bool IsMajorGridLine(DateTime time)
        {
            return TimeScale switch
            {
                TimeScale.Minutes => time.Minute == 0,
                TimeScale.Hours => time.Hour % 6 == 0,
                TimeScale.Days => time.Hour == 0,
                _ => false
            };
        }

        private bool ShouldShowTimeLabel(DateTime time)
        {
            return TimeScale switch
            {
                TimeScale.Minutes => time.Minute % 60 == 0,
                TimeScale.Hours => time.Minute == 0,
                TimeScale.Days => time.Hour % 6 == 0 && time.Minute == 0,
                _ => time.Minute == 0
            };
        }

        private string GetTimeLabelFormat()
        {
            return TimeScale switch
            {
                TimeScale.Minutes => "HH:mm",
                TimeScale.Hours => "dd.MM HH:mm",
                TimeScale.Days => "dd.MM",
                _ => "dd.MM HH:mm"
            };
        }

        private Brush GetStatusBrush(TaskStatus status)
        {
            return status switch
            {
                TaskStatus.Planned => new SolidColorBrush(Colors.LightBlue),
                TaskStatus.InProgress => new SolidColorBrush(Colors.Orange),
                TaskStatus.Completed => new SolidColorBrush(Colors.LightGreen),
                TaskStatus.Paused => new SolidColorBrush(Colors.Yellow),
                TaskStatus.Cancelled => new SolidColorBrush(Colors.LightCoral),
                _ => new SolidColorBrush(Colors.LightGray)
            };
        }

        private Brush GetTextBrush(TaskStatus status)
        {
            return status switch
            {
                TaskStatus.Planned => new SolidColorBrush(Colors.DarkBlue),
                TaskStatus.InProgress => new SolidColorBrush(Colors.DarkOrange),
                TaskStatus.Completed => new SolidColorBrush(Colors.DarkGreen),
                TaskStatus.Paused => new SolidColorBrush(Colors.DarkGoldenrod),
                TaskStatus.Cancelled => new SolidColorBrush(Colors.DarkRed),
                _ => new SolidColorBrush(Colors.Black)
            };
        }

        private string CreateTaskToolTip(ProductionTask task)
        {
            var tooltip = $"Задание: {task.Detail?.Name}\n";
            tooltip += $"Количество: {task.Quantity}\n";
            tooltip += $"Статус: {GetStatusDisplayName(task.Status)}\n\n";

            if (task.PlannedStartTime.HasValue && task.PlannedEndTime.HasValue)
            {
                tooltip += $"Плановое время:\n";
                tooltip += $"  Начало: {task.PlannedStartTime:dd.MM.yy HH:mm}\n";
                tooltip += $"  Окончание: {task.PlannedEndTime:dd.MM.yy HH:mm}\n";
                tooltip += $"  Длительность: {task.PlannedEndTime - task.PlannedStartTime:hh\\:mm}\n\n";
            }

            if (task.ActualStartTime.HasValue)
            {
                tooltip += $"Фактическое время:\n";
                tooltip += $"  Начало: {task.ActualStartTime:dd.MM.yy HH:mm}\n";
                if (task.ActualEndTime.HasValue)
                {
                    tooltip += $"  Окончание: {task.ActualEndTime:dd.MM.yy HH:mm}\n";
                    tooltip += $"  Длительность: {task.ActualEndTime - task.ActualStartTime:hh\\:mm}\n";
                }
            }

            return tooltip.TrimEnd();
        }

        private string CreateStageToolTip(ProductionTaskStage stage)
        {
            var tooltip = $"Этап: {stage.RouteStage?.OperationName}\n";
            tooltip += $"Станок: {stage.AssignedMachine?.Name}\n";
            tooltip += $"Количество: {stage.QuantityToProcess}\n";
            tooltip += $"Статус: {GetStatusDisplayName(stage.Status)}\n\n";

            if (stage.PlannedStartTime.HasValue && stage.PlannedEndTime.HasValue)
            {
                tooltip += $"Плановое время:\n";
                tooltip += $"  Начало: {stage.PlannedStartTime:dd.MM.yy HH:mm}\n";
                tooltip += $"  Окончание: {stage.PlannedEndTime:dd.MM.yy HH:mm}\n";
                tooltip += $"  Длительность: {stage.PlannedDuration:hh\\:mm}\n\n";
            }

            if (stage.ActualStartTime.HasValue)
            {
                tooltip += $"Фактическое время:\n";
                tooltip += $"  Начало: {stage.ActualStartTime:dd.MM.yy HH:mm}\n";
                if (stage.ActualEndTime.HasValue)
                {
                    tooltip += $"  Окончание: {stage.ActualEndTime:dd.MM.yy HH:mm}\n";
                    if (stage.ActualDuration.HasValue)
                        tooltip += $"  Длительность: {stage.ActualDuration:hh\\:mm}\n";
                }
            }

            return tooltip.TrimEnd();
        }

        private string GetStatusDisplayName(TaskStatus status)
        {
            return status switch
            {
                TaskStatus.Planned => "Запланировано",
                TaskStatus.InProgress => "В работе",
                TaskStatus.Completed => "Завершено",
                TaskStatus.Paused => "Приостановлено",
                TaskStatus.Cancelled => "Отменено",
                _ => status.ToString()
            };
        }
        #endregion

        #region Context Menu Handlers
        private void StartTask_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTask?.Task != null)
            {
                TaskContextMenuOpening?.Invoke(this, new TaskEventArgs(_selectedTask, "Start"));
            }
        }

        private void PauseTask_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTask?.Task != null)
            {
                TaskContextMenuOpening?.Invoke(this, new TaskEventArgs(_selectedTask, "Pause"));
            }
        }

        private void CompleteTask_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTask?.Task != null)
            {
                TaskContextMenuOpening?.Invoke(this, new TaskEventArgs(_selectedTask, "Complete"));
            }
        }

        private void SplitOperation_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTask?.TaskStage != null)
            {
                TaskContextMenuOpening?.Invoke(this, new TaskEventArgs(_selectedTask, "Split"));
            }
        }

        private void ChangeTime_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTask != null)
            {
                TaskContextMenuOpening?.Invoke(this, new TaskEventArgs(_selectedTask, "ChangeTime"));
            }
        }

        private void ShowDetails_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTask != null)
            {
                TaskContextMenuOpening?.Invoke(this, new TaskEventArgs(_selectedTask, "ShowDetails"));
            }
        }
        #endregion
    }

    #region Enums and Supporting Classes
    public enum TimeScale
    {
        Minutes,
        Hours,
        Days
    }

    public enum GanttItemType
    {
        Task,
        Stage
    }

    public class GanttTaskViewModel
    {
        public ProductionTask Task { get; set; }
        public ProductionTaskStage TaskStage { get; set; }
        public GanttItemType Type { get; set; }
        public double XPosition { get; set; }
        public double YPosition { get; set; }
        public double BarWidth { get; set; }
        public string DisplayText { get; set; }
        public Brush StatusBrush { get; set; }
        public Brush TextBrush { get; set; }
        public string ToolTipText { get; set; }
    }

    public class GanttTaskLabelViewModel
    {
        public ProductionTask Task { get; set; }
        public string TaskName { get; set; }
        public string DetailInfo { get; set; }
    }

    public class TaskEventArgs : EventArgs
    {
        public GanttTaskViewModel TaskViewModel { get; }
        public string Action { get; }

        public TaskEventArgs(GanttTaskViewModel taskViewModel, string action = null)
        {
            TaskViewModel = taskViewModel;
            Action = action;
        }
    }

    public class TaskDragEventArgs : EventArgs
    {
        public GanttTaskViewModel TaskViewModel { get; }
        public DateTime NewStartTime { get; }
        public DateTime NewEndTime { get; }

        public TaskDragEventArgs(GanttTaskViewModel taskViewModel, DateTime newStartTime, DateTime newEndTime)
        {
            TaskViewModel = taskViewModel;
            NewStartTime = newStartTime;
            NewEndTime = newEndTime;
        }
    }
    #endregion
}
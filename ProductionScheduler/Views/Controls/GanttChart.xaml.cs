// File: Views/Controls/GanttChart.xaml.cs
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ProductionScheduler.Models;
using TaskStatus = ProductionScheduler.Models.TaskStatus;

namespace ProductionScheduler.Views.Controls
{
    public partial class GanttChart : UserControl
    {
        private const double PIXELS_PER_HOUR = 50; // Масштаб: 50 пикселей = 1 час
        private const double ROW_HEIGHT = 40; // Высота строки для каждого задания
        private DateTime _startDate;
        private DateTime _endDate;
        private ObservableCollection<GanttTaskViewModel> _tasks;

        public static readonly DependencyProperty TasksProperty = DependencyProperty.Register(
            "Tasks", typeof(ObservableCollection<ProductionTask>), typeof(GanttChart),
            new PropertyMetadata(null, OnTasksChanged));

        public ObservableCollection<ProductionTask> Tasks
        {
            get => (ObservableCollection<ProductionTask>)GetValue(TasksProperty);
            set => SetValue(TasksProperty, value);
        }

        public GanttChart()
        {
            InitializeComponent();
            _tasks = new ObservableCollection<GanttTaskViewModel>();
            TasksItemsControl.ItemsSource = _tasks;
        }

        private static void OnTasksChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GanttChart gantt)
            {
                gantt.UpdateGanttChart();
            }
        }

        private void UpdateGanttChart()
        {
            if (Tasks == null || !Tasks.Any())
            {
                _tasks.Clear();
                return;
            }

            CalculateTimeRange();
            CreateTimeScale();
            CreateTaskBars();
        }

        private void CalculateTimeRange()
        {
            var startTimes = Tasks.Where(t => t.PlannedStartTime.HasValue).Select(t => t.PlannedStartTime.Value);
            var endTimes = Tasks.Where(t => t.PlannedEndTime.HasValue).Select(t => t.PlannedEndTime.Value);

            if (startTimes.Any() && endTimes.Any())
            {
                _startDate = startTimes.Min().Date;
                _endDate = endTimes.Max().Date.AddDays(1);
            }
            else
            {
                _startDate = DateTime.Today;
                _endDate = DateTime.Today.AddDays(1);
            }

            // Добавляем немного отступа
            _startDate = _startDate.AddHours(-2);
            _endDate = _endDate.AddHours(2);
        }

        private void CreateTimeScale()
        {
            TimeScaleCanvas.Children.Clear();

            var totalHours = (_endDate - _startDate).TotalHours;
            var canvasWidth = totalHours * PIXELS_PER_HOUR;
            TimeScaleCanvas.Width = canvasWidth;

            // Создаем временные метки каждый час
            for (var current = _startDate; current <= _endDate; current = current.AddHours(1))
            {
                var x = (current - _startDate).TotalHours * PIXELS_PER_HOUR;

                // Вертикальная линия
                var line = new Line
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = 60,
                    Stroke = Brushes.Gray,
                    StrokeThickness = current.Hour == 0 ? 2 : 1
                };
                TimeScaleCanvas.Children.Add(line);

                // Подпись времени
                if (current.Hour % 2 == 0) // Показываем каждые 2 часа
                {
                    var textBlock = new TextBlock
                    {
                        Text = current.ToString("dd.MM HH:mm"),
                        FontSize = 10,
                        Foreground = Brushes.Black
                    };
                    Canvas.SetLeft(textBlock, x + 5);
                    Canvas.SetTop(textBlock, 40);
                    TimeScaleCanvas.Children.Add(textBlock);
                }
            }
        }

        private void CreateTaskBars()
        {
            _tasks.Clear();

            for (int i = 0; i < Tasks.Count; i++)
            {
                var task = Tasks[i];
                if (!task.PlannedStartTime.HasValue || !task.PlannedEndTime.HasValue)
                    continue;

                CreateTaskBar(task, i);
                CreateTaskStages(task, i);
            }
        }

        private void CreateTaskBar(ProductionTask task, int rowIndex)
        {
            var startX = (task.PlannedStartTime.Value - _startDate).TotalHours * PIXELS_PER_HOUR;
            var duration = task.PlannedEndTime.Value - task.PlannedStartTime.Value;
            var width = duration.TotalHours * PIXELS_PER_HOUR;

            var taskViewModel = new GanttTaskViewModel
            {
                XPosition = startX,
                YPosition = rowIndex * ROW_HEIGHT,
                BarWidth = width,
                DisplayText = $"{task.Detail?.Name} ({task.Quantity})",
                StatusBrush = GetStatusBrush(task.Status),
                ToolTipText = $"Задание: {task.Detail?.Name}\nКоличество: {task.Quantity}\nПлан: {task.PlannedStartTime:dd.MM HH:mm} - {task.PlannedEndTime:dd.MM HH:mm}\nСтатус: {task.Status}"
            };

            _tasks.Add(taskViewModel);
        }

        private void CreateTaskStages(ProductionTask task, int rowIndex)
        {
            if (task.TaskStages == null) return;

            double stageOffset = 5; // Смещение этапов от основной полосы задания
            double stageHeight = 20; // Высота полосы этапа

            foreach (var stage in task.TaskStages)
            {
                if (!stage.PlannedStartTime.HasValue || !stage.PlannedEndTime.HasValue)
                    continue;

                var startX = (stage.PlannedStartTime.Value - _startDate).TotalHours * PIXELS_PER_HOUR;
                var duration = stage.PlannedEndTime.Value - stage.PlannedStartTime.Value;
                var width = duration.TotalHours * PIXELS_PER_HOUR;

                var stageViewModel = new GanttTaskViewModel
                {
                    XPosition = startX,
                    YPosition = rowIndex * ROW_HEIGHT + stageOffset,
                    BarWidth = width,
                    DisplayText = stage.RouteStage?.OperationName ?? "N/A",
                    StatusBrush = GetStatusBrush(stage.Status),
                    ToolTipText = $"Этап: {stage.RouteStage?.OperationName}\nСтанок: {stage.AssignedMachine?.Name}\nПлан: {stage.PlannedStartTime:dd.MM HH:mm} - {stage.PlannedEndTime:dd.MM HH:mm}\nСтатус: {stage.Status}"
                };

                _tasks.Add(stageViewModel);
            }
        }

        private Brush GetStatusBrush(TaskStatus status)
        {
            return status switch
            {
                TaskStatus.Planned => Brushes.LightBlue,
                TaskStatus.InProgress => Brushes.Orange,
                TaskStatus.Completed => Brushes.LightGreen,
                TaskStatus.Paused => Brushes.Yellow,
                TaskStatus.Cancelled => Brushes.LightCoral,
                _ => Brushes.Gray
            };
        }
    }

    // ViewModel для элементов диаграммы Ганта
    public class GanttTaskViewModel
    {
        public double XPosition { get; set; }
        public double YPosition { get; set; }
        public double BarWidth { get; set; }
        public string DisplayText { get; set; }
        public Brush StatusBrush { get; set; }
        public string ToolTipText { get; set; }
    }
}
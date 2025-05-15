// File: ViewModels/TimeEditViewModel.cs
using ProductionScheduler.Data;
using ProductionScheduler.Models;
using System;
using System.Windows.Input;
using System.Windows;
using TaskStatus = ProductionScheduler.Models.TaskStatus;

namespace ProductionScheduler.ViewModels
{
    public class TimeEditViewModel : ViewModelBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ProductionTask _task;
        private readonly ProductionTaskStage _taskStage;

        #region Properties
        private DateTime? _plannedStartTime;
        public DateTime? PlannedStartTime
        {
            get => _plannedStartTime;
            set => SetProperty(ref _plannedStartTime, value);
        }

        private DateTime? _plannedEndTime;
        public DateTime? PlannedEndTime
        {
            get => _plannedEndTime;
            set => SetProperty(ref _plannedEndTime, value);
        }

        private DateTime? _actualStartTime;
        public DateTime? ActualStartTime
        {
            get => _actualStartTime;
            set => SetProperty(ref _actualStartTime, value);
        }

        private DateTime? _actualEndTime;
        public DateTime? ActualEndTime
        {
            get => _actualEndTime;
            set => SetProperty(ref _actualEndTime, value);
        }

        private TimeSpan _plannedDuration;
        public TimeSpan PlannedDuration
        {
            get => _plannedDuration;
            set => SetProperty(ref _plannedDuration, value);
        }

        private TimeSpan? _actualDuration;
        public TimeSpan? ActualDuration
        {
            get => _actualDuration;
            set => SetProperty(ref _actualDuration, value);
        }

        private string _notes;
        public string Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        private string _itemName;
        public string ItemName
        {
            get => _itemName;
            set => SetProperty(ref _itemName, value);
        }

        private bool _isTaskEdit;
        public bool IsTaskEdit
        {
            get => _isTaskEdit;
            set => SetProperty(ref _isTaskEdit, value);
        }

        private string _machineName;
        public string MachineName
        {
            get => _machineName;
            set => SetProperty(ref _machineName, value);
        }
        #endregion

        #region Commands
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand CalculateDurationCommand { get; }
        public ICommand SetCurrentTimeStartCommand { get; }
        public ICommand SetCurrentTimeEndCommand { get; }
        #endregion

        public event Action<bool> RequestClose;

        // Конструктор для редактирования задания
        public TimeEditViewModel(ProductionTask task, ApplicationDbContext context)
        {
            _task = task;
            _context = context;
            _isTaskEdit = true;

            InitializeProperties();
            InitializeCommands();
        }

        // Конструктор для редактирования этапа
        public TimeEditViewModel(ProductionTaskStage taskStage, ApplicationDbContext context)
        {
            _taskStage = taskStage;
            _context = context;
            _isTaskEdit = false;

            InitializeProperties();
            InitializeCommands();
        }

        private void InitializeProperties()
        {
            if (_isTaskEdit && _task != null)
            {
                ItemName = $"Задание: {_task.Detail?.Name} ({_task.Quantity} шт.)";
                PlannedStartTime = _task.PlannedStartTime;
                PlannedEndTime = _task.PlannedEndTime;
                ActualStartTime = _task.ActualStartTime;
                ActualEndTime = _task.ActualEndTime;
                Notes = _task.Notes;
                MachineName = "Все станки";

                if (PlannedStartTime.HasValue && PlannedEndTime.HasValue)
                {
                    PlannedDuration = PlannedEndTime.Value - PlannedStartTime.Value;
                }

                if (ActualStartTime.HasValue && ActualEndTime.HasValue)
                {
                    ActualDuration = ActualEndTime.Value - ActualStartTime.Value;
                }
            }
            else if (!_isTaskEdit && _taskStage != null)
            {
                ItemName = $"Этап: {_taskStage.RouteStage?.OperationName} ({_taskStage.QuantityToProcess} шт.)";
                PlannedStartTime = _taskStage.PlannedStartTime;
                PlannedEndTime = _taskStage.PlannedEndTime;
                ActualStartTime = _taskStage.ActualStartTime;
                ActualEndTime = _taskStage.ActualEndTime;
                PlannedDuration = _taskStage.PlannedDuration;
                ActualDuration = _taskStage.ActualDuration;
                MachineName = _taskStage.AssignedMachine?.Name ?? "Не назначен";
                Notes = $"Норма: {_taskStage.StandardTimePerUnitAtExecution} ч/шт, Переналадка: {_taskStage.PlannedSetupTime} ч";
            }
        }

        private void InitializeCommands()
        {
            SaveCommand = new RelayCommand(ExecuteSave, CanExecuteSave);
            CancelCommand = new RelayCommand(ExecuteCancel);
            CalculateDurationCommand = new RelayCommand(ExecuteCalculateDuration);
            SetCurrentTimeStartCommand = new RelayCommand(ExecuteSetCurrentTimeStart);
            SetCurrentTimeEndCommand = new RelayCommand(ExecuteSetCurrentTimeEnd);
        }

        private bool CanExecuteSave()
        {
            return PlannedStartTime.HasValue && PlannedEndTime.HasValue && PlannedEndTime > PlannedStartTime;
        }

        private void ExecuteSave()
        {
            try
            {
                if (_isTaskEdit && _task != null)
                {
                    SaveTask();
                }
                else if (!_isTaskEdit && _taskStage != null)
                {
                    SaveTaskStage();
                }

                RequestClose?.Invoke(true);
                MessageBox.Show("Время успешно сохранено", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveTask()
        {
            _task.PlannedStartTime = PlannedStartTime;
            _task.PlannedEndTime = PlannedEndTime;
            _task.ActualStartTime = ActualStartTime;
            _task.ActualEndTime = ActualEndTime;
            _task.Notes = Notes;

            // Обновляем этапы в соответствии с новым временем задания
            UpdateTaskStagesTime();

            _context.SaveChanges();
        }

        private void UpdateTaskStagesTime()
        {
            if (!PlannedStartTime.HasValue || !PlannedEndTime.HasValue)
                return;

            var stages = _context.ProductionTaskStages
                .Where(pts => pts.ProductionTaskId == _task.Id)
                .OrderBy(pts => pts.OrderInTask)
                .ToList();

            var totalPlannedDuration = stages.Sum(s => s.PlannedDuration.TotalHours);
            var newTaskDuration = (PlannedEndTime.Value - PlannedStartTime.Value).TotalHours;
            var timeScale = totalPlannedDuration > 0 ? newTaskDuration / totalPlannedDuration : 1;

            var currentTime = PlannedStartTime.Value;
            foreach (var stage in stages)
            {
                var newStageDuration = TimeSpan.FromHours(stage.PlannedDuration.TotalHours * timeScale);
                stage.PlannedStartTime = currentTime;
                stage.PlannedEndTime = currentTime.Add(newStageDuration);
                stage.PlannedDuration = newStageDuration;
                currentTime = stage.PlannedEndTime.Value;
            }
        }

        private void SaveTaskStage()
        {
            _taskStage.PlannedStartTime = PlannedStartTime;
            _taskStage.PlannedEndTime = PlannedEndTime;
            _taskStage.ActualStartTime = ActualStartTime;
            _taskStage.ActualEndTime = ActualEndTime;

            if (PlannedStartTime.HasValue && PlannedEndTime.HasValue)
            {
                _taskStage.PlannedDuration = PlannedEndTime.Value - PlannedStartTime.Value;
            }

            if (ActualStartTime.HasValue && ActualEndTime.HasValue)
            {
                _taskStage.ActualDuration = ActualEndTime.Value - ActualStartTime.Value;
            }

            _context.SaveChanges();
        }

        private void ExecuteCancel()
        {
            RequestClose?.Invoke(false);
        }

        private void ExecuteCalculateDuration()
        {
            if (PlannedStartTime.HasValue && PlannedEndTime.HasValue)
            {
                PlannedDuration = PlannedEndTime.Value - PlannedStartTime.Value;
            }

            if (ActualStartTime.HasValue && ActualEndTime.HasValue)
            {
                ActualDuration = ActualEndTime.Value - ActualStartTime.Value;
            }
        }

        private void ExecuteSetCurrentTimeStart()
        {
            if (!_isTaskEdit && _taskStage != null)
            {
                ActualStartTime = DateTime.Now;
                if (!ActualEndTime.HasValue)
                {
                    // Также запускаем этап
                    _taskStage.Status = TaskStatus.InProgress;
                }
            }
            else if (_isTaskEdit && _task != null)
            {
                ActualStartTime = DateTime.Now;
                if (_task.Status == TaskStatus.Planned)
                {
                    _task.Status = TaskStatus.InProgress;
                }
            }
        }

        private void ExecuteSetCurrentTimeEnd()
        {
            if (!_isTaskEdit && _taskStage != null)
            {
                ActualEndTime = DateTime.Now;
                if (ActualStartTime.HasValue)
                {
                    ActualDuration = ActualEndTime.Value - ActualStartTime.Value;
                    _taskStage.Status = TaskStatus.Completed;
                }
            }
            else if (_isTaskEdit && _task != null)
            {
                ActualEndTime = DateTime.Now;
                if (ActualStartTime.HasValue)
                {
                    ActualDuration = ActualEndTime.Value - ActualStartTime.Value;
                    _task.Status = TaskStatus.Completed;
                }
            }
        }
    }
}
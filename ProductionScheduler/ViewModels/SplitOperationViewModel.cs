// File: ViewModels/SplitOperationViewModel.cs - Исправленная версия
using ProductionScheduler.Data;
using ProductionScheduler.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows;
using System;
using Microsoft.EntityFrameworkCore;
using TaskStatus = ProductionScheduler.Models.TaskStatus;

namespace ProductionScheduler.ViewModels
{
    public class SplitOperationViewModel : ViewModelBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ProductionTaskStage _originalStage;

        #region Properties
        private int _totalQuantity;
        public int TotalQuantity
        {
            get => _totalQuantity;
            set => SetProperty(ref _totalQuantity, value);
        }

        private int _numberOfParts;
        public int NumberOfParts
        {
            get => _numberOfParts;
            set
            {
                if (SetProperty(ref _numberOfParts, value))
                {
                    UpdateSplitParts();
                }
            }
        }

        private int _quantityPerPart;
        public int QuantityPerPart
        {
            get => _quantityPerPart;
            set
            {
                if (SetProperty(ref _quantityPerPart, value))
                {
                    UpdateFromQuantityPerPart();
                }
            }
        }

        private ObservableCollection<Machine> _availableMachines;
        public ObservableCollection<Machine> AvailableMachines
        {
            get => _availableMachines;
            set => SetProperty(ref _availableMachines, value);
        }

        private ObservableCollection<SplitPart> _splitParts;
        public ObservableCollection<SplitPart> SplitParts
        {
            get => _splitParts;
            set => SetProperty(ref _splitParts, value);
        }

        private bool _isQuantityMode = true;
        public bool IsQuantityMode
        {
            get => _isQuantityMode;
            set => SetProperty(ref _isQuantityMode, value);
        }

        private bool _isPartsMode = false;
        public bool IsPartsMode
        {
            get => _isPartsMode;
            set => SetProperty(ref _isPartsMode, value);
        }

        private bool _createSeparateTasks = true;
        public bool CreateSeparateTasks
        {
            get => _createSeparateTasks;
            set => SetProperty(ref _createSeparateTasks, value);
        }
        #endregion

        #region Commands
        public ICommand SplitCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand AddPartCommand { get; }
        public ICommand RemovePartCommand { get; }
        #endregion

        public SplitOperationViewModel(ProductionTaskStage originalStage, ApplicationDbContext context)
        {
            _originalStage = originalStage;
            _context = context;

            TotalQuantity = originalStage.QuantityToProcess;
            NumberOfParts = 2;

            LoadAvailableMachines();
            UpdateSplitParts();

            SplitCommand = new RelayCommand(ExecuteSplit, CanExecuteSplit);
            CancelCommand = new RelayCommand(ExecuteCancel);
            AddPartCommand = new RelayCommand(ExecuteAddPart);
            RemovePartCommand = new RelayCommand<SplitPart>(ExecuteRemovePart);
        }

        public event Action<bool> RequestClose;

        private void LoadAvailableMachines()
        {
            try
            {
                var routeStage = _context.RouteStages
                    .Include(rs => rs.ApplicableMachineType)
                    .FirstOrDefault(rs => rs.Id == _originalStage.RouteStageId);

                if (routeStage != null)
                {
                    var machines = _context.Machines
                        .Include(m => m.MachineType)
                        .Where(m => m.MachineTypeId == routeStage.MachineTypeId)
                        .OrderBy(m => m.Name)
                        .ToList();

                    AvailableMachines = new ObservableCollection<Machine>(machines);
                }
                else
                {
                    AvailableMachines = new ObservableCollection<Machine>();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки станков: {ex.Message}", "Ошибка");
                AvailableMachines = new ObservableCollection<Machine>();
            }
        }

        private void UpdateSplitParts()
        {
            if (NumberOfParts <= 0) return;

            var parts = new ObservableCollection<SplitPart>();
            int quantityPerPart = TotalQuantity / NumberOfParts;
            int remainder = TotalQuantity % NumberOfParts;

            for (int i = 0; i < NumberOfParts; i++)
            {
                int quantity = quantityPerPart + (i < remainder ? 1 : 0);
                var part = new SplitPart
                {
                    PartNumber = i + 1,
                    Quantity = quantity,
                    AvailableMachines = AvailableMachines
                };

                // Автоматически выбираем станок
                if (AvailableMachines.Any())
                {
                    // Для первой части выбираем оригинальный станок, если возможно
                    if (i == 0 && _originalStage.MachineId.HasValue)
                    {
                        part.SelectedMachine = AvailableMachines.FirstOrDefault(m => m.Id == _originalStage.MachineId.Value);
                    }

                    // Если станок не выбран, выбираем первый доступный
                    if (part.SelectedMachine == null)
                    {
                        part.SelectedMachine = AvailableMachines.First();
                    }
                }

                parts.Add(part);
            }

            SplitParts = parts;
        }

        private void UpdateFromQuantityPerPart()
        {
            if (QuantityPerPart <= 0) return;
            NumberOfParts = (int)Math.Ceiling((double)TotalQuantity / QuantityPerPart);
        }

        private bool CanExecuteSplit()
        {
            return SplitParts?.All(p => p.SelectedMachine != null && p.Quantity > 0) == true &&
                   SplitParts.Sum(p => p.Quantity) == TotalQuantity;
        }

        private void ExecuteSplit()
        {
            try
            {
                if (CreateSeparateTasks)
                {
                    // Создаем отдельные задания (новая функция)
                    CreateSeparateTasksMethod();
                }
                else
                {
                    // Старая логика - разделяем в рамках одного задания
                    SplitWithinSameTask();
                }

                RequestClose?.Invoke(true);
                MessageBox.Show($"Операция успешно разделена", "Успех");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка разделения: {ex.Message}", "Ошибка");
            }
        }

        private void CreateSeparateTasksMethod()
        {
            // Загружаем исходное задание
            var originalTask = _context.ProductionTasks
                .Include(pt => pt.Detail)
                .Include(pt => pt.TaskStages)
                .ThenInclude(pts => pts.RouteStage)
                .FirstOrDefault(pt => pt.Id == _originalStage.ProductionTaskId);

            if (originalTask == null)
            {
                throw new Exception("Исходное задание не найдено");
            }

            // Удаляем оригинальное задание
            _context.ProductionTasks.Remove(originalTask);
            _context.SaveChanges();

            // Создаем новые задания для каждой части
            foreach (var part in SplitParts)
            {
                // Создаем новое задание
                var newTask = new ProductionTask
                {
                    DetailId = originalTask.DetailId,
                    Quantity = part.Quantity,
                    CreationTime = DateTime.Now,
                    Status = TaskStatus.Planned,
                    PlannedStartTime = originalTask.PlannedStartTime,
                    PlannedEndTime = originalTask.PlannedEndTime,
                    Notes = $"Разделено из задания #{originalTask.Id} (часть {part.PartNumber})"
                };

                _context.ProductionTasks.Add(newTask);
                _context.SaveChanges(); // Сохраняем, чтобы получить ID

                // Создаем этапы для нового задания
                var sortedStages = originalTask.TaskStages.OrderBy(pts => pts.OrderInTask).ToList();

                foreach (var originalTaskStage in sortedStages)
                {
                    var newTaskStage = new ProductionTaskStage
                    {
                        ProductionTaskId = newTask.Id,
                        RouteStageId = originalTaskStage.RouteStageId,
                        QuantityToProcess = part.Quantity,
                        OrderInTask = originalTaskStage.OrderInTask,
                        Status = TaskStatus.Planned,
                        StandardTimePerUnitAtExecution = originalTaskStage.StandardTimePerUnitAtExecution,
                        PlannedSetupTime = originalTaskStage.PlannedSetupTime,
                        PlannedStartTime = originalTaskStage.PlannedStartTime,
                        PlannedEndTime = originalTaskStage.PlannedEndTime
                    };

                    // Если это разделяемый этап, используем выбранный станок
                    if (originalTaskStage.Id == _originalStage.Id)
                    {
                        newTaskStage.MachineId = part.SelectedMachine.Id;
                        // Пересчитываем длительность для нового количества
                        var durationPerUnit = originalTaskStage.PlannedDuration.TotalHours / originalTask.Quantity;
                        newTaskStage.PlannedDuration = TimeSpan.FromHours(durationPerUnit * part.Quantity);
                    }
                    else
                    {
                        // Для других этапов сохраняем оригинальный станок
                        newTaskStage.MachineId = originalTaskStage.MachineId;
                        // Пересчитываем длительность пропорционально количеству
                        var durationPerUnit = originalTaskStage.PlannedDuration.TotalHours / originalTask.Quantity;
                        newTaskStage.PlannedDuration = TimeSpan.FromHours(durationPerUnit * part.Quantity);
                    }

                    _context.ProductionTaskStages.Add(newTaskStage);
                }
            }

            _context.SaveChanges();
        }

        private void SplitWithinSameTask()
        {
            // Обновляем оригинальный этап (первая часть)
            var firstPart = SplitParts.First();
            _originalStage.QuantityToProcess = firstPart.Quantity;
            _originalStage.MachineId = firstPart.SelectedMachine.Id;

            // Пересчитываем длительность для первой части
            var originalDurationPerUnit = _originalStage.PlannedDuration.TotalHours / TotalQuantity;
            _originalStage.PlannedDuration = TimeSpan.FromHours(originalDurationPerUnit * firstPart.Quantity);

            // Создаем дочерние этапы для остальных частей
            foreach (var part in SplitParts.Skip(1))
            {
                var subStage = new ProductionTaskStage
                {
                    ProductionTaskId = _originalStage.ProductionTaskId,
                    RouteStageId = _originalStage.RouteStageId,
                    MachineId = part.SelectedMachine.Id,
                    QuantityToProcess = part.Quantity,
                    OrderInTask = _originalStage.OrderInTask,
                    Status = _originalStage.Status,
                    StandardTimePerUnitAtExecution = _originalStage.StandardTimePerUnitAtExecution,
                    PlannedSetupTime = 0, // Нет переналадки для той же детали
                    PlannedDuration = TimeSpan.FromHours(originalDurationPerUnit * part.Quantity),
                    PlannedStartTime = _originalStage.PlannedStartTime,
                    PlannedEndTime = _originalStage.PlannedStartTime?.Add(TimeSpan.FromHours(originalDurationPerUnit * part.Quantity)),
                    ParentProductionTaskStageId = _originalStage.Id
                };

                _context.ProductionTaskStages.Add(subStage);
            }

            _context.SaveChanges();
        }

        private void ExecuteCancel()
        {
            RequestClose?.Invoke(false);
        }

        private void ExecuteAddPart()
        {
            NumberOfParts++;
        }

        private void ExecuteRemovePart(SplitPart part)
        {
            if (SplitParts.Count > 2)
            {
                SplitParts.Remove(part);
                NumberOfParts = SplitParts.Count;
                UpdateSplitParts();
            }
        }
    }

    public class SplitPart : ViewModelBase
    {
        public int PartNumber { get; set; }

        private int _quantity;
        public int Quantity
        {
            get => _quantity;
            set => SetProperty(ref _quantity, value);
        }

        private Machine _selectedMachine;
        public Machine SelectedMachine
        {
            get => _selectedMachine;
            set => SetProperty(ref _selectedMachine, value);
        }

        public ObservableCollection<Machine> AvailableMachines { get; set; }
    }
}
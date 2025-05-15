// File: ViewModels/EmployeeWorkViewModel.cs - Исправленная версия
using ProductionScheduler.Data;
using ProductionScheduler.Models;
using ProductionScheduler.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System;
using System.Windows;
using System.Collections.Generic;
using TaskStatus = ProductionScheduler.Models.TaskStatus;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace ProductionScheduler.ViewModels
{
    public class EmployeeWorkViewModel : ViewModelBase
    {
        private ApplicationDbContext _context;
        private ProductionPlanningService _planningService;
        private MachineAlternativeService _machineAlternativeService;

        #region Properties for New Task Creation
        private ObservableCollection<Detail> _availableDetails;
        public ObservableCollection<Detail> AvailableDetails
        {
            get => _availableDetails;
            set => SetProperty(ref _availableDetails, value);
        }

        private Detail _selectedDetailForNewTask;
        public Detail SelectedDetailForNewTask
        {
            get => _selectedDetailForNewTask;
            set
            {
                if (SetProperty(ref _selectedDetailForNewTask, value))
                {
                    LoadRouteStagesForNewTask();
                    UpdatePlannedTimes();
                    ((RelayCommand)CreateNewTaskCommand).RaiseCanExecuteChanged();
                }
            }
        }

        private int _newTaskQuantity = 1;
        public int NewTaskQuantity
        {
            get => _newTaskQuantity;
            set
            {
                if (SetProperty(ref _newTaskQuantity, value))
                {
                    UpdatePlannedTimes();
                    ((RelayCommand)CreateNewTaskCommand).RaiseCanExecuteChanged();
                }
            }
        }

        // Новые свойства для разделения задач
        private int _maxTaskSize = 10;
        public int MaxTaskSize
        {
            get => _maxTaskSize;
            set
            {
                if (SetProperty(ref _maxTaskSize, value))
                {
                    UpdatePlannedTimes();
                }
            }
        }

        private bool _allowTaskSplitting = true;
        public bool AllowTaskSplitting
        {
            get => _allowTaskSplitting;
            set
            {
                if (SetProperty(ref _allowTaskSplitting, value))
                {
                    UpdatePlannedTimes();
                }
            }
        }

        private ObservableCollection<RouteStageWithMachine> _routeStagesForNewTask;
        public ObservableCollection<RouteStageWithMachine> RouteStagesForNewTask
        {
            get => _routeStagesForNewTask;
            set => SetProperty(ref _routeStagesForNewTask, value);
        }

        // Плановые времена для нового задания
        private string _totalPlannedTime;
        public string TotalPlannedTime
        {
            get => _totalPlannedTime;
            set => SetProperty(ref _totalPlannedTime, value);
        }

        private DateTime? _plannedStartTime;
        public DateTime? PlannedStartTime
        {
            get => _plannedStartTime;
            set
            {
                if (SetProperty(ref _plannedStartTime, value))
                {
                    UpdatePlannedTimes();
                }
            }
        }

        private DateTime? _plannedEndTime;
        public DateTime? PlannedEndTime
        {
            get => _plannedEndTime;
            set => SetProperty(ref _plannedEndTime, value);
        }

        private bool _useAutomaticPlanning = true;
        public bool UseAutomaticPlanning
        {
            get => _useAutomaticPlanning;
            set
            {
                if (SetProperty(ref _useAutomaticPlanning, value))
                {
                    UpdatePlannedTimes();
                }
            }
        }

        private string _planningWarnings;
        public string PlanningWarnings
        {
            get => _planningWarnings;
            set => SetProperty(ref _planningWarnings, value);
        }

        // Новое свойство для отображения количества создаваемых задач
        private string _taskSplitInfo;
        public string TaskSplitInfo
        {
            get => _taskSplitInfo;
            set => SetProperty(ref _taskSplitInfo, value);
        }

        // Свойства для отображения конфликтов станков
        private ObservableCollection<MachineConflictViewModel> _machineConflicts;
        public ObservableCollection<MachineConflictViewModel> MachineConflicts
        {
            get => _machineConflicts;
            set => SetProperty(ref _machineConflicts, value);
        }

        private bool _hasConflicts;
        public bool HasConflicts
        {
            get => _hasConflicts;
            set => SetProperty(ref _hasConflicts, value);
        }
        #endregion

        #region Properties for Displaying Tasks and Stages

        private ObservableCollection<ProductionTask> _activeProductionTasks;
        public ObservableCollection<ProductionTask> ActiveProductionTasks
        {
            get => _activeProductionTasks;
            set => SetProperty(ref _activeProductionTasks, value);
        }

        private ProductionTask _selectedTask;
        public ProductionTask SelectedTask
        {
            get => _selectedTask;
            set
            {
                if (SetProperty(ref _selectedTask, value))
                {
                    LoadTaskStagesDetails();
                    ((RelayCommand)StartTaskCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)PauseTaskCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)CompleteTaskCommand).RaiseCanExecuteChanged();
                }
            }
        }

        private ObservableCollection<ProductionTaskStageViewModel> _selectedTaskStages;
        public ObservableCollection<ProductionTaskStageViewModel> SelectedTaskStages
        {
            get => _selectedTaskStages;
            set => SetProperty(ref _selectedTaskStages, value);
        }
        #endregion

        #region Commands
        public ICommand CreateNewTaskCommand { get; }
        public ICommand RefreshDataCommand { get; }
        public ICommand StartTaskCommand { get; }
        public ICommand PauseTaskCommand { get; }
        public ICommand CompleteTaskCommand { get; }
        public ICommand StartStageCommand { get; }
        public ICommand PauseStageCommand { get; }
        public ICommand CompleteStageCommand { get; }
        public ICommand SuggestOptimalTimeCommand { get; }
        public ICommand SplitStageCommand { get; }
        public ICommand ChangeTaskSizeCommand { get; }
        public ICommand CheckMachineConflictsCommand { get; }
        #endregion

        public EmployeeWorkViewModel()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== EmployeeWorkViewModel Constructor Start ===");

                _context = new ApplicationDbContext();
                _planningService = new ProductionPlanningService(_context);
                _machineAlternativeService = new MachineAlternativeService(_context);

                // Инициализируем команды
                CreateNewTaskCommand = new RelayCommand(ExecuteCreateNewTask, CanExecuteCreateNewTask);
                RefreshDataCommand = new RelayCommand(LoadAllData);
                StartTaskCommand = new RelayCommand(ExecuteStartTask, () => SelectedTask?.Status == TaskStatus.Planned);
                PauseTaskCommand = new RelayCommand(ExecutePauseTask, () => SelectedTask?.Status == TaskStatus.InProgress);
                CompleteTaskCommand = new RelayCommand(ExecuteCompleteTask, () => SelectedTask?.Status == TaskStatus.InProgress);
                StartStageCommand = new RelayCommand<ProductionTaskStageViewModel>(ExecuteStartStage);
                PauseStageCommand = new RelayCommand<ProductionTaskStageViewModel>(ExecutePauseStage);
                CompleteStageCommand = new RelayCommand<ProductionTaskStageViewModel>(ExecuteCompleteStage);
                SuggestOptimalTimeCommand = new RelayCommand(ExecuteSuggestOptimalTime);
                SplitStageCommand = new RelayCommand<ProductionTaskStageViewModel>(ExecuteSplitStage);
                ChangeTaskSizeCommand = new RelayCommand(UpdatePlannedTimes);
                CheckMachineConflictsCommand = new RelayCommand(ExecuteCheckMachineConflicts);

                // Инициализируем коллекции
                AvailableDetails = new ObservableCollection<Detail>();
                ActiveProductionTasks = new ObservableCollection<ProductionTask>();
                RouteStagesForNewTask = new ObservableCollection<RouteStageWithMachine>();
                SelectedTaskStages = new ObservableCollection<ProductionTaskStageViewModel>();
                MachineConflicts = new ObservableCollection<MachineConflictViewModel>();

                PlannedStartTime = DateTime.Now;

                // Загружаем данные
                if (_context.Database.CanConnect())
                {
                    LoadAllData();
                }

                System.Diagnostics.Debug.WriteLine("=== EmployeeWorkViewModel Constructor End ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EmployeeWorkViewModel Constructor Error: {ex.Message}");
                MessageBox.Show($"Ошибка инициализации: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region Data Loading Methods

        private void LoadAllData()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== LoadAllData Start ===");
                LoadAvailableDetails();
                LoadActiveProductionTasks();
                System.Diagnostics.Debug.WriteLine("=== LoadAllData End ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadAllData Error: {ex.Message}");
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadAvailableDetails()
        {
            try
            {
                var details = _context.Details.ToList();
                var detailsWithRoutes = new ObservableCollection<Detail>();

                foreach (var detail in details)
                {
                    var hasRoutes = _context.RouteStages.Any(rs => rs.DetailId == detail.Id);
                    if (hasRoutes)
                    {
                        detailsWithRoutes.Add(detail);
                    }
                }

                AvailableDetails = detailsWithRoutes;
                System.Diagnostics.Debug.WriteLine($"Loaded {AvailableDetails.Count} details with routes");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadAvailableDetails Error: {ex.Message}");
                AvailableDetails = new ObservableCollection<Detail>();
            }
        }

        private void LoadRouteStagesForNewTask()
        {
            try
            {
                if (SelectedDetailForNewTask == null)
                {
                    RouteStagesForNewTask = new ObservableCollection<RouteStageWithMachine>();
                    return;
                }

                var routeStages = _context.RouteStages
                    .Include(rs => rs.ApplicableMachineType)
                    .Where(rs => rs.DetailId == SelectedDetailForNewTask.Id)
                    .OrderBy(rs => rs.OrderInRoute)
                    .ToList();

                var routeStagesWithMachines = new ObservableCollection<RouteStageWithMachine>();

                foreach (var rs in routeStages)
                {
                    var machines = _context.Machines
                        .Include(m => m.MachineType)
                        .Where(m => m.MachineTypeId == rs.MachineTypeId)
                        .OrderBy(m => m.Name)
                        .ToList();

                    var routeStageWithMachine = new RouteStageWithMachine
                    {
                        RouteStage = rs,
                        AvailableMachines = new ObservableCollection<Machine>(machines)
                    };

                    // Автоматически выбираем первый доступный станок
                    if (machines.Any())
                    {
                        routeStageWithMachine.SelectedMachine = GetBestMachine(machines, rs);
                    }

                    // Подписываемся на изменение выбранного станка для пересчета времени
                    routeStageWithMachine.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(RouteStageWithMachine.SelectedMachine))
                        {
                            UpdatePlannedTimes();
                            ((RelayCommand)CreateNewTaskCommand).RaiseCanExecuteChanged();
                        }
                    };

                    routeStagesWithMachines.Add(routeStageWithMachine);
                }

                RouteStagesForNewTask = routeStagesWithMachines;
                System.Diagnostics.Debug.WriteLine($"Loaded {RouteStagesForNewTask.Count} route stages");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadRouteStagesForNewTask Error: {ex.Message}");
                RouteStagesForNewTask = new ObservableCollection<RouteStageWithMachine>();
            }
        }

        private Machine GetBestMachine(List<Machine> machines, RouteStage routeStage)
        {
            try
            {
                // Простая логика выбора лучшего станка
                // TODO: Здесь можно добавить более сложную логику учета загруженности станков

                // Возвращаем первый станок из списка
                return machines.First();
            }
            catch
            {
                return machines.FirstOrDefault();
            }
        }

        private void LoadActiveProductionTasks()
        {
            try
            {
                var tasks = _context.ProductionTasks
                    .Include(pt => pt.Detail)
                    .Include(pt => pt.TaskStages)
                        .ThenInclude(pts => pts.RouteStage)
                    .Include(pt => pt.TaskStages)
                        .ThenInclude(pts => pts.AssignedMachine)
                    .Where(pt => pt.Status != TaskStatus.Completed && pt.Status != TaskStatus.Cancelled)
                    .OrderByDescending(pt => pt.CreationTime)
                    .ToList();

                ActiveProductionTasks = new ObservableCollection<ProductionTask>(tasks);
                System.Diagnostics.Debug.WriteLine($"Loaded {ActiveProductionTasks.Count} active tasks");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadActiveProductionTasks Error: {ex.Message}");
                ActiveProductionTasks = new ObservableCollection<ProductionTask>();
            }
        }

        private void LoadTaskStagesDetails()
        {
            try
            {
                if (SelectedTask == null)
                {
                    SelectedTaskStages = new ObservableCollection<ProductionTaskStageViewModel>();
                    return;
                }

                var stages = _context.ProductionTaskStages
                    .Include(pts => pts.RouteStage)
                    .Include(pts => pts.AssignedMachine)
                    .Where(pts => pts.ProductionTaskId == SelectedTask.Id)
                    .OrderBy(pts => pts.OrderInTask)
                    .ToList();

                var stageViewModels = new ObservableCollection<ProductionTaskStageViewModel>();

                foreach (var stage in stages)
                {
                    stageViewModels.Add(new ProductionTaskStageViewModel(stage, this));
                }

                SelectedTaskStages = stageViewModels;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadTaskStagesDetails Error: {ex.Message}");
                SelectedTaskStages = new ObservableCollection<ProductionTaskStageViewModel>();
            }
        }

        #endregion

        #region Time Calculation Methods

        private void UpdatePlannedTimes()
        {
            try
            {
                if (SelectedDetailForNewTask == null || RouteStagesForNewTask == null || NewTaskQuantity <= 0)
                {
                    TotalPlannedTime = "";
                    PlannedEndTime = null;
                    PlanningWarnings = "";
                    TaskSplitInfo = "";
                    return;
                }

                // Проверяем, выбраны ли все станки
                var selectedMachines = RouteStagesForNewTask.Where(rs => rs.SelectedMachine != null).ToList();
                if (selectedMachines.Count != RouteStagesForNewTask.Count)
                {
                    TotalPlannedTime = "Выберите станки для всех этапов";
                    return;
                }

                // Рассчитываем разделение задач
                var taskSizes = CalculateTaskSizes(NewTaskQuantity, AllowTaskSplitting ? MaxTaskSize : NewTaskQuantity);
                TaskSplitInfo = taskSizes.Count > 1 ? $"Будет создано {taskSizes.Count} заданий: {string.Join(", ", taskSizes)} шт." : "";

                // Используем планирование со сменами
                if (UseAutomaticPlanning)
                {
                    // Автоматическое планирование с учетом смен
                    var stageAssignments = RouteStagesForNewTask
                        .Select(rs => (rs.RouteStage, rs.SelectedMachine))
                        .ToList();

                    // Планируем первое задание для примера
                    var firstTaskSize = taskSizes.First();
                    var plan = _planningService.PlanTask(SelectedDetailForNewTask, firstTaskSize,
                        PlannedStartTime ?? DateTime.Now, stageAssignments);

                    // Обновляем информацию из плана для первого задания
                    for (int i = 0; i < RouteStagesForNewTask.Count; i++)
                    {
                        var stageWithMachine = RouteStagesForNewTask[i];
                        var stagePlan = plan.StagePlans[i];

                        stageWithMachine.PlannedSetupTime = stagePlan.PlannedSetupTime;
                        stageWithMachine.PlannedDuration = stagePlan.PlannedDuration;
                        stageWithMachine.PlannedStartTime = stagePlan.PlannedStartTime;
                        stageWithMachine.PlannedEndTime = stagePlan.PlannedEndTime;
                        stageWithMachine.AssignedShift = stagePlan.AssignedShift;
                        stageWithMachine.SplitAcrossShifts = stagePlan.SplitAcrossShifts;
                    }

                    PlannedStartTime = plan.PlannedStartTime;
                    PlannedEndTime = plan.PlannedEndTime;

                    // Для множественных заданий приблизительно умножаем время
                    var totalDuration = plan.TotalDuration;
                    if (taskSizes.Count > 1)
                    {
                        // Приблизительный расчет времени для всех заданий
                        var avgTaskSize = (double)NewTaskQuantity / taskSizes.Count;
                        var timePerUnit = totalDuration.TotalMinutes / firstTaskSize;
                        totalDuration = TimeSpan.FromMinutes(timePerUnit * NewTaskQuantity * 1.1); // +10% на переналадки
                        PlannedEndTime = PlannedStartTime?.Add(totalDuration);
                    }

                    TotalPlannedTime = $"{totalDuration:hh\\:mm\\:ss}";

                    // Предупреждения о планировании
                    PlanningWarnings = "";
                    if (plan.ExceedsPreferredTime)
                    {
                        PlanningWarnings += "⚠️ Задание выходит за предпочтительное время (08:00-17:00)\n";
                    }

                    var hasNightShifts = plan.StagePlans.Any(sp => sp.AssignedShift?.ShiftType == ShiftType.Second ||
                                                                   sp.AssignedShift?.ShiftType == ShiftType.Third);
                    if (hasNightShifts)
                    {
                        PlanningWarnings += "🌙 Некоторые этапы запланированы на ночные смены\n";
                    }

                    var hasSplitOperations = plan.StagePlans.Any(sp => sp.SplitAcrossShifts);
                    if (hasSplitOperations)
                    {
                        PlanningWarnings += "⚡ Некоторые операции будут разделены между сменами\n";
                    }

                    if (taskSizes.Count > 1)
                    {
                        PlanningWarnings += $"📋 Задание будет разделено на {taskSizes.Count} частей\n";
                    }
                }
                else
                {
                    // Простой расчет без учета смен
                    var totalHours = CalculateSimpleTotalTime() * taskSizes.Count;
                    TotalPlannedTime = $"{totalHours:F2} ч ({TimeSpan.FromHours(totalHours):hh\\:mm})";
                    PlannedEndTime = PlannedStartTime?.AddHours(totalHours);
                    PlanningWarnings = "Простой расчет без учета смен и обедов";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdatePlannedTimes Error: {ex.Message}");
                TotalPlannedTime = "Ошибка расчета";
                PlanningWarnings = $"Ошибка: {ex.Message}";
            }
        }

        private List<int> CalculateTaskSizes(int totalQuantity, int maxTaskSize)
        {
            var sizes = new List<int>();

            if (!AllowTaskSplitting || totalQuantity <= maxTaskSize)
            {
                sizes.Add(totalQuantity);
                return sizes;
            }

            var remaining = totalQuantity;
            while (remaining > 0)
            {
                var currentSize = Math.Min(remaining, maxTaskSize);
                sizes.Add(currentSize);
                remaining -= currentSize;
            }

            return sizes;
        }

        private double CalculateSimpleTotalTime()
        {
            double totalHours = 0;
            var currentTime = PlannedStartTime ?? DateTime.Now;

            foreach (var stageWithMachine in RouteStagesForNewTask)
            {
                var routeStage = stageWithMachine.RouteStage;
                var machine = stageWithMachine.SelectedMachine;

                if (machine == null) continue;

                // Рассчитываем время переналадки
                double setupTime = CalculateSetupTime(machine, SelectedDetailForNewTask);

                // Время на обработку всех деталей + переналадка
                double stageHours = (routeStage.StandardTimePerUnit * NewTaskQuantity) + setupTime;
                totalHours += stageHours;

                // Обновляем информацию в RouteStageWithMachine для отображения
                stageWithMachine.PlannedSetupTime = setupTime;
                stageWithMachine.PlannedDuration = TimeSpan.FromHours(stageHours);
                stageWithMachine.PlannedStartTime = currentTime;
                stageWithMachine.PlannedEndTime = currentTime.AddHours(stageHours);

                currentTime = stageWithMachine.PlannedEndTime.Value;
            }

            return totalHours;
        }

        private double CalculateSetupTime(Machine machine, Detail detail)
        {
            try
            {
                // Ищем последнюю завершенную операцию на этом станке
                var lastTaskStage = _context.ProductionTaskStages
                    .Include(pts => pts.ProductionTask)
                    .Where(pts => pts.MachineId == machine.Id && pts.ActualEndTime.HasValue)
                    .OrderByDescending(pts => pts.ActualEndTime)
                    .FirstOrDefault();

                if (lastTaskStage == null)
                {
                    return 0; // Первая операция на станке - переналадка не нужна
                }

                // Проверяем, была ли последняя операция для той же детали
                if (lastTaskStage.ProductionTask?.DetailId == detail.Id)
                {
                    return 0; // Предыдущая операция была для той же детали
                }

                return 10.0 / 60.0; // 10 минут в часах
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CalculateSetupTime Error: {ex.Message}");
                return 0;
            }
        }

        private void ExecuteSuggestOptimalTime()
        {
            try
            {
                // Предлагаем оптимальное время начала (ближайшее к предпочтительному времени)
                var now = DateTime.Now;
                var preferredStart = new TimeSpan(8, 0, 0); // 08:00

                DateTime suggestedStart;

                // Если сейчас до 8:00, предлагаем 8:00 сегодня
                if (now.TimeOfDay < preferredStart)
                {
                    suggestedStart = now.Date.Add(preferredStart);
                }
                // Если сейчас после 17:00, предлагаем 8:00 завтра
                else if (now.TimeOfDay > new TimeSpan(17, 0, 0))
                {
                    suggestedStart = now.Date.AddDays(1).Add(preferredStart);
                }
                // Иначе предлагаем ближайший час
                else
                {
                    suggestedStart = new DateTime(now.Year, now.Month, now.Day, now.Hour + 1, 0, 0);
                }

                PlannedStartTime = suggestedStart;
                UpdatePlannedTimes();
            }
            catch(Exception ex)
            {
                MessageBox.Show($"Ошибка в методе ExecuteSuggestOptimalTime: {ex.Message}", "Ошибка");
            }
        }

        private void ExecuteCheckMachineConflicts()
        {
            try
            {
                if (RouteStagesForNewTask == null || !RouteStagesForNewTask.Any())
                {
                    MessageBox.Show("Сначала выберите деталь и станки для этапов", "Информация");
                    return;
                }

                // Анализируем конфликты
                var conflictAnalysis = _machineAlternativeService.AnalyzeMachineConflicts(
                    RouteStagesForNewTask.ToList(), PlannedStartTime ?? DateTime.Now);

                HasConflicts = conflictAnalysis.HasConflicts;

                if (conflictAnalysis.HasConflicts)
                {
                    // Преобразуем конфликты в ViewModel
                    var conflictVMs = conflictAnalysis.Conflicts.Select(c => new MachineConflictViewModel
                    {
                        OperationName = c.RouteStage.OperationName,
                        MachineName = c.Machine.Name,
                        ConflictTime = c.ConflictTime,
                        ConflictingTaskInfo = $"Задание #{c.ConflictingTask.ProductionTaskId} на деталь '{c.ConflictingTask.ProductionTask?.Detail?.Name}'",
                        SuggestedWaitTime = c.SuggestedWaitTime,
                        Alternatives = conflictAnalysis.Alternatives
                            .Where(a => a.Machine.MachineTypeId == c.RouteStage.MachineTypeId)
                            .ToList()
                    }).ToList();

                    MachineConflicts = new ObservableCollection<MachineConflictViewModel>(conflictVMs);

                    MessageBox.Show($"Обнаружено {conflictAnalysis.Conflicts.Count} конфликтов станков. Проверьте вкладку 'Конфликты станков'",
                        "Конфликты обнаружены", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    MachineConflicts = new ObservableCollection<MachineConflictViewModel>();
                    MessageBox.Show("Конфликтов станков не обнаружено. Все выбранные станки доступны в запланированное время.",
                        "Конфликтов нет", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (ArgumentException ex)
            {
                MessageBox.Show($"Ошибка проверки конфликтов: {ex.Message}", "Ошибка");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка предложения оптимального времени: {ex.Message}", "Ошибка");
            }
        }

        #endregion

        #region Task Management Commands

        private bool CanExecuteCreateNewTask()
        {
            return SelectedDetailForNewTask != null &&
                   NewTaskQuantity > 0 &&
                   RouteStagesForNewTask?.All(rs => rs.SelectedMachine != null) == true;
        }

        private void ExecuteCreateNewTask()
        {
            if (!CanExecuteCreateNewTask())
            {
                MessageBox.Show("Выберите деталь, укажите количество и назначьте станки для всех этапов.",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("=== ExecuteCreateNewTask Start ===");

                // Рассчитываем размеры задач
                var taskSizes = CalculateTaskSizes(NewTaskQuantity, AllowTaskSplitting ? MaxTaskSize : NewTaskQuantity);
                var createdTasks = new List<ProductionTask>();

                // Создаем задания с планированием
                foreach (var taskSize in taskSizes)
                {
                    var taskNumber = taskSizes.IndexOf(taskSize) + 1;

                    if (UseAutomaticPlanning)
                    {
                        var task = CreateTaskWithAutomaticPlanning(taskSize, taskNumber, taskSizes.Count);
                        if (task != null) createdTasks.Add(task);
                    }
                    else
                    {
                        var task = CreateTaskWithSimplePlanning(taskSize, taskNumber, taskSizes.Count);
                        if (task != null) createdTasks.Add(task);
                    }
                }

                // Показываем результат
                var message = $"Создано {createdTasks.Count} заданий на деталь '{SelectedDetailForNewTask.Name}':\n\n";
                message += $"Общее количество: {NewTaskQuantity} шт.\n";
                message += $"Задания: {string.Join(", ", taskSizes)} шт.\n";
                message += $"Плановое время: {TotalPlannedTime}\n";
                message += $"Начало: {PlannedStartTime:dd.MM.yy HH:mm}\n";
                message += $"Окончание: {PlannedEndTime:dd.MM.yy HH:mm}\n\n";

                if (!string.IsNullOrEmpty(PlanningWarnings))
                {
                    message += "Предупреждения:\n" + PlanningWarnings;
                }

                MessageBox.Show(message, "Задания созданы", MessageBoxButton.OK, MessageBoxImage.Information);

                LoadActiveProductionTasks();

                // Сброс
                ResetNewTaskForm();

                System.Diagnostics.Debug.WriteLine("=== ExecuteCreateNewTask End ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ExecuteCreateNewTask Error: {ex.Message}");
                MessageBox.Show($"Ошибка создания заданий: {ex.Message}",
                    "Ошибка БД", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private ProductionTask CreateTaskWithAutomaticPlanning(int taskSize, int taskNumber, int totalTasks)
        {
            try
            {
                // Получаем план от сервиса планирования
                var stageAssignments = RouteStagesForNewTask
                    .Select(rs => (rs.RouteStage, rs.SelectedMachine))
                    .ToList();

                // Рассчитываем смещение времени для последующих заданий
                var timeOffset = TimeSpan.FromMinutes((taskNumber - 1) * 30); // 30 минут между заданиями
                var actualStartTime = (PlannedStartTime ?? DateTime.Now).Add(timeOffset);

                var plan = _planningService.PlanTask(SelectedDetailForNewTask, taskSize,
                    actualStartTime, stageAssignments);

                // Создаем задание
                var newTask = new ProductionTask
                {
                    DetailId = SelectedDetailForNewTask.Id,
                    Quantity = taskSize,
                    CreationTime = DateTime.Now,
                    Status = TaskStatus.Planned,
                    Notes = totalTasks > 1 ? $"Задание {taskNumber} из {totalTasks}" : "Автоматическое планирование с учетом смен",
                    PlannedStartTime = plan.PlannedStartTime,
                    PlannedEndTime = plan.PlannedEndTime
                };

                _context.ProductionTasks.Add(newTask);
                _context.SaveChanges();

                // Создаем этапы задания на основе плана
                for (int i = 0; i < plan.StagePlans.Count; i++)
                {
                    var stagePlan = plan.StagePlans[i];

                    var taskStage = new ProductionTaskStage
                    {
                        ProductionTaskId = newTask.Id,
                        RouteStageId = stagePlan.RouteStage.Id,
                        MachineId = stagePlan.Machine.Id,
                        QuantityToProcess = stagePlan.QuantityToProcess,
                        OrderInTask = stagePlan.RouteStage.OrderInRoute,
                        Status = TaskStatus.Planned,
                        StandardTimePerUnitAtExecution = stagePlan.StandardTimePerUnitAtExecution,
                        PlannedSetupTime = stagePlan.PlannedSetupTime,
                        PlannedDuration = stagePlan.PlannedDuration,
                        PlannedStartTime = stagePlan.PlannedStartTime,
                        PlannedEndTime = stagePlan.PlannedEndTime
                    };

                    _context.ProductionTaskStages.Add(taskStage);
                }

                _context.SaveChanges();
                return newTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateTaskWithAutomaticPlanning Error: {ex.Message}");
                return null;
            }
        }

        private ProductionTask CreateTaskWithSimplePlanning(int taskSize, int taskNumber, int totalTasks)
        {
            try
            {
                // Рассчитываем смещение времени для последующих заданий
                var previousTaskDuration = TimeSpan.FromHours(CalculateSimpleTotalTime());
                var timeOffset = previousTaskDuration.Multiply(taskNumber - 1);
                var actualStartTime = (PlannedStartTime ?? DateTime.Now).Add(timeOffset);
                var actualEndTime = actualStartTime.Add(TimeSpan.FromHours(CalculateSimpleTotalTime()));

                var newTask = new ProductionTask
                {
                    DetailId = SelectedDetailForNewTask.Id,
                    Quantity = taskSize,
                    CreationTime = DateTime.Now,
                    Status = TaskStatus.Planned,
                    Notes = totalTasks > 1 ? $"Задание {taskNumber} из {totalTasks}" : "Простое планирование",
                    PlannedStartTime = actualStartTime,
                    PlannedEndTime = actualEndTime
                };

                _context.ProductionTasks.Add(newTask);
                _context.SaveChanges();

                // Создаем этапы задания
                var currentPlannedTime = actualStartTime;

                foreach (var stageWithMachine in RouteStagesForNewTask)
                {
                    var routeStage = stageWithMachine.RouteStage;
                    var selectedMachine = stageWithMachine.SelectedMachine;

                    double setupTime = CalculateSetupTime(selectedMachine, SelectedDetailForNewTask);
                    double stageHours = (routeStage.StandardTimePerUnit * taskSize) + setupTime;
                    var stageDuration = TimeSpan.FromHours(stageHours);

                    var taskStage = new ProductionTaskStage
                    {
                        ProductionTaskId = newTask.Id,
                        RouteStageId = routeStage.Id,
                        MachineId = selectedMachine.Id,
                        QuantityToProcess = taskSize,
                        OrderInTask = routeStage.OrderInRoute,
                        Status = TaskStatus.Planned,
                        StandardTimePerUnitAtExecution = routeStage.StandardTimePerUnit,
                        PlannedSetupTime = setupTime,
                        PlannedDuration = stageDuration,
                        PlannedStartTime = currentPlannedTime,
                        PlannedEndTime = currentPlannedTime.AddHours(stageHours)
                    };

                    _context.ProductionTaskStages.Add(taskStage);
                    currentPlannedTime = taskStage.PlannedEndTime.Value;
                }

                _context.SaveChanges();
                return newTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateTaskWithSimplePlanning Error: {ex.Message}");
                return null;
            }
        }

        private void ResetNewTaskForm()
        {
            SelectedDetailForNewTask = null;
            RouteStagesForNewTask = new ObservableCollection<RouteStageWithMachine>();
            NewTaskQuantity = 1;
            TotalPlannedTime = "";
            PlannedStartTime = DateTime.Now;
            PlannedEndTime = null;
            PlanningWarnings = "";
            TaskSplitInfo = "";
        }

        private void ExecuteStartTask()
        {
            if (SelectedTask == null) return;

            try
            {
                SelectedTask.Status = TaskStatus.InProgress;
                SelectedTask.ActualStartTime = DateTime.Now;

                _context.SaveChanges();
                LoadActiveProductionTasks();
                MessageBox.Show($"Задание '{SelectedTask.Detail?.Name}' запущено.", "Информация");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка запуска задания: {ex.Message}", "Ошибка");
            }
        }

        private void ExecutePauseTask()
        {
            if (SelectedTask == null) return;

            try
            {
                SelectedTask.Status = TaskStatus.Paused;
                _context.SaveChanges();
                LoadActiveProductionTasks();
                MessageBox.Show($"Задание '{SelectedTask.Detail?.Name}' приостановлено.", "Информация");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка приостановки задания: {ex.Message}", "Ошибка");
            }
        }

        private void ExecuteCompleteTask()
        {
            if (SelectedTask == null) return;

            try
            {
                SelectedTask.Status = TaskStatus.Completed;
                SelectedTask.ActualEndTime = DateTime.Now;
                _context.SaveChanges();
                LoadActiveProductionTasks();
                MessageBox.Show($"Задание '{SelectedTask.Detail?.Name}' завершено.", "Информация");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка завершения задания: {ex.Message}", "Ошибка");
            }
        }

        public void ExecuteStartStage(ProductionTaskStageViewModel stageVM)
        {
            if (stageVM?.TaskStage == null) return;

            try
            {
                stageVM.TaskStage.Status = TaskStatus.InProgress;
                stageVM.TaskStage.ActualStartTime = DateTime.Now;
                _context.SaveChanges();
                LoadTaskStagesDetails();
                MessageBox.Show($"Этап '{stageVM.TaskStage.RouteStage?.OperationName}' запущен.", "Информация");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка запуска этапа: {ex.Message}", "Ошибка");
            }
        }

        public void ExecutePauseStage(ProductionTaskStageViewModel stageVM)
        {
            if (stageVM?.TaskStage == null) return;

            try
            {
                stageVM.TaskStage.Status = TaskStatus.Paused;
                _context.SaveChanges();
                LoadTaskStagesDetails();
                MessageBox.Show($"Этап '{stageVM.TaskStage.RouteStage?.OperationName}' приостановлен.", "Информация");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка приостановки этапа: {ex.Message}", "Ошибка");
            }
        }

        public void ExecuteCompleteStage(ProductionTaskStageViewModel stageVM)
        {
            if (stageVM?.TaskStage == null) return;

            try
            {
                stageVM.TaskStage.Status = TaskStatus.Completed;
                stageVM.TaskStage.ActualEndTime = DateTime.Now;

                if (stageVM.TaskStage.ActualStartTime.HasValue && stageVM.TaskStage.ActualEndTime.HasValue)
                {
                    stageVM.TaskStage.ActualDuration = stageVM.TaskStage.ActualEndTime.Value - stageVM.TaskStage.ActualStartTime.Value;
                }

                _context.SaveChanges();
                LoadTaskStagesDetails();
                MessageBox.Show($"Этап '{stageVM.TaskStage.RouteStage?.OperationName}' завершен.", "Информация");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка завершения этапа: {ex.Message}", "Ошибка");
            }
        }

        public void ExecuteSplitStage(ProductionTaskStageViewModel stageVM)
        {
            if (stageVM?.TaskStage == null) return;

            try
            {
                // Загружаем связанные данные
                var stage = _context.ProductionTaskStages
                    .Include(pts => pts.RouteStage)
                    .FirstOrDefault(pts => pts.Id == stageVM.TaskStage.Id);

                if (stage == null) return;

                var splitVM = new SplitOperationViewModel(stage, _context);
                var splitWindow = new Views.SplitOperationWindow(splitVM);
                splitWindow.Owner = Application.Current.MainWindow;

                if (splitWindow.ShowDialog() == true)
                {
                    LoadTaskStagesDetails();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка разделения этапа: {ex.Message}", "Ошибка");
            }
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _context?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // Оставляем существующие вспомогательные классы
    public class RouteStageWithMachine : ViewModelBase
    {
        public RouteStage RouteStage { get; set; }

        private ObservableCollection<Machine> _availableMachines;
        public ObservableCollection<Machine> AvailableMachines
        {
            get => _availableMachines;
            set => SetProperty(ref _availableMachines, value);
        }

        private Machine _selectedMachine;
        public Machine SelectedMachine
        {
            get => _selectedMachine;
            set => SetProperty(ref _selectedMachine, value);
        }

        // Расчетные поля
        public double PlannedSetupTime { get; set; }
        public TimeSpan PlannedDuration { get; set; }
        public DateTime? PlannedStartTime { get; set; }
        public DateTime? PlannedEndTime { get; set; }

        // Новые поля для планирования со сменами
        public WorkShift AssignedShift { get; set; }
        public bool SplitAcrossShifts { get; set; }

        // Свойства для отображения информации о сменах
        public string ShiftInfo => AssignedShift != null ?
            $"{AssignedShift.Name} ({AssignedShift.StartTime:hh\\:mm}-{AssignedShift.EndTime:hh\\:mm})" :
            "Предпочтительное время";

        public string SplitInfo => SplitAcrossShifts ? "⚡ Разделена" : "";
    }

    public class ProductionTaskStageViewModel : ViewModelBase
    {
        private readonly EmployeeWorkViewModel _parentViewModel;

        public ProductionTaskStage TaskStage { get; }

        public ProductionTaskStageViewModel(ProductionTaskStage taskStage, EmployeeWorkViewModel parentViewModel)
        {
            TaskStage = taskStage;
            _parentViewModel = parentViewModel;

            StartCommand = new RelayCommand(() => _parentViewModel.ExecuteStartStage(this),
                                          () => TaskStage.Status == TaskStatus.Planned);
            PauseCommand = new RelayCommand(() => _parentViewModel.ExecutePauseStage(this),
                                          () => TaskStage.Status == TaskStatus.InProgress);
            CompleteCommand = new RelayCommand(() => _parentViewModel.ExecuteCompleteStage(this),
                                             () => TaskStage.Status == TaskStatus.InProgress);
            SplitCommand = new RelayCommand(() => _parentViewModel.ExecuteSplitStage(this),
                                          () => TaskStage.Status == TaskStatus.Planned && !TaskStage.ParentProductionTaskStageId.HasValue);
        }

        public ICommand StartCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand CompleteCommand { get; }
        public ICommand SplitCommand { get; }
    }

    // Класс для отображения конфликтов станков
    public class MachineConflictViewModel : ViewModelBase
    {
        public string OperationName { get; set; }
        public string MachineName { get; set; }
        public DateTime ConflictTime { get; set; }
        public string ConflictingTaskInfo { get; set; }
        public TimeSpan SuggestedWaitTime { get; set; }
        public List<MachineAlternative> Alternatives { get; set; } = new List<MachineAlternative>();

        public string ConflictTimeFormatted => ConflictTime.ToString("dd.MM.yy HH:mm");
        public string SuggestedWaitTimeFormatted => $"{SuggestedWaitTime.TotalMinutes:F0} мин";
        public string AlternativesCount => $"{Alternatives.Count(a => a.IsAvailable)} доступно";
        public string BestAlternative => Alternatives.FirstOrDefault(a => a.IsAvailable)?.Machine?.Name ?? "Нет доступных";
    }
}
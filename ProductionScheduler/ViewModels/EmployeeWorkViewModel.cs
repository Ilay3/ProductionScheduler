// File: ViewModels/EmployeeWorkViewModel.cs
using ProductionScheduler.Data;
using ProductionScheduler.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input; // Для ICommand
using Microsoft.EntityFrameworkCore; // Для Include
using System.Collections.Generic; // Для List
using System;
using System.Windows; // Для DateTime

namespace ProductionScheduler.ViewModels
{
    public class EmployeeWorkViewModel : ViewModelBase
    {
        private readonly ApplicationDbContext _context;

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
            set => SetProperty(ref _selectedDetailForNewTask, value);
        }

        private int _newTaskQuantity;
        public int NewTaskQuantity
        {
            get => _newTaskQuantity;
            set => SetProperty(ref _newTaskQuantity, value);
        }
        #endregion

        #region Properties for Displaying Tasks and Stages (пока просто список)

        private ObservableCollection<ProductionTask> _activeProductionTasks;
        public ObservableCollection<ProductionTask> ActiveProductionTasks
        {
            get => _activeProductionTasks;
            set => SetProperty(ref _activeProductionTasks, value);
        }


        #endregion

        #region Commands
        public ICommand CreateNewTaskCommand { get; }
        public ICommand RefreshDataCommand { get; }
        // TODO: Команды для управления Гантом, разделения, фиксации времени и т.д.
        #endregion

        public EmployeeWorkViewModel()
        {
            _context = new ApplicationDbContext();

            CreateNewTaskCommand = new RelayCommand(ExecuteCreateNewTask, CanExecuteCreateNewTask);
            RefreshDataCommand = new RelayCommand(LoadAllData);

            LoadAllData();
            NewTaskQuantity = 1; // Значение по умолчанию
        }

        private void LoadAllData()
        {
            LoadAvailableDetails();
            LoadActiveProductionTasks();
            // TODO: Загрузка других необходимых данных (например, станки для выбора)
        }

        private void LoadAvailableDetails()
        {
            // Загружаем детали, у которых есть хотя бы один этап в маршруте
            AvailableDetails = new ObservableCollection<Detail>(
                _context.Details
                        .Where(d => d.RouteStages.Any())
                        .OrderBy(d => d.Name)
                        .ToList());
        }

        private void LoadActiveProductionTasks()
        {
            // Загружаем незавершенные задачи
            ActiveProductionTasks = new ObservableCollection<ProductionTask>(
                _context.ProductionTasks
                        .Include(pt => pt.Detail) // Загружаем связанную деталь
                        .Include(pt => pt.TaskStages) // Загружаем этапы задания
                            .ThenInclude(pts => pts.RouteStage) // Внутри этапов задания загружаем нормативный этап
                                .ThenInclude(rs => rs.ApplicableMachineType) // И тип станка для нормативного этапа
                        .Include(pt => pt.TaskStages)
                            .ThenInclude(pts => pts.AssignedMachine) // И конкретный назначенный станок
                                .ThenInclude(m => m.MachineType)   // И его тип
                        .Where(pt => pt.Status != Models.TaskStatus.Completed && pt.Status != Models.TaskStatus.Cancelled)
                        .OrderByDescending(pt => pt.CreationTime)
                        .ToList());
        }

        private bool CanExecuteCreateNewTask()
        {
            return SelectedDetailForNewTask != null && NewTaskQuantity > 0;
        }

        private void ExecuteCreateNewTask()
        {
            if (!CanExecuteCreateNewTask()) return;

            // 1. Получить маршрут для выбранной детали
            var routeStagesForDetail = _context.RouteStages
                                        .Where(rs => rs.DetailId == SelectedDetailForNewTask.Id)
                                        .OrderBy(rs => rs.OrderInRoute)
                                        .ToList();

            if (!routeStagesForDetail.Any())
            {
                MessageBox.Show($"Для детали '{SelectedDetailForNewTask.Name}' не определен маршрут.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Создать ProductionTask
            var newTask = new ProductionTask
            {
                DetailId = SelectedDetailForNewTask.Id,
                Quantity = NewTaskQuantity,
                CreationTime = DateTime.Now,
                Status = Models.TaskStatus.Planned
                // PlannedStartTime/EndTime будут рассчитаны позже, при размещении на Ганте/выборе станков
            };

            // 3. Создать ProductionTaskStages на основе RouteStages
            foreach (var routeStage in routeStagesForDetail)
            {
                var taskStage = new ProductionTaskStage
                {
                    ProductionTask = newTask, // EF Core сам установит ProductionTaskId
                    RouteStageId = routeStage.Id,
                    QuantityToProcess = NewTaskQuantity, // Пока что все детали идут через все этапы
                    OrderInTask = routeStage.OrderInRoute,
                    Status = Models.TaskStatus.Planned,
                    StandardTimePerUnitAtExecution = routeStage.StandardTimePerUnit,
                    // MachineId, PlannedStartTime, PlannedEndTime, PlannedDuration - будут определены на следующем этапе планирования
                };
                newTask.TaskStages.Add(taskStage);
            }

            _context.ProductionTasks.Add(newTask);

            try
            {
                _context.SaveChanges();
                MessageBox.Show($"Новое задание на деталь '{SelectedDetailForNewTask.Name}' ({NewTaskQuantity} шт.) создано.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadActiveProductionTasks(); // Обновить список активных заданий

                // Сбросить поля для нового задания
                SelectedDetailForNewTask = null;
                NewTaskQuantity = 1;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка создания задания: {ex.Message}\n{ex.InnerException?.Message}", "Ошибка БД", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
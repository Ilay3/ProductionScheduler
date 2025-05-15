// File: Services/SmartPlanningService.cs - Улучшенный сервис планирования
using ProductionScheduler.Data;
using ProductionScheduler.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace ProductionScheduler.Services
{
    public class SmartPlanningService
    {
        private readonly ApplicationDbContext _context;
        private readonly WorkShift[] _workShifts;

        public SmartPlanningService(ApplicationDbContext context)
        {
            _context = context;
            _workShifts = WorkShift.GetStandardShifts();
        }

        // Планирование с разделением задач и интеллектуальным выбором станков
        public async Task<SmartPlanningResult> PlanTasksWithSplitting(
            Detail detail,
            int totalQuantity,
            DateTime preferredStartTime,
            int maxTaskSize = 10,
            bool allowAlternativeMachines = true)
        {
            var result = new SmartPlanningResult
            {
                OriginalDetail = detail,
                TotalQuantity = totalQuantity,
                TaskPlans = new List<TaskPlan>(),
                PlanningWarnings = new List<string>(),
                AlternativeOptions = new List<AlternativeOption>()
            };

            // Получаем маршрут детали
            var routeStages = await _context.RouteStages
                .Include(rs => rs.ApplicableMachineType)
                .ThenInclude(mt => mt.Machines)
                .Where(rs => rs.DetailId == detail.Id)
                .OrderBy(rs => rs.OrderInRoute)
                .ToListAsync();

            if (!routeStages.Any())
            {
                result.PlanningWarnings.Add("Для детали не найден маршрут обработки");
                return result;
            }

            // Разделяем задачу на подзадачи
            var taskSizes = CalculateTaskSizes(totalQuantity, maxTaskSize);
            var currentTime = preferredStartTime;

            for (int taskIndex = 0; taskIndex < taskSizes.Count; taskIndex++)
            {
                var taskQuantity = taskSizes[taskIndex];
                var taskPlan = await PlanSingleTask(detail, taskQuantity, currentTime,
                    routeStages, allowAlternativeMachines, taskIndex + 1);

                result.TaskPlans.Add(taskPlan);
                result.PlanningWarnings.AddRange(taskPlan.Warnings);
                result.AlternativeOptions.AddRange(taskPlan.AlternativeOptions);

                // Сдвигаем время для следующей задачи с небольшим буфером
                currentTime = taskPlan.PlannedEndTime.AddMinutes(15);
            }

            return result;
        }

        private List<int> CalculateTaskSizes(int totalQuantity, int maxTaskSize)
        {
            var sizes = new List<int>();
            var remaining = totalQuantity;

            while (remaining > 0)
            {
                var currentSize = Math.Min(remaining, maxTaskSize);
                sizes.Add(currentSize);
                remaining -= currentSize;
            }

            return sizes;
        }

        private async Task<TaskPlan> PlanSingleTask(
            Detail detail,
            int quantity,
            DateTime startTime,
            List<RouteStage> routeStages,
            bool allowAlternativeMachines,
            int taskNumber)
        {
            var taskPlan = new TaskPlan
            {
                TaskNumber = taskNumber,
                Detail = detail,
                Quantity = quantity,
                PlannedStartTime = startTime,
                StageAssignments = new List<StageAssignment>(),
                Warnings = new List<string>(),
                AlternativeOptions = new List<AlternativeOption>()
            };

            var currentTime = startTime;

            foreach (var routeStage in routeStages)
            {
                var assignment = await PlanStageWithSmartMachineSelection(
                    routeStage, quantity, currentTime, allowAlternativeMachines);

                if (assignment == null)
                {
                    taskPlan.Warnings.Add($"Не удалось найти станок для операции {routeStage.OperationName}");
                    continue;
                }

                assignment.PlannedStartTime = currentTime;
                assignment.PlannedEndTime = currentTime.Add(assignment.Duration);

                taskPlan.StageAssignments.Add(assignment);
                currentTime = assignment.PlannedEndTime.Value;

                // Добавляем альтернативные варианты если есть
                if (assignment.AlternativeMachines.Any())
                {
                    taskPlan.AlternativeOptions.Add(new AlternativeOption
                    {
                        StageId = routeStage.Id,
                        StageName = routeStage.OperationName,
                        SelectedMachine = assignment.Machine,
                        AlternativeMachines = assignment.AlternativeMachines,
                        Reason = assignment.SelectionReason
                    });
                }
            }

            taskPlan.PlannedEndTime = currentTime;
            taskPlan.TotalDuration = taskPlan.PlannedEndTime - taskPlan.PlannedStartTime;

            return taskPlan;
        }

        private async Task<StageAssignment> PlanStageWithSmartMachineSelection(
            RouteStage routeStage,
            int quantity,
            DateTime preferredStartTime,
            bool allowAlternatives)
        {
            // Получаем все подходящие станки
            var availableMachines = await _context.Machines
                .Where(m => m.MachineTypeId == routeStage.MachineTypeId)
                .ToListAsync();

            if (!availableMachines.Any())
            {
                return null;
            }

            // Анализируем загрузку каждого станка
            var machineAnalysis = new List<MachineAnalysis>();

            foreach (var machine in availableMachines)
            {
                var analysis = await AnalyzeMachineAvailability(machine, preferredStartTime, routeStage, quantity);
                machineAnalysis.Add(analysis);
            }

            // Сортируем по приоритету
            var sortedMachines = machineAnalysis
                .OrderBy(ma => ma.ConflictLevel)
                .ThenBy(ma => ma.EarliestAvailableTime)
                .ThenBy(ma => ma.SetupTime)
                .ToList();

            var bestMachine = sortedMachines.First();
            var alternatives = sortedMachines.Skip(1).Take(3).ToList();

            var assignment = new StageAssignment
            {
                RouteStage = routeStage,
                Machine = bestMachine.Machine,
                Quantity = quantity,
                SetupTime = bestMachine.SetupTime,
                ProcessingTime = routeStage.StandardTimePerUnit * quantity,
                Duration = TimeSpan.FromHours(bestMachine.SetupTime + (routeStage.StandardTimePerUnit * quantity)),
                SelectionReason = bestMachine.SelectionReason,
                AlternativeMachines = alternatives.Select(a => new AlternativeMachine
                {
                    Machine = a.Machine,
                    Reason = a.SelectionReason,
                    EarliestAvailableTime = a.EarliestAvailableTime,
                    SetupTime = a.SetupTime
                }).ToList()
            };

            return assignment;
        }

        private async Task<MachineAnalysis> AnalyzeMachineAvailability(
            Machine machine,
            DateTime preferredStartTime,
            RouteStage routeStage,
            int quantity)
        {
            var analysis = new MachineAnalysis
            {
                Machine = machine,
                EarliestAvailableTime = preferredStartTime
            };

            // Проверяем текущую загрузку станка
            var activeTaskStages = await _context.ProductionTaskStages
                .Include(pts => pts.ProductionTask)
                .ThenInclude(pt => pt.Detail)
                .Where(pts => pts.MachineId == machine.Id &&
                             pts.Status != Models.TaskStatus.Completed &&
                             pts.Status != Models.TaskStatus.Cancelled)
                .OrderBy(pts => pts.PlannedStartTime)
                .ToListAsync();

            // Рассчитываем конфликты
            var requiredDuration = TimeSpan.FromHours(routeStage.StandardTimePerUnit * quantity);
            var proposedEndTime = preferredStartTime.Add(requiredDuration);

            foreach (var taskStage in activeTaskStages)
            {
                if (taskStage.PlannedStartTime.HasValue && taskStage.PlannedEndTime.HasValue)
                {
                    // Проверяем пересечение
                    if (preferredStartTime < taskStage.PlannedEndTime &&
                        proposedEndTime > taskStage.PlannedStartTime)
                    {
                        analysis.ConflictLevel++;
                        analysis.ConflictingTasks.Add(taskStage);

                        // Обновляем earliest available time
                        if (taskStage.PlannedEndTime > analysis.EarliestAvailableTime)
                        {
                            analysis.EarliestAvailableTime = taskStage.PlannedEndTime.Value;
                        }
                    }
                }
            }

            // Рассчитываем время переналадки
            analysis.SetupTime = await CalculateSetupTime(machine, routeStage);

            // Определяем причину выбора
            if (analysis.ConflictLevel == 0)
            {
                analysis.SelectionReason = "Станок свободен в требуемое время";
            }
            else
            {
                var delayHours = (analysis.EarliestAvailableTime - preferredStartTime).TotalHours;
                analysis.SelectionReason = $"Задержка {delayHours:F1}ч из-за {analysis.ConflictLevel} конфликт(ов)";
            }

            return analysis;
        }

        private async Task<double> CalculateSetupTime(Machine machine, RouteStage routeStage)
        {
            // Находим последнюю операцию на станке
            var lastOperation = await _context.ProductionTaskStages
                .Include(pts => pts.ProductionTask)
                .ThenInclude(pt => pt.Detail)
                .Where(pts => pts.MachineId == machine.Id && pts.ActualEndTime.HasValue)
                .OrderByDescending(pts => pts.ActualEndTime)
                .FirstOrDefaultAsync();

            if (lastOperation == null)
            {
                return 0; // Первая операция
            }

            // Если та же деталь - переналадка не нужна
            if (lastOperation.ProductionTask.Detail.Id == routeStage.DetailId)
            {
                return 0;
            }

            return 10.0 / 60.0; // 10 минут в часах
        }

        // Оптимизация планирования с учетом смен
        public async Task<SmartPlanningResult> OptimizePlanningWithShifts(SmartPlanningResult planningResult)
        {
            foreach (var taskPlan in planningResult.TaskPlans)
            {
                await OptimizeTaskWithShifts(taskPlan);
            }

            return planningResult;
        }

        private async Task OptimizeTaskWithShifts(TaskPlan taskPlan)
        {
            foreach (var assignment in taskPlan.StageAssignments)
            {
                var optimizedSchedule = FindOptimalShiftSchedule(
                    assignment.PlannedStartTime.Value,
                    assignment.Duration);

                if (optimizedSchedule != null)
                {
                    assignment.PlannedStartTime = optimizedSchedule.OptimalStartTime;
                    assignment.PlannedEndTime = optimizedSchedule.OptimalEndTime;
                    assignment.AssignedShifts = optimizedSchedule.Shifts;
                    assignment.ShiftBreaks = optimizedSchedule.Breaks;
                }
            }

            // Пересчитываем общее время задачи
            if (taskPlan.StageAssignments.Any())
            {
                taskPlan.PlannedStartTime = taskPlan.StageAssignments.First().PlannedStartTime.Value;
                taskPlan.PlannedEndTime = taskPlan.StageAssignments.Last().PlannedEndTime.Value;
                taskPlan.TotalDuration = taskPlan.PlannedEndTime - taskPlan.PlannedStartTime;
            }
        }

        private ShiftOptimization FindOptimalShiftSchedule(DateTime plannedStart, TimeSpan duration)
        {
            var currentTime = plannedStart;
            var remainingDuration = duration;
            var shifts = new List<WorkShift>();
            var breaks = new List<TimeSpan>();

            while (remainingDuration > TimeSpan.Zero)
            {
                var currentShift = FindShiftForTime(currentTime);
                if (currentShift == null) break;

                var shiftEnd = GetShiftEndTime(currentTime.Date, currentShift);
                var availableTimeInShift = shiftEnd - currentTime;

                // Учитываем обеденный перерыв
                if (currentTime.TimeOfDay < currentShift.BreakStart &&
                    currentTime.Add(remainingDuration).TimeOfDay > currentShift.BreakStart)
                {
                    var breakDuration = currentShift.BreakEnd - currentShift.BreakStart;
                    breaks.Add(breakDuration);
                    availableTimeInShift -= breakDuration;
                }

                if (availableTimeInShift >= remainingDuration)
                {
                    // Операция помещается в текущую смену
                    shifts.Add(currentShift);
                    break;
                }
                else
                {
                    // Нужно разделить на смены
                    shifts.Add(currentShift);
                    remainingDuration -= availableTimeInShift;

                    // Переходим к следующей смене
                    var nextShift = GetNextShift(currentShift);
                    currentTime = GetShiftStartTime(currentTime.Date.AddDays(
                        nextShift.ShiftType == ShiftType.First ? 1 : 0), nextShift);
                }
            }

            return new ShiftOptimization
            {
                OptimalStartTime = plannedStart,
                OptimalEndTime = plannedStart.Add(duration).Add(TimeSpan.FromMinutes(breaks.Sum(b => b.TotalMinutes))),
                Shifts = shifts,
                Breaks = breaks
            };
        }

        private WorkShift FindShiftForTime(DateTime time)
        {
            return _workShifts.FirstOrDefault(s => s.IsTimeInShift(time));
        }

        private DateTime GetShiftStartTime(DateTime date, WorkShift shift)
        {
            return date.Add(shift.StartTime);
        }

        private DateTime GetShiftEndTime(DateTime date, WorkShift shift)
        {
            if (shift.ShiftType == ShiftType.Second && shift.EndTime < shift.StartTime)
            {
                return date.AddDays(1).Add(shift.EndTime);
            }
            return date.Add(shift.EndTime);
        }

        private WorkShift GetNextShift(WorkShift currentShift)
        {
            return currentShift.ShiftType switch
            {
                ShiftType.First => _workShifts.First(s => s.ShiftType == ShiftType.Second),
                ShiftType.Second => _workShifts.First(s => s.ShiftType == ShiftType.Third),
                ShiftType.Third => _workShifts.First(s => s.ShiftType == ShiftType.First),
                _ => _workShifts.First()
            };
        }
    }

    // Вспомогательные классы
    public class SmartPlanningResult
    {
        public Detail OriginalDetail { get; set; }
        public int TotalQuantity { get; set; }
        public List<TaskPlan> TaskPlans { get; set; } = new List<TaskPlan>();
        public List<string> PlanningWarnings { get; set; } = new List<string>();
        public List<AlternativeOption> AlternativeOptions { get; set; } = new List<AlternativeOption>();
        public DateTime? EarliestStartTime { get; set; }
        public DateTime? LatestEndTime { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public int TotalTasks => TaskPlans.Count;
    }

    public class TaskPlan
    {
        public int TaskNumber { get; set; }
        public Detail Detail { get; set; }
        public int Quantity { get; set; }
        public DateTime PlannedStartTime { get; set; }
        public DateTime PlannedEndTime { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public List<StageAssignment> StageAssignments { get; set; } = new List<StageAssignment>();
        public List<string> Warnings { get; set; } = new List<string>();
        public List<AlternativeOption> AlternativeOptions { get; set; } = new List<AlternativeOption>();
    }

    public class StageAssignment
    {
        public RouteStage RouteStage { get; set; }
        public Machine Machine { get; set; }
        public int Quantity { get; set; }
        public double SetupTime { get; set; }
        public double ProcessingTime { get; set; }
        public TimeSpan Duration { get; set; }
        public DateTime? PlannedStartTime { get; set; }
        public DateTime? PlannedEndTime { get; set; }
        public string SelectionReason { get; set; }
        public List<AlternativeMachine> AlternativeMachines { get; set; } = new List<AlternativeMachine>();
        public List<WorkShift> AssignedShifts { get; set; } = new List<WorkShift>();
        public List<TimeSpan> ShiftBreaks { get; set; } = new List<TimeSpan>();
    }

    public class MachineAnalysis
    {
        public Machine Machine { get; set; }
        public int ConflictLevel { get; set; }
        public DateTime EarliestAvailableTime { get; set; }
        public double SetupTime { get; set; }
        public string SelectionReason { get; set; }
        public List<ProductionTaskStage> ConflictingTasks { get; set; } = new List<ProductionTaskStage>();
    }

    public class AlternativeOption
    {
        public int StageId { get; set; }
        public string StageName { get; set; }
        public Machine SelectedMachine { get; set; }
        public List<AlternativeMachine> AlternativeMachines { get; set; } = new List<AlternativeMachine>();
        public string Reason { get; set; }
    }

    public class AlternativeMachine
    {
        public Machine Machine { get; set; }
        public string Reason { get; set; }
        public DateTime EarliestAvailableTime { get; set; }
        public double SetupTime { get; set; }
    }

    public class ShiftOptimization
    {
        public DateTime OptimalStartTime { get; set; }
        public DateTime OptimalEndTime { get; set; }
        public List<WorkShift> Shifts { get; set; } = new List<WorkShift>();
        public List<TimeSpan> Breaks { get; set; } = new List<TimeSpan>();
    }
}
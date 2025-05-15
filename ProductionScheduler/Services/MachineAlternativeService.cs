// File: Services/MachineAlternativeService.cs
using ProductionScheduler.Data;
using ProductionScheduler.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using ProductionScheduler.ViewModels;
using TaskStatus = ProductionScheduler.Models.TaskStatus;

namespace ProductionScheduler.Services
{
    public class MachineAlternativeService
    {
        private readonly ApplicationDbContext _context;

        public MachineAlternativeService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Проверяет конфликты назначения станков и предлагает альтернативы
        public MachineConflictAnalysis AnalyzeMachineConflicts(List<RouteStageWithMachine> routeStages, DateTime startTime)
        {
            var analysis = new MachineConflictAnalysis
            {
                HasConflicts = false,
                Conflicts = new List<MachineConflict>(),
                Alternatives = new List<MachineAlternative>()
            };

            foreach (var stageWithMachine in routeStages)
            {
                if (stageWithMachine.SelectedMachine == null) continue;

                var conflict = CheckMachineAvailability(stageWithMachine, startTime);
                if (conflict != null)
                {
                    analysis.HasConflicts = true;
                    analysis.Conflicts.Add(conflict);

                    // Ищем альтернативные станки
                    var alternatives = FindAlternativeMachines(stageWithMachine.RouteStage, conflict.ConflictTime);
                    analysis.Alternatives.AddRange(alternatives);
                }
            }

            return analysis;
        }

        private MachineConflict CheckMachineAvailability(RouteStageWithMachine stageWithMachine, DateTime startTime)
        {
            var machine = stageWithMachine.SelectedMachine;
            var plannedStart = stageWithMachine.PlannedStartTime ?? startTime;
            var plannedEnd = stageWithMachine.PlannedEndTime ?? plannedStart.AddHours(stageWithMachine.RouteStage.StandardTimePerUnit);

            // Ищем конфликтующие задачи на этом станке
            var conflictingTasks = _context.ProductionTaskStages
                .Include(pts => pts.ProductionTask)
                .ThenInclude(pt => pt.Detail)
                .Include(pts => pts.RouteStage)
                .Where(pts => pts.MachineId == machine.Id &&
                             pts.Status != TaskStatus.Completed &&
                             pts.Status != TaskStatus.Cancelled &&
                             pts.PlannedStartTime.HasValue &&
                             pts.PlannedEndTime.HasValue)
                .Where(pts => pts.PlannedStartTime < plannedEnd && pts.PlannedEndTime > plannedStart)
                .ToList();

            if (conflictingTasks.Any())
            {
                var firstConflict = conflictingTasks.OrderBy(ct => ct.PlannedStartTime).First();
                return new MachineConflict
                {
                    RouteStage = stageWithMachine.RouteStage,
                    Machine = machine,
                    ConflictTime = firstConflict.PlannedStartTime.Value,
                    ConflictingTask = firstConflict,
                    SuggestedWaitTime = firstConflict.PlannedEndTime.Value - plannedStart
                };
            }

            return null;
        }

        private List<MachineAlternative> FindAlternativeMachines(RouteStage routeStage, DateTime conflictTime)
        {
            var alternatives = new List<MachineAlternative>();

            // Ищем все станки подходящего типа
            var allMachines = _context.Machines
                .Include(m => m.MachineType)
                .Where(m => m.MachineTypeId == routeStage.MachineTypeId)
                .ToList();

            foreach (var machine in allMachines)
            {
                var availability = CheckMachineAvailabilityAtTime(machine, conflictTime, routeStage.StandardTimePerUnit);
                alternatives.Add(new MachineAlternative
                {
                    Machine = machine,
                    IsAvailable = availability.IsAvailable,
                    EarliestAvailableTime = availability.EarliestAvailableTime,
                    LoadFactor = availability.LoadFactor,
                    Priority = CalculateMachinePriority(machine, availability)
                });
            }

            return alternatives.OrderBy(a => a.Priority).ThenBy(a => a.EarliestAvailableTime).ToList();
        }

        private MachineAvailability CheckMachineAvailabilityAtTime(Machine machine, DateTime startTime, double operationDuration)
        {
            var endTime = startTime.AddHours(operationDuration);

            // Проверяем загрузку станка
            var tasksOnMachine = _context.ProductionTaskStages
                .Where(pts => pts.MachineId == machine.Id &&
                             pts.Status != TaskStatus.Completed &&
                             pts.Status != TaskStatus.Cancelled &&
                             pts.PlannedStartTime.HasValue &&
                             pts.PlannedEndTime.HasValue)
                .OrderBy(pts => pts.PlannedStartTime)
                .ToList();

            // Проверяем конфликты
            var hasConflict = tasksOnMachine.Any(pts =>
                pts.PlannedStartTime < endTime && pts.PlannedEndTime > startTime);

            DateTime earliestAvailable = startTime;
            if (hasConflict)
            {
                // Находим первое свободное время
                var lastTask = tasksOnMachine
                    .Where(pts => pts.PlannedStartTime <= endTime)
                    .OrderByDescending(pts => pts.PlannedEndTime)
                    .FirstOrDefault();

                if (lastTask?.PlannedEndTime.HasValue == true)
                {
                    earliestAvailable = lastTask.PlannedEndTime.Value;
                }
            }

            // Рассчитываем коэффициент загрузки (для приоритета)
            var totalPlannedTime = tasksOnMachine.Sum(pts => pts.PlannedDuration.TotalHours);
            var loadFactor = totalPlannedTime / 24.0; // Приблизительная загрузка на день

            return new MachineAvailability
            {
                IsAvailable = !hasConflict,
                EarliestAvailableTime = earliestAvailable,
                LoadFactor = loadFactor
            };
        }

        private int CalculateMachinePriority(Machine machine, MachineAvailability availability)
        {
            int priority = 0;

            // Свободные станки имеют приоритет
            if (availability.IsAvailable)
                priority -= 100;

            // Менее загруженные станки предпочтительнее
            priority += (int)(availability.LoadFactor * 50);

            // Можно добавить другие критерии:
            // - Производительность станка
            // - Приоритет из справочника станков
            // - Близость к предыдущей операции

            return priority;
        }

        // Автоматически выбирает лучший станок из альтернатив
        public Machine SelectBestAlternative(RouteStage routeStage, DateTime plannedTime)
        {
            var alternatives = FindAlternativeMachines(routeStage, plannedTime);
            return alternatives.FirstOrDefault(a => a.IsAvailable)?.Machine ?? alternatives.FirstOrDefault()?.Machine;
        }
    }

    // Классы для анализа конфликтов станков
    public class MachineConflictAnalysis
    {
        public bool HasConflicts { get; set; }
        public List<MachineConflict> Conflicts { get; set; } = new List<MachineConflict>();
        public List<MachineAlternative> Alternatives { get; set; } = new List<MachineAlternative>();
    }

    public class MachineConflict
    {
        public RouteStage RouteStage { get; set; }
        public Machine Machine { get; set; }
        public DateTime ConflictTime { get; set; }
        public ProductionTaskStage ConflictingTask { get; set; }
        public TimeSpan SuggestedWaitTime { get; set; }
    }

    public class MachineAlternative
    {
        public Machine Machine { get; set; }
        public bool IsAvailable { get; set; }
        public DateTime EarliestAvailableTime { get; set; }
        public double LoadFactor { get; set; }
        public int Priority { get; set; }
    }

    public class MachineAvailability
    {
        public bool IsAvailable { get; set; }
        public DateTime EarliestAvailableTime { get; set; }
        public double LoadFactor { get; set; }
    }
}
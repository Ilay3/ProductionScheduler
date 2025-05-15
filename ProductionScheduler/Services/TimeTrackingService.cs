// File: Services/TimeTrackingService.cs
using ProductionScheduler.Data;
using ProductionScheduler.Models;
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using TaskStatus = ProductionScheduler.Models.TaskStatus;

namespace ProductionScheduler.Services
{
    public class TimeTrackingService
    {
        private readonly ApplicationDbContext _context;

        public TimeTrackingService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Начать отслеживание времени для задания
        public bool StartTaskTracking(int taskId)
        {
            try
            {
                var task = _context.ProductionTasks.Find(taskId);
                if (task == null) return false;

                task.ActualStartTime = DateTime.Now;
                task.Status = TaskStatus.InProgress;
                _context.SaveChanges();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StartTaskTracking Error: {ex.Message}");
                return false;
            }
        }

        // Завершить отслеживание времени для задания
        public bool CompleteTaskTracking(int taskId)
        {
            try
            {
                var task = _context.ProductionTasks.Find(taskId);
                if (task == null) return false;

                task.ActualEndTime = DateTime.Now;
                task.Status = TaskStatus.Completed;

                // Рассчитываем фактическую длительность
                if (task.ActualStartTime.HasValue)
                {
                    var actualDuration = task.ActualEndTime.Value - task.ActualStartTime.Value;
                    task.Notes += $" | Фактическое время: {actualDuration:hh\\:mm}";
                }

                _context.SaveChanges();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CompleteTaskTracking Error: {ex.Message}");
                return false;
            }
        }

        // Приостановить задание
        public bool PauseTaskTracking(int taskId)
        {
            try
            {
                var task = _context.ProductionTasks.Find(taskId);
                if (task == null) return false;

                task.Status = TaskStatus.Paused;
                _context.SaveChanges();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PauseTaskTracking Error: {ex.Message}");
                return false;
            }
        }

        // Начать отслеживание времени для этапа
        public bool StartStageTracking(int stageId)
        {
            try
            {
                var stage = _context.ProductionTaskStages.Find(stageId);
                if (stage == null) return false;

                stage.ActualStartTime = DateTime.Now;
                stage.Status = TaskStatus.InProgress;
                _context.SaveChanges();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StartStageTracking Error: {ex.Message}");
                return false;
            }
        }

        // Завершить отслеживание времени для этапа
        public bool CompleteStageTracking(int stageId)
        {
            try
            {
                var stage = _context.ProductionTaskStages.Find(stageId);
                if (stage == null) return false;

                stage.ActualEndTime = DateTime.Now;
                stage.Status = TaskStatus.Completed;

                // Рассчитываем фактическую длительность
                if (stage.ActualStartTime.HasValue)
                {
                    stage.ActualDuration = stage.ActualEndTime.Value - stage.ActualStartTime.Value;
                }

                _context.SaveChanges();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CompleteStageTracking Error: {ex.Message}");
                return false;
            }
        }

        // Приостановить этап
        public bool PauseStageTracking(int stageId)
        {
            try
            {
                var stage = _context.ProductionTaskStages.Find(stageId);
                if (stage == null) return false;

                stage.Status = TaskStatus.Paused;
                _context.SaveChanges();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PauseStageTracking Error: {ex.Message}");
                return false;
            }
        }

        // Получить статистику выполнения
        public ExecutionStatistics GetExecutionStatistics(int taskId)
        {
            try
            {
                var task = _context.ProductionTasks
                    .Include(pt => pt.TaskStages)
                    .ThenInclude(pts => pts.RouteStage)
                    .FirstOrDefault(pt => pt.Id == taskId);

                if (task == null) return null;

                var stats = new ExecutionStatistics
                {
                    TaskId = taskId,
                    PlannedDuration = task.PlannedEndTime.HasValue && task.PlannedStartTime.HasValue
                        ? task.PlannedEndTime.Value - task.PlannedStartTime.Value
                        : TimeSpan.Zero,
                    ActualDuration = task.ActualEndTime.HasValue && task.ActualStartTime.HasValue
                        ? task.ActualEndTime.Value - task.ActualStartTime.Value
                        : TimeSpan.Zero,
                    StageStatistics = new List<StageStatistics>()
                };

                foreach (var stage in task.TaskStages)
                {
                    var stageStat = new StageStatistics
                    {
                        StageId = stage.Id,
                        StageName = stage.RouteStage?.OperationName ?? "Неизвестный этап",
                        PlannedDuration = stage.PlannedDuration,
                        ActualDuration = stage.ActualDuration ?? TimeSpan.Zero,
                        Efficiency = CalculateEfficiency(stage.PlannedDuration, stage.ActualDuration),
                        Deviation = CalculateDeviation(stage.PlannedDuration, stage.ActualDuration)
                    };

                    stats.StageStatistics.Add(stageStat);
                }

                stats.OverallEfficiency = CalculateEfficiency(stats.PlannedDuration, stats.ActualDuration);
                stats.OverallDeviation = CalculateDeviation(stats.PlannedDuration, stats.ActualDuration);

                return stats;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetExecutionStatistics Error: {ex.Message}");
                return null;
            }
        }

        // Получить текущую длительность для активного этапа
        public TimeSpan GetCurrentDuration(int stageId)
        {
            try
            {
                var stage = _context.ProductionTaskStages.Find(stageId);
                if (stage?.ActualStartTime.HasValue == true && stage.Status == TaskStatus.InProgress)
                {
                    return DateTime.Now - stage.ActualStartTime.Value;
                }
                return TimeSpan.Zero;
            }
            catch
            {
                return TimeSpan.Zero;
            }
        }

        // Получить отклонение от нормы в процентах
        public double GetDeviationPercentage(int stageId)
        {
            try
            {
                var stage = _context.ProductionTaskStages.Find(stageId);
                if (stage == null || !stage.ActualStartTime.HasValue) return 0;

                var actualDuration = stage.ActualEndTime.HasValue
                    ? stage.ActualDuration.Value
                    : DateTime.Now - stage.ActualStartTime.Value;

                if (stage.PlannedDuration.TotalMinutes > 0)
                {
                    return ((actualDuration.TotalMinutes - stage.PlannedDuration.TotalMinutes) / stage.PlannedDuration.TotalMinutes) * 100;
                }

                return 0;
            }
            catch
            {
                return 0;
            }
        }

        private double CalculateEfficiency(TimeSpan planned, TimeSpan? actual)
        {
            if (!actual.HasValue || actual.Value.TotalMinutes == 0 || planned.TotalMinutes == 0)
                return 0;

            return (planned.TotalMinutes / actual.Value.TotalMinutes) * 100;
        }

        private TimeSpan CalculateDeviation(TimeSpan planned, TimeSpan? actual)
        {
            if (!actual.HasValue)
                return TimeSpan.Zero;

            return actual.Value - planned;
        }

        // Автоматическое обновление фактического времени для активных этапов
        public void UpdateActiveStagesDuration()
        {
            try
            {
                var activeStages = _context.ProductionTaskStages
                    .Where(pts => pts.Status == TaskStatus.InProgress && pts.ActualStartTime.HasValue)
                    .ToList();

                foreach (var stage in activeStages)
                {
                    // Обновляем текущую длительность для отображения в UI
                    // Фактическая длительность будет установлена при завершении этапа
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateActiveStagesDuration Error: {ex.Message}");
            }
        }
    }

    // Классы для статистики выполнения
    public class ExecutionStatistics
    {
        public int TaskId { get; set; }
        public TimeSpan PlannedDuration { get; set; }
        public TimeSpan ActualDuration { get; set; }
        public double OverallEfficiency { get; set; }
        public TimeSpan OverallDeviation { get; set; }
        public List<StageStatistics> StageStatistics { get; set; } = new List<StageStatistics>();
    }

    public class StageStatistics
    {
        public int StageId { get; set; }
        public string StageName { get; set; }
        public TimeSpan PlannedDuration { get; set; }
        public TimeSpan ActualDuration { get; set; }
        public double Efficiency { get; set; }
        public TimeSpan Deviation { get; set; }
    }
}
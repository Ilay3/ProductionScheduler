// File: Services/ProductionPlanningService.cs
using ProductionScheduler.Data;
using ProductionScheduler.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProductionScheduler.Services
{
    public class ProductionPlanningService
    {
        private readonly ApplicationDbContext _context;
        private readonly WorkShift[] _workShifts;

        public ProductionPlanningService(ApplicationDbContext context)
        {
            _context = context;
            _workShifts = WorkShift.GetStandardShifts();
        }

        // Планировать задание с учетом смен и предпочтительного времени
        public ProductionTaskPlan PlanTask(Detail detail, int quantity, DateTime preferredStartTime,
            List<(RouteStage, Machine)> stageAssignments)
        {
            var plan = new ProductionTaskPlan
            {
                Detail = detail,
                Quantity = quantity,
                PreferredStartTime = preferredStartTime,
                StagePlans = new List<ProductionTaskStagePlan>()
            };

            var currentTime = preferredStartTime;

            // Предпочтительное время сотрудника: 08:00-17:00 (обед 12:00-13:00)
            var preferredShiftStart = new TimeSpan(8, 0, 0);
            var preferredShiftEnd = new TimeSpan(17, 0, 0);
            var preferredBreakStart = new TimeSpan(12, 0, 0);
            var preferredBreakEnd = new TimeSpan(13, 0, 0);

            foreach (var (routeStage, machine) in stageAssignments)
            {
                // Рассчитываем время переналадки
                double setupTime = CalculateSetupTime(machine, detail, currentTime);

                // Общее время операции
                double stageHours = (routeStage.StandardTimePerUnit * quantity) + setupTime;
                var stageDuration = TimeSpan.FromHours(stageHours);

                // Планируем этап с учетом смен
                var stagePlan = PlanStageWithShifts(routeStage, machine, currentTime, stageDuration,
                    setupTime, quantity, preferredShiftStart, preferredShiftEnd,
                    preferredBreakStart, preferredBreakEnd);

                plan.StagePlans.Add(stagePlan);
                currentTime = stagePlan.PlannedEndTime.Value;
            }

            plan.PlannedStartTime = plan.StagePlans.First().PlannedStartTime;
            plan.PlannedEndTime = plan.StagePlans.Last().PlannedEndTime;
            plan.TotalDuration = plan.PlannedEndTime.Value - plan.PlannedStartTime.Value;

            // Проверяем, выходит ли план за предпочтительное время
            plan.ExceedsPreferredTime = CheckIfExceedsPreferredTime(plan, preferredShiftStart, preferredShiftEnd);

            return plan;
        }

        private ProductionTaskStagePlan PlanStageWithShifts(RouteStage routeStage, Machine machine,
            DateTime startTime, TimeSpan duration, double setupTime, int quantity,
            TimeSpan preferredStart, TimeSpan preferredEnd,
            TimeSpan preferredBreakStart, TimeSpan preferredBreakEnd)
        {
            var plan = new ProductionTaskStagePlan
            {
                RouteStage = routeStage,
                Machine = machine,
                QuantityToProcess = quantity,
                PlannedSetupTime = setupTime,
                PlannedDuration = duration,
                StandardTimePerUnitAtExecution = routeStage.StandardTimePerUnit
            };

            // Находим подходящую смену для начального времени
            var startTimeSlot = FindBestTimeSlot(startTime, duration, preferredStart, preferredEnd,
                preferredBreakStart, preferredBreakEnd);

            plan.PlannedStartTime = startTimeSlot.StartTime;
            plan.PlannedEndTime = startTimeSlot.EndTime;
            plan.AssignedShift = startTimeSlot.Shift;
            plan.SplitAcrossShifts = startTimeSlot.SplitAcrossShifts;

            return plan;
        }

        private TimeSlot FindBestTimeSlot(DateTime requestedStart, TimeSpan duration,
            TimeSpan preferredStart, TimeSpan preferredEnd,
            TimeSpan preferredBreakStart, TimeSpan preferredBreakEnd)
        {
            var currentTime = requestedStart;

            // Проверяем, можем ли разместить в предпочтительное время
            if (CanFitInPreferredTime(currentTime, duration, preferredStart, preferredEnd,
                preferredBreakStart, preferredBreakEnd))
            {
                var endTime = CalculateEndTimeWithBreaks(currentTime, duration,
                    preferredBreakStart, preferredBreakEnd);

                return new TimeSlot
                {
                    StartTime = currentTime,
                    EndTime = endTime,
                    Shift = null, // Предпочтительное время (не привязано к конкретной смене)
                    SplitAcrossShifts = false
                };
            }

            // Находим подходящую рабочую смену
            return FindShiftTimeSlot(currentTime, duration);
        }

        private bool CanFitInPreferredTime(DateTime startTime, TimeSpan duration,
            TimeSpan preferredStart, TimeSpan preferredEnd,
            TimeSpan preferredBreakStart, TimeSpan preferredBreakEnd)
        {
            var startTimeOfDay = startTime.TimeOfDay;

            // Проверяем, что начинается в предпочтительное время
            if (startTimeOfDay < preferredStart || startTimeOfDay > preferredEnd)
                return false;

            // Рассчитываем время окончания с учетом обеда
            var endTime = CalculateEndTimeWithBreaks(startTime, duration,
                preferredBreakStart, preferredBreakEnd);

            // Проверяем, что заканчивается в предпочтительное время
            return endTime.TimeOfDay <= preferredEnd;
        }

        private DateTime CalculateEndTimeWithBreaks(DateTime startTime, TimeSpan duration,
            TimeSpan breakStart, TimeSpan breakEnd)
        {
            var endTime = startTime + duration;
            var breakDuration = breakEnd - breakStart;

            // Если операция пересекается с обедом, добавляем время обеда
            if (startTime.TimeOfDay < breakEnd && endTime.TimeOfDay > breakStart)
            {
                endTime = endTime.Add(breakDuration);
            }

            return endTime;
        }

        private TimeSlot FindShiftTimeSlot(DateTime requestedStart, TimeSpan duration)
        {
            var currentTime = requestedStart;

            foreach (var shift in _workShifts)
            {
                if (shift.IsTimeInShift(currentTime))
                {
                    var shiftStartTime = GetShiftStartTime(currentTime, shift);
                    var shiftEndTime = GetShiftEndTime(currentTime, shift);
                    var availableTime = shiftEndTime - currentTime;

                    // Вычитаем время обеда, если он еще не прошел
                    if (currentTime.TimeOfDay < shift.BreakEnd)
                    {
                        var breakDuration = shift.BreakEnd - shift.BreakStart;
                        availableTime = availableTime.Subtract(breakDuration);
                    }

                    if (availableTime >= duration)
                    {
                        // Помещается в текущую смену
                        var endTime = CalculateEndTimeWithShiftBreaks(currentTime, duration, shift);
                        return new TimeSlot
                        {
                            StartTime = currentTime,
                            EndTime = endTime,
                            Shift = shift,
                            SplitAcrossShifts = false
                        };
                    }
                    else
                    {
                        // Нужно разделить между сменами или перенести на следующую смену
                        var nextShift = GetNextShift(shift);
                        var nextShiftStart = GetNextShiftStartTime(currentTime, nextShift);
                        var endTime = nextShiftStart + duration;

                        return new TimeSlot
                        {
                            StartTime = nextShiftStart,
                            EndTime = endTime,
                            Shift = nextShift,
                            SplitAcrossShifts = true
                        };
                    }
                }
            }

            // Если не нашли подходящую смену, начинаем со следующей первой смены
            var nextFirstShift = _workShifts.First(s => s.ShiftType == ShiftType.First);
            var nextDayStart = currentTime.Date.AddDays(1).Add(nextFirstShift.StartTime);

            return new TimeSlot
            {
                StartTime = nextDayStart,
                EndTime = nextDayStart + duration,
                Shift = nextFirstShift,
                SplitAcrossShifts = false
            };
        }

        private DateTime GetShiftStartTime(DateTime currentTime, WorkShift shift)
        {
            return currentTime.Date.Add(shift.StartTime);
        }

        private DateTime GetShiftEndTime(DateTime currentTime, WorkShift shift)
        {
            if (shift.ShiftType == ShiftType.Second)
            {
                return currentTime.Date.AddDays(1); // До полуночи
            }
            return currentTime.Date.Add(shift.EndTime);
        }

        private DateTime CalculateEndTimeWithShiftBreaks(DateTime startTime, TimeSpan duration, WorkShift shift)
        {
            var endTime = startTime + duration;

            // Если операция пересекается с обедом смены
            if (startTime.TimeOfDay < shift.BreakEnd && endTime.TimeOfDay > shift.BreakStart)
            {
                var breakDuration = shift.BreakEnd - shift.BreakStart;
                endTime = endTime.Add(breakDuration);
            }

            return endTime;
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

        private DateTime GetNextShiftStartTime(DateTime currentTime, WorkShift nextShift)
        {
            if (nextShift.ShiftType == ShiftType.First && currentTime.TimeOfDay > new TimeSpan(6, 0, 0))
            {
                // Если следующая смена - первая, и сейчас уже после 6 утра, берем завтрашний день
                return currentTime.Date.AddDays(1).Add(nextShift.StartTime);
            }
            else if (nextShift.ShiftType == ShiftType.Third)
            {
                // Третья смена начинается в полночь
                return currentTime.Date.AddDays(1).Add(nextShift.StartTime);
            }
            else
            {
                return currentTime.Date.Add(nextShift.StartTime);
            }
        }

        private double CalculateSetupTime(Machine machine, Detail detail, DateTime currentTime)
        {
            try
            {
                // Ищем последнюю завершенную операцию на этом станке
                var lastTaskStage = _context.ProductionTaskStages
                    .Where(pts => pts.MachineId == machine.Id && pts.ActualEndTime.HasValue)
                    .OrderByDescending(pts => pts.ActualEndTime)
                    .FirstOrDefault();

                if (lastTaskStage == null)
                {
                    return 0; // Первая операция на станке
                }

                // Проверяем, была ли последняя операция для той же детали
                var lastTask = _context.ProductionTasks.FirstOrDefault(pt => pt.Id == lastTaskStage.ProductionTaskId);
                if (lastTask?.DetailId == detail.Id)
                {
                    return 0; // Та же деталь - переналадка не нужна
                }

                return 10.0 / 60.0; // 10 минут в часах
            }
            catch
            {
                return 0;
            }
        }

        private bool CheckIfExceedsPreferredTime(ProductionTaskPlan plan, TimeSpan preferredStart, TimeSpan preferredEnd)
        {
            if (!plan.PlannedStartTime.HasValue || !plan.PlannedEndTime.HasValue)
                return false;

            var startTime = plan.PlannedStartTime.Value.TimeOfDay;
            var endTime = plan.PlannedEndTime.Value.TimeOfDay;

            return startTime < preferredStart || endTime > preferredEnd;
        }
    }

    // Вспомогательные классы для планирования
    public class ProductionTaskPlan
    {
        public Detail Detail { get; set; }
        public int Quantity { get; set; }
        public DateTime PreferredStartTime { get; set; }
        public DateTime? PlannedStartTime { get; set; }
        public DateTime? PlannedEndTime { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public List<ProductionTaskStagePlan> StagePlans { get; set; }
        public bool ExceedsPreferredTime { get; set; }
    }

    public class ProductionTaskStagePlan
    {
        public RouteStage RouteStage { get; set; }
        public Machine Machine { get; set; }
        public int QuantityToProcess { get; set; }
        public double PlannedSetupTime { get; set; }
        public TimeSpan PlannedDuration { get; set; }
        public DateTime? PlannedStartTime { get; set; }
        public DateTime? PlannedEndTime { get; set; }
        public double StandardTimePerUnitAtExecution { get; set; }
        public WorkShift AssignedShift { get; set; }
        public bool SplitAcrossShifts { get; set; }
    }

    public class TimeSlot
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public WorkShift Shift { get; set; }
        public bool SplitAcrossShifts { get; set; }
    }
}
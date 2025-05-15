// File: Models/WorkShift.cs
using System;

namespace ProductionScheduler.Models
{
    public enum ShiftType
    {
        First = 1,   // 1-я смена: 06:00-15:00
        Second = 2,  // 2-я смена: 15:00-00:00
        Third = 3    // 3-я смена: 00:00-06:00
    }

    public class WorkShift
    {
        public int Id { get; set; }
        public ShiftType ShiftType { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public TimeSpan BreakStart { get; set; }
        public TimeSpan BreakEnd { get; set; }
        public string Name { get; set; }

        // Статические методы для получения стандартных смен
        public static WorkShift[] GetStandardShifts()
        {
            return new WorkShift[]
            {
                new WorkShift
                {
                    Id = 1,
                    ShiftType = ShiftType.First,
                    Name = "1-я смена",
                    StartTime = new TimeSpan(8, 0, 0),   // 08:00
                    EndTime = new TimeSpan(17, 0, 0),    // 17:00
                    BreakStart = new TimeSpan(12, 00, 0), // 12:00
                    BreakEnd = new TimeSpan(13, 00, 0)    // 13:00
                },
                new WorkShift
                {
                    Id = 2,
                    ShiftType = ShiftType.Second,
                    Name = "2-я смена",
                    StartTime = new TimeSpan(17, 0, 0),  // 17:00
                    EndTime = new TimeSpan(21, 00, 0),  // 21:00
                    BreakStart = new TimeSpan(22, 00, 0), // 22:00
                    BreakEnd = new TimeSpan(1, 0, 0)    // 01:00
                },
                new WorkShift
                {
                    Id = 3,
                    ShiftType = ShiftType.Third,
                    Name = "3-я смена",
                    StartTime = new TimeSpan(1, 0, 0),   // 01:00
                    EndTime = new TimeSpan(4, 0, 0),     // 04:00
                    BreakStart = new TimeSpan(5, 00, 0),  // 05:00
                    BreakEnd = new TimeSpan(8, 00, 0)     // 08:00
                }
            };
        }

        // Получить рабочее время в смене (с учетом обеда)
        public double GetWorkingHours()
        {
            double totalHours;

            if (ShiftType == ShiftType.Second)
            {
                totalHours = 9; // 9 часов
            }
            else
            {
                totalHours = (EndTime - StartTime).TotalHours;
            }

            // Вычитаем время обеда
            double breakHours = (BreakEnd - BreakStart).TotalHours;
            return totalHours - breakHours;
        }

        // Проверить, попадает ли время в эту смену
        public bool IsTimeInShift(DateTime dateTime)
        {
            var time = dateTime.TimeOfDay;

            if (ShiftType == ShiftType.Second)
            {
                return time >= StartTime || time <= new TimeSpan(0, 0, 0);
            }
            else if (ShiftType == ShiftType.Third)
            {
                // Для третьей смены время от 00:00 до 06:00
                return time >= StartTime && time <= EndTime;
            }
            else
            {
                // Для первой смены обычная проверка
                return time >= StartTime && time <= EndTime;
            }
        }

        // Получить следующий рабочий момент времени (после обеда, если сейчас обед)
        public DateTime GetNextWorkingTime(DateTime currentTime)
        {
            var time = currentTime.TimeOfDay;

            // Если сейчас обеденное время
            if (time >= BreakStart && time <= BreakEnd)
            {
                return currentTime.Date.Add(BreakEnd);
            }

            return currentTime;
        }
    }
}
// File: Data/TestDataSeeder.cs
using ProductionScheduler.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System;

namespace ProductionScheduler.Data
{
    public static class TestDataSeeder
    {
        public static void SeedTestData(ApplicationDbContext context)
        {
            try
            {
                // Проверяем, есть ли уже данные
                if (context.MachineTypes.Any())
                {
                    System.Diagnostics.Debug.WriteLine("Data already exists, skipping seed");
                    return; // Данные уже есть
                }

                System.Diagnostics.Debug.WriteLine("Starting data seeding...");

                // Создаем типы станков
                var machineType1 = new MachineType { Name = "Токарный ЧПУ" };
                var machineType2 = new MachineType { Name = "Фрезерный универсальный" };
                var machineType3 = new MachineType { Name = "Сверлильный" };

                context.MachineTypes.AddRange(machineType1, machineType2, machineType3);
                context.SaveChanges();
                System.Diagnostics.Debug.WriteLine("Machine types created");

                // Создаем станки
                var machine1 = new Machine { Name = "Токарный-001", MachineTypeId = machineType1.Id };
                var machine2 = new Machine { Name = "Токарный-002", MachineTypeId = machineType1.Id };
                var machine3 = new Machine { Name = "Фрезерный-001", MachineTypeId = machineType2.Id };
                var machine4 = new Machine { Name = "Сверлильный-001", MachineTypeId = machineType3.Id };

                context.Machines.AddRange(machine1, machine2, machine3, machine4);
                context.SaveChanges();
                System.Diagnostics.Debug.WriteLine("Machines created");

                // Создаем деталь
                var detail1 = new Detail { Name = "Вал приводной", Code = "VAL-001" };
                var detail2 = new Detail { Name = "Корпус редуктора", Code = "CORP-001" };

                context.Details.AddRange(detail1, detail2);
                context.SaveChanges();
                System.Diagnostics.Debug.WriteLine("Details created");

                // Создаем маршруты для деталей
                var route1Stage1 = new RouteStage
                {
                    DetailId = detail1.Id,
                    OperationNumber = "010",
                    OperationName = "Токарная обработка",
                    MachineTypeId = machineType1.Id,
                    StandardTimePerUnit = 0.5, // 30 минут
                    OrderInRoute = 1
                };

                var route1Stage2 = new RouteStage
                {
                    DetailId = detail1.Id,
                    OperationNumber = "020",
                    OperationName = "Фрезерование шпоночного паза",
                    MachineTypeId = machineType2.Id,
                    StandardTimePerUnit = 0.25, // 15 минут
                    OrderInRoute = 2
                };

                var route2Stage1 = new RouteStage
                {
                    DetailId = detail2.Id,
                    OperationNumber = "010",
                    OperationName = "Фрезерование корпуса",
                    MachineTypeId = machineType2.Id,
                    StandardTimePerUnit = 1.0, // 1 час
                    OrderInRoute = 1
                };

                var route2Stage2 = new RouteStage
                {
                    DetailId = detail2.Id,
                    OperationNumber = "020",
                    OperationName = "Сверление отверстий",
                    MachineTypeId = machineType3.Id,
                    StandardTimePerUnit = 0.33, // 20 минут
                    OrderInRoute = 2
                };

                context.RouteStages.AddRange(route1Stage1, route1Stage2, route2Stage1, route2Stage2);
                context.SaveChanges();
                System.Diagnostics.Debug.WriteLine("Route stages created");

                System.Diagnostics.Debug.WriteLine("Test data seeded successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error seeding test data: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }
    }
}
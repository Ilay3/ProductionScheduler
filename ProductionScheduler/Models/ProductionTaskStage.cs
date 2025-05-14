// File: Models/ProductionTaskStage.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProductionScheduler.Models
{
    public class ProductionTaskStage
    {
        public int Id { get; set; }

        [Required]
        public int ProductionTaskId { get; set; } // Ссылка на родительское задание
        [ForeignKey("ProductionTaskId")]
        public virtual ProductionTask ProductionTask { get; set; }

        [Required]
        public int RouteStageId { get; set; } // Ссылка на соответствующий этап из маршрута детали
        [ForeignKey("RouteStageId")]
        public virtual RouteStage RouteStage { get; set; } // Нормативный этап

        public int? MachineId { get; set; } // Ссылка на КОНКРЕТНЫЙ станок, назначенный на этот этап
                                            // Nullable, если станок назначается позже или этап не требует станка (например, ОТК)
        [ForeignKey("MachineId")]
        public virtual Machine AssignedMachine { get; set; }

        public int QuantityToProcess { get; set; } // Количество деталей для обработки на этом этапе (может отличаться от общего в задании, если этап разделен)

        public int OrderInTask { get; set; } // Порядок этого этапа в рамках производственного задания (соответствует OrderInRoute из RouteStage)

        public DateTime? PlannedStartTime { get; set; }
        public DateTime? PlannedEndTime { get; set; }
        public TimeSpan PlannedDuration { get; set; } // Расчетная плановая длительность этапа

        public DateTime? ActualStartTime { get; set; }
        public DateTime? ActualEndTime { get; set; }
        public TimeSpan? ActualDuration { get; set; } // Фактическая длительность

        public TaskStatus Status { get; set; } // Статус конкретного этапа

        public double StandardTimePerUnitAtExecution { get; set; } // Норма времени на единицу, актуальная на момент планирования/выполнения (может быть взята из RouteStage.StandardTimePerUnit)
        public double PlannedSetupTime { get; set; } // Плановое время на переналадку для этого этапа (в часах или минутах)

        // Для разделенных задач
        public int? ParentProductionTaskStageId { get; set; } // Если этот этап является частью разделенной задачи
        [ForeignKey("ParentProductionTaskStageId")]
        public virtual ProductionTaskStage ParentStage { get; set; }
        public virtual ICollection<ProductionTaskStage> SubStages { get; set; }


        public ProductionTaskStage()
        {
            Status = TaskStatus.Planned;
            SubStages = new HashSet<ProductionTaskStage>();
        }
    }
}
// File: Models/ProductionTask.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProductionScheduler.Models
{
    public enum TaskStatus
    {
        Planned,        // Запланировано
        InProgress,     // В работе
        Paused,         // Приостановлено
        Completed,      // Завершено
        Cancelled       // Отменено
    }

    public class ProductionTask
    {
        public int Id { get; set; }

        [Required]
        public int DetailId { get; set; } // Ссылка на деталь
        [ForeignKey("DetailId")]
        public virtual Detail Detail { get; set; }

        [Required]
        public int Quantity { get; set; } // Количество деталей в задании

        public DateTime CreationTime { get; set; }      // Время создания задания
        public DateTime? PlannedStartTime { get; set; }   // Плановое время начала всего задания
        public DateTime? PlannedEndTime { get; set; }     // Плановое время окончания всего задания
        public DateTime? ActualStartTime { get; set; }    // Фактическое время начала всего задания
        public DateTime? ActualEndTime { get; set; }      // Фактическое время окончания всего задания

        public TaskStatus Status { get; set; }

        // TODO: Ссылка на сотрудника, создавшего/ответственного за задание (если будет справочник сотрудников)
        // public int? EmployeeId { get; set; }
        // public virtual Employee ResponsibleEmployee { get; set; }

        public string Notes { get; set; } // Примечания к заданию

        // Навигационное свойство для этапов этого задания
        public virtual ICollection<ProductionTaskStage> TaskStages { get; set; }

        public ProductionTask()
        {
            TaskStages = new HashSet<ProductionTaskStage>();
            CreationTime = DateTime.Now;
            Status = TaskStatus.Planned;
        }
    }
}
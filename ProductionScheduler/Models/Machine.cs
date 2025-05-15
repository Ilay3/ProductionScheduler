// File: Models/Machine.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace ProductionScheduler.Models
{
    public class Machine
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        public int? MachineTypeId { get; set; }

        [ForeignKey("MachineTypeId")]
        public virtual MachineType MachineType { get; set; }

        // Навигационное свойство для этапов заданий, назначенных на этот станок
        public virtual ICollection<ProductionTaskStage> TaskStages { get; set; }

        public Machine()
        {
            TaskStages = new HashSet<ProductionTaskStage>();
        }
    }
}
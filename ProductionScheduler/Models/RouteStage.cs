// File: Models/RouteStage.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProductionScheduler.Models
{
    public class RouteStage
    {
        public int Id { get; set; }
        public int DetailId { get; set; }
        public virtual Detail Detail { get; set; }

        [Required]
        [MaxLength(50)]
        public string OperationNumber { get; set; } // Номер операции

        [Required]
        [MaxLength(200)]
        public string OperationName { get; set; }   // Наименование операции

        // Внешний ключ для ТИПА станка, на котором может выполняться этот этап
        public int MachineTypeId { get; set; }

        [ForeignKey("MachineTypeId")]
        public virtual MachineType ApplicableMachineType { get; set; }

        public double StandardTimePerUnit { get; set; } // Норма времени на единицу (например, в часах)
        public int OrderInRoute { get; set; }          // Порядок этапа в маршруте
    }
}
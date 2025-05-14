// File: Models/MachineType.cs
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ProductionScheduler.Models
{
    public class MachineType
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } // Например, "Токарный ЧПУ", "Фрезерный универсальный"

        // Навигационное свойство для станков этого типа
        public virtual ICollection<Machine> Machines { get; set; }

        // Навигационное свойство для этапов маршрута, которые могут выполняться на этом типе станка
        public virtual ICollection<RouteStage> ApplicableRouteStages { get; set; }

        public MachineType()
        {
            Machines = new HashSet<Machine>();
            ApplicableRouteStages = new HashSet<RouteStage>();
        }
    }
}

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ProductionScheduler.Models
{
    public class Detail
    {
        [Key] // Первичный ключ
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } // Наименование детали

        [Required]
        [MaxLength(100)]
        public string Code { get; set; } // Код детали (артикул)

        // Навигационное свойство для этапов маршрута
        public virtual ICollection<RouteStage> RouteStages { get; set; } = new List<RouteStage>();

        public override string ToString()
        {
            return $"{Name} ({Code})";
        }
    }
}

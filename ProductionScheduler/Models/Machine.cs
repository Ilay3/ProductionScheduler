// File: Models/Machine.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; // Для ForeignKey

namespace ProductionScheduler.Models
{
    public class Machine
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } // Имя/Инвентарный номер станка

        // Внешний ключ для типа станка
        public int? MachineTypeId { get; set; } // Nullable, если станок может быть без указания типа (но лучше делать обязательным)
                                                // Если делаем обязательным: public int MachineTypeId { get; set; }

        [ForeignKey("MachineTypeId")]
        public virtual MachineType MachineType { get; set; }

    }
}
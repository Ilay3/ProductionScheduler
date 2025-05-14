// File: Data/ApplicationDbContext.cs
using Microsoft.EntityFrameworkCore;
using ProductionScheduler.Models;

namespace ProductionScheduler.Data
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<Detail> Details { get; set; }
        public DbSet<RouteStage> RouteStages { get; set; }
        public DbSet<Machine> Machines { get; set; }
        public DbSet<MachineType> MachineTypes { get; set; }
        public DbSet<ProductionTask> ProductionTasks { get; set; } 
        public DbSet<ProductionTaskStage> ProductionTaskStages { get; set; }


        public ApplicationDbContext() { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlite("Data Source=productionscheduler.db");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Настройка связей (многие из них EF Core выведет по соглашениям, но можно указать явно)

            modelBuilder.Entity<Detail>()
                .HasMany(d => d.RouteStages)
                .WithOne(rs => rs.Detail)
                .HasForeignKey(rs => rs.DetailId)
                .OnDelete(DeleteBehavior.Cascade); // При удалении детали удалять ее этапы

            modelBuilder.Entity<MachineType>()
                .HasMany(mt => mt.Machines)
                .WithOne(m => m.MachineType)
                .HasForeignKey(m => m.MachineTypeId)
                .OnDelete(DeleteBehavior.SetNull); // При удалении типа станка, у станков MachineTypeId станет null (или Restrict, если не разрешать удаление типа, пока есть станки)
                                                   // Если MachineTypeId в Machine обязательный, то лучше Restrict или кастомная логика.

            modelBuilder.Entity<MachineType>()
                .HasMany(mt => mt.ApplicableRouteStages)
                .WithOne(rs => rs.ApplicableMachineType)
                .HasForeignKey(rs => rs.MachineTypeId)
                .OnDelete(DeleteBehavior.Restrict); // Нельзя удалить тип станка, если он используется в этапах маршрута

            // Уникальный индекс для кода детали
            modelBuilder.Entity<Detail>()
                .HasIndex(d => d.Code)
                .IsUnique();

            // Уникальный индекс для имени станка
            modelBuilder.Entity<Machine>()
                .HasIndex(m => m.Name)
                .IsUnique();

            // Уникальный индекс для имени типа станка
            modelBuilder.Entity<MachineType>()
                .HasIndex(mt => mt.Name)
                .IsUnique();

            // Конфигурация для ProductionTask
            modelBuilder.Entity<ProductionTask>(entity =>
            {
                entity.HasOne(pt => pt.Detail)
                      .WithMany() // У одной детали может быть много заданий
                      .HasForeignKey(pt => pt.DetailId)
                      .OnDelete(DeleteBehavior.Restrict); // Не удалять деталь, если есть связанные задания (или Cascade, если нужно)

                entity.HasMany(pt => pt.TaskStages)
                      .WithOne(pts => pts.ProductionTask)
                      .HasForeignKey(pts => pts.ProductionTaskId)
                      .OnDelete(DeleteBehavior.Cascade); // При удалении задания удалять его этапы
            });

            // Конфигурация для ProductionTaskStage
            modelBuilder.Entity<ProductionTaskStage>(entity =>
            {
                entity.HasOne(pts => pts.RouteStage) // Связь с нормативным этапом
                      .WithMany() // У одного RouteStage может быть много фактических выполнений в разных заданиях
                      .HasForeignKey(pts => pts.RouteStageId)
                      .OnDelete(DeleteBehavior.Restrict); // Не удалять этап маршрута, если он используется в заданиях

                entity.HasOne(pts => pts.AssignedMachine) // Связь с конкретным станком
                      .WithMany() // У одного станка может быть много назначенных этапов заданий
                      .HasForeignKey(pts => pts.MachineId)
                      .OnDelete(DeleteBehavior.SetNull); // Если станок удален, у этапа задания MachineId станет null
                                                         // (возможно, лучше Restrict, если станок не должен удаляться при активных задачах)

                // Для связи "родитель-потомок" при разделении этапов
                entity.HasOne(pts => pts.ParentStage)
                      .WithMany(p => p.SubStages)
                      .HasForeignKey(pts => pts.ParentProductionTaskStageId)
                      .OnDelete(DeleteBehavior.Restrict); // или SetNull, в зависимости от логики
            });

        }
    }
}
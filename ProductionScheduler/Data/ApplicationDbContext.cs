// File: Data/ApplicationDbContext.cs (обновленная версия)
using Microsoft.EntityFrameworkCore;
using ProductionScheduler.Models;
using System;

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

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var connectionString = "Data Source=productionscheduler.db";
                optionsBuilder.UseSqlite(connectionString);

                // Добавляем логирование для диагностики
#if DEBUG
                optionsBuilder.LogTo(Console.WriteLine);
                optionsBuilder.EnableSensitiveDataLogging();
#endif
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Настройка связей
            ConfigureDetailEntity(modelBuilder);
            ConfigureMachineTypeEntity(modelBuilder);
            ConfigureMachineEntity(modelBuilder);
            ConfigureProductionTaskEntity(modelBuilder);
            ConfigureProductionTaskStageEntity(modelBuilder);
            ConfigureRouteStageEntity(modelBuilder);
        }

        private void ConfigureDetailEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Detail>(entity =>
            {
                entity.HasKey(d => d.Id);
                entity.Property(d => d.Name).IsRequired().HasMaxLength(200);
                entity.Property(d => d.Code).IsRequired().HasMaxLength(100);
                entity.HasIndex(d => d.Code).IsUnique();

                entity.HasMany(d => d.RouteStages)
                      .WithOne(rs => rs.Detail)
                      .HasForeignKey(rs => rs.DetailId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }

        private void ConfigureMachineTypeEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MachineType>(entity =>
            {
                entity.HasKey(mt => mt.Id);
                entity.Property(mt => mt.Name).IsRequired().HasMaxLength(100);
                entity.HasIndex(mt => mt.Name).IsUnique();

                entity.HasMany(mt => mt.Machines)
                      .WithOne(m => m.MachineType)
                      .HasForeignKey(m => m.MachineTypeId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasMany(mt => mt.ApplicableRouteStages)
                      .WithOne(rs => rs.ApplicableMachineType)
                      .HasForeignKey(rs => rs.MachineTypeId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }

        private void ConfigureMachineEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Machine>(entity =>
            {
                entity.HasKey(m => m.Id);
                entity.Property(m => m.Name).IsRequired().HasMaxLength(100);
                entity.HasIndex(m => m.Name).IsUnique();

                // Связь с этапами заданий
                entity.HasMany(m => m.TaskStages)
                      .WithOne(pts => pts.AssignedMachine)
                      .HasForeignKey(pts => pts.MachineId)
                      .OnDelete(DeleteBehavior.SetNull);
            });
        }

        private void ConfigureProductionTaskEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProductionTask>(entity =>
            {
                entity.HasKey(pt => pt.Id);
                entity.Property(pt => pt.Quantity).IsRequired();
                entity.Property(pt => pt.CreationTime).IsRequired();
                entity.Property(pt => pt.Status).IsRequired();
                entity.Property(pt => pt.Notes).IsRequired(false);

                entity.HasOne(pt => pt.Detail)
                      .WithMany()
                      .HasForeignKey(pt => pt.DetailId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(pt => pt.TaskStages)
                      .WithOne(pts => pts.ProductionTask)
                      .HasForeignKey(pts => pts.ProductionTaskId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }

        private void ConfigureProductionTaskStageEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProductionTaskStage>(entity =>
            {
                entity.HasKey(pts => pts.Id);
                entity.Property(pts => pts.QuantityToProcess).IsRequired();
                entity.Property(pts => pts.OrderInTask).IsRequired();
                entity.Property(pts => pts.Status).IsRequired();
                entity.Property(pts => pts.StandardTimePerUnitAtExecution).IsRequired();
                entity.Property(pts => pts.PlannedSetupTime).IsRequired();
                entity.Property(pts => pts.PlannedDuration).IsRequired();

                entity.HasOne(pts => pts.RouteStage)
                      .WithMany()
                      .HasForeignKey(pts => pts.RouteStageId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(pts => pts.AssignedMachine)
                      .WithMany()
                      .HasForeignKey(pts => pts.MachineId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(pts => pts.ParentStage)
                      .WithMany(p => p.SubStages)
                      .HasForeignKey(pts => pts.ParentProductionTaskStageId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }

        private void ConfigureRouteStageEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RouteStage>(entity =>
            {
                entity.HasKey(rs => rs.Id);
                entity.Property(rs => rs.OperationNumber).IsRequired().HasMaxLength(50);
                entity.Property(rs => rs.OperationName).IsRequired().HasMaxLength(200);
                entity.Property(rs => rs.StandardTimePerUnit).IsRequired();
                entity.Property(rs => rs.OrderInRoute).IsRequired();
            });
        }
    }
}
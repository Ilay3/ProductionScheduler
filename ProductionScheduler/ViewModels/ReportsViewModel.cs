// File: ViewModels/ReportsViewModel.cs
using ProductionScheduler.Data;
using ProductionScheduler.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System;

namespace ProductionScheduler.ViewModels
{
    public class ReportsViewModel : ViewModelBase
    {
        private readonly ApplicationDbContext _context;

        #region Properties
        private DateTime _startDate = DateTime.Today.AddDays(-30);
        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                if (SetProperty(ref _startDate, value))
                    LoadReports();
            }
        }

        private DateTime _endDate = DateTime.Today;
        public DateTime EndDate
        {
            get => _endDate;
            set
            {
                if (SetProperty(ref _endDate, value))
                    LoadReports();
            }
        }

        private ObservableCollection<TaskReport> _taskReports;
        public ObservableCollection<TaskReport> TaskReports
        {
            get => _taskReports;
            set => SetProperty(ref _taskReports, value);
        }

        private ObservableCollection<MachineReport> _machineReports;
        public ObservableCollection<MachineReport> MachineReports
        {
            get => _machineReports;
            set => SetProperty(ref _machineReports, value);
        }

        private string _summary;
        public string Summary
        {
            get => _summary;
            set => SetProperty(ref _summary, value);
        }
        #endregion

        public ICommand RefreshCommand { get; }

        public ReportsViewModel()
        {
            _context = new ApplicationDbContext();
            RefreshCommand = new RelayCommand(LoadReports);
            LoadReports();
        }

        private void LoadReports()
        {
            LoadTaskReports();
            LoadMachineReports();
            CalculateSummary();
        }

        private void LoadTaskReports()
        {
            var tasks = _context.ProductionTasks
                .Where(pt => pt.CreationTime >= StartDate && pt.CreationTime <= EndDate.AddDays(1))
                .Select(pt => new
                {
                    pt.Id,
                    DetailName = pt.Detail.Name,
                    DetailCode = pt.Detail.Code,
                    pt.Quantity,
                    pt.Status,
                    pt.PlannedStartTime,
                    pt.PlannedEndTime,
                    pt.ActualStartTime,
                    pt.ActualEndTime,
                    Stages = pt.TaskStages.Select(pts => new
                    {
                        pts.PlannedDuration,
                        pts.ActualDuration,
                        pts.PlannedSetupTime
                    }).ToList()
                })
                .ToList();

            var reports = tasks.Select(t => new TaskReport
            {
                TaskId = t.Id,
                DetailName = t.DetailName,
                DetailCode = t.DetailCode,
                Quantity = t.Quantity,
                Status = t.Status.ToString(),
                PlannedDuration = t.Stages.Sum(s => s.PlannedDuration.TotalHours),
                ActualDuration = t.Stages.Sum(s => s.ActualDuration?.TotalHours ?? 0),
                PlannedStart = t.PlannedStartTime?.ToString("dd.MM.yy HH:mm"),
                PlannedEnd = t.PlannedEndTime?.ToString("dd.MM.yy HH:mm"),
                ActualStart = t.ActualStartTime?.ToString("dd.MM.yy HH:mm"),
                ActualEnd = t.ActualEndTime?.ToString("dd.MM.yy HH:mm"),
            })
            .ToList();

            foreach (var report in reports)
            {
                if (report.ActualDuration > 0)
                {
                    report.Efficiency = (report.PlannedDuration / report.ActualDuration) * 100;
                    report.Deviation = report.ActualDuration - report.PlannedDuration;
                }
            }

            TaskReports = new ObservableCollection<TaskReport>(reports);
        }

        private void LoadMachineReports()
        {
            var machines = _context.Machines
                .Select(m => new
                {
                    m.Id,
                    m.Name,
                    TypeName = m.MachineType.Name,
                    Stages = m.TaskStages.Where(pts => pts.ProductionTask.CreationTime >= StartDate &&
                             pts.ProductionTask.CreationTime <= EndDate.AddDays(1))
                        .Select(pts => new
                        {
                            pts.PlannedDuration,
                            pts.ActualDuration,
                            pts.PlannedSetupTime,
                            pts.Status
                        }).ToList()
                })
                .Where(m => m.Stages.Any())
                .ToList();

            var reports = machines.Select(m => new MachineReport
            {
                MachineName = m.Name,
                MachineType = m.TypeName,
                TotalTasks = m.Stages.Count,
                CompletedTasks = m.Stages.Count(s => s.Status == Models.TaskStatus.Completed),
                PlannedHours = m.Stages.Sum(s => s.PlannedDuration.TotalHours),
                ActualHours = m.Stages.Sum(s => s.ActualDuration?.TotalHours ?? 0),
                SetupTime = m.Stages.Sum(s => s.PlannedSetupTime)
            })
            .ToList();

            foreach (var report in reports)
            {
                if (report.ActualHours > 0)
                {
                    report.Utilization = (report.ActualHours / (report.PlannedHours + report.SetupTime)) * 100;
                }
            }

            MachineReports = new ObservableCollection<MachineReport>(reports);
        }

        private void CalculateSummary()
        {
            if (TaskReports?.Any() != true)
            {
                Summary = "Нет данных за выбранный период";
                return;
            }

            var totalTasks = TaskReports.Count;
            var completedTasks = TaskReports.Count(r => r.Status == "Completed");
            var avgEfficiency = TaskReports.Where(r => r.Efficiency > 0).Average(r => r.Efficiency);
            var totalPlanned = TaskReports.Sum(r => r.PlannedDuration);
            var totalActual = TaskReports.Sum(r => r.ActualDuration);

            Summary = $"Всего заданий: {totalTasks} | Завершено: {completedTasks} | " +
                     $"План: {totalPlanned:F1}ч | Факт: {totalActual:F1}ч | " +
                     $"Эффективность: {avgEfficiency:F1}%";
        }
    }

    public class TaskReport
    {
        public int TaskId { get; set; }
        public string DetailName { get; set; }
        public string DetailCode { get; set; }
        public int Quantity { get; set; }
        public string Status { get; set; }
        public double PlannedDuration { get; set; }
        public double ActualDuration { get; set; }
        public string PlannedStart { get; set; }
        public string PlannedEnd { get; set; }
        public string ActualStart { get; set; }
        public string ActualEnd { get; set; }
        public double Efficiency { get; set; }
        public double Deviation { get; set; }
    }

    public class MachineReport
    {
        public string MachineName { get; set; }
        public string MachineType { get; set; }
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public double PlannedHours { get; set; }
        public double ActualHours { get; set; }
        public double SetupTime { get; set; }
        public double Utilization { get; set; }
    }
}
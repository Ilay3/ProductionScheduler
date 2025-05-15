// File: ViewModels/SplitOperationViewModel.cs
using ProductionScheduler.Data;
using ProductionScheduler.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows;
using System;

namespace ProductionScheduler.ViewModels
{
    public class SplitOperationViewModel : ViewModelBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ProductionTaskStage _originalStage;

        #region Properties
        private int _totalQuantity;
        public int TotalQuantity
        {
            get => _totalQuantity;
            set => SetProperty(ref _totalQuantity, value);
        }

        private int _numberOfParts;
        public int NumberOfParts
        {
            get => _numberOfParts;
            set
            {
                if (SetProperty(ref _numberOfParts, value))
                {
                    UpdateSplitParts();
                }
            }
        }

        private int _quantityPerPart;
        public int QuantityPerPart
        {
            get => _quantityPerPart;
            set
            {
                if (SetProperty(ref _quantityPerPart, value))
                {
                    UpdateFromQuantityPerPart();
                }
            }
        }

        private ObservableCollection<Machine> _availableMachines;
        public ObservableCollection<Machine> AvailableMachines
        {
            get => _availableMachines;
            set => SetProperty(ref _availableMachines, value);
        }

        private ObservableCollection<SplitPart> _splitParts;
        public ObservableCollection<SplitPart> SplitParts
        {
            get => _splitParts;
            set => SetProperty(ref _splitParts, value);
        }

        private bool _isQuantityMode = true;
        public bool IsQuantityMode
        {
            get => _isQuantityMode;
            set => SetProperty(ref _isQuantityMode, value);
        }

        private bool _isPartsMode = false;
        public bool IsPartsMode
        {
            get => _isPartsMode;
            set => SetProperty(ref _isPartsMode, value);
        }
        #endregion

        #region Commands
        public ICommand SplitCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand AddPartCommand { get; }
        public ICommand RemovePartCommand { get; }
        #endregion

        public SplitOperationViewModel(ProductionTaskStage originalStage, ApplicationDbContext context)
        {
            _originalStage = originalStage;
            _context = context;

            TotalQuantity = originalStage.QuantityToProcess;
            NumberOfParts = 2;

            LoadAvailableMachines();
            UpdateSplitParts();

            SplitCommand = new RelayCommand(ExecuteSplit, CanExecuteSplit);
            CancelCommand = new RelayCommand(ExecuteCancel);
            AddPartCommand = new RelayCommand(ExecuteAddPart);
            RemovePartCommand = new RelayCommand<SplitPart>(ExecuteRemovePart);
        }

        public event Action<bool> RequestClose;

        private void LoadAvailableMachines()
        {
            var machineTypeId = _originalStage.RouteStage.MachineTypeId;
            var machines = _context.Machines
                .Where(m => m.MachineTypeId == machineTypeId)
                .OrderBy(m => m.Name)
                .ToList();

            AvailableMachines = new ObservableCollection<Machine>(machines);
        }

        private void UpdateSplitParts()
        {
            if (NumberOfParts <= 0) return;

            var parts = new ObservableCollection<SplitPart>();
            int quantityPerPart = TotalQuantity / NumberOfParts;
            int remainder = TotalQuantity % NumberOfParts;

            for (int i = 0; i < NumberOfParts; i++)
            {
                int quantity = quantityPerPart + (i < remainder ? 1 : 0);
                parts.Add(new SplitPart
                {
                    PartNumber = i + 1,
                    Quantity = quantity,
                    AvailableMachines = AvailableMachines
                });
            }

            SplitParts = parts;
        }

        private void UpdateFromQuantityPerPart()
        {
            if (QuantityPerPart <= 0) return;
            NumberOfParts = (int)Math.Ceiling((double)TotalQuantity / QuantityPerPart);
        }

        private bool CanExecuteSplit()
        {
            return SplitParts?.All(p => p.SelectedMachine != null && p.Quantity > 0) == true &&
                   SplitParts.Sum(p => p.Quantity) == TotalQuantity;
        }

        private void ExecuteSplit()
        {
            try
            {
                // Обновляем оригинальный этап
                var firstPart = SplitParts.First();
                _originalStage.QuantityToProcess = firstPart.Quantity;
                _originalStage.MachineId = firstPart.SelectedMachine.Id;

                // Создаем дочерние этапы
                foreach (var part in SplitParts.Skip(1))
                {
                    var subStage = new ProductionTaskStage
                    {
                        ProductionTaskId = _originalStage.ProductionTaskId,
                        RouteStageId = _originalStage.RouteStageId,
                        MachineId = part.SelectedMachine.Id,
                        QuantityToProcess = part.Quantity,
                        OrderInTask = _originalStage.OrderInTask,
                        Status = _originalStage.Status,
                        StandardTimePerUnitAtExecution = _originalStage.StandardTimePerUnitAtExecution,
                        PlannedSetupTime = 0, // Нет переналадки для той же детали
                        PlannedDuration = TimeSpan.FromHours(_originalStage.StandardTimePerUnitAtExecution * part.Quantity),
                        ParentProductionTaskStageId = _originalStage.Id
                    };

                    _context.ProductionTaskStages.Add(subStage);
                }

                _context.SaveChanges();
                RequestClose?.Invoke(true);
                MessageBox.Show($"Операция разделена на {SplitParts.Count} частей", "Успех");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка разделения: {ex.Message}", "Ошибка");
            }
        }

        private void ExecuteCancel()
        {
            RequestClose?.Invoke(false);
        }

        private void ExecuteAddPart()
        {
            NumberOfParts++;
        }

        private void ExecuteRemovePart(SplitPart part)
        {
            if (SplitParts.Count > 2)
            {
                SplitParts.Remove(part);
                NumberOfParts = SplitParts.Count;
                UpdateSplitParts();
            }
        }
    }

    public class SplitPart : ViewModelBase
    {
        public int PartNumber { get; set; }

        private int _quantity;
        public int Quantity
        {
            get => _quantity;
            set => SetProperty(ref _quantity, value);
        }

        private Machine _selectedMachine;
        public Machine SelectedMachine
        {
            get => _selectedMachine;
            set => SetProperty(ref _selectedMachine, value);
        }

        public ObservableCollection<Machine> AvailableMachines { get; set; }
    }
}
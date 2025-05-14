// File: ViewModels/AdminViewModel.cs
using ProductionScheduler.Data;
using ProductionScheduler.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Globalization;
using Microsoft.EntityFrameworkCore; // Для Include

namespace ProductionScheduler.ViewModels
{
    public class AdminViewModel : ViewModelBase
    {
        private readonly ApplicationDbContext _context;

        #region Editing State Flags
        private bool _isEditingMachine = false;
        private bool _isEditingDetail = false;
        private bool _isEditingRouteStage = false;
        private bool _isEditingMachineType = false;
        #endregion

        #region --- MachineType Properties & Commands ---
        private ObservableCollection<MachineType> _machineTypes;
        public ObservableCollection<MachineType> MachineTypes { get => _machineTypes; set => SetProperty(ref _machineTypes, value); }

        private MachineType _selectedMachineType;
        public MachineType SelectedMachineType
        {
            get => _selectedMachineType;
            set
            {
                if (SetProperty(ref _selectedMachineType, value))
                {
                    if (_selectedMachineType != null && !_isEditingMachineType && CanStartNewOperation()) MachineTypeNameToEdit = _selectedMachineType.Name;
                    else if (_selectedMachineType == null && !_isEditingMachineType) MachineTypeNameToEdit = string.Empty;
                    UpdateAllCommandsCanExecute();
                }
            }
        }
        private string _machineTypeNameToEdit;
        public string MachineTypeNameToEdit { get => _machineTypeNameToEdit; set { if (SetProperty(ref _machineTypeNameToEdit, value)) UpdateAllCommandsCanExecute(); } }

        public ICommand AddNewMachineTypeCommand { get; }
        public ICommand SaveMachineTypeCommand { get; }
        public ICommand EditMachineTypeCommand { get; }
        public ICommand DeleteMachineTypeCommand { get; }
        public ICommand CancelEditMachineTypeCommand { get; }
        #endregion

        #region --- Machine Properties & Commands ---
        private ObservableCollection<Machine> _machines;
        public ObservableCollection<Machine> Machines { get => _machines; set => SetProperty(ref _machines, value); }

        private Machine _selectedMachine;
        public Machine SelectedMachine
        {
            get => _selectedMachine;
            set
            {
                if (SetProperty(ref _selectedMachine, value))
                {
                    if (_selectedMachine != null && !_isEditingMachine && CanStartNewOperation())
                    {
                        MachineNameToEdit = _selectedMachine.Name;
                        SelectedMachineTypeForMachineEdit = MachineTypes?.FirstOrDefault(mt => mt.Id == _selectedMachine.MachineTypeId);
                    }
                    else if (_selectedMachine == null && !_isEditingMachine)
                    {
                        MachineNameToEdit = string.Empty;
                        SelectedMachineTypeForMachineEdit = null;
                    }
                    UpdateAllCommandsCanExecute();
                }
            }
        }
        private string _machineNameToEdit;
        public string MachineNameToEdit { get => _machineNameToEdit; set { if (SetProperty(ref _machineNameToEdit, value)) UpdateAllCommandsCanExecute(); } }

        private MachineType _selectedMachineTypeForMachineEdit;
        public MachineType SelectedMachineTypeForMachineEdit { get => _selectedMachineTypeForMachineEdit; set { if (SetProperty(ref _selectedMachineTypeForMachineEdit, value)) UpdateAllCommandsCanExecute(); } }

        public ICommand AddNewMachineCommand { get; }
        public ICommand SaveMachineCommand { get; }
        public ICommand EditMachineCommand { get; }
        public ICommand DeleteMachineCommand { get; }
        public ICommand CancelEditMachineCommand { get; }
        #endregion

        #region --- Detail Properties & Commands ---
        private ObservableCollection<Detail> _details;
        public ObservableCollection<Detail> Details { get => _details; set => SetProperty(ref _details, value); }

        private Detail _selectedDetail;
        public Detail SelectedDetail
        {
            get => _selectedDetail;
            set
            {
                if (SetProperty(ref _selectedDetail, value))
                {
                    if (_selectedDetail != null && !_isEditingDetail && CanStartNewOperation())
                    {
                        DetailNameToEdit = _selectedDetail.Name;
                        DetailCodeToEdit = _selectedDetail.Code;
                        LoadRouteStagesForSelectedDetail();
                    }
                    else if (_selectedDetail == null && !_isEditingDetail) // Если снимаем выбор детали
                    {
                        DetailNameToEdit = string.Empty;
                        DetailCodeToEdit = string.Empty;
                        RouteStagesVM = null;       // <--- ИСПРАВЛЕНО: RouteStages на RouteStagesVM
                        SelectedRouteStageVM = null; // <--- ИСПРАВЛЕНО: SelectedRouteStage на SelectedRouteStageVM
                        ClearRouteStageEditFields();
                    }
                    UpdateAllCommandsCanExecute();
                }
            }
        }
        private string _detailNameToEdit;
        public string DetailNameToEdit { get => _detailNameToEdit; set { if (SetProperty(ref _detailNameToEdit, value)) UpdateAllCommandsCanExecute(); } }
        private string _detailCodeToEdit;
        public string DetailCodeToEdit { get => _detailCodeToEdit; set { if (SetProperty(ref _detailCodeToEdit, value)) UpdateAllCommandsCanExecute(); } }

        public ICommand AddNewDetailCommand { get; }
        public ICommand SaveDetailCommand { get; }
        public ICommand EditDetailCommand { get; }
        public ICommand DeleteDetailCommand { get; }
        public ICommand CancelEditDetailCommand { get; }
        #endregion

        #region --- RouteStage Properties & Commands ---
        private ObservableCollection<RouteStageViewModel> _routeStagesVM;
        public ObservableCollection<RouteStageViewModel> RouteStagesVM { get => _routeStagesVM; set => SetProperty(ref _routeStagesVM, value); }

        private RouteStageViewModel _selectedRouteStageVM;
        public RouteStageViewModel SelectedRouteStageVM
        {
            get => _selectedRouteStageVM;
            set
            {
                if (SetProperty(ref _selectedRouteStageVM, value))
                {
                    if (_selectedRouteStageVM != null && !_isEditingRouteStage && CanStartNewStageOperation())
                    {
                        StageOpNumberToEdit = _selectedRouteStageVM.OperationNumber;
                        StageOpNameToEdit = _selectedRouteStageVM.OperationName;
                        SelectedMachineTypeForStageEdit = MachineTypes?.FirstOrDefault(mt => mt.Id == _selectedRouteStageVM.MachineTypeId);
                        StageStdTimeToEdit = _selectedRouteStageVM.StandardTimePerUnit.ToString(CultureInfo.InvariantCulture);
                        StageOrderInRouteToDisplay = _selectedRouteStageVM.OrderInRoute.ToString();
                    }
                    else if (_selectedRouteStageVM == null && !_isEditingRouteStage) ClearRouteStageEditFields();
                    UpdateAllCommandsCanExecute();
                }
            }
        }
        private string _stageOpNumberToEdit;
        public string StageOpNumberToEdit { get => _stageOpNumberToEdit; set { if (SetProperty(ref _stageOpNumberToEdit, value)) UpdateAllCommandsCanExecute(); } }
        private string _stageOpNameToEdit;
        public string StageOpNameToEdit { get => _stageOpNameToEdit; set { if (SetProperty(ref _stageOpNameToEdit, value)) UpdateAllCommandsCanExecute(); } }

        private MachineType _selectedMachineTypeForStageEdit;
        public MachineType SelectedMachineTypeForStageEdit { get => _selectedMachineTypeForStageEdit; set { if (SetProperty(ref _selectedMachineTypeForStageEdit, value)) UpdateAllCommandsCanExecute(); } }

        private string _stageStdTimeToEdit;
        public string StageStdTimeToEdit { get => _stageStdTimeToEdit; set { if (SetProperty(ref _stageStdTimeToEdit, value)) UpdateAllCommandsCanExecute(); } }
        private string _stageOrderInRouteToDisplay;
        public string StageOrderInRouteToDisplay { get => _stageOrderInRouteToDisplay; set => SetProperty(ref _stageOrderInRouteToDisplay, value); }

        public ICommand AddNewRouteStageCommand { get; }
        public ICommand SaveRouteStageCommand { get; }
        public ICommand EditRouteStageCommand { get; }
        public ICommand DeleteRouteStageCommand { get; }
        public ICommand CancelEditRouteStageCommand { get; }
        #endregion

        public AdminViewModel()
        {
            _context = new ApplicationDbContext();

            // MachineTypes
            AddNewMachineTypeCommand = new RelayCommand(PrepareNewMachineType, CanStartNewOperation);
            SaveMachineTypeCommand = new RelayCommand(SaveMachineType, CanSaveMachineType);
            EditMachineTypeCommand = new RelayCommand(StartEditMachineType, () => SelectedMachineType != null && CanStartNewOperation());
            DeleteMachineTypeCommand = new RelayCommand(DeleteMachineType, () => SelectedMachineType != null && CanStartNewOperation());
            CancelEditMachineTypeCommand = new RelayCommand(CancelMachineTypeOperation, CanStartNewOperation); // Отменить можно всегда, если не в процессе другой операции

            // Machines
            AddNewMachineCommand = new RelayCommand(PrepareNewMachine, CanStartNewOperation);
            SaveMachineCommand = new RelayCommand(SaveMachine, CanSaveMachine);
            EditMachineCommand = new RelayCommand(StartEditMachine, () => SelectedMachine != null && CanStartNewOperation());
            DeleteMachineCommand = new RelayCommand(DeleteMachine, () => SelectedMachine != null && CanStartNewOperation());
            CancelEditMachineCommand = new RelayCommand(CancelMachineOperation, CanStartNewOperation);

            // Details
            AddNewDetailCommand = new RelayCommand(PrepareNewDetail, CanStartNewOperation);
            SaveDetailCommand = new RelayCommand(SaveDetail, CanSaveDetail);
            EditDetailCommand = new RelayCommand(StartEditDetail, () => SelectedDetail != null && CanStartNewOperation());
            DeleteDetailCommand = new RelayCommand(DeleteDetail, () => SelectedDetail != null && CanStartNewOperation());
            CancelEditDetailCommand = new RelayCommand(CancelDetailOperation, CanStartNewOperation);

            // RouteStages
            AddNewRouteStageCommand = new RelayCommand(PrepareNewRouteStage, CanStartNewStageOperation);
            SaveRouteStageCommand = new RelayCommand(SaveRouteStage, CanSaveRouteStage);
            EditRouteStageCommand = new RelayCommand(StartEditRouteStage, () => SelectedRouteStageVM != null && CanStartNewStageOperation());
            DeleteRouteStageCommand = new RelayCommand(DeleteRouteStage, () => SelectedRouteStageVM != null && CanStartNewStageOperation());
            CancelEditRouteStageCommand = new RelayCommand(CancelRouteStageOperation, CanStartNewStageOperation);

            LoadInitialData();
        }

        private void LoadInitialData()
        {
            LoadMachineTypes();
            LoadMachines();
            LoadDetails();
            ResetAllFormsToIdle();
        }

        private bool CanStartNewOperation() => !_isEditingMachineType && !_isEditingMachine && !_isEditingDetail && !_isEditingRouteStage;
        private bool CanStartNewStageOperation() => SelectedDetail != null && CanStartNewOperation();

        #region General UI State Management
        private void ResetAllFormsToIdle()
        {
            _isEditingMachineType = false; _isEditingMachine = false; _isEditingDetail = false; _isEditingRouteStage = false;

            var oldSelectedMachineType = SelectedMachineType;
            var oldSelectedMachine = SelectedMachine;
            var oldSelectedDetail = SelectedDetail;
            var oldSelectedRouteStageVM = SelectedRouteStageVM;

            if (oldSelectedMachineType != null) SelectedMachineType = null;
            if (oldSelectedMachine != null) SelectedMachine = null;
            if (oldSelectedDetail != null) SelectedDetail = null;
            if (oldSelectedRouteStageVM != null) SelectedRouteStageVM = null;


            if (MachineTypeNameToEdit != string.Empty) MachineTypeNameToEdit = string.Empty;
            if (MachineNameToEdit != string.Empty) MachineNameToEdit = string.Empty;
            if (SelectedMachineTypeForMachineEdit != null) SelectedMachineTypeForMachineEdit = null;
            if (DetailNameToEdit != string.Empty) DetailNameToEdit = string.Empty;
            if (DetailCodeToEdit != string.Empty) DetailCodeToEdit = string.Empty;
            ClearRouteStageEditFields();
            if (SelectedMachineTypeForStageEdit != null) SelectedMachineTypeForStageEdit = null;

            UpdateAllCommandsCanExecute();
        }
        private void ClearRouteStageEditFields() { StageOpNumberToEdit = string.Empty; StageOpNameToEdit = string.Empty; StageStdTimeToEdit = string.Empty; StageOrderInRouteToDisplay = string.Empty; SelectedMachineTypeForStageEdit = null; }

        private void UpdateAllCommandsCanExecute()
        {
            ((RelayCommand)AddNewMachineTypeCommand).RaiseCanExecuteChanged();
            ((RelayCommand)SaveMachineTypeCommand).RaiseCanExecuteChanged();
            ((RelayCommand)EditMachineTypeCommand).RaiseCanExecuteChanged();
            ((RelayCommand)DeleteMachineTypeCommand).RaiseCanExecuteChanged();
            ((RelayCommand)CancelEditMachineTypeCommand).RaiseCanExecuteChanged();

            ((RelayCommand)AddNewMachineCommand).RaiseCanExecuteChanged();
            ((RelayCommand)SaveMachineCommand).RaiseCanExecuteChanged();
            ((RelayCommand)EditMachineCommand).RaiseCanExecuteChanged();
            ((RelayCommand)DeleteMachineCommand).RaiseCanExecuteChanged();
            ((RelayCommand)CancelEditMachineCommand).RaiseCanExecuteChanged();

            ((RelayCommand)AddNewDetailCommand).RaiseCanExecuteChanged();
            ((RelayCommand)SaveDetailCommand).RaiseCanExecuteChanged();
            ((RelayCommand)EditDetailCommand).RaiseCanExecuteChanged();
            ((RelayCommand)DeleteDetailCommand).RaiseCanExecuteChanged();
            ((RelayCommand)CancelEditDetailCommand).RaiseCanExecuteChanged();

            ((RelayCommand)AddNewRouteStageCommand).RaiseCanExecuteChanged();
            ((RelayCommand)SaveRouteStageCommand).RaiseCanExecuteChanged();
            ((RelayCommand)EditRouteStageCommand).RaiseCanExecuteChanged();
            ((RelayCommand)DeleteRouteStageCommand).RaiseCanExecuteChanged();
            ((RelayCommand)CancelEditRouteStageCommand).RaiseCanExecuteChanged();
        }
        #endregion

        #region MachineType Logic
        private void LoadMachineTypes() { MachineTypes = new ObservableCollection<MachineType>(_context.MachineTypes.OrderBy(mt => mt.Name).ToList()); }
        private void PrepareNewMachineType() { SelectedMachineType = null; _isEditingMachineType = false; MachineTypeNameToEdit = string.Empty; UpdateAllCommandsCanExecute(); }
        private bool CanSaveMachineType() => (_isEditingMachineType && SelectedMachineType != null && !string.IsNullOrWhiteSpace(MachineTypeNameToEdit)) || (!_isEditingMachineType && SelectedMachineType == null && !string.IsNullOrWhiteSpace(MachineTypeNameToEdit));
        private void SaveMachineType()
        {
            if (!CanSaveMachineType()) return;
            string name = MachineTypeNameToEdit.Trim(); MachineType typeToSave;
            if (_isEditingMachineType && SelectedMachineType != null) { if (_context.MachineTypes.Any(mt => mt.Name == name && mt.Id != SelectedMachineType.Id)) { MessageBox.Show("Другой тип с таким именем уже существует."); return; } typeToSave = SelectedMachineType; typeToSave.Name = name; }
            else { if (_context.MachineTypes.Any(mt => mt.Name == name)) { MessageBox.Show("Тип станка с таким именем уже существует."); return; } typeToSave = new MachineType { Name = name }; _context.MachineTypes.Add(typeToSave); }
            try { _context.SaveChanges(); int id = typeToSave.Id; LoadMachineTypes(); SelectedMachineType = MachineTypes.FirstOrDefault(mt => mt.Id == id); MessageBox.Show("Тип станка сохранен."); }
            catch (System.Exception ex) { MessageBox.Show($"Ошибка сохранения типа станка: {ex.Message}"); }
            finally { _isEditingMachineType = false; UpdateAllCommandsCanExecute(); }
        }
        private void StartEditMachineType() { if (SelectedMachineType == null) return; _isEditingMachineType = true; MachineTypeNameToEdit = SelectedMachineType.Name; UpdateAllCommandsCanExecute(); }
        private void DeleteMachineType()
        {
            if (SelectedMachineType == null) return;
            if (_context.Machines.Any(m => m.MachineTypeId == SelectedMachineType.Id) || _context.RouteStages.Any(rs => rs.MachineTypeId == SelectedMachineType.Id)) { MessageBox.Show("Тип станка используется станками или этапами маршрутов и не может быть удален."); return; }
            if (MessageBox.Show($"Удалить тип станка '{SelectedMachineType.Name}'?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            { _context.MachineTypes.Remove(SelectedMachineType); try { _context.SaveChanges(); LoadMachineTypes(); MessageBox.Show("Тип станка удален."); } catch (System.Exception ex) { MessageBox.Show($"Ошибка удаления типа станка: {ex.Message}"); } finally { _isEditingMachineType = false; SelectedMachineType = null; UpdateAllCommandsCanExecute(); } }
        }
        private void CancelMachineTypeOperation() { _isEditingMachineType = false; SelectedMachineType = null; /*LoadMachineTypes();*/ UpdateAllCommandsCanExecute(); } // SelectedMachineType=null вызовет обновление полей
        #endregion

        #region Machine Logic
        private void LoadMachines() { Machines = new ObservableCollection<Machine>(_context.Machines.Include(m => m.MachineType).OrderBy(m => m.Name).ToList()); }
        private void PrepareNewMachine() { SelectedMachine = null; _isEditingMachine = false; MachineNameToEdit = string.Empty; SelectedMachineTypeForMachineEdit = null; UpdateAllCommandsCanExecute(); }
        private bool CanSaveMachine() => (_isEditingMachine && SelectedMachine != null && !string.IsNullOrWhiteSpace(MachineNameToEdit) && SelectedMachineTypeForMachineEdit != null) || (!_isEditingMachine && SelectedMachine == null && !string.IsNullOrWhiteSpace(MachineNameToEdit) && SelectedMachineTypeForMachineEdit != null);
        private void SaveMachine()
        {
            if (!CanSaveMachine()) { MessageBox.Show("Имя станка и тип должны быть указаны и корректны."); return; }
            string name = MachineNameToEdit.Trim(); Machine machineToSave;
            if (_isEditingMachine && SelectedMachine != null) { if (_context.Machines.Any(m => m.Name == name && m.Id != SelectedMachine.Id)) { MessageBox.Show("Другой станок с таким именем уже существует."); return; } machineToSave = SelectedMachine; machineToSave.Name = name; machineToSave.MachineTypeId = SelectedMachineTypeForMachineEdit.Id; }
            else { if (_context.Machines.Any(m => m.Name == name)) { MessageBox.Show("Станок с таким именем уже существует."); return; } machineToSave = new Machine { Name = name, MachineTypeId = SelectedMachineTypeForMachineEdit.Id }; _context.Machines.Add(machineToSave); }
            try { _context.SaveChanges(); int id = machineToSave.Id; LoadMachines(); SelectedMachine = Machines.FirstOrDefault(m => m.Id == id); MessageBox.Show("Станок сохранен."); }
            catch (System.Exception ex) { MessageBox.Show($"Ошибка сохранения станка: {ex.Message}"); }
            finally { _isEditingMachine = false; UpdateAllCommandsCanExecute(); }
        }
        private void StartEditMachine() { if (SelectedMachine == null) return; _isEditingMachine = true; MachineNameToEdit = SelectedMachine.Name; SelectedMachineTypeForMachineEdit = MachineTypes.FirstOrDefault(mt => mt.Id == SelectedMachine.MachineTypeId); UpdateAllCommandsCanExecute(); }
        private void DeleteMachine()
        {
            if (SelectedMachine == null) return;
            // TODO: Проверить, используется ли станок в АКТИВНЫХ задачах, а не только в RouteStages.
            // Пока что RouteStage не хранит конкретный MachineId, а только MachineTypeId.
            // Поэтому прямая проверка использования станка в RouteStage по MachineId невозможна.
            // Если логика изменится, и RouteStage будет хранить MachineId, тогда проверка нужна здесь.
            if (MessageBox.Show($"Удалить станок '{SelectedMachine.Name}'?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                _context.Machines.Remove(SelectedMachine);
                try { _context.SaveChanges(); LoadMachines(); MessageBox.Show("Станок удален."); }
                catch (System.Exception ex) { MessageBox.Show($"Ошибка удаления станка: {ex.Message}"); }
                finally { _isEditingMachine = false; SelectedMachine = null; UpdateAllCommandsCanExecute(); }
            }
        }
        private void CancelMachineOperation() { _isEditingMachine = false; SelectedMachine = null; UpdateAllCommandsCanExecute(); }
        #endregion

        #region Detail Logic
        private void LoadDetails() { Details = new ObservableCollection<Detail>(_context.Details.OrderBy(d => d.Name).ToList()); }
        private void LoadRouteStagesForSelectedDetail()
        {
            if (SelectedDetail != null) RouteStagesVM = new ObservableCollection<RouteStageViewModel>(
                _context.RouteStages.Include(rs => rs.ApplicableMachineType)
                .Where(rs => rs.DetailId == SelectedDetail.Id)
                .OrderBy(rs => rs.OrderInRoute)
                .Select(rs => new RouteStageViewModel(rs)));
            else RouteStagesVM = null;
            SelectedRouteStageVM = null;
        }
        private void PrepareNewDetail() { SelectedDetail = null; _isEditingDetail = false; DetailNameToEdit = string.Empty; DetailCodeToEdit = string.Empty; RouteStagesVM = null; UpdateAllCommandsCanExecute(); }
        private bool CanSaveDetail() => (_isEditingDetail && SelectedDetail != null && !string.IsNullOrWhiteSpace(DetailNameToEdit) && !string.IsNullOrWhiteSpace(DetailCodeToEdit)) || (!_isEditingDetail && SelectedDetail == null && !string.IsNullOrWhiteSpace(DetailNameToEdit) && !string.IsNullOrWhiteSpace(DetailCodeToEdit));
        private void SaveDetail()
        {
            if (!CanSaveDetail()) return;
            string name = DetailNameToEdit.Trim(); string code = DetailCodeToEdit.Trim(); Detail detailToSave;
            if (_isEditingDetail && SelectedDetail != null) { if (_context.Details.Any(d => d.Code == code && d.Id != SelectedDetail.Id)) { MessageBox.Show("Другая деталь с таким кодом уже существует."); return; } detailToSave = SelectedDetail; detailToSave.Name = name; detailToSave.Code = code; }
            else { if (_context.Details.Any(d => d.Code == code)) { MessageBox.Show("Деталь с таким кодом уже существует."); return; } detailToSave = new Detail { Name = name, Code = code }; _context.Details.Add(detailToSave); }
            try { _context.SaveChanges(); int id = detailToSave.Id; LoadDetails(); SelectedDetail = Details.FirstOrDefault(d => d.Id == id); MessageBox.Show("Деталь сохранена."); }
            catch (System.Exception ex) { MessageBox.Show($"Ошибка сохранения детали: {ex.Message}"); }
            finally { _isEditingDetail = false; UpdateAllCommandsCanExecute(); }
        }
        private void StartEditDetail() { if (SelectedDetail == null) return; _isEditingDetail = true; DetailNameToEdit = SelectedDetail.Name; DetailCodeToEdit = SelectedDetail.Code; UpdateAllCommandsCanExecute(); }
        private void DeleteDetail()
        {
            if (SelectedDetail == null) return;
            if (MessageBox.Show($"Удалить деталь '{SelectedDetail.Name}' и все ее этапы?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                _context.RouteStages.RemoveRange(_context.RouteStages.Where(rs => rs.DetailId == SelectedDetail.Id));
                _context.Details.Remove(SelectedDetail);
                try { _context.SaveChanges(); LoadDetails(); MessageBox.Show("Деталь удалена."); }
                catch (System.Exception ex) { MessageBox.Show($"Ошибка удаления детали: {ex.Message}"); }
                finally { _isEditingDetail = false; SelectedDetail = null; UpdateAllCommandsCanExecute(); }
            }
        }
        private void CancelDetailOperation() { _isEditingDetail = false; SelectedDetail = null; UpdateAllCommandsCanExecute(); }
        #endregion

        #region RouteStage Logic
        private void PrepareNewRouteStage() { SelectedRouteStageVM = null; _isEditingRouteStage = false; ClearRouteStageEditFields(); UpdateAllCommandsCanExecute(); }
        private bool CanSaveRouteStage()
        {
            if (SelectedDetail == null || SelectedMachineTypeForStageEdit == null) return false;
            bool fieldsValid = !string.IsNullOrWhiteSpace(StageOpNumberToEdit) && !string.IsNullOrWhiteSpace(StageOpNameToEdit) && double.TryParse(StageStdTimeToEdit, NumberStyles.Any, CultureInfo.InvariantCulture, out double time) && time > 0;
            return (_isEditingRouteStage && SelectedRouteStageVM != null && fieldsValid) || (!_isEditingRouteStage && SelectedRouteStageVM == null && fieldsValid);
        }
        private void SaveRouteStage()
        {
            if (!CanSaveRouteStage()) { MessageBox.Show("Деталь, тип станка и все поля этапа должны быть корректно заполнены и норма времени должна быть > 0."); return; }
            string opNum = StageOpNumberToEdit.Trim(); string opName = StageOpNameToEdit.Trim(); double stdTime = double.Parse(StageStdTimeToEdit, CultureInfo.InvariantCulture);
            RouteStage stageToSave;
            if (_isEditingRouteStage && SelectedRouteStageVM != null)
            {
                stageToSave = _context.RouteStages.Find(SelectedRouteStageVM.Id); // Находим сущность в контексте
                if (stageToSave == null) { MessageBox.Show("Редактируемый этап не найден в базе данных."); return; }
                stageToSave.OperationNumber = opNum; stageToSave.OperationName = opName; stageToSave.MachineTypeId = SelectedMachineTypeForStageEdit.Id; stageToSave.StandardTimePerUnit = stdTime;
            }
            else
            {
                int order = (RouteStagesVM?.Where(vm => vm != null).Count() > 0) ? RouteStagesVM.Where(vm => vm != null).Max(rsVM => rsVM.OrderInRoute) + 1 : 1;
                stageToSave = new RouteStage { DetailId = SelectedDetail.Id, OperationNumber = opNum, OperationName = opName, MachineTypeId = SelectedMachineTypeForStageEdit.Id, StandardTimePerUnit = stdTime, OrderInRoute = order };
                _context.RouteStages.Add(stageToSave);
            }
            try { _context.SaveChanges(); int id = stageToSave.Id; LoadRouteStagesForSelectedDetail(); SelectedRouteStageVM = RouteStagesVM?.FirstOrDefault(rsVM => rsVM.Id == id); MessageBox.Show("Этап сохранен."); }
            catch (System.Exception ex) { MessageBox.Show($"Ошибка сохранения этапа: {ex.Message}"); }
            finally { _isEditingRouteStage = false; UpdateAllCommandsCanExecute(); }
        }
        private void StartEditRouteStage()
        {
            if (SelectedRouteStageVM == null) return; _isEditingRouteStage = true;
            StageOpNumberToEdit = SelectedRouteStageVM.OperationNumber;
            StageOpNameToEdit = SelectedRouteStageVM.OperationName;
            SelectedMachineTypeForStageEdit = MachineTypes?.FirstOrDefault(mt => mt.Id == SelectedRouteStageVM.MachineTypeId);
            StageStdTimeToEdit = SelectedRouteStageVM.StandardTimePerUnit.ToString(CultureInfo.InvariantCulture);
            StageOrderInRouteToDisplay = SelectedRouteStageVM.OrderInRoute.ToString();
            UpdateAllCommandsCanExecute();
        }
        private void DeleteRouteStage()
        {
            if (SelectedRouteStageVM == null || SelectedDetail == null) return;
            if (MessageBox.Show($"Удалить этап '{SelectedRouteStageVM.OperationName}'?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                var stageToRemove = _context.RouteStages.Find(SelectedRouteStageVM.Id);
                if (stageToRemove != null) _context.RouteStages.Remove(stageToRemove);
                else { MessageBox.Show("Удаляемый этап не найден."); return; }
                try { _context.SaveChanges(); LoadRouteStagesForSelectedDetail(); MessageBox.Show("Этап удален."); }
                catch (System.Exception ex) { MessageBox.Show($"Ошибка удаления этапа: {ex.Message}"); }
                finally { _isEditingRouteStage = false; SelectedRouteStageVM = null; UpdateAllCommandsCanExecute(); }
            }
        }
        private void CancelRouteStageOperation() { _isEditingRouteStage = false; SelectedRouteStageVM = null; /*LoadRouteStagesForSelectedDetail();*/ UpdateAllCommandsCanExecute(); }
        #endregion
    }

    // Вспомогательная ViewModel для отображения RouteStage с именем типа станка
    public class RouteStageViewModel : ViewModelBase // Убедитесь, что ViewModelBase здесь доступен или продублируйте/перенесите
    {
        private readonly RouteStage _routeStage;
        public RouteStageViewModel(RouteStage routeStage) { _routeStage = routeStage; }

        public int Id => _routeStage.Id;
        public string OperationNumber => _routeStage.OperationNumber;
        public string OperationName => _routeStage.OperationName;
        public int MachineTypeId => _routeStage.MachineTypeId;
        public string ApplicableMachineTypeName => _routeStage.ApplicableMachineType?.Name ?? "N/A";
        public double StandardTimePerUnit => _routeStage.StandardTimePerUnit;
        public int OrderInRoute => _routeStage.OrderInRoute;
    }
}
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Services;

namespace InventoryManagementSystem.UI.ViewModels
{
    public partial class ManufacturingViewModel : ViewModelBase
    {
        private readonly ManufacturingService _manufacturingService;
        private readonly InventoryService _inventoryService;
        
        public LanguageService Language { get; }

        private List<BillOfMaterialListItem> _allBomsList = new();
        private List<ManufacturingOrderListItem> _allMOsList = new();
        private bool _isLoadingDetail;

        [ObservableProperty]
        private int _selectedTabIndex;

        // ==================== TABS SHARED ====================
        [ObservableProperty]
        private ObservableCollection<Product> _products = new();

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        public List<string> Units { get; } = new() { "Pcs", "Box", "g", "kg", "l", "Per Unit" };

        // ==================== TAB 1: BoM PROPERTIES ====================
        [ObservableProperty]
        private ObservableCollection<BillOfMaterialListItem> _boms = new();

        [ObservableProperty]
        private BillOfMaterialListItem? _selectedBom;

        [ObservableProperty]
        private bool _isFormVisible;

        [ObservableProperty]
        private string _searchText = string.Empty;

        // BoM Form Fields
        private BillOfMaterial? _editingBom;

        [ObservableProperty]
        private Product? _selectedFinalProduct;

        [ObservableProperty]
        private double _quantity = 1.0;

        [ObservableProperty]
        private string _reference = string.Empty;

        [ObservableProperty]
        private string _selectedBomType = "Manufacture this product";

        [ObservableProperty]
        private string _company = "My Company";

        [ObservableProperty]
        private double _yieldPercent = 100.0;

        [ObservableProperty]
        private double _scrapPercent = 0.0;

        [ObservableProperty]
        private ObservableCollection<BomLineViewModel> _componentLines = new();

        public List<string> BomTypes { get; } = new() { "Manufacture this product", "Kit" };

        public bool IsManufactureType
        {
            get => SelectedBomType == "Manufacture this product";
            set
            {
                if (value)
                {
                    SelectedBomType = "Manufacture this product";
                    OnPropertyChanged(nameof(IsManufactureType));
                    OnPropertyChanged(nameof(IsKitType));
                }
            }
        }

        public bool IsKitType
        {
            get => SelectedBomType == "Kit";
            set
            {
                if (value)
                {
                    SelectedBomType = "Kit";
                    OnPropertyChanged(nameof(IsManufactureType));
                    OnPropertyChanged(nameof(IsKitType));
                }
            }
        }

        // ==================== TAB 2: MO PROPERTIES ====================
        [ObservableProperty]
        private ObservableCollection<ManufacturingOrderListItem> _manufacturingOrders = new();

        [ObservableProperty]
        private ManufacturingOrderListItem? _selectedMO;

        [ObservableProperty]
        private bool _isMOFormVisible;

        [ObservableProperty]
        private string _searchMOText = string.Empty;

        // MO Form Fields
        private ManufacturingOrder? _editingMO;

        [ObservableProperty]
        private Product? _mOProduct;

        [ObservableProperty]
        private BillOfMaterialListItem? _selectedBoMForMO;

        [ObservableProperty]
        private double _mOTargetQuantity = 1.0;

        [ObservableProperty]
        private double _mOActualQuantity = 1.0;

        [ObservableProperty]
        private string _mONumber = string.Empty;

        [ObservableProperty]
        private string _mOStatus = "Draft";

        public bool IsDraftState => MOStatus == "Draft" || string.IsNullOrEmpty(MOStatus);
        public bool IsConfirmedState => MOStatus == "Confirmed";
        public bool IsDoneState => MOStatus == "Done";

        partial void OnMOStatusChanged(string value)
        {
            OnPropertyChanged(nameof(IsDraftState));
            OnPropertyChanged(nameof(IsConfirmedState));
            OnPropertyChanged(nameof(IsDoneState));
        }

        [ObservableProperty]
        private string _mOCompany = "My Company";

        [ObservableProperty]
        private ObservableCollection<MoLineViewModel> _mOComponentLines = new();

        [ObservableProperty]
        private bool _isProduceStateActive; // True when user clicked "Produce" and is editing final actual yield

        [ObservableProperty]
        private ObservableCollection<BillOfMaterialListItem> _activeBoms = new();

        // ==================== TAB 3: REPORTING PROPERTIES ====================
        [ObservableProperty]
        private int _totalMOsCount;

        [ObservableProperty]
        private int _completedMOsCount;

        [ObservableProperty]
        private decimal _totalProductionCost;

        [ObservableProperty]
        private ObservableCollection<ManufacturingOrderListItem> _reportOrders = new();

        // ==================== CONSTRUCTOR ====================
        public ManufacturingViewModel(
            ManufacturingService manufacturingService,
            InventoryService inventoryService,
            LanguageService languageService)
        {
            _manufacturingService = manufacturingService;
            _inventoryService = inventoryService;
            Language = languageService;

            LoadBomsCommand.Execute(null);
        }

        partial void OnSelectedTabIndexChanged(int value)
        {
            ErrorMessage = string.Empty;
            if (value == 0)
            {
                LoadBomsCommand.Execute(null);
            }
            else if (value == 1)
            {
                LoadMOsCommand.Execute(null);
            }
            else if (value == 2)
            {
                LoadReportCommand.Execute(null);
            }
        }

        // ==================== TAB 1: BoM METHODS ====================
        [RelayCommand]
        public async Task LoadBoms()
        {
            try
            {
                var list = await _manufacturingService.GetAllBomsAsync();
                _allBomsList = list;
                FilterBoms();

                var productList = await _inventoryService.GetAllProductsAsync();
                Products = new ObservableCollection<Product>(productList.OrderBy(p => p.Name));
                
                ErrorMessage = string.Empty;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading BoMs: {ex.Message}";
            }
        }

        partial void OnSearchTextChanged(string value)
        {
            FilterBoms();
        }

        private void FilterBoms()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                Boms = new ObservableCollection<BillOfMaterialListItem>(_allBomsList);
            }
            else
            {
                var query = SearchText.ToLower();
                var filtered = _allBomsList.Where(b => 
                    b.ProductName.ToLower().Contains(query) || 
                    b.Reference.ToLower().Contains(query) ||
                    b.BomType.ToLower().Contains(query) ||
                    b.Company.ToLower().Contains(query)
                ).ToList();
                Boms = new ObservableCollection<BillOfMaterialListItem>(filtered);
            }
        }

        [RelayCommand]
        public void ShowCreateBomForm()
        {
            _editingBom = null;
            SelectedFinalProduct = null;
            Quantity = 1.0;
            Reference = $"BOM-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";
            SelectedBomType = "Manufacture this product";
            OnPropertyChanged(nameof(IsManufactureType));
            OnPropertyChanged(nameof(IsKitType));
            Company = "My Company";
            ComponentLines.Clear();
            ErrorMessage = string.Empty;

            AddComponentLine();

            IsFormVisible = true;
        }

        [RelayCommand]
        public async Task OpenBomDetail(BillOfMaterialListItem item)
        {
            if (item == null) return;

            try
            {
                _editingBom = item.BillOfMaterial;
                SelectedFinalProduct = Products.FirstOrDefault(p => p.Id == _editingBom.ProductId);
                Quantity = _editingBom.Quantity;
                Reference = _editingBom.Reference;
                SelectedBomType = _editingBom.BomType;
                OnPropertyChanged(nameof(IsManufactureType));
                OnPropertyChanged(nameof(IsKitType));
                Company = _editingBom.Company;
                YieldPercent = _editingBom.YieldPercent;
                ScrapPercent = _editingBom.ScrapPercent;

                ComponentLines.Clear();
                var lines = await _manufacturingService.GetBomLinesAsync(_editingBom.Id);
                foreach (var line in lines)
                {
                    var product = Products.FirstOrDefault(p => p.Id == line.ProductId);
                    ComponentLines.Add(new BomLineViewModel(Products, Units)
                    {
                        SelectedProduct = product,
                        Quantity = line.Quantity,
                        Unit = line.Unit,
                        ScrapPercent = line.ScrapPercent
                    });
                }

                IsFormVisible = true;
                ErrorMessage = string.Empty;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading BoM details: {ex.Message}";
            }
        }

        [RelayCommand]
        public void AddComponentLine()
        {
            var line = new BomLineViewModel(Products, Units);
            ComponentLines.Add(line);
        }

        [RelayCommand]
        public void RemoveComponentLine(BomLineViewModel line)
        {
            if (ComponentLines.Contains(line))
            {
                ComponentLines.Remove(line);
            }
        }

        [RelayCommand]
        public void CloseForm()
        {
            IsFormVisible = false;
            _editingBom = null;
            ErrorMessage = string.Empty;
        }

        [RelayCommand]
        public async Task SaveBom()
        {
            if (SelectedFinalProduct == null)
            {
                ErrorMessage = "Please select a final manufactured product.";
                return;
            }

            if (Quantity <= 0)
            {
                ErrorMessage = "Quantity must be greater than zero.";
                return;
            }

            if (ComponentLines.Count == 0)
            {
                ErrorMessage = "Please add at least one component to the Bill of Materials.";
                return;
            }

            foreach (var line in ComponentLines)
            {
                if (line.SelectedProduct == null)
                {
                    ErrorMessage = "Please ensure all component rows have a selected product.";
                    return;
                }
                if (line.Quantity <= 0)
                {
                    ErrorMessage = "Please ensure all component rows have quantity greater than zero.";
                    return;
                }
            }

            try
            {
                var bom = _editingBom ?? new BillOfMaterial();
                bom.ProductId = SelectedFinalProduct.Id;
                bom.Quantity = Quantity;
                bom.Reference = Reference;
                bom.BomType = SelectedBomType;
                bom.Company = Company;
                bom.YieldPercent = YieldPercent;
                bom.ScrapPercent = ScrapPercent;

                var lines = ComponentLines.Select(cl => new BillOfMaterialLine
                {
                    ProductId = cl.SelectedProduct!.Id,
                    Quantity = cl.Quantity,
                    Unit = cl.Unit,
                    ScrapPercent = cl.ScrapPercent
                }).ToList();

                await _manufacturingService.SaveBomAsync(bom, lines);
                await LoadBoms();
                
                IsFormVisible = false;
                _editingBom = null;
                ErrorMessage = string.Empty;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to save BoM: {ex.Message}";
            }
        }

        // ==================== TAB 2: MO METHODS ====================
        [RelayCommand]
        public async Task LoadMOs()
        {
            try
            {
                var list = await _manufacturingService.GetAllManufacturingOrdersAsync();
                _allMOsList = list;
                FilterMOs();

                var bomsList = await _manufacturingService.GetAllBomsAsync();
                ActiveBoms = new ObservableCollection<BillOfMaterialListItem>(bomsList);

                var productList = await _inventoryService.GetAllProductsAsync();
                Products = new ObservableCollection<Product>(productList.OrderBy(p => p.Name));

                ErrorMessage = string.Empty;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading MOs: {ex.Message}";
            }
        }

        partial void OnSearchMOTextChanged(string value)
        {
            FilterMOs();
        }

        private void FilterMOs()
        {
            if (string.IsNullOrWhiteSpace(SearchMOText))
            {
                ManufacturingOrders = new ObservableCollection<ManufacturingOrderListItem>(_allMOsList);
            }
            else
            {
                var query = SearchMOText.ToLower();
                var filtered = _allMOsList.Where(o => 
                    o.MONumber.ToLower().Contains(query) || 
                    o.ProductName.ToLower().Contains(query) ||
                    o.Status.ToLower().Contains(query) ||
                    o.Company.ToLower().Contains(query)
                ).ToList();
                ManufacturingOrders = new ObservableCollection<ManufacturingOrderListItem>(filtered);
            }
        }

        partial void OnSelectedBoMForMOChanged(BillOfMaterialListItem? value)
        {
            if (_isLoadingDetail) return;

            if (value != null)
            {
                MOProduct = Products.FirstOrDefault(p => p.Id == value.BillOfMaterial.ProductId);
                MOTargetQuantity = value.BillOfMaterial.Quantity;
                MOActualQuantity = value.BillOfMaterial.Quantity;
                _ = PopulateMOComponentsFromBoMAsync(value.BillOfMaterial.Id);
            }
        }

        private async Task PopulateMOComponentsFromBoMAsync(int bomId)
        {
            try
            {
                MOComponentLines.Clear();
                var lines = await _manufacturingService.BuildExpectedLinesFromBomAsync(bomId, MOTargetQuantity);
                foreach (var line in lines)
                {
                    var product = Products.FirstOrDefault(p => p.Id == line.ProductId);
                    MOComponentLines.Add(new MoLineViewModel
                    {
                        ProductId = line.ProductId,
                        ProductName = product?.Name ?? "Unknown Product",
                        ExpectedQuantity = line.ExpectedQuantity,
                        ActualQuantity = line.ActualQuantity,
                        Unit = line.Unit,
                        UnitCost = line.UnitCost
                    });
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load BoM components: {ex.Message}";
            }
        }

        partial void OnMOActualQuantityChanged(double value)
        {
            // Linearly scale ingredient components based on the yield
            if (MOTargetQuantity > 0 && MOComponentLines.Count > 0)
            {
                var ratio = value / MOTargetQuantity;
                foreach (var line in MOComponentLines)
                {
                    line.ActualQuantity = Math.Round(line.ExpectedQuantity * ratio, 4);
                }
            }
        }

        [RelayCommand]
        public void ShowCreateMOForm()
        {
            _editingMO = null;
            SelectedBoMForMO = null;
            MOProduct = null;
            MOTargetQuantity = 1.0;
            MOActualQuantity = 1.0;
            MONumber = $"MO-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}";
            MOStatus = "Draft";
            MOCompany = "My Company";
            MOComponentLines.Clear();
            IsProduceStateActive = false;
            ErrorMessage = string.Empty;

            IsMOFormVisible = true;
        }

        [RelayCommand]
        public async Task OpenMODetail(ManufacturingOrderListItem item)
        {
            if (item == null) return;

            try
            {
                _isLoadingDetail = true;
                _editingMO = item.ManufacturingOrder;
                MOProduct = Products.FirstOrDefault(p => p.Id == _editingMO.ProductId);
                
                // Set BoM selection
                SelectedBoMForMO = ActiveBoms.FirstOrDefault(b => b.BillOfMaterial.Id == _editingMO.BomId);
                
                MOTargetQuantity = _editingMO.TargetQuantity;
                MOActualQuantity = _editingMO.Status == "Done" ? _editingMO.ActualQuantity : _editingMO.TargetQuantity;
                MONumber = _editingMO.MONumber;
                MOStatus = _editingMO.Status;
                MOCompany = _editingMO.Company;
                IsProduceStateActive = false;

                MOComponentLines.Clear();
                var lines = await _manufacturingService.GetManufacturingOrderLinesAsync(_editingMO.Id);
                foreach (var line in lines)
                {
                    var product = Products.FirstOrDefault(p => p.Id == line.ProductId);
                    MOComponentLines.Add(new MoLineViewModel
                    {
                        ProductId = line.ProductId,
                        ProductName = product?.Name ?? "Unknown Product",
                        ExpectedQuantity = line.ExpectedQuantity,
                        ActualQuantity = _editingMO.Status == "Done" ? line.ActualQuantity : line.ExpectedQuantity,
                        Unit = line.Unit,
                        UnitCost = line.UnitCost > 0 ? line.UnitCost : (product?.Cost ?? 0m)
                    });
                }

                IsMOFormVisible = true;
                ErrorMessage = string.Empty;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading MO details: {ex.Message}";
            }
            finally
            {
                _isLoadingDetail = false;
            }
        }

        private bool _isSaving;

        [RelayCommand]
        public async Task SaveMODraft()
        {
            if (_isSaving) return;
            _isSaving = true;

            if (SelectedBoMForMO == null)
            {
                ErrorMessage = "Please select a Bill of Materials recipe.";
                _isSaving = false;
                return;
            }

            if (MOTargetQuantity <= 0)
            {
                ErrorMessage = "Target quantity must be greater than zero.";
                _isSaving = false;
                return;
            }

            try
            {
                var mo = _editingMO ?? new ManufacturingOrder();
                mo.MONumber = MONumber;
                mo.BomId = SelectedBoMForMO.BillOfMaterial.Id;
                mo.ProductId = MOProduct?.Id ?? 0;
                mo.TargetQuantity = MOTargetQuantity;
                mo.Status = "Draft";
                mo.Company = MOCompany;
                mo.OrderDate = DateTime.Now;

                var lines = MOComponentLines.Select(cl => new ManufacturingOrderLine
                {
                    ProductId = cl.ProductId,
                    ExpectedQuantity = cl.ExpectedQuantity,
                    ActualQuantity = cl.ExpectedQuantity, // default same as expected
                    Unit = cl.Unit,
                    UnitCost = cl.UnitCost
                }).ToList();

                await _manufacturingService.SaveManufacturingOrderAsync(mo, lines);
                _editingMO = mo;
                await LoadMOs();

                IsMOFormVisible = false;
                ErrorMessage = string.Empty;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to save Manufacturing Order: {ex.Message}";
            }
            finally
            {
                _isSaving = false;
            }
        }

        [RelayCommand]
        public async Task ConfirmMO()
        {
            if (_isSaving) return;
            _isSaving = true;

            if (SelectedBoMForMO == null)
            {
                ErrorMessage = "Please select a Bill of Materials recipe.";
                _isSaving = false;
                return;
            }

            if (MOTargetQuantity <= 0)
            {
                ErrorMessage = "Target quantity must be greater than zero.";
                _isSaving = false;
                return;
            }

            try
            {
                // Auto-save first as a draft to ensure it exists in the database
                var mo = _editingMO ?? new ManufacturingOrder();
                mo.MONumber = MONumber;
                mo.BomId = SelectedBoMForMO.BillOfMaterial.Id;
                mo.ProductId = MOProduct?.Id ?? 0;
                mo.TargetQuantity = MOTargetQuantity;
                mo.Status = "Draft";
                mo.Company = MOCompany;
                if (mo.Id == 0) mo.OrderDate = DateTime.Now;

                var lines = MOComponentLines.Select(cl => new ManufacturingOrderLine
                {
                    ProductId = cl.ProductId,
                    ExpectedQuantity = cl.ExpectedQuantity,
                    ActualQuantity = cl.ExpectedQuantity,
                    Unit = cl.Unit,
                    UnitCost = cl.UnitCost
                }).ToList();

                await _manufacturingService.SaveManufacturingOrderAsync(mo, lines);
                _editingMO = mo;

                // Confirm the order in database
                await _manufacturingService.ConfirmManufacturingOrderAsync(_editingMO.Id);
                await LoadMOs();
                
                // Reload MO details in the same view (so the form stays open and changes state)
                var item = ManufacturingOrders.FirstOrDefault(o => o.ManufacturingOrder.Id == _editingMO.Id);
                if (item != null)
                {
                    await OpenMODetail(item);
                }
                
                ErrorMessage = string.Empty;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to confirm MO: {ex.Message}";
            }
            finally
            {
                _isSaving = false;
            }
        }

        [RelayCommand]
        public void StartProductionInput()
        {
            // Prompt the actual produced quantity edit and ingredients adjustments
            MOActualQuantity = MOTargetQuantity;
            IsProduceStateActive = true;
            ErrorMessage = string.Empty;
        }

        [RelayCommand]
        public async Task RecordProduction()
        {
            if (_editingMO == null) return;

            if (MOActualQuantity < 0)
            {
                ErrorMessage = "Actual quantity cannot be negative.";
                return;
            }

            foreach (var line in MOComponentLines)
            {
                if (line.ActualQuantity < 0)
                {
                    ErrorMessage = "Ingredient actual quantities cannot be negative.";
                    return;
                }
            }

            try
            {
                var lines = MOComponentLines.Select(cl => new ManufacturingOrderLine
                {
                    ProductId = cl.ProductId,
                    ExpectedQuantity = cl.ExpectedQuantity,
                    ActualQuantity = cl.ActualQuantity,
                    Unit = cl.Unit,
                    UnitCost = cl.UnitCost
                }).ToList();

                // Save in service (updates stock, creates stock movements, sets status to Done, logs cost)
                string username = UserSession.CurrentUser?.Username ?? "System";
                await _manufacturingService.ProduceManufacturingOrderAsync(_editingMO.Id, MOActualQuantity, lines, username);
                
                await LoadMOs();
                IsMOFormVisible = false;
                IsProduceStateActive = false;
                _editingMO = null;
                ErrorMessage = string.Empty;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Production failed: {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task DeleteMO(ManufacturingOrderListItem item)
        {
            if (item == null) return;

            try
            {
                await _manufacturingService.DeleteManufacturingOrderAsync(item.ManufacturingOrder.Id);
                await LoadMOs();
                SelectedMO = null;
                ErrorMessage = string.Empty;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to delete MO: {ex.Message}";
            }
        }

        [RelayCommand]
        public void CloseMOForm()
        {
            IsMOFormVisible = false;
            IsProduceStateActive = false;
            _editingMO = null;
            ErrorMessage = string.Empty;
        }

        // ==================== TAB 3: REPORTING METHODS ====================
        [RelayCommand]
        public async Task LoadReport()
        {
            try
            {
                var summary = await _manufacturingService.GetProductionReportAsync();
                TotalMOsCount = summary.TotalMOs;
                CompletedMOsCount = summary.CompletedMOs;
                TotalProductionCost = summary.TotalProductionCost;

                ReportOrders = new ObservableCollection<ManufacturingOrderListItem>(summary.CompletedOrders);
                ErrorMessage = string.Empty;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading production reports: {ex.Message}";
            }
        }
    }

    public partial class MoLineViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _productName = string.Empty;

        [ObservableProperty]
        private int _productId;

        [ObservableProperty]
        private double _expectedQuantity;

        [ObservableProperty]
        private double _actualQuantity;

        [ObservableProperty]
        private string _unit = "Pcs";

        [ObservableProperty]
        private decimal _unitCost;
    }

    public partial class BomLineViewModel : ViewModelBase
    {
        [ObservableProperty]
        private Product? _selectedProduct;

        [ObservableProperty]
        private double _quantity = 1.0;

        [ObservableProperty]
        private string _unit = "Pcs";

        [ObservableProperty]
        private double _scrapPercent;

        public ObservableCollection<Product> Products { get; }
        public List<string> Units { get; }

        public BomLineViewModel(IEnumerable<Product> products, List<string> units)
        {
            Products = new ObservableCollection<Product>(products);
            Units = units;
        }

        partial void OnSelectedProductChanged(Product? value)
        {
            if (value != null)
            {
                Unit = value.Unit;
            }
        }
    }
}

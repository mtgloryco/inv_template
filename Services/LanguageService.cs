using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace InventoryManagementSystem.Services
{
    public class LanguageService : INotifyPropertyChanged
    {
        private string _currentLanguage = "en"; // Default
        public string CurrentLanguage 
        { 
            get => _currentLanguage;
            private set
            {
                if (_currentLanguage != value)
                {
                    _currentLanguage = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Resources));
                }
            }
        }

        // Expose a dictionary binding for the UI
        public Dictionary<string, string> Resources => _dictionaries[_currentLanguage];

        private readonly Dictionary<string, Dictionary<string, string>> _dictionaries = new()
        {
            ["en"] = new() 
            {
                ["Dashboard"] = "Dashboard",
                ["POS"] = "POS / Cashier",
                ["Inventory"] = "Inventory",
                ["Reports"] = "Reports",
                ["Insights"] = "Insights",
                ["Users"] = "Users",
                ["License"] = "License",
                ["Exit"] = "Exit Application",
                ["TotalRevenue"] = "Total Revenue",
                ["TotalProfit"] = "Total Profit",
                ["InventoryValue"] = "Inventory Value",
                ["LowStockWarning"] = "Low Stock Warning",
                ["QuickActions"] = "Quick Actions",
                ["AddProduct"] = "Add Product",
                ["StockIN"] = "Stock IN",
                ["StockOUT"] = "Stock OUT",
                ["BulkImport"] = "Bulk Import",
                ["RecentMovements"] = "Recent Stock Movements",
                ["Welcome"] = "Welcome back, {0}",
                ["ItemsLow"] = "items are running low on stock. Check Inventory.",
                ["Login"] = "Login",
                ["Username"] = "Username",
                ["Password"] = "Password",
                ["LoginButton"] = "Login"
            },
            ["fr"] = new()
            {
                ["Dashboard"] = "Tableau de bord",
                ["POS"] = "Caisse / PV",
                ["Inventory"] = "Inventaire",
                ["Reports"] = "Rapports",
                ["Insights"] = "Analyses",
                ["Users"] = "Utilisateurs",
                ["License"] = "Licence",
                ["Exit"] = "Quitter",
                ["TotalRevenue"] = "Revenu Total",
                ["TotalProfit"] = "Benefice Total",
                ["InventoryValue"] = "Valeur Inventaire",
                ["LowStockWarning"] = "Alerte Stock Faible",
                ["QuickActions"] = "Actions Rapides",
                ["AddProduct"] = "Ajouter Produit",
                ["StockIN"] = "Entree Stock",
                ["StockOUT"] = "Sortie Stock",
                ["BulkImport"] = "Import Global",
                ["RecentMovements"] = "Mouvements Recents",
                ["Welcome"] = "Bienvenue, {0}",
                ["ItemsLow"] = "articles sont en rupture de stock. Verifier l'inventaire.",
                ["Login"] = "Connexion",
                ["Username"] = "Nom d'utilisateur",
                ["Password"] = "Mot de passe",
                ["LoginButton"] = "Se connecter"
            },
            ["rw"] = new()
            {
                ["Dashboard"] = "Ikibaho (Dashboard)",
                ["POS"] = "Ahagurirwa (POS)",
                ["Inventory"] = "Ububiko",
                ["Reports"] = "Raporo",
                ["Insights"] = "Isesengura",
                ["Users"] = "Abakozi",
                ["License"] = "Uruhushya",
                ["Exit"] = "Funga Porogaramu",
                ["TotalRevenue"] = "Amafaranga Yinjiye",
                ["TotalProfit"] = "Inyungu",
                ["InventoryValue"] = "Agaciro k'Ibicuruzwa",
                ["LowStockWarning"] = "Ibicuruzwa Byabaye Bike",
                ["QuickActions"] = "Ibikorwa Byihuse",
                ["AddProduct"] = "Ongeramo Igicuruzwa",
                ["StockIN"] = "Kwinjiza/Kurangura",
                ["StockOUT"] = "Gusohora/Kugurisha",
                ["BulkImport"] = "Injiza Byinshi (Import)",
                ["RecentMovements"] = "Ibyahindutse Vuba",
                ["Welcome"] = "Murakaza neza, {0}",
                ["ItemsLow"] = "byabaye bike cyane mu bubiko. Reba Ububiko.",
                ["Login"] = "Injira",
                ["Username"] = "Izina",
                ["Password"] = "Ijambo ry'ibanga",
                ["LoginButton"] = "Injira"
            }
        };

        public void SetLanguage(string code)
        {
            if (_dictionaries.ContainsKey(code))
            {
                CurrentLanguage = code;
            }
        }

        public string GetString(string key)
        {
            if (Resources.TryGetValue(key, out var value))
            {
                return value;
            }
            return key; // Fallback to key itself
        }
        
        // Dynamic property access for Binding: {Binding Language.Res[Key]}
        public string this[string key] => GetString(key);

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

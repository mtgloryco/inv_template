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
                ["Inventory"] = "Products",
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
                ["LoginButton"] = "Login",
                // Inventory
                ["Inv_PaneTitle"] = "Manage Product",
                ["Inv_ProductName"] = "Product Name",
                ["Inv_SKU"] = "SKU",
                ["Inv_Category"] = "Category",
                ["Inv_Unit"] = "Unit (e.g., Pcs, Kg)",
                ["Inv_InitialStock"] = "Initial Stock",
                ["Inv_MovementType"] = "Movement Type",
                ["Inv_Quantity"] = "Quantity",
                ["Inv_CostPerUnit"] = "Cost per Unit",
                ["Inv_SellingPrice"] = "Selling Price",
                ["Inv_Reason"] = "Reason",
                ["Inv_SearchPlaceholder"] = "Search products...",
                ["Inv_Import"] = "Import (CSV)",
                ["Inv_NewProduct"] = "+ New Product",
                ["Inv_Stock"] = "Stock",
                ["Inv_Actions"] = "Actions",
                ["Inv_Cancel"] = "Cancel",
                ["Inv_Save"] = "Save",

                // POS
                ["POS_Title"] = "Point of Sale",
                ["POS_Subtitle"] = "Select products to add to cart",
                ["POS_SearchPlaceholder"] = "Search by Name or SKU...",
                ["POS_CurrentOrder"] = "Current Order",
                ["POS_Items"] = "Items",
                ["POS_TotalAmount"] = "Total Amount",
                ["POS_AmountPaid"] = "Amount Paid",
                ["POS_ChangeDue"] = "Change Due",
                ["POS_Checkout"] = "COMPLETE CHECKOUT",
                ["POS_PaymentSuccess"] = "Payment Successful!",
                ["POS_ReceiptDetails"] = "Receipt Details",
                ["POS_Close"] = "Close",
                ["POS_Print"] = "Print Receipt",

                // Settings
                ["Settings_Title"] = "System Settings",
                ["Settings_StoreInfo"] = "Store Information",
                ["Settings_StoreInfoDesc"] = "These details will appear on printed receipts.",
                ["Settings_StoreName"] = "Store Name",
                ["Settings_Address"] = "Address",
                ["Settings_Currency"] = "Currency",
                ["Settings_Printer"] = "Printer Name",
                ["Settings_Save"] = "Save Settings",

                // Reports
                ["Rep_Title"] = "Reports & Analytics",
                ["Rep_Subtitle"] = "Generate financial and inventory reports",
                ["Rep_Sales"] = "Sales Report",
                ["Rep_InvValue"] = "Inventory Val.",
                ["Rep_LowStock"] = "Low Stock",
                ["Rep_Profit"] = "Profit & Loss",
                ["Rep_Generate"] = "Generate Report",
                ["Rep_ExportPDF"] = "Export PDF",
                // Sidebar
                ["Suppliers"] = "Suppliers",
                ["PurchaseOrders"] = "Purchase Orders",
                ["Forecasting"] = "Forecasting",
                ["ReorderDashboard"] = "Reorder Dashboard",
                ["ExpiryDashboard"] = "Expiry Dashboard",
                ["Locations"] = "Locations",
                ["Returns"] = "Returns",
                ["Bundles"] = "Bundles & Kits",
                ["AuditTrail"] = "Audit Trail",
                ["AdvancedInsights"] = "Advanced Insights",
                ["Settings"] = "Settings"
            },
            ["fr"] = new()
            {
                ["Dashboard"] = "Tableau de bord",
                ["POS"] = "Caisse / PV",
                ["Inventory"] = "Produits",
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
                ["LoginButton"] = "Se connecter",

                // Inventory
                ["Inv_PaneTitle"] = "Gerer Produit",
                ["Inv_ProductName"] = "Nom du Produit",
                ["Inv_SKU"] = "SKU",
                ["Inv_Category"] = "Categorie",
                ["Inv_Unit"] = "Unite (ex: Pcs, Kg)",
                ["Inv_InitialStock"] = "Stock Initial",
                ["Inv_MovementType"] = "Type de Mouvement",
                ["Inv_Quantity"] = "Quantite",
                ["Inv_CostPerUnit"] = "Cout unitaire",
                ["Inv_SellingPrice"] = "Prix de Vente",
                ["Inv_Reason"] = "Raison",
                ["Inv_SearchPlaceholder"] = "Rechercher...",
                ["Inv_Import"] = "Importer (CSV)",
                ["Inv_NewProduct"] = "+ Nouveau Produit",
                ["Inv_Stock"] = "Stock",
                ["Inv_Actions"] = "Actions",
                ["Inv_Cancel"] = "Annuler",
                ["Inv_Save"] = "Enregistrer",

                // POS
                ["POS_Title"] = "Point de Vente",
                ["POS_Subtitle"] = "Selectionner produits",
                ["POS_SearchPlaceholder"] = "Recherche par Nom ou SKU...",
                ["POS_CurrentOrder"] = "Commande Actuelle",
                ["POS_Items"] = "Articles",
                ["POS_TotalAmount"] = "Montant Total",
                ["POS_AmountPaid"] = "Montant Paye",
                ["POS_ChangeDue"] = "Monnaie a Rendre",
                ["POS_Checkout"] = "TERMINER VENTE",
                ["POS_PaymentSuccess"] = "Paiement Reussi!",
                ["POS_ReceiptDetails"] = "Details du Recu",
                ["POS_Close"] = "Fermer",
                ["POS_Print"] = "Imprimer Recu",

                // Settings
                ["Settings_Title"] = "Parametres Systeme",
                ["Settings_StoreInfo"] = "Information Magasin",
                ["Settings_StoreInfoDesc"] = "Ces details apparaitront sur les recus.",
                ["Settings_StoreName"] = "Nom du Magasin",
                ["Settings_Address"] = "Adresse",
                ["Settings_Currency"] = "Devise",
                ["Settings_Printer"] = "Nom Imprimante",
                ["Settings_Save"] = "Enregistrer Parametres",

                // Reports
                ["Rep_Title"] = "Rapports & Analyses",
                ["Rep_Subtitle"] = "Generer rapports financiers",
                ["Rep_Sales"] = "Rapport Ventes",
                ["Rep_InvValue"] = "Val. Inventaire",
                ["Rep_LowStock"] = "Stock Faible",
                ["Rep_Profit"] = "Pertes & Profits",
                ["Rep_Generate"] = "Generer Rapport",
                ["Rep_ExportPDF"] = "Exporter PDF",
                // Sidebar
                ["Suppliers"] = "Fournisseurs",
                ["PurchaseOrders"] = "Bons de Commande",
                ["Forecasting"] = "Previsions",
                ["ReorderDashboard"] = "Tableau Reapprov.",
                ["ExpiryDashboard"] = "Tableau Expiration",
                ["Locations"] = "Emplacements",
                ["Returns"] = "Retours",
                ["Bundles"] = "Kitting & Bundles",
                ["AuditTrail"] = "Audit System",
                ["AdvancedInsights"] = "Analyses Avancees",
                ["Settings"] = "Parametres"
            },
            ["rw"] = new()
            {
                ["Dashboard"] = "Ikibaho (Dashboard)",
                ["POS"] = "Ahagurirwa (POS)",
                ["Inventory"] = "Ibicuruzwa",
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
                ["LoginButton"] = "Injira",

                // Inventory
                ["Inv_PaneTitle"] = "Hindura Igicuruzwa",
                ["Inv_ProductName"] = "Izina ry'igicuruzwa",
                ["Inv_SKU"] = "SKU (Kode)",
                ["Inv_Category"] = "Icyiciro",
                ["Inv_Unit"] = "Urugero (urugero: Pcs, Kg)",
                ["Inv_InitialStock"] = "Ingano itangira",
                ["Inv_MovementType"] = "Ubwoko bw'igikorwa",
                ["Inv_Quantity"] = "Ingano",
                ["Inv_CostPerUnit"] = "Igiciro cyo kurangura",
                ["Inv_SellingPrice"] = "Igiciro cyo kugurisha",
                ["Inv_Reason"] = "Impamvu",
                ["Inv_SearchPlaceholder"] = "Shakisha...",
                ["Inv_Import"] = "Injiza (CSV)",
                ["Inv_NewProduct"] = "+ Igicuruzwa Gishya",
                ["Inv_Stock"] = "Ububiko",
                ["Inv_Actions"] = "Ibikorwa",
                ["Inv_Cancel"] = "Bureka",
                ["Inv_Save"] = "Bika",

                // POS
                ["POS_Title"] = "Aho bagurishiriza",
                ["POS_Subtitle"] = "Hitamo ibicuruzwa",
                ["POS_SearchPlaceholder"] = "Shaka Izina cyangwa SKU...",
                ["POS_CurrentOrder"] = "Ibigurwa",
                ["POS_Items"] = "Ibicuruzwa",
                ["POS_TotalAmount"] = "Yose Hamwe",
                ["POS_AmountPaid"] = "Ayishyuwe",
                ["POS_ChangeDue"] = "Agarurwa",
                ["POS_Checkout"] = "SOZA KUGURISHA",
                ["POS_PaymentSuccess"] = "Kwishyura byagenze neza!",
                ["POS_ReceiptDetails"] = "Imiterere ya Risiti",
                ["POS_Close"] = "Funga",
                ["POS_Print"] = "Sohora Risiti",

                // Settings
                ["Settings_Title"] = "Igenamiterere",
                ["Settings_StoreInfo"] = "Amakuru y'Iduka",
                ["Settings_StoreInfoDesc"] = "Ibi bizagaragara kuri risiti.",
                ["Settings_StoreName"] = "Izina ry'Iduka",
                ["Settings_Address"] = "Aderesi",
                ["Settings_Currency"] = "Ifaranga",
                ["Settings_Printer"] = "Izina rya Printer",
                ["Settings_Save"] = "Bika Igenamiterere",

                // Reports
                ["Rep_Title"] = "Raporo & Isesengura",
                ["Rep_Subtitle"] = "Reba raporo z'imari n'ububiko",
                ["Rep_Sales"] = "Raporo y'ibwaguzwe",
                ["Rep_InvValue"] = "Agaciro k'ububiko",
                ["Rep_LowStock"] = "Ibike mu bubiko",
                ["Rep_Profit"] = "Inyungu & Igihombo",
                ["Rep_Generate"] = "Kora Raporo",
                ["Rep_ExportPDF"] = "Bika nka PDF",
                // Sidebar
                ["Suppliers"] = "Abasupplier",
                ["PurchaseOrders"] = "Bons de Commande",
                ["Forecasting"] = "Ibibanziriza Igihe",
                ["ReorderDashboard"] = "Guhindura Ububiko",
                ["ExpiryDashboard"] = "Ibirangiriza Igihe",
                ["Locations"] = "Ahari Ububiko",
                ["Returns"] = "Ibiregarwa",
                ["Bundles"] = "Ibicuruzwa Bivanzwe",
                ["AuditTrail"] = "Imicungire y'Ububiko",
                ["AdvancedInsights"] = "Isesengura Ryimbitse",
                ["Settings"] = "Igenamiterere"
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

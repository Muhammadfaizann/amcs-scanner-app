using System;
using System.Runtime.ConstrainedExecution;

namespace amcs_scanner_app;
/// <summary>
/// Klasse zum speichern von Einstellungen
/// </summary>
public class AppSettings
{
    public AppSettings() { }
    public string CurrentUserName { get; set; } = string.Empty;
    public int MaxCombinedOrders { get; set; } = 2;
    public double DisplayHeight { get; set; } = 700;
    public double DisplayWidth { get; set; } = 350;
    public bool IsPrintPerPosition { get; set; } = true;
    public bool IsPrintAtStart { get; set; } = false;
    public bool IsPrintAtEnd { get; set; } = false;
    public bool IsSequentialPicking { get; set; } = false;
    public int MaxCombinedBarcodes { get; set; } = 10;
    public bool AllPickingItemsMustBeDone { get; set; } = false;
    public bool AllInventoryItemsMustBeDone { get; set; } = false;
    public bool PerformPickingWithScan { get; set; } = true;
    public decimal DifferencePercent { get; set; } = 0.01m;
    public amcs_shared_lib.Models.Configuration.Groups.AppSettings Configuration { get; set; } = new amcs_shared_lib.Models.Configuration.Groups.AppSettings();
    public WarehouseServiceSettings WarehouseSettings { get; set; } = new WarehouseServiceSettings();
    public InventoryServiceSettings InventorySettings { get; set; } = new InventoryServiceSettings();
    public UserServiceSettings UserServiceSettings { get; set; } = new UserServiceSettings();
    public ConfigurationSettings ConfigurationSettings { get; set; } = new ConfigurationSettings();
    public ApplicationIdentifiers ApplicationIdentifiers { get; set; } = new ApplicationIdentifiers();
}

public class ConfigurationSettings
{
    public string BaseUrl = "http://20.160.236.195:5035/";

    public string GetAllByGroup = "settings/getallbygroup";
}

public class WarehouseServiceSettings
{
    public string BaseUrl = "http://20.160.236.195:5030/";

    public string PingUrl = "Ping";

    public string GetPicklistByIdUrl = "Picking/GetPicklistById";

    public string GetPickingOrderOverviewUrl = "Picking/GetPicklistOverview";

    public string GetPickingOrderOverviewWithLimitUrl = "picking/GetPicklistOverviewWithLimit";

    public string GetPickingOrdersByStatusWithLimitUrl = "picking/GetPicklistByStatusWithLimit";

    public string GetPickingOrdersByStatusUrl = "picking/GetPickingOrdersByStatus";

    public string GetPickingOrdersByUserUrl = "picking/GetPickingOrdersByUser";

    public string PostSetPickingOrderUrl = "picking/PostSetPickList";

    public string PostSetPickingItemUrl = "Picking/PostSetPickItem";

    public string PickingOrderStatusHub = "pickingorderstatushub";

    public string PostPrintPickingLabel = "Picking/PostPrintPickingLabel";

}

public class InventoryServiceSettings          
{
    public string BaseUrl = ""; //http...........Port

    public string PingUrl = "Ping"; //Ping

    public string GetInventoryListByIdUrl = ""; // Inventory/GetInventoryListById | GetItemsByOrderId

    public string GetInventoryOrdersByStatusUrl = ""; //Inventory/GetInventoryOrdersByStatus | PostGetFiltered

    public string GetInventoryOrdersByStatusWithLimitUrl = ""; // Inventory/GetInventoryOrdersByStatusWithLimit | GetAll  //GetInventoryListOverview

    public string GetInventoryOrdersByUserUrl = ""; // Inventory/GetInventoryOrdersByUser  | GetByUser

    public string PostSetInventoryItemUrl = ""; // Inventory/PostSetInventoryItem  | 

    public string PostSetInventoryOrderUrl = ""; //Inventory/PostSetInventoryList

    public string InventoryOrderStatusHub = ""; //InventoryOrderStatusHub

}

public class UserServiceSettings
{
    public string BaseUrl = "http://20.160.236.195:5031/";
    public string AuthenticationUrl = "login/authenticateuser";
}

public class ApplicationIdentifiers
{
    /// <summary>
    /// ApplicationIdentifier, die hier definiert sind, werden per Reflection im Barcodeparser geladen und vom Scanner erkannt.
    /// </summary>
    public ApplicationIdentifier User { get; set; } = new ApplicationIdentifier() { Description = "User", Identifier = "91", FixedLength = true, ValueLength = 4, HasDecimalPoint = false };
    public ApplicationIdentifier Storage { get; set; } = new ApplicationIdentifier() { Description = "Storage", Identifier = "92", FixedLength = false, ValueLength = null, HasDecimalPoint = false };
    public ApplicationIdentifier Order { get; set; } = new ApplicationIdentifier() { Description = "Order", Identifier = "93", FixedLength = false, ValueLength = null, HasDecimalPoint = false };
    public ApplicationIdentifier Article { get; set; } = new ApplicationIdentifier() { Description = "Article", Identifier = "94", FixedLength = false, ValueLength = null, HasDecimalPoint = false };
    public ApplicationIdentifier PaymentAmountInLocalCurrency { get; set; } = new ApplicationIdentifier { Description = "PaymentAmountInLocalCurrency", Identifier = "390", FixedLength = false, HasDecimalPoint = true };
}

public class ApplicationIdentifier
{
    public string Description { get; set; }
    public string Identifier { get; set; }
    public bool FixedLength { get; set; }
    public bool HasDecimalPoint { get; set; }
    public int? ValueLength { get; set; }
}

public class BarcodeContent
{
    public string Description { get; set; }
    public string Identifier { get; set; }
    public string Value { get; set; }
}


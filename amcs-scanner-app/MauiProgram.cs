using amcs_scanner_app.Client;
using amcs_scanner_app.Handler;
using amcs_scanner_app.View;
using amcs_scanner_app.View.Inventory;
using amcs_shared_lib.Cache;
using amcs_shared_lib.Models.HTTP;
using System.Globalization;

namespace amcs_scanner_app;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });
        builder.Services.AddSingleton<AppSettings>();
        builder.Services.AddSingleton<UserServiceClient>();
        builder.Services.AddSingleton<ConfigurationClient>();
        builder.Services.AddSingleton<WarehouseClient>();
		builder.Services.AddSingleton<InventoryClient>();
        builder.Services.AddSingleton<MessageCache<SetPickItemRequest>>();
        builder.Services.AddSingleton<MessageCache<SetPicklistRequest>>();
        builder.Services.AddSingleton<MessageCache<SetInventoryItemRequest>>();
        builder.Services.AddSingleton<MessageCache<SetInventoryListRequest>>();
        builder.Services.AddSingleton<PickingHandler>();
		builder.Services.AddSingleton<InventoryHandler>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddSingleton<OverviewPage>();
        builder.Services.AddSingleton<PickingOrderPage>();
		builder.Services.AddSingleton<PickingOrderItemsPage>();
		builder.Services.AddSingleton<ShowItemPiece>();
		builder.Services.AddSingleton<ShowItemWiege>();
		builder.Services.AddSingleton<WaitingForConnection>();
		builder.Services.AddSingleton<FlyoutSettings>();
		builder.Services.AddSingleton<InventoryOrdersPage>();
		builder.Services.AddSingleton<InventoryItemsPage>();
		builder.Services.AddSingleton<ShowItem>();
		builder.Services.AddSingleton<ShowMissingItem>();
        return builder.Build();
    }
}

namespace amcs_scanner_app.View;

public partial class FlyoutSettings : ContentPage
{
    private AppSettings _settings;
    public FlyoutSettings(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        SequentialPicking.IsToggled = _settings.IsSequentialPicking;
        PrintPerPosition.IsToggled = _settings.IsPrintPerPosition;
        PrintAtStart.IsToggled = _settings.IsPrintAtStart;
        PrintAtEnd.IsToggled = _settings.IsPrintAtEnd;

    }
    private void FlyoutOpen(object sender, EventArgs e)
    {
        Shell.Current.FlyoutBehavior = FlyoutBehavior.Disabled;
        Shell.Current.FlyoutBehavior = FlyoutBehavior.Flyout;
        Shell.Current.FlyoutIsPresented = true;
    }

    private void Switch_Toggled(object sender, ToggledEventArgs e)
    {

    }

    private void SequentialPickingToggled(object sender, ToggledEventArgs e)
    {
        var switch0 = sender as Switch;
        if (switch0.IsToggled) _settings.IsSequentialPicking = true;
        else _settings.IsSequentialPicking = false;

    }

    private void PrintPerPositionToggled(object sender, ToggledEventArgs e)
    {
        var switch1 = sender as Switch;
        if (switch1.IsToggled) _settings.IsPrintPerPosition = true;
        else _settings.IsPrintPerPosition = false;

    }

    private void PrintAtStartToggled(object sender, ToggledEventArgs e)
    {
        var switch2 = sender as Switch;
        if (switch2.IsToggled) _settings.IsPrintAtStart = true;
        else _settings.IsPrintAtStart = false;

    }
    private void PrintAtEndToggled(object sender, ToggledEventArgs e)
    {
        var switch3 = sender as Switch;
        if (switch3.IsToggled) _settings.IsPrintAtEnd = true;
        else _settings.IsPrintAtEnd = false;

    }


    private void BackToOverviewPage(object sender, EventArgs e)
    {
        Shell.Current.GoToAsync(nameof(OverviewPage));
    }
}

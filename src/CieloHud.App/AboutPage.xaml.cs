namespace CieloHud.App;

/// <summary>
/// What CieloHud is, its privacy, where the code is, and the licenses that must travel with the app (decision 032):
/// the BSD license of d3-celestial and the OFL of the fonts ask for their text to go with the binary, so the texts are
/// app assets shown here. Also the way into diagnostics, which is no longer on the HUD.
/// </summary>
public partial class AboutPage : ContentPage
{
    private const string RepositoryUrl = "https://github.com/victorvalenciamunoz/CieloHud";

    /// <summary>License texts shipped as assets (see the csproj), in the order they are shown.</summary>
    private static readonly string[] LicenseFiles =
    [
        "licenses/LICENSE",
        "licenses/THIRD-PARTY-NOTICES.md",
        "licenses/BSD-d3-celestial.txt",
        "licenses/OFL-ChakraPetch.txt",
        "licenses/OFL-OpenSans.txt",
    ];

    public AboutPage()
    {
        InitializeComponent();
        VersionLabel.Text = $"Versión {AppInfo.Current.VersionString}";
    }

    private async void OnRepositoryClicked(object? sender, EventArgs e) =>
        await Launcher.Default.OpenAsync(RepositoryUrl);

    private async void OnDiagnosticsClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(DiagnosticsPage));

    private async void OnLicensesClicked(object? sender, EventArgs e)
    {
        if (LicensesLabel.IsVisible)
        {
            LicensesLabel.IsVisible = false;
            return;
        }

        if (string.IsNullOrEmpty(LicensesLabel.Text))
        {
            var texts = new List<string>();
            foreach (var file in LicenseFiles)
            {
                await using var stream = await FileSystem.OpenAppPackageFileAsync(file);
                using var reader = new StreamReader(stream);
                texts.Add(await reader.ReadToEndAsync());
            }
            LicensesLabel.Text = string.Join("\n\n——————————\n\n", texts);
        }
        LicensesLabel.IsVisible = true;
    }
}

using System;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml;

namespace DJLibrary.WinUiSpike;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        bool smoke = Environment.GetCommandLineArgs().Any(
            value => string.Equals(value, "--smoke", StringComparison.OrdinalIgnoreCase));
        try
        {
            var mainWindow = new MainWindow();
            _window = mainWindow;
            mainWindow.Activate();

            // Separate synthetic-only runner. Never read a real catalog, app settings
            // or private music. The host CI must additionally verify an explicit PASS file.
            if (smoke)
            {
                string result = await mainWindow.RunSyntheticSmokeAsync();
                WriteSmokeResult("PASS\n" + result);
            }
        }
        catch (Exception ex)
        {
            if (smoke)
            {
                var partial = (_window as MainWindow)?.PartialSmokeEvidence ?? "";
                WriteSmokeResult("FAIL\n" + ex + "\nCOMPLETED_BEFORE_FAILURE\n" + partial);
                Environment.ExitCode = 1;
            }
            else
            {
                throw;
            }
        }
        finally
        {
            if (smoke) Current.Exit();
        }
    }

    private static void WriteSmokeResult(string result)
    {
        string? path = Environment.GetEnvironmentVariable("DJLIB_WINUI_SMOKE_REPORT");
        if (string.IsNullOrWhiteSpace(path)) return;
        // Only the isolated CI/manual smoke explicitly supplies this path.
        File.WriteAllText(path, result);
    }
}

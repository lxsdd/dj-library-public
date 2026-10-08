using System;
using System.IO;
using System.Windows;

namespace DJLibrary
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            bool selfTest = args != null && Array.Exists(args, delegate(string x)
            {
                return String.Equals(x, "--self-test-public", StringComparison.OrdinalIgnoreCase) ||
                       String.Equals(x, "--self-test-ci", StringComparison.OrdinalIgnoreCase) ||
                       String.Equals(x, "--self-test", StringComparison.OrdinalIgnoreCase);
            });
            try
            {
                if (selfTest)
                {
                    // Self-contained synthetic fixture only; no owner dataset, app
                    // data catalog, real foobar profile or network calls.
                    string report = PublicDataSelfTest.Run();
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SELF-TEST.txt"), report);
                    return;
                }

                DataStore data = new DataStore();
                // Reuse the current user's catalog if it already exists, and
                // bootstrap an empty SQLite schema-v4 catalog on first launch.
                using (CatalogService catalog = CatalogService.EnsureInitialized(null))
                    data.LoadFromCatalog(catalog, null);
                data.Validate();

                Application app = new Application();
                app.ShutdownMode = ShutdownMode.OnLastWindowClose;
                MainWindow window = new MainWindow(data);
                app.Run(window);
            }
            catch (Exception ex)
            {
                if (selfTest)
                {
                    try
                    {
                        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SELF-TEST-ERROR.txt"), ex.ToString());
                    }
                    catch { }
                    Environment.ExitCode = 1;
                    return;
                }
                MessageBox.Show("DJ Library could not open.\n\n" + ex,
                    "DJ Library", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

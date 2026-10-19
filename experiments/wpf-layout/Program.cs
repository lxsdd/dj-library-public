using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using System.Xml.Serialization;
using DJLibrary;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        try
        {
            // The identical semantic XML snapshot also runs in a legacy .NET
            // Framework executable. This report is written only when a CI
            // temporary path is explicitly provided, never to APPDATA.
            PersistenceCompatibilitySnapshot.WriteReportFromEnvironment();
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

            // The built-in production contracts must still pass with real source files.
            Console.WriteLine("PASS production geometry: " + WindowGeometrySettings.ValidateContract());
            Console.WriteLine("PASS production layout: " + GridLayoutSettings.ValidateContract());

            // Synthetic named states, including a real HWND and its rendered DataGrid.
            // No call to SettingsManager.Save/Load/Attach and no profile writes.
            var settings = new AppSettings();
            double originalLeft, originalTop, originalWidth, originalHeight;
            var before = CreateWindow(out DataGrid source);
            before.Show();
            Render(before);
            try
            {
                source.Columns[0].DisplayIndex = 1; // Artist to second place
                source.Columns[1].DisplayIndex = 0; // Title to first place
                source.Columns[0].Width = new DataGridLength(230, DataGridLengthUnitType.Pixel);
                source.Columns[1].Width = new DataGridLength(2, DataGridLengthUnitType.Star);
                source.Columns[2].Width = DataGridLength.Auto;
                source.Columns[2].Visibility = Visibility.Collapsed;
                var view = CollectionViewSource.GetDefaultView(source.ItemsSource);
                view.SortDescriptions.Clear();
                view.SortDescriptions.Add(new SortDescription("Artist", ListSortDirection.Descending));
                Render(before);

                originalLeft = before.Left;
                originalTop = before.Top;
                originalWidth = before.Width;
                originalHeight = before.Height;
                GridLayoutSettings.Capture(settings, "catalog.tracks", source);
                WindowGeometrySettings.Capture(settings, "catalog.window", before);
                if (settings.GridLayouts.Count != 1 || settings.WindowGeometries.Count != 1)
                    throw new InvalidDataException("Missing named layout or geometry captured from actual WPF window");
            }
            finally { before.Close(); }

            // Round-trip settings ONLY through a MemoryStream. No user's APPDATA touched.
            AppSettings saved;
            var serializer = new XmlSerializer(typeof(AppSettings));
            using (var mem = new MemoryStream())
            {
                serializer.Serialize(mem, settings);
                mem.Position = 0;
                saved = (AppSettings)serializer.Deserialize(mem);
            }

            if (saved.GridLayouts == null || saved.GridLayouts.Count != 1)
                throw new InvalidDataException("Missing named grid state after XML round-trip");
            if (saved.WindowGeometries == null || saved.WindowGeometries.Count != 1)
                throw new InvalidDataException("Missing named window state after XML round-trip");

            var after = CreateWindow(out DataGrid target);
            try
            {
                // Invoke the product's actual geometry-application routine against
                // an in-memory NamedWindowGeometry; public Restore reads APPDATA.
                var apply = typeof(WindowGeometrySettings).GetMethod("Apply",
                    BindingFlags.Static | BindingFlags.NonPublic);
                if (apply == null) throw new InvalidOperationException("Production geometry Apply missing");
                var state = saved.WindowGeometries[0];
                if (!(bool)apply.Invoke(null, new object[] { after, state }))
                    throw new InvalidOperationException("Production geometry Apply rejected valid synthetic state");
                // A small CI virtual desktop can legitimately clamp a restored
                // window. Compare to the production work-area normalization,
                // not the unbounded old monitor coordinates.
                var remembered = new Rect(originalLeft, originalTop, originalWidth, originalHeight);
                var work = SystemParameters.WorkArea;
                var expected = WindowGeometrySettings.NormalizeForWorkArea(
                    remembered, work, after.MinWidth, after.MinHeight);
                if (Math.Abs(after.Left - expected.Left) > 2
                    || Math.Abs(after.Top - expected.Top) > 2
                    || Math.Abs(after.Width - expected.Width) > 2
                    || Math.Abs(after.Height - expected.Height) > 2)
                    throw new InvalidDataException(
                        "Saved normal window geometry differs from safely clamped restore: " +
                        "remembered=" + remembered + "; work=" + work +
                        "; expected=" + expected +
                        "; actual=" + new Rect(after.Left, after.Top, after.Width, after.Height));

                after.Show();
                Render(after);
                GridLayoutSettings.Apply(saved, "catalog.tracks", target);
                Render(after);

                var artist = target.Columns.First(x => x.SortMemberPath == "Artist");
                var title = target.Columns.First(x => x.SortMemberPath == "Title");
                var year = target.Columns.First(x => x.SortMemberPath == "Year");
                if (title.DisplayIndex != 0 || artist.DisplayIndex != 1)
                    throw new InvalidDataException("Column order lost on newly created DataGrid");
                if (artist.Width.UnitType != DataGridLengthUnitType.Pixel
                    || Math.Abs(artist.Width.Value - 230) > 2)
                    throw new InvalidDataException("Pixel column width not restored");
                if (title.Width.UnitType != DataGridLengthUnitType.Star
                    || Math.Abs(title.Width.Value - 2) > 0.01)
                    throw new InvalidDataException("Star column width not restored");
                if (year.Width.UnitType != DataGridLengthUnitType.Auto
                    || year.Visibility != Visibility.Collapsed)
                    throw new InvalidDataException("Auto/hidden column state lost");

                var restoredView = CollectionViewSource.GetDefaultView(target.ItemsSource);
                if (restoredView.SortDescriptions.Count != 1
                    || restoredView.SortDescriptions[0].PropertyName != "Artist"
                    || restoredView.SortDescriptions[0].Direction != ListSortDirection.Descending)
                    throw new InvalidDataException("Sort description not restored");

                // Geometry should safely reappear if previous monitor is disconnected.
                var clamped = WindowGeometrySettings.NormalizeForWorkArea(
                    new Rect(9000, 4500, 3300, 2400), new Rect(0, 0, 1920, 1080), 700, 500);
                if (clamped.Left < 0 || clamped.Top < 0
                    || clamped.Right > 1920.01 || clamped.Bottom > 1080.01)
                    throw new InvalidDataException("Offscreen monitor recovery unsafe");

                // A late-stale main window save must not overwrite auxiliary states.
                Console.WriteLine("PASS ownership: " + SettingsManager.ValidateStaleSnapshotMergeContract());
                Console.WriteLine("PASS actual WPF HWND: XML-only round-trip restores geometry, order, pixel/star/auto widths, visibility and sort on a second window");
            }
            finally { after.Close(); }
            app.Shutdown();
            return 0;
        }
        catch (Exception e)
        {
            // WinExe has no interactive console. Emit a diagnostics report ONLY when
            // the isolated Actions harness supplies an explicit temporary path.
            string reportPath = Environment.GetEnvironmentVariable("WPF_LAYOUT_SMOKE_REPORT");
            if (!string.IsNullOrWhiteSpace(reportPath))
            {
                try { File.WriteAllText(reportPath, e.ToString()); }
                catch { }
            }
            Console.Error.WriteLine("FAIL persistence contract: " + e);
            return 1;
        }
    }

    private static Window CreateWindow(out DataGrid grid)
    {
        grid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserSortColumns = true,
            CanUserResizeColumns = true,
            CanUserReorderColumns = true,
            ItemsSource = new[]
            {
                new Record { Artist = "B", Title = "Synthetic B", Year = "2025" },
                new Record { Artist = "A", Title = "Synthetic A", Year = "2026" }
            }
        };
        grid.Columns.Add(Column("Artist", new DataGridLength(145, DataGridLengthUnitType.Pixel)));
        grid.Columns.Add(Column("Title", new DataGridLength(1, DataGridLengthUnitType.Star)));
        grid.Columns.Add(Column("Year", DataGridLength.Auto));
        return new Window
        {
            Content = grid,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = 180,
            Top = 140,
            Width = 1000,
            Height = 690,
            MinWidth = 600,
            MinHeight = 400
        };
    }

    private static DataGridTextColumn Column(string property, DataGridLength width)
    {
        return new DataGridTextColumn
        {
            Header = property,
            SortMemberPath = property,
            Binding = new Binding(property),
            Width = width
        };
    }

    private static void Render(Window window)
    {
        window.Dispatcher.Invoke((Action)(() => { }), DispatcherPriority.ApplicationIdle);
        window.UpdateLayout();
    }

    private sealed class Record
    {
        public string Artist { get; set; }
        public string Title { get; set; }
        public string Year { get; set; }
    }
}

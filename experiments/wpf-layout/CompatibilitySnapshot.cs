// Runs UNMODIFIED DJ Library AppSettings/XML serializer on Framework 4.8 and
// .NET 10, comparing semantic values, without opening any profile file.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.Globalization;
using DJLibrary;

internal static class PersistenceCompatibilitySnapshot
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidDataException("Cross-runtime settings: " + message);
    }

    public static int Main(string[] args)
    {
        try
        {
            WriteReportFromEnvironment();
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("FAIL WPF cross-runtime XML: " + e);
            return 1;
        }
    }

    public static void WriteReportFromEnvironment()
    {
        string path = Environment.GetEnvironmentVariable("WPF_LAYOUT_COMPAT_REPORT");
        if (String.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException("Explicit CI temporary report path is mandatory");

        var model = new AppSettings();
        model.WindowLeft = 128.5;
        model.WindowTop = 43.25;
        model.WindowWidth = 1370.75;
        model.WindowHeight = 810;
        model.Maximized = false;
        model.SelectedTab = 1;
        model.BridgeDirectory = @"X:\SYNTHETIC_ONLY\Björk\日本語";
        model.LastManualBridgeDirectory = @"X:\SYNTHETIC_ONLY\Classic";
        model.KnownBridgeDirectories = new List<string> {
            @"X:\SYNTHETIC_ONLY\Björk\日本語", @"X:\SYNTHETIC_ONLY\Another"
        };
        model.GridLayouts = new List<NamedGridLayout> {
            new NamedGridLayout {
                Key = "catalog.tracks",
                Columns = new List<ColumnSetting> {
                    new ColumnSetting { Key = "ARTIST", DisplayIndex = 1, Width = 235.5,
                        WidthUnit = "Pixel", Visible = true },
                    new ColumnSetting { Key = "TITLE", DisplayIndex = 0, Width = 2,
                        WidthUnit = "Star", Visible = true },
                    new ColumnSetting { Key = "YEAR", DisplayIndex = 2, Width = 123,
                        WidthUnit = "Auto", Visible = false }
                },
                Sorts = new List<SortSetting> {
                    new SortSetting { Property = "ARTIST", Descending = true },
                    new SortSetting { Property = "TITLE", Descending = false }
                }
            },
            new NamedGridLayout { Key = "catalog.discs",
                Columns = new List<ColumnSetting> {
                    new ColumnSetting { Key = "TITLE", DisplayIndex = 0, Width = 180.25,
                        WidthUnit = "Pixel", Visible = true }
                }
            }
        };
        model.WindowGeometries = new List<NamedWindowGeometry> {
            new NamedWindowGeometry {
                Key = "catalog.window", Left = 128.5, Top = 43.25,
                Width = 1370.75, Height = 810, Maximized = false
            },
            new NamedWindowGeometry {
                Key = "catalog.manager", Left = 29, Top = 57,
                Width = 900.5, Height = 622.25, Maximized = true
            }
        };
        AppSettings restored;
        var serializer = new XmlSerializer(typeof(AppSettings));
        using (var mem = new MemoryStream())
        {
            serializer.Serialize(mem, model);
            mem.Position = 0;
            restored = (AppSettings)serializer.Deserialize(mem);
        }
        Require(restored.SelectedTab == 1 && restored.BridgeDirectory == model.BridgeDirectory,
            "global selection or Unicode path lost");
        Require(restored.KnownBridgeDirectories.SequenceEqual(model.KnownBridgeDirectories),
            "Unicode recent paths lost");
        Require(restored.GridLayouts.Count == 2 && restored.WindowGeometries.Count == 2,
            "named state collections changed");
        Require(restored.GridLayouts[0].Columns.Count == 3 &&
                restored.GridLayouts[0].Sorts.Count == 2 &&
                restored.GridLayouts[0].Columns[1].WidthUnit == "Star" &&
                restored.GridLayouts[0].Columns[2].Visible == false,
            "column width unit, hidden state or sorts lost");
        Require(restored.WindowGeometries[1].Maximized &&
                Math.Abs(restored.WindowGeometries[1].Height - 622.25) < 0.00001,
            "maximized state or fractional geometry lost");

        // A deliberately sparse, PRE WidthUnit sample simulates a historical
        // settings.xml. It is read from a StringReader, never a user profile.
        // Omitted named layouts and window geometries must retain safe defaults.
        const string historicXml =
            "<AppSettings>" +
            "<WindowLeft>42.5</WindowLeft><WindowTop>81</WindowTop>" +
            "<WindowWidth>1200</WindowWidth><WindowHeight>710</WindowHeight>" +
            "<Maximized>true</Maximized><SelectedTab>1</SelectedTab>" +
            "<TrackColumns><ColumnSetting>" +
            "<Key>ARTIST</Key><DisplayIndex>0</DisplayIndex>" +
            "<Width>186.5</Width><Visible>true</Visible>" +
            "</ColumnSetting></TrackColumns>" +
            "<TrackSorts><SortSetting><Property>ARTIST</Property>" +
            "<Descending>true</Descending></SortSetting></TrackSorts>" +
            @"<BridgeDirectory>X:\SYNTHETIC_ONLY\legacy</BridgeDirectory>" +
            "</AppSettings>";
        AppSettings historic;
        using (var reader = new StringReader(historicXml))
            historic = (AppSettings)serializer.Deserialize(reader);
        Require(historic.SelectedTab == 1 && historic.Maximized &&
                historic.TrackColumns.Count == 1 && historic.TrackSorts.Count == 1,
            "old sparse XML global/column/sort settings lost");
        Require(historic.TrackColumns[0].WidthUnit == "" &&
                SettingsManager.RestoreColumnWidth(historic.TrackColumns[0]).UnitType ==
                    System.Windows.Controls.DataGridLengthUnitType.Pixel &&
                Math.Abs(SettingsManager.RestoreColumnWidth(historic.TrackColumns[0]).Value - 186.5) < 0.001,
            "legacy WidthUnit omission must restore exact pixel width");
        Require(historic.GridLayouts != null && historic.GridLayouts.Count == 0 &&
                historic.WindowGeometries != null && historic.WindowGeometries.Count == 0,
            "missing newly introduced named state fields do not default to safe empty collections");

        var report = new StringBuilder();
        report.AppendLine("PASS WPF cross-runtime AppSettings semantic XML: v1");
        report.AppendLine("tab=" + restored.SelectedTab.ToString(Culture));
        report.AppendLine("historic-tab=" + historic.SelectedTab.ToString(Culture));
        report.AppendLine("historic-max=" + historic.Maximized);
        report.AppendLine("historic-width=" + SettingsManager.RestoreColumnWidth(
            historic.TrackColumns[0]).Value.ToString("R", Culture));
        report.AppendLine("historic-widthunit=" + historic.TrackColumns[0].WidthUnit);
        report.AppendLine("historic-named=" + historic.GridLayouts.Count + "/" +
            historic.WindowGeometries.Count);

        report.AppendLine("window=" + String.Join(",",
            new [] { restored.WindowLeft, restored.WindowTop, restored.WindowWidth,
                     restored.WindowHeight }.Select(x => x.ToString("R", Culture))));
        report.AppendLine("bridge=" + restored.BridgeDirectory);
        report.AppendLine("manualBridge=" + restored.LastManualBridgeDirectory);
        report.AppendLine("known=" + String.Join("|", restored.KnownBridgeDirectories.ToArray()));
        foreach (NamedGridLayout grid in restored.GridLayouts)
        {
            report.AppendLine("grid=" + grid.Key);
            foreach (ColumnSetting col in grid.Columns)
                report.AppendLine("column=" + String.Join("|", new[] {
                    col.Key, col.DisplayIndex.ToString(Culture), col.Width.ToString("R", Culture),
                    col.WidthUnit, col.Visible ? "true" : "false"
                }));
            foreach (SortSetting sort in grid.Sorts)
                report.AppendLine("sort=" + sort.Property + "|" + (sort.Descending ? "desc" : "asc"));
        }
        foreach (NamedWindowGeometry window in restored.WindowGeometries)
            report.AppendLine("geometry=" + String.Join("|", new[] {
                window.Key,
                window.Left.ToString("R", Culture), window.Top.ToString("R", Culture),
                window.Width.ToString("R", Culture), window.Height.ToString("R", Culture),
                window.Maximized ? "max" : "normal"
            }));
        File.WriteAllText(path, report.ToString(), new UTF8Encoding(false));
    }
}

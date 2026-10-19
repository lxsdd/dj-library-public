using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

// Same source compiled against Framework 4.8 and the isolated .NET 10 adapter.
internal static class JsonParityHarness
{
    public sealed class Document
    {
        public string Format { get; set; }
        public int Version { get; set; }
        public List<Release> Releases { get; set; }
    }
    public sealed class Release
    {
        public string Album { get; set; }
        public int TotalDiscs { get; set; }
        public List<Track> Tracks { get; set; }
    }
    public sealed class Track
    {
        public string Artist { get; set; }
        public string Title { get; set; }
        public double Bpm { get; set; }
    }
    private static void Need(bool value, string label)
    {
        if (!value) throw new InvalidDataException("JSON parity: " + label);
    }
    private static string Num(object value)
    {
        return Convert.ToString(value, CultureInfo.InvariantCulture) +
            "(" + value.GetType().Name + ")";
    }
    public static int Main()
    {
        try
        {
            string report = Environment.GetEnvironmentVariable("DJLIB_JSON_PARITY_TEMP_REPORT");
            Need(!String.IsNullOrWhiteSpace(report), "explicit temp report path required");
            var json = new JavaScriptSerializer { MaxJsonLength = 4096, RecursionLimit = 20 };
            const string input = "{\"schema_version\":1,\"title\":\"Bj\u00F6rk \u6771\u4EAC\"," +
                "\"proposals\":[{\"title\":\"Synthetic\",\"valid\":true,\"bpm\":128.5}]," +
                "\"large\":9223372036854775807,\"fraction\":0.125,\"empty\":null,\"flags\":[true,false]}";
            var root = json.DeserializeObject(input) as Dictionary<string, object>;
            Need(root != null && (int)root["schema_version"] == 1, "object / int");
            Need((long)root["large"] == long.MaxValue && root["empty"] == null, "long / null");
            var rows = root["proposals"] as IList;
            Need(rows != null && rows.Count == 1, "array");
            var row = rows[0] as Dictionary<string, object>;
            Need(row != null && (bool)row["valid"], "nested object / bool");
            var flags = root["flags"] as IList;
            Need(flags != null && flags.Count == 2 && (bool)flags[0] && !(bool)flags[1], "flags");

            var source = new Document {
                Format = "dj-library-interchange", Version = 1,
                Releases = new List<Release> {
                    new Release {
                        Album = "D\u00E9j\u00E0 Vu", TotalDiscs = 2,
                        Tracks = new List<Track> {
                            new Track { Artist = "Bj\u00F6rk", Title = "\u7EC8\u7AE0", Bpm = 128.5 }
                        }
                    }
                }
            };
            var typed = json.Deserialize<Document>(json.Serialize(source));
            Need(typed != null && typed.Format == source.Format &&
                typed.Releases.Count == 1 &&
                typed.Releases[0].Tracks[0].Title == "\u7EC8\u7AE0", "typed DTO");

            json.MaxJsonLength = 5;
            bool limited = false;
            try { json.DeserializeObject(input); }
            catch (ArgumentException) { limited = true; }
            catch (InvalidOperationException) { limited = true; }
            Need(limited, "configured JSON length");
            var lines = new[] {
                "PASS DJLIB cross-runtime JSON semantic contract",
                "schema=" + Num(root["schema_version"]),
                "big=" + Num(root["large"]),
                "fraction=" + Num(root["fraction"]),
                "bpm=" + Num(row["bpm"]),
                "title=" + root["title"],
                "proposal=" + row["title"] + "/" + row["valid"],
                "flags=" + flags[0] + "/" + flags[1],
                "typed=" + typed.Format + "/" + typed.Version + "/" +
                    typed.Releases[0].Album + "/" + typed.Releases[0].TotalDiscs + "/" +
                    typed.Releases[0].Tracks[0].Artist + "/" +
                    typed.Releases[0].Tracks[0].Title + "/" +
                    typed.Releases[0].Tracks[0].Bpm.ToString("R", CultureInfo.InvariantCulture),
                "length-rejected=" + limited
            };
            File.WriteAllText(report, String.Join("\n", lines) + "\n", new UTF8Encoding(false));
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("FAIL: " + e);
            return 1;
        }
    }
}

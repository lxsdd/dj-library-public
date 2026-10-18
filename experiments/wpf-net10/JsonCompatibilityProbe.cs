using System;
using System.Collections;
using System.Collections.Generic;
using System.Web.Script.Serialization;

public static class JsonCompatibilityProbe
{
    public sealed class Interchange
    {
        public string Format { get; set; }
        public int Version { get; set; }
        public List<Release> Releases { get; set; }
    }
    public sealed class Release
    {
        public string Album { get; set; }
        public int TotalDiscs { get; set; }
    }

    public static int Main()
    {
        var serializer = new JavaScriptSerializer { MaxJsonLength = 4096, RecursionLimit = 20 };
        var root = serializer.DeserializeObject("{\"schema_version\":1,\"proposals\":[{\"title\":\"Test\",\"valid\":true}],\"large\":9223372036854775807}") as Dictionary<string, object>;
        if (root == null || (int)root["schema_version"] != 1 || (long)root["large"] != long.MaxValue) return 1;
        var rows = root["proposals"] as IList;
        if (rows == null || rows.Count != 1) return 2;
        var row = rows[0] as Dictionary<string, object>;
        if (row == null || (string)row["title"] != "Test" || !(bool)row["valid"]) return 3;

        var original = new Interchange {
            Format = "dj-library-interchange", Version = 1,
            Releases = new List<Release> { new Release { Album = "Synthetic", TotalDiscs = 2 } }
        };
        string json = serializer.Serialize(original);
        var copy = serializer.Deserialize<Interchange>(json);
        if (copy == null || copy.Releases == null || copy.Releases.Count != 1 ||
            copy.Releases[0].Album != "Synthetic" || copy.Releases[0].TotalDiscs != 2) return 4;

        bool rejected = false;
        serializer.MaxJsonLength = 5;
        try { serializer.DeserializeObject(json); }
        catch (InvalidOperationException) { rejected = true; }
        if (!rejected) return 5;

        Console.WriteLine("PASS: isolated JSON object/array/number/DTO/length synthetic contract.");
        return 0;
    }
}

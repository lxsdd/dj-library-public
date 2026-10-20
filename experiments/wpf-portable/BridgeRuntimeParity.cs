// Same original BridgeSnapshotReader.cs compiled on Framework 4.8 and .NET 10.
// Test-only DigitalItem shape: exactly the fields the unchanged reader assigns.
// All fixtures and reports live under disposable GitHub runner TEMP.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace DJLibrary
{
    public sealed class DigitalItem
    {
        public int ItemId { get; set; }
        public string Path { get; set; }
        public int Subsong { get; set; }
        public string Artist { get; set; }
        public string Title { get; set; }
        public string OriginalTitle { get; set; }
        public string RemixedBy { get; set; }
        public string Album { get; set; }
        public string AlbumArtist { get; set; }
        public string Genre { get; set; }
        public string Style { get; set; }
        public double Bpm { get; set; }
        public string Label { get; set; }
        public string Catalog { get; set; }
        public double DurationSeconds { get; set; }
        public string Codec { get; set; }
        public string Bitrate { get; set; }
        public string Fingerprint { get; set; }
        public string MetadataVectorsJson { get; set; }
    }

    internal static class BridgeRuntimeParity
    {
        private static void Need(bool ok, string why)
        {
            if (!ok) throw new InvalidDataException("Bridge runtime parity: " + why);
        }

        private static string State(int schema, long generation, bool complete, int count)
        {
            return "schema_version\t" + schema.ToString(CultureInfo.InvariantCulture) + "\n" +
                "generation\t" + generation.ToString(CultureInfo.InvariantCulture) + "\n" +
                "complete\t" + (complete ? "1" : "0") + "\n" +
                "item_count\t" + count.ToString(CultureInfo.InvariantCulture) + "\n" +
                "last_change_utc\t2026-10-20T08:00:00Z\n" +
                "source_id\tci-bridge-parity\n" +
                "source_name\tCI synthetic foobar snapshot\n" +
                "profile_path\tX:\\SYNTHETIC\\NOT_REAL_FOOBAR\n" +
                "producer_version\tci-only\n" +
                "producer_pid\t1\n";
        }

        private static string[] Row(int schema, int subsong)
        {
            int n = schema == 1 ? BridgeSnapshotReader.ItemColumnCount :
                schema == 2 ? BridgeSnapshotReader.ItemColumnCountV2 :
                BridgeSnapshotReader.ItemColumnCountV3;
            string[] p = new string[n];
            for (int i = 0; i < n; i++) p[i] = "";
            p[0] = @"X:\SYNTHETIC\NeverExists\Björk - 日本語.flac";
            p[1] = subsong.ToString(CultureInfo.InvariantCulture);
            p[2] = "Björk";
            p[4] = "Photon Fixture Only";
            p[7] = "Invented Album";
            p[8] = "Björk";
            p[14] = "Progressive House";
            p[15] = "Electronic";
            p[16] = "128.5";
            p[19] = "302.25";
            p[21] = "FLAC";
            p[22] = "900";
            p[23] = "ci-fingerprint";
            if (n >= 25) p[24] = "{\"COMMENT\":\"no real track\"}";
            if (n >= 26) p[25] =
                "[{\"name\":\"ARTIST\",\"values\":[\"Björk\",\"Guest\"]}]";
            return p;
        }

        private static void Write(string dir, int schema, long generation, bool complete,
            int declared, IList<string[]> items, bool wrongHeader)
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "bridge-state.tsv"),
                State(schema, generation, complete, declared), new UTF8Encoding(false));
            string header = schema == 1 ? BridgeSnapshotReader.ItemHeader :
                schema == 2 ? BridgeSnapshotReader.ItemHeaderV2 :
                BridgeSnapshotReader.ItemHeaderV3;
            using (FileStream fs = File.Create(Path.Combine(dir, "digital-items.tsv.gz")))
            using (GZipStream gz = new GZipStream(fs, CompressionMode.Compress))
            using (StreamWriter sw = new StreamWriter(gz, new UTF8Encoding(false)))
            {
                sw.WriteLine(wrongHeader ? "wrong-header" : header);
                foreach (string[] fields in items) sw.WriteLine(String.Join("\t", fields));
            }
        }

        private static void ExpectInvalidLoad(string dir, BridgeSnapshotStatus status, string name)
        {
            bool blocked = false;
            try { BridgeSnapshotReader.LoadItems(dir, status); }
            catch (InvalidDataException) { blocked = true; }
            Need(blocked, name + " failed closed");
        }

        public static int Main()
        {
            string directory = null;
            try
            {
                Need(String.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"),
                    "true", StringComparison.OrdinalIgnoreCase) &&
                    String.Equals(Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT"),
                    "github-hosted", StringComparison.OrdinalIgnoreCase),
                    "must run only on a disposable GitHub-hosted Windows runner");
                string root = Environment.GetEnvironmentVariable("RUNNER_TEMP");
                string report = Environment.GetEnvironmentVariable("DJLIB_BRIDGE_PARITY_REPORT");
                Need(!String.IsNullOrWhiteSpace(root) && !String.IsNullOrWhiteSpace(report) &&
                    Path.GetFullPath(report).StartsWith(
                        Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) +
                        Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase),
                    "report must be inside RUNNER_TEMP");
                directory = Path.Combine(root, "djlib-bridge-parity-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                var result = new List<string> { "PASS original BridgeSnapshotReader cross-runtime v1/v2/v3 semantic parity" };

                BridgeSnapshotReader.ValidateSchemaV3Contract();
                result.Add("built-in-v3=PASS");
                for (int schema = 1; schema <= 3; schema++)
                {
                    string dir = Path.Combine(directory, "schema-" + schema);
                    Write(dir, schema, 9, true, 1, new[] { Row(schema, 0) }, false);
                    BridgeSnapshotStatus status = BridgeSnapshotReader.Inspect(dir, true);
                    Need(status.Present && status.Compatible && status.Complete &&
                        status.Generation == 9 && status.VerifiedItemCount == 1 &&
                        status.DeclaredItemCount == 1 && status.SchemaVersion == schema &&
                        status.SourceId == "ci-bridge-parity", "verified schema " + schema);
                    List<DigitalItem> loaded = BridgeSnapshotReader.LoadItems(dir, status);
                    Need(loaded.Count == 1 && loaded[0].ItemId == 1 &&
                        loaded[0].Artist == "Björk" && loaded[0].Title == "Photon Fixture Only" &&
                        loaded[0].Subsong == 0 && loaded[0].Bpm == 128.5 &&
                        loaded[0].DurationSeconds == 302.25 &&
                        loaded[0].Path.Contains("日本語") &&
                        loaded[0].Genre == "Progressive House", "data schema " + schema);
                    if (schema == 3)
                        Need(loaded[0].MetadataVectorsJson ==
                            "[{\"name\":\"ARTIST\",\"values\":[\"Björk\",\"Guest\"]}]",
                            "lossless v3 metadata vectors");
                    else Need(String.IsNullOrEmpty(loaded[0].MetadataVectorsJson),
                        "v1/v2 must not gain synthetic metadata vectors");
                    result.Add("schema-" + schema + "=" + loaded[0].Artist + "|" +
                        loaded[0].Title + "|" + loaded[0].Genre + "|" +
                        loaded[0].Bpm.ToString("R", CultureInfo.InvariantCulture) + "|" +
                        loaded[0].DurationSeconds.ToString("R", CultureInfo.InvariantCulture) +
                        "|vectors=" + (loaded[0].MetadataVectorsJson ?? ""));
                }

                string stale = Path.Combine(directory, "stale-generation");
                Write(stale, 3, 9, true, 1, new[] { Row(3, 0) }, false);
                BridgeSnapshotStatus previous = BridgeSnapshotReader.Inspect(stale, true);
                Write(stale, 3, 10, true, 1, new[] { Row(3, 0) }, false);
                ExpectInvalidLoad(stale, previous, "changed generation");
                result.Add("changed-generation=REJECTED");

                string duplicate = Path.Combine(directory, "duplicate");
                Write(duplicate, 3, 9, true, 2,
                    new[] { Row(3, 0), Row(3, 0) }, false);
                BridgeSnapshotStatus duplicated = BridgeSnapshotReader.Inspect(duplicate, true);
                Need(duplicated.Compatible && duplicated.VerifiedItemCount == 2,
                    "duplicate snapshot must pass count but fail unique identity");
                ExpectInvalidLoad(duplicate, duplicated, "duplicate path+subsong");
                result.Add("duplicate-identity=REJECTED");

                string mismatched = Path.Combine(directory, "wrong-count");
                Write(mismatched, 3, 9, true, 2, new[] { Row(3, 0) }, false);
                BridgeSnapshotStatus wrongCount = BridgeSnapshotReader.Inspect(mismatched, true);
                Need(!wrongCount.Compatible && !String.IsNullOrWhiteSpace(wrongCount.Error),
                    "wrong declared item count rejected on inspect");
                ExpectInvalidLoad(mismatched, wrongCount, "mismatched item count");
                result.Add("wrong-count=REJECTED");

                string incomplete = Path.Combine(directory, "incomplete");
                Write(incomplete, 3, 9, false, 1, new[] { Row(3, 0) }, false);
                BridgeSnapshotStatus inProgress = BridgeSnapshotReader.Inspect(incomplete, true);
                Need(!inProgress.Complete, "in-progress producer must not appear complete");
                ExpectInvalidLoad(incomplete, inProgress, "incomplete producer");
                result.Add("incomplete=REJECTED");

                string invalidHeader = Path.Combine(directory, "header");
                Write(invalidHeader, 3, 9, true, 1, new[] { Row(3, 0) }, true);
                BridgeSnapshotStatus badHeader = BridgeSnapshotReader.Inspect(invalidHeader, true);
                Need(!badHeader.Compatible, "wrong column header");
                result.Add("wrong-header=REJECTED");

                string schemaUnknown = Path.Combine(directory, "schema-unsupported");
                Write(schemaUnknown, 4, 9, true, 1, new[] { Row(3, 0) }, false);
                BridgeSnapshotStatus unsupported = BridgeSnapshotReader.Inspect(schemaUnknown, true);
                Need(!unsupported.Compatible, "unsupported newer Bridge schema must fail closed");
                result.Add("unsupported-schema=REJECTED");

                File.WriteAllText(report, String.Join("\n", result.ToArray()) + "\n",
                    new UTF8Encoding(false));
                foreach (string line in result) Console.WriteLine(line);
                return 0;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("FAIL original Bridge runtime parity: " + e);
                return 1;
            }
            finally
            {
                if (directory != null && Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }
    }
}

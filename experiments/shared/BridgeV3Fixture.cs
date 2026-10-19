// Disposable producer-format Bridge v3 fixture for A/B UI; no foobar profile or music accessed.
// Production BridgeSnapshotReader is linked unchanged, but DigitalItem below is an EXPERIMENT DTO.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DJLibrary
{
    public sealed class DigitalItem
    {
        public int ItemId { get; set; }
        public string Path { get; set; } = "";
        public int Subsong { get; set; }
        public string Artist { get; set; } = "";
        public string Title { get; set; } = "";
        public string OriginalTitle { get; set; } = "";
        public string RemixedBy { get; set; } = "";
        public string Album { get; set; } = "";
        public string AlbumArtist { get; set; } = "";
        public string Genre { get; set; } = "";
        public string Style { get; set; } = "";
        public double Bpm { get; set; }
        public string Label { get; set; } = "";
        public string Catalog { get; set; } = "";
        public double DurationSeconds { get; set; }
        public string Codec { get; set; } = "";
        public string Bitrate { get; set; } = "";
        public string Fingerprint { get; set; } = "";
        public string MetadataVectorsJson { get; set; } = "";
    }
}

namespace DJLibrary.SyntheticComparison
{
    public static class BridgeV3Fixture
    {
        private const int Count = 1345;
        private const string Vectors = "[{\"name\":\"ARTIST\",\"values\":[\"Bridge Artist\",\"Synthetic\"]}]";

        private static void Require(bool ok, string detail)
        {
            if (!ok) throw new InvalidDataException("TEMP Bridge v3: " + detail);
        }

        private static string State(int schema, int generation) =>
            "schema_version\t" + schema.ToString(CultureInfo.InvariantCulture) + "\n" +
            "generation\t" + generation.ToString(CultureInfo.InvariantCulture) + "\n" +
            "complete\t1\nitem_count\t" + Count.ToString(CultureInfo.InvariantCulture) + "\n" +
            "last_change_utc\t2026-10-19T12:00:00Z\n" +
            "source_id\tsynthetic-bridge-v3\nsource_name\tDisposable Bridge A/B\n" +
            "profile_path\tX:\\DJLIB_SYNTH\\fake_profile\n" +
            "producer_version\tsynthetic-test\nproducer_pid\t1\n";

        private static void WriteItems(string path, bool duplicateLast = false, bool badSubsongLast = false)
        {
            using var file = File.Create(path);
            using var gzip = new GZipStream(file, CompressionMode.Compress);
            using var output = new StreamWriter(gzip, new UTF8Encoding(false));
            output.WriteLine(BridgeSnapshotReader.ItemHeaderV3);
            for (int i = 1; i <= Count; i++)
            {
                string[] fields = new string[BridgeSnapshotReader.ItemColumnCountV3];
                Array.Fill(fields, "");
                int identity = duplicateLast && i == Count ? 1 : i;
                fields[0] = "X:\\DJLIB_SYNTH\\bridge_" +
                            identity.ToString("D5", CultureInfo.InvariantCulture) + ".flac";
                fields[1] = badSubsongLast && i == Count ? "invalid" : "0";
                fields[2] = "Bridge Artist " + i.ToString("D4", CultureInfo.InvariantCulture);
                fields[4] = "Bridge Track " + i.ToString("D4", CultureInfo.InvariantCulture);
                fields[14] = "House";
                fields[19] = "180";
                fields[21] = "FLAC";
                fields[22] = "900";
                fields[23] = "synthetic-fingerprint-" + i.ToString(CultureInfo.InvariantCulture);
                fields[24] = "{}";
                fields[25] = Vectors;
                output.WriteLine(string.Join("\t", fields));
            }
        }

        private static string FileHash(string path) =>
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

        private static void RequireLoadRejection(string folder, BridgeSnapshotStatus status, string label)
        {
            bool rejected = false;
            try { BridgeSnapshotReader.LoadItems(folder, status); }
            catch (InvalidDataException) { rejected = true; }
            Require(rejected, "untrusted " + label + " snapshot was accepted");
        }

        public static List<SyntheticTrackRow> Load()
        {
            // First run the unchanged production Bridge v3 fixture/checker.
            BridgeSnapshotReader.ValidateSchemaV3Contract();
            string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "DJLibrary-Bridge-AB-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string statePath = System.IO.Path.Combine(folder, "bridge-state.tsv");
            string itemsPath = System.IO.Path.Combine(folder, "digital-items.tsv.gz");
            try
            {
                File.WriteAllText(statePath, State(3, 7), new UTF8Encoding(false));
                WriteItems(itemsPath);
                string stateBefore = FileHash(statePath);
                string itemsBefore = FileHash(itemsPath);

                var status = BridgeSnapshotReader.Inspect(folder, verifyItems: true);
                Require(status.Present && status.Complete && status.Compatible &&
                    status.SchemaVersion == 3 && status.Generation == 7 &&
                    status.VerifiedItemCount == Count && status.SourceId == "synthetic-bridge-v3" &&
                    string.IsNullOrEmpty(status.Error), "original Inspect did not validate v3");
                var items = BridgeSnapshotReader.LoadItems(folder, status);
                Require(items.Count == Count &&
                    items.Select(x => x.Path + "\n" + x.Subsong).Distinct(StringComparer.OrdinalIgnoreCase).Count() == Count &&
                    items[0].MetadataVectorsJson == Vectors &&
                    items[^1].Artist == "Bridge Artist 1345", "original LoadItems lost row/vector identity");
                Require(FileHash(statePath) == stateBefore && FileHash(itemsPath) == itemsBefore,
                    "read-only original parser changed fixture bytes");
                var rows = items.Select(x => new SyntheticTrackRow(
                    "bridge:" + x.Path + "\n" + x.Subsong.ToString(CultureInfo.InvariantCulture),
                    x.Artist, x.Title, x.Genre, "2026",
                    x.DurationSeconds.ToString("0", CultureInfo.InvariantCulture))).ToList();

                WriteItems(itemsPath, duplicateLast: true);
                RequireLoadRejection(folder, status, "duplicate path/subsong");
                WriteItems(itemsPath, badSubsongLast: true);
                RequireLoadRejection(folder, status, "invalid subsong");
                WriteItems(itemsPath);
                File.WriteAllText(statePath, State(3, 8), new UTF8Encoding(false));
                RequireLoadRejection(folder, status, "changed generation");
                File.WriteAllText(statePath, State(999, 7), new UTF8Encoding(false));
                Require(!BridgeSnapshotReader.Inspect(folder, verifyItems: false).Compatible,
                    "unsupported schema was accepted");
                return rows;
            }
            finally
            {
                try { Directory.Delete(folder, recursive: true); } catch { }
            }
        }
    }
}

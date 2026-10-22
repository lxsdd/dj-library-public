using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace DJLibrary
{
    // Original Framework application, original BridgeSnapshotReader.
    // The only fixture comes from the C++ Bridge producer; it is embedded in
    // the test executable and is not loaded from user Music or AppData paths.
    internal static class BridgeV3GoldenSelfTest
    {
        private const string ExpectedBlob = "f3b75d4608bacba23b2fd21b3350a7966e668a2e";

        internal static void Run(string tempRoot)
        {
            string source;
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
                "DJLibrary.BridgeV3CueGolden"))
            {
                if (stream == null) throw new InvalidDataException("Canonical V3 CUE test resource missing.");
                using (StreamReader reader = new StreamReader(stream, new UTF8Encoding(false)))
                    source = reader.ReadToEnd().Replace("\r\n", "\n");
            }
            byte[] bytes = Encoding.UTF8.GetBytes(source);
            byte[] head = Encoding.ASCII.GetBytes("blob " + bytes.Length + "\0");
            byte[] gitObject = new byte[head.Length + bytes.Length];
            Buffer.BlockCopy(head, 0, gitObject, 0, head.Length);
            Buffer.BlockCopy(bytes, 0, gitObject, head.Length, bytes.Length);
            string hash;
            using (SHA1 sha = SHA1.Create())
                hash = BitConverter.ToString(sha.ComputeHash(gitObject)).Replace("-", "").ToLowerInvariant();
            if (hash != ExpectedBlob)
                throw new InvalidDataException("Bridge C++ golden fixture source SHA changed.");

            string[] lines = source.TrimEnd('\r', '\n').Split('\n');
            if (lines.Length != 4)
                throw new InvalidDataException("Expected exact Bridge header and three CUE identity rows.");
            for (int i = 0; i < lines.Length; i++)
                if (lines[i].Split('\t').Length != BridgeSnapshotReader.ItemColumnCountV3)
                    throw new InvalidDataException("Bridge wire contract has an unexpected column count.");

            string dir = Path.Combine(tempRoot, "synthetic-bridge-cue-golden");
            Directory.CreateDirectory(dir);
            Write(dir, lines[0], new[] { lines[1], lines[2], lines[3] });
            BridgeSnapshotStatus verified = BridgeSnapshotReader.Inspect(dir, true);
            if (!verified.Present || !verified.Complete || !verified.Compatible ||
                verified.SchemaVersion != 3 || verified.VerifiedItemCount != 3)
                throw new InvalidDataException("Original DJ Library C# V3 reader rejected canonical producer snapshot.");
            List<DigitalItem> items = BridgeSnapshotReader.LoadItems(dir, verified);
            if (items.Count != 3) throw new InvalidDataException("Golden Bridge item count changed.");
            for (int i = 0; i < items.Count; i++)
            {
                string[] wire = lines[i + 1].Split('\t');
                DigitalItem t = items[i];
                if (t.Path != "SYNTHETIC:/Cue Mix.flac" || t.Subsong != i ||
                    t.Title != "Cue Track" || t.Genre != "Trance" ||
                    t.Fingerprint != wire[23] || t.MetadataVectorsJson != wire[25] ||
                    t.Codec != "FLAC" || t.DurationSeconds != 300)
                    throw new InvalidDataException("DJ Library projected a CUE golden track incorrectly.");
            }

            // A virtual-only snapshot must not invent a physical subsong 0.
            Write(dir, lines[0], new[] { lines[2], lines[3] });
            verified = BridgeSnapshotReader.Inspect(dir, true);
            if (!verified.Compatible || verified.VerifiedItemCount != 2)
                throw new InvalidDataException("Virtual-only V3 fixture rejected.");
            items = BridgeSnapshotReader.LoadItems(dir, verified);
            if (items.Count != 2 || items[0].Subsong != 1 || items[1].Subsong != 2)
                throw new InvalidDataException("Virtual-only snapshot fabricated physical identity.");

            Write(dir, lines[0], new[] { lines[2], lines[2] });
            ExpectReject(dir, "duplicate (path,subsong)");
            string[] headerParts = lines[0].Split('\t');
            Write(dir, String.Join("\t", headerParts, 0, 25), new[] { lines[1] });
            ExpectReject(dir, "truncated V3 header");
        }

        private static void Write(string directory, string header, string[] data)
        {
            string state = "schema_version\t3\n" + "generation\t7\n" + "complete\t1\n" +
                "item_count\t" + data.Length + "\n" +
                "last_change_utc\t2026-10-22T12:00:00Z\n" +
                "source_id\tsynthetic-golden\nsource_name\tRead-only CUE golden\n" +
                "profile_path\tSYNTHETIC:/empty-profile\n" +
                "producer_version\t0.1.0-test\nproducer_pid\t1\n";
            File.WriteAllText(Path.Combine(directory, "bridge-state.tsv"), state, new UTF8Encoding(false));
            using (FileStream file = File.Create(Path.Combine(directory, "digital-items.tsv.gz")))
            using (GZipStream gzip = new GZipStream(file, CompressionMode.Compress))
            using (StreamWriter writer = new StreamWriter(gzip, new UTF8Encoding(false)))
            {
                writer.WriteLine(header);
                foreach (string row in data) writer.WriteLine(row);
            }
        }

        private static void ExpectReject(string directory, string kind)
        {
            BridgeSnapshotStatus status = BridgeSnapshotReader.Inspect(directory, true);
            if (!status.Compatible) return;
            try { BridgeSnapshotReader.LoadItems(directory, status); }
            catch (InvalidDataException) { return; }
            throw new InvalidDataException("Unsafe CUE golden mutation accepted: " + kind);
        }
    }
}

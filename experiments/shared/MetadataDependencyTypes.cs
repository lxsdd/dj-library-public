// EXPERIMENT ONLY: inert DTO declarations corresponding to production
// src/CdMetadataSources.cs. This test never calls CD metadata lookup,
// foobar Bridge, HTTP or MusicBrainz. Full production CatalogService source
// and its entire schema/migration/SQL implementation remain unmodified.
using System;
using System.Globalization;
namespace DJLibrary
{
    public sealed class MetadataEvidence
    {
        public string Field { get; set; } = "";
        public string Value { get; set; } = "";
        public string Source { get; set; } = "";
        public string Detail { get; set; } = "";
        public string Url { get; set; } = "";
        public double Confidence { get; set; }
    }
    public sealed class MetadataSourceStatus
    {
        public string Source { get; set; } = "";
        public string Text { get; set; } = "";
        public string Url { get; set; } = "";
    }
    public sealed class CatalogTocMatch
    {
        public long ReleaseId { get; set; }
        public long DiscId { get; set; }
        public int DiscNumber { get; set; }
        public string AlbumArtist { get; set; } = "";
        public string Album { get; set; } = "";
        public string DisplayText =>
            (string.IsNullOrWhiteSpace(AlbumArtist) ? "—" : AlbumArtist) +
            " — " + (string.IsNullOrWhiteSpace(Album) ? "—" : Album) +
            " · Disc " + DiscNumber.ToString(CultureInfo.CurrentCulture);
    }
}

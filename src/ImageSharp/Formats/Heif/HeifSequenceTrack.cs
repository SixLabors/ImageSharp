// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Hevc;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Owns the bounded image behavior retained from one HEIF image-sequence track.
/// </summary>
internal sealed class HeifSequenceTrack
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HeifSequenceTrack"/> class.
    /// </summary>
    public HeifSequenceTrack()
    {
    }

    /// <summary>
    /// Gets or sets the file-defined track identifier.
    /// </summary>
    public uint Id { get; set; }

    /// <summary>
    /// Gets or sets the displayed track width in pixels.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Gets or sets the displayed track height in pixels.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Gets or sets the coded sample width in pixels before track presentation transforms.
    /// </summary>
    public int CodedWidth { get; set; }

    /// <summary>
    /// Gets or sets the coded sample height in pixels before track presentation transforms.
    /// </summary>
    public int CodedHeight { get; set; }

    /// <summary>
    /// Gets or sets the track transformation matrix.
    /// </summary>
    public HeifTrackMatrix Matrix { get; set; }

    /// <summary>
    /// Gets or sets the media time scale in units per second.
    /// </summary>
    public uint MediaTimescale { get; set; }

    /// <summary>
    /// Gets or sets the declared media duration in media-time-scale units.
    /// </summary>
    public ulong MediaDuration { get; set; }

    /// <summary>
    /// Gets or sets the total number of samples declared by the sample table.
    /// </summary>
    public uint TotalSampleCount { get; set; }

    /// <summary>
    /// Gets or sets the coded sample-entry type.
    /// </summary>
    public Heif4CharCode CodecType { get; set; }

    /// <summary>
    /// Gets or sets the parsed AV1 configuration when <see cref="CodecType"/> is <see cref="Heif4CharCode.Av01"/>.
    /// </summary>
    public Av1CodecConfiguration? Av1CodecConfiguration { get; set; }

    /// <summary>
    /// Gets or sets the parsed HEVC configuration when <see cref="CodecType"/> is <see cref="Heif4CharCode.Hvc1"/>.
    /// </summary>
    public HevcCodecConfiguration? HevcCodecConfiguration { get; set; }

    /// <summary>
    /// Gets or sets the retained sample descriptors in decode order.
    /// </summary>
    public HeifSequenceSample[] Samples { get; set; } = [];

    /// <summary>
    /// Gets or sets the compact zero-based sample indices referenced by retained samples.
    /// </summary>
    public int[] DirectReferenceSampleIndices { get; set; } = [];

    /// <summary>
    /// Gets or sets the identifier of the master track served by this auxiliary track, or zero for a master track.
    /// </summary>
    public uint AuxiliaryForTrackId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the track is an alpha auxiliary image sequence.
    /// </summary>
    public bool IsAlpha { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the color track is premultiplied by this auxiliary alpha track.
    /// </summary>
    public bool IsPremultiplied { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether every reference picture is intra coded.
    /// </summary>
    public bool AllReferencePicturesIntra { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether coded pictures use intra-picture prediction.
    /// </summary>
    public bool IntraPicturePredictionUsed { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of reference pictures permitted for one coded picture.
    /// </summary>
    public byte MaximumReferencesPerPicture { get; set; }

    /// <summary>
    /// Gets or sets the number of times the sequence is played. Zero indicates indefinite repetition.
    /// </summary>
    public ushort RepeatCount { get; set; } = 1;

    /// <summary>
    /// Gets or sets the track handler type.
    /// </summary>
    public Heif4CharCode HandlerType { get; set; }

    /// <summary>
    /// Gets or sets the track duration in movie-time-scale units.
    /// </summary>
    public ulong TrackDuration { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the track contains the edit list required for hidden samples.
    /// </summary>
    public bool HasEditList { get; set; }
}

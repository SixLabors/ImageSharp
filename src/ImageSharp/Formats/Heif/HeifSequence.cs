// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Contains the selected color and optional alpha tracks of one HEIF image sequence.
/// </summary>
internal sealed class HeifSequence
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HeifSequence"/> class.
    /// </summary>
    /// <param name="colorTrack">The selected master image-sequence track.</param>
    /// <param name="alphaTrack">The linked alpha image-sequence track, when present.</param>
    /// <param name="movieTimescale">The movie time scale in units per second.</param>
    public HeifSequence(HeifSequenceTrack colorTrack, HeifSequenceTrack? alphaTrack, uint movieTimescale)
    {
        this.ColorTrack = colorTrack;
        this.AlphaTrack = alphaTrack;
        this.MovieTimescale = movieTimescale;
    }

    /// <summary>
    /// Gets the selected master image-sequence track.
    /// </summary>
    public HeifSequenceTrack ColorTrack { get; }

    /// <summary>
    /// Gets the linked alpha image-sequence track, when present.
    /// </summary>
    public HeifSequenceTrack? AlphaTrack { get; }

    /// <summary>
    /// Gets the movie time scale in units per second.
    /// </summary>
    public uint MovieTimescale { get; }
}

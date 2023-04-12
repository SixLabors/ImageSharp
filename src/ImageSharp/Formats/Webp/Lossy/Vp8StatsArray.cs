// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Text.Json.Serialization;

namespace SixLabors.ImageSharp.Formats.Webp.Lossy;

internal class Vp8StatsArray
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Vp8StatsArray"/> class.
    /// </summary>
    public Vp8StatsArray() => this.Stats = new uint[WebpConstants.NumProbas];

    /// <summary>
    /// Initializes a new instance of the <see cref="Vp8StatsArray"/> class.
    /// Only used for unit tests.
    /// </summary>
    [JsonConstructor]
    public Vp8StatsArray(uint[] stats) => this.Stats = stats;

    public uint[] Stats { get; }
}

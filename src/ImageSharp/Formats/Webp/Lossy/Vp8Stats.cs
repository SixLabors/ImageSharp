// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Text.Json.Serialization;

namespace SixLabors.ImageSharp.Formats.Webp.Lossy;

internal class Vp8Stats
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Vp8Stats"/> class.
    /// </summary>
    public Vp8Stats()
    {
        this.Stats = new Vp8StatsArray[WebpConstants.NumCtx];
        for (int i = 0; i < WebpConstants.NumCtx; i++)
        {
            this.Stats[i] = new Vp8StatsArray();
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Vp8Stats"/> class.
    /// Only used for unit tests.
    /// </summary>
    [JsonConstructor]
    public Vp8Stats(Vp8StatsArray[] stats) => this.Stats = stats;

    public Vp8StatsArray[] Stats { get; }
}

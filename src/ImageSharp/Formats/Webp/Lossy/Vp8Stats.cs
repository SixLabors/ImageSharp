// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
            this.Stats[i] = new();
        }
    }

    public Vp8StatsArray[] Stats { get; }
}

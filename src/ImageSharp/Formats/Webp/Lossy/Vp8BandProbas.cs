// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp.Lossy;

/// <summary>
/// All the probabilities associated to one band.
/// </summary>
internal class Vp8BandProbas
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Vp8BandProbas"/> class.
    /// </summary>
    public Vp8BandProbas()
    {
        this.Probabilities = new Vp8ProbaArray[WebpConstants.NumCtx];
        for (int i = 0; i < WebpConstants.NumCtx; i++)
        {
            this.Probabilities[i] = new();
        }
    }

    /// <summary>
    /// Gets the Probabilities.
    /// </summary>
    public Vp8ProbaArray[] Probabilities { get; }
}

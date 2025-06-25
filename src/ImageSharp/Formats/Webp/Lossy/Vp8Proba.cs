// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp.Lossy;

/// <summary>
/// Data for all frame-persistent probabilities.
/// </summary>
internal class Vp8Proba
{
    private const int MbFeatureTreeProbs = 3;

    /// <summary>
    /// Initializes a new instance of the <see cref="Vp8Proba"/> class.
    /// </summary>
    public Vp8Proba()
    {
        this.Segments = new uint[MbFeatureTreeProbs];
        this.Bands = new Vp8BandProbas[WebpConstants.NumTypes, WebpConstants.NumBands];
        this.BandsPtr = new Vp8BandProbas[WebpConstants.NumTypes][];

        for (int i = 0; i < WebpConstants.NumTypes; i++)
        {
            for (int j = 0; j < WebpConstants.NumBands; j++)
            {
                this.Bands[i, j] = new Vp8BandProbas();
            }
        }

        for (int i = 0; i < WebpConstants.NumTypes; i++)
        {
            this.BandsPtr[i] = new Vp8BandProbas[16 + 1];
        }
    }

    public uint[] Segments { get; }

    public Vp8BandProbas[,] Bands { get; }

    public Vp8BandProbas[][] BandsPtr { get; }
}

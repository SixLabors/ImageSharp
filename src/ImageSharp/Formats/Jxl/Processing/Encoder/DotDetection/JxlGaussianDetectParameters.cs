// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.DotDetection;

internal struct JxlGaussianDetectParameters()
{
    /// <summary>
    /// At least one pixel must have a larger energy than THigh.
    /// </summary>
    public double THigh;

    /// <summary>
    /// All pixels must have a larger energy than TLow.
    /// </summary>
    public double TLow;

    /// <summary>
    /// Discard dots larger than this containing window.
    /// </summary>
    public int MaxWindowSize;
    public double MaxL2Loss;
    public double MaxCustomLoss;

    /// <summary>
    /// If the intensity is too low, discard it.
    /// </summary>
    public double MinIntensity;

    /// <summary>
    /// The mean and the mode must be close.
    /// </summary>
    public double MaxDistMeanMode;

    /// <summary>
    /// Maximum number of negative pixels.
    /// </summary>
    public int MaxNegativePixels;
    public int MinScore;

    /// <summary>
    /// Maximum number of connected components to keep.
    /// </summary>
    public int MaxCC = 50;

    /// <summary>
    /// Percentage in [0,100] of connected components to keep
    /// </summary>
    public int PercCC = 15;
}

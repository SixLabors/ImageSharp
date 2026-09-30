// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Ans;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular;

internal sealed class JxlModularOptions
{
    /// <summary>
    /// Gets or sets value indicating when to stop decoding/encoding
    /// when reaching a non-meta channel that has a dimension bigger
    /// than this value.
    /// </summary>
    public int MaxChannelSize { get; set; } = 0xFFFFFF;

    /// <summary>
    /// Gets or sets value used during decoding for validation
    /// of transforms (squeezing) scheme.
    /// </summary>
    public int GroupDimension { get; set; } = 0x1FFFFFFF;

    /// <summary>
    /// Gets or sets fraction of pixels to look at to learn a MA tree.
    /// </summary>
    public float NumberOfRepeats { get; set; } = 0.5f;

    /// <summary>
    /// Gets or sets maximum number of previous channel properties
    /// to use in the MA trees.
    /// </summary>
    public int MaxProperties { get; set; }

    /// <summary>
    /// Gets or sets properties that default to channel, group, weighted,
    /// gradient residual, W-NW, NW-N, N-NE, N-NN.
    /// </summary>
    public List<int> SplittingHeuristicsProperties { get; set; } = [0, 1, 15, 9, 10, 11, 12, 13];

    public float SplittingHeuristicsNodeThreshold { get; set; } = 96f;

    public int MaxPropertyValues { get; set; } = 32;

    public JxlPredictor Predictor { get; set; } = JxlPredictor.Undefined;

    public int WpMode { get; set; }

    public float FastDecodeMultiplier { get; set; } = 1.01f;

    public JxlTreeMode WpTreeMode { get; set; } = JxlTreeMode.Default;

    public bool SkipEncoderFastPath { get; set; }

    public JxlTreeKind TreeKind { get; set; } = JxlTreeKind.Learn;

    public JxlHistogramParameters HistogramParameters { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether to ignore the image and just
    /// pretend all tokens are zeroes.
    /// </summary>
    public bool ZeroTokens { get; set; }
}

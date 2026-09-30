// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.IO.Entropy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Ans;

/// <summary>
/// ANS histogram parameters
/// </summary>
internal sealed class JxlHistogramParameters
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JxlHistogramParameters"/> class with default values.
    /// </summary>
    public JxlHistogramParameters()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlHistogramParameters"/> class using the specified
    /// speed tier that prioritizes performance over compression and vice versa.
    /// </summary>
    public JxlHistogramParameters(JxlSpeedTier tier)
    {
        if (tier > JxlSpeedTier.Falcon)
        {
            // Fast modes
            this.Clustering = JxlClusteringType.Fastest;
            this.Lz77Method = JxlLz77Method.None;
        }
        else if (tier > JxlSpeedTier.Tortoise)
        {
            // Normal modes
            this.Clustering = JxlClusteringType.Fast;
        }
        else
        {
            // Slow modes
            this.Clustering = JxlClusteringType.Best;
        }

        if (tier > JxlSpeedTier.Tortoise)
        {
            this.UIntMethod = JxlHybridUIntMethod.None;
        }

        if (tier >= JxlSpeedTier.Squirrel)
        {
            this.AnsHistogramStrategy = JxlAnsHistogramStrategy.Approximate;
        }
    }

    /// <summary>
    /// Gets or sets the clustering type. Default is Best.
    /// </summary>
    public JxlClusteringType Clustering { get; set; } = JxlClusteringType.Best;

    /// <summary>
    /// Gets or sets the hybrid uint method. Default is Best.
    /// </summary>
    public JxlHybridUIntMethod UIntMethod { get; set; } = JxlHybridUIntMethod.Best;

    /// <summary>
    /// Gets or sets the LZ77 method. Default is Rle.
    /// </summary>
    public JxlLz77Method Lz77Method { get; set; } = JxlLz77Method.Rle;

    /// <summary>
    /// Gets or sets the ANS histogram strategy. Default is Precise.
    /// </summary>
    public JxlAnsHistogramStrategy AnsHistogramStrategy { get; set; } = JxlAnsHistogramStrategy.Precise;

    /// <summary>
    /// Gets or sets image widths.
    /// </summary>
    public List<int> ImageWidths { get; set; } = [];

    /// <summary>
    /// Gets or sets the max number of histograms.
    /// </summary>
    public uint MaxHistograms { get; set; } = ~0u;

    /// <summary>
    /// Gets or sets a value indicating whether to prefer Huffman coding.
    /// </summary>
    public bool ForceHuffman { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether global state should be initialized.
    /// (True by default)
    /// </summary>
    public bool InitializeGlobalState { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether streaming mode is enabled.
    /// </summary>
    public bool StreamingMode { get; set; }

    public bool AddMissingSymbols { get; set; }

    public bool AddFixedHistograms { get; set; }

    /// <summary>
    /// Gets the uint configuration for histogram parameters.
    /// </summary>
    public JxlAnsHybridUIntConfiguration UIntConfig => this.UIntMethod switch
    {
        JxlHybridUIntMethod.ContextMap => new(2, 0, 1),
        JxlHybridUIntMethod.Method000 => new(0, 0, 0),
        _ => new(),
    };
}

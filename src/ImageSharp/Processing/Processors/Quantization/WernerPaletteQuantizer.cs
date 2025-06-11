// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Processing.Processors.Quantization;

/// <summary>
/// A palette quantizer consisting of colors as defined in the original second edition of Werner’s Nomenclature of Colours 1821.
/// The hex codes were collected and defined by Nicholas Rougeux <see href="https://www.c82.net/werner"/>
/// </summary>
public sealed class WernerPaletteQuantizer : PaletteQuantizer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WernerPaletteQuantizer" /> class.
    /// </summary>
    public WernerPaletteQuantizer()
        : this(new QuantizerOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WernerPaletteQuantizer" /> class.
    /// </summary>
    /// <param name="options">The quantizer options defining quantization rules.</param>
    public WernerPaletteQuantizer(QuantizerOptions options)
        : base(Color.WernerPalette, options)
    {
    }
}

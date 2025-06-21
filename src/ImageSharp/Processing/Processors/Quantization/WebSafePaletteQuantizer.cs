// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Processing.Processors.Quantization;

/// <summary>
/// A palette quantizer consisting of web safe colors as defined in the CSS Color Module Level 4.
/// </summary>
public sealed class WebSafePaletteQuantizer : PaletteQuantizer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WebSafePaletteQuantizer" /> class.
    /// </summary>
    public WebSafePaletteQuantizer()
        : this(new())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSafePaletteQuantizer" /> class.
    /// </summary>
    /// <param name="options">The quantizer options defining quantization rules.</param>
    public WebSafePaletteQuantizer(QuantizerOptions options)
        : base(Color.WebSafePalette, options)
    {
    }
}

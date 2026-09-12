// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Image;

/// <summary>
/// Abstracts a way for the codec to output images during decoding.
/// </summary>
internal interface IJxlImageOutput : IDisposable
{
    /// <summary>
    /// Gets or sets the pixel format for the output pixels.
    /// </summary>
    public JxlPixelFormat PixelFormat { get; set; }

    /// <summary>
    /// Gets or sets the output bit depth for unsigned data types.
    /// </summary>
    public int BitsPerSample { get; set; }

    /// <summary>
    /// Gets or sets the pixel buffer for image output.
    /// </summary>
    public Memory<byte> Buffer { get; set; }

    /// <summary>
    /// Gets or sets length of a row of image buffer in bytes.
    /// </summary>
    public int Stride { get; set; }

    /// <summary>
    /// Outputs the image.
    /// </summary>
    /// <param name="data">Data where the image should be output.</param>
    /// <param name="x">X offset</param>
    /// <param name="y">Y offset</param>
    /// <param name="pixels">Pixels to output.</param>
    public void Output(
        Span<byte> data,
        int x,
        int y,
        Span<byte> pixels);

    /// <summary>
    /// Initializes image data.
    /// </summary>
    /// <param name="data">Output image data.</param>
    /// <param name="numPixelsPerThread">Number of pixels a thread processes.</param>
    public void Initialize(
        Span<byte> data,
        int numPixelsPerThread);
}

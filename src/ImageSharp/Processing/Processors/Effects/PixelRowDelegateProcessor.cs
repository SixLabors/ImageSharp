// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Effects;

/// <summary>
/// Applies a user defined row processing delegate to the image.
/// </summary>
internal sealed class PixelRowDelegateProcessor : IImageProcessor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PixelRowDelegateProcessor"/> class.
    /// </summary>
    /// <param name="pixelRowOperation">The user defined, row processing delegate.</param>
    /// <param name="modifiers">The <see cref="PixelConversionModifiers"/> to apply during the pixel conversions.</param>
    public PixelRowDelegateProcessor(PixelRowOperation pixelRowOperation, PixelConversionModifiers modifiers)
    {
        this.PixelRowOperation = pixelRowOperation;
        this.Modifiers = modifiers;
    }

    /// <summary>
    /// Gets the user defined row processing delegate to the image.
    /// </summary>
    public PixelRowOperation PixelRowOperation { get; }

    /// <summary>
    /// Gets the <see cref="PixelConversionModifiers"/> to apply during the pixel conversions.
    /// </summary>
    public PixelConversionModifiers Modifiers { get; }

    /// <inheritdoc />
    public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
        where TPixel : unmanaged, IPixel<TPixel>
        => new PixelRowDelegateProcessor<TPixel, PixelRowDelegate>(
            new PixelRowDelegate(this.PixelRowOperation),
            configuration,
            this.Modifiers,
            source,
            sourceRectangle);

    /// <summary>
    /// A <see langword="struct"/> implementing the row processing logic for <see cref="PixelRowDelegateProcessor"/>.
    /// </summary>
    public readonly struct PixelRowDelegate : IPixelRowDelegate
    {
        private readonly PixelRowOperation pixelRowOperation;

        [MethodImpl(InliningOptions.ShortMethod)]
        public PixelRowDelegate(PixelRowOperation pixelRowOperation)
            => this.pixelRowOperation = pixelRowOperation;

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void Invoke(Span<Vector4> span, Point offset) => this.pixelRowOperation(span);
    }
}

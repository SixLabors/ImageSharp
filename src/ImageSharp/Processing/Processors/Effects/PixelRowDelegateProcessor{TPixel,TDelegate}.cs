// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.ColorProfiles.Companding;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Effects;

/// <summary>
/// The base class for all processors that accept a user defined row processing delegate.
/// </summary>
/// <typeparam name="TPixel">The pixel format.</typeparam>
/// <typeparam name="TDelegate">The row processor type.</typeparam>
internal sealed class PixelRowDelegateProcessor<TPixel, TDelegate> : ImageProcessor<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
    where TDelegate : struct, IPixelRowDelegate
{
    private readonly TDelegate rowDelegate;

    /// <summary>
    /// The <see cref="PixelConversionModifiers"/> to apply during the pixel conversions.
    /// </summary>
    private readonly PixelConversionModifiers modifiers;

    /// <summary>
    /// Initializes a new instance of the <see cref="PixelRowDelegateProcessor{TPixel,TDelegate}"/> class.
    /// </summary>
    /// <param name="rowDelegate">The row processor to use to process each pixel row</param>
    /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
    /// <param name="modifiers">The <see cref="PixelConversionModifiers"/> to apply during the pixel conversions.</param>
    /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
    /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
    public PixelRowDelegateProcessor(
        in TDelegate rowDelegate,
        Configuration configuration,
        PixelConversionModifiers modifiers,
        Image<TPixel> source,
        Rectangle sourceRectangle)
        : base(configuration, source, sourceRectangle)
    {
        this.rowDelegate = rowDelegate;
        this.modifiers = modifiers;
    }

    /// <inheritdoc/>
    protected override void OnFrameApply(ImageFrame<TPixel> source)
    {
        Rectangle interest = Rectangle.Intersect(this.SourceRectangle, source.Bounds);
        RowOperation operation = new(interest.X, source.PixelBuffer, this.Configuration, this.modifiers, this.rowDelegate);

        ParallelRowIterator.IterateRows<RowOperation, Vector4>(
            this.Configuration,
            interest,
            in operation);
    }

    /// <summary>
    /// A <see langword="struct"/> implementing the convolution logic for <see cref="PixelRowDelegateProcessor{TPixel,TDelegate}"/>.
    /// </summary>
    private readonly struct RowOperation : IRowOperation<Vector4>
    {
        private readonly int startX;
        private readonly Buffer2D<TPixel> source;
        private readonly Configuration configuration;
        private readonly bool scaled;
        private readonly bool premultiply;
        private readonly bool unpremultiply;
        private readonly bool associated;
        private readonly bool compand;
        private readonly TDelegate rowProcessor;

        [MethodImpl(InliningOptions.ShortMethod)]
        public RowOperation(
            int startX,
            Buffer2D<TPixel> source,
            Configuration configuration,
            PixelConversionModifiers modifiers,
            in TDelegate rowProcessor)
        {
            this.startX = startX;
            this.source = source;
            this.configuration = configuration;
            this.scaled = modifiers.IsDefined(PixelConversionModifiers.Scale);
            this.premultiply = modifiers.IsDefined(PixelConversionModifiers.Premultiply);
            this.unpremultiply = modifiers.IsDefined(PixelConversionModifiers.UnPremultiply);

            // With no alpha flag, the pixel format keeps its native alpha representation.
            this.associated = this.premultiply
                || (!this.unpremultiply && TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated);
            this.compand = modifiers.IsDefined(PixelConversionModifiers.SRgbCompand);
            this.rowProcessor = rowProcessor;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public int GetRequiredBufferLength(Rectangle bounds)
            => bounds.Width;

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void Invoke(int y, Span<Vector4> span)
        {
            Span<TPixel> rowSpan = this.source.DangerousGetRowSpan(y).Slice(this.startX, span.Length);

            if (this.compand)
            {
                if (this.scaled)
                {
                    PixelOperations<TPixel>.Instance.ToUnassociatedScaledVector4(this.configuration, rowSpan, span);
                }
                else
                {
                    PixelOperations<TPixel>.Instance.ToUnassociatedVector4(this.configuration, rowSpan, span);
                }

                SRgbCompanding.Expand(span);

                if (this.associated)
                {
                    // Expand in straight form, then restore the requested or native alpha representation.
                    Numerics.Premultiply(span);
                }
            }
            else if (this.premultiply)
            {
                if (this.scaled)
                {
                    PixelOperations<TPixel>.Instance.ToAssociatedScaledVector4(this.configuration, rowSpan, span);
                }
                else
                {
                    PixelOperations<TPixel>.Instance.ToAssociatedVector4(this.configuration, rowSpan, span);
                }
            }
            else if (this.unpremultiply)
            {
                if (this.scaled)
                {
                    PixelOperations<TPixel>.Instance.ToUnassociatedScaledVector4(this.configuration, rowSpan, span);
                }
                else
                {
                    PixelOperations<TPixel>.Instance.ToUnassociatedVector4(this.configuration, rowSpan, span);
                }
            }
            else if (this.scaled)
            {
                PixelOperations<TPixel>.Instance.ToScaledVector4(this.configuration, rowSpan, span);
            }
            else
            {
                PixelOperations<TPixel>.Instance.ToVector4(this.configuration, rowSpan, span);
            }

            // Run the user defined pixel shader to the current row of pixels
            Unsafe.AsRef(in this.rowProcessor).Invoke(span, new Point(this.startX, y));

            if (this.compand)
            {
                if (this.associated)
                {
                    // Remove association before companding; the destination receives straight vectors.
                    Numerics.UnPremultiply(span);
                }

                SRgbCompanding.Compress(span);
                if (this.scaled)
                {
                    PixelOperations<TPixel>.Instance.FromUnassociatedScaledVector4Destructive(this.configuration, span, rowSpan);
                }
                else
                {
                    PixelOperations<TPixel>.Instance.FromUnassociatedVector4Destructive(this.configuration, span, rowSpan);
                }
            }
            else if (this.premultiply)
            {
                if (this.scaled)
                {
                    PixelOperations<TPixel>.Instance.FromAssociatedScaledVector4Destructive(this.configuration, span, rowSpan);
                }
                else
                {
                    PixelOperations<TPixel>.Instance.FromAssociatedVector4Destructive(this.configuration, span, rowSpan);
                }
            }
            else if (this.unpremultiply)
            {
                if (this.scaled)
                {
                    PixelOperations<TPixel>.Instance.FromUnassociatedScaledVector4Destructive(this.configuration, span, rowSpan);
                }
                else
                {
                    PixelOperations<TPixel>.Instance.FromUnassociatedVector4Destructive(this.configuration, span, rowSpan);
                }
            }
            else if (this.scaled)
            {
                PixelOperations<TPixel>.Instance.FromScaledVector4Destructive(this.configuration, span, rowSpan);
            }
            else
            {
                PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, span, rowSpan);
            }
        }
    }
}

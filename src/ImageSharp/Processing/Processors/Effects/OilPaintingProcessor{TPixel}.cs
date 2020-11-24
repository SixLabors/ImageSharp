// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Effects
{
    /// <summary>
    /// Applies oil painting effect processing to the image.
    /// </summary>
    /// <remarks>Adapted from <see href="https://softwarebydefault.com/2013/06/29/oil-painting-cartoon-filter/"/> by Dewald Esterhuizen.</remarks>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class OilPaintingProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly OilPaintingProcessor definition;

        /// <summary>
        /// Initializes a new instance of the <see cref="OilPaintingProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="definition">The <see cref="OilPaintingProcessor"/> defining the processor parameters.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public OilPaintingProcessor(Configuration configuration, OilPaintingProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
            : base(configuration, source, sourceRectangle)
        {
            this.definition = definition;
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            int brushSize = this.definition.BrushSize;
            if (brushSize <= 0 || brushSize > source.Height || brushSize > source.Width)
            {
                throw new ArgumentOutOfRangeException(nameof(brushSize));
            }

            using Buffer2D<TPixel> targetPixels = this.Configuration.MemoryAllocator.Allocate2D<TPixel>(source.Size());

            source.CopyTo(targetPixels);

            var operation = new RowIntervalOperation(this.SourceRectangle, targetPixels, source, this.Configuration, brushSize >> 1, this.definition.Levels);
            ParallelRowIterator.IterateRowIntervals(
                this.Configuration,
                this.SourceRectangle,
                in operation);

            Buffer2D<TPixel>.SwapOrCopyContent(source.PixelBuffer, targetPixels);
        }

        /// <summary>
        /// A <see langword="struct"/> implementing the convolution logic for <see cref="OilPaintingProcessor{T}"/>.
        /// </summary>
        private readonly struct RowIntervalOperation : IRowIntervalOperation
        {
            private readonly Rectangle bounds;
            private readonly Buffer2D<TPixel> targetPixels;
            private readonly ImageFrame<TPixel> source;
            private readonly Configuration configuration;
            private readonly int radius;
            private readonly int levels;

            [MethodImpl(InliningOptions.ShortMethod)]
            public RowIntervalOperation(
                Rectangle bounds,
                Buffer2D<TPixel> targetPixels,
                ImageFrame<TPixel> source,
                Configuration configuration,
                int radius,
                int levels)
            {
                this.bounds = bounds;
                this.targetPixels = targetPixels;
                this.source = source;
                this.configuration = configuration;
                this.radius = radius;
                this.levels = levels;
            }

            /// <inheritdoc/>
            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(in RowInterval rows)
            {
                int maxY = this.bounds.Bottom - 1;
                int maxX = this.bounds.Right - 1;

                /* Allocate the two temporary Vector4 buffers, one for the source row and one for the target row.
                 * The ParallelHelper.IterateRowsWithTempBuffers overload is not used in this case because
                 * the two allocated buffers have a length equal to the width of the source image,
                 * and not just equal to the width of the target rectangle to process.
                 * Furthermore, there are two buffers being allocated in this case, so using that overload would
                 * have still required the explicit allocation of the secondary buffer.
                 * Similarly, one temporary float buffer is also allocated from the pool, and that is used
                 * to create the target bins for all the color channels being processed.
                 * This buffer is only rented once outside of the main processing loop, and its contents
                 * are cleared for each loop iteration, to avoid the repeated allocation for each processed pixel. */
                using IMemoryOwner<Vector4> sourceRowBuffer = this.configuration.MemoryAllocator.Allocate<Vector4>(this.source.Width);
                using IMemoryOwner<Vector4> targetRowBuffer = this.configuration.MemoryAllocator.Allocate<Vector4>(this.source.Width);
                using IMemoryOwner<float> bins = this.configuration.MemoryAllocator.Allocate<float>(this.levels * 4);

                Span<Vector4> sourceRowVector4Span = sourceRowBuffer.Memory.Span;
                Span<Vector4> sourceRowAreaVector4Span = sourceRowVector4Span.Slice(this.bounds.X, this.bounds.Width);

                Span<Vector4> targetRowVector4Span = targetRowBuffer.Memory.Span;
                Span<Vector4> targetRowAreaVector4Span = targetRowVector4Span.Slice(this.bounds.X, this.bounds.Width);

                ref float binsRef = ref bins.GetReference();
                ref int intensityBinRef = ref Unsafe.As<float, int>(ref binsRef);
                ref float redBinRef = ref Unsafe.Add(ref binsRef, this.levels);
                ref float blueBinRef = ref Unsafe.Add(ref redBinRef, this.levels);
                ref float greenBinRef = ref Unsafe.Add(ref blueBinRef, this.levels);

                for (int y = rows.Min; y < rows.Max; y++)
                {
                    Span<TPixel> sourceRowPixelSpan = this.source.GetPixelRowSpan(y);
                    Span<TPixel> sourceRowAreaPixelSpan = sourceRowPixelSpan.Slice(this.bounds.X, this.bounds.Width);

                    PixelOperations<TPixel>.Instance.ToVector4(this.configuration, sourceRowAreaPixelSpan, sourceRowAreaVector4Span);

                    for (int x = this.bounds.X; x < this.bounds.Right; x++)
                    {
                        int maxIntensity = 0;
                        int maxIndex = 0;

                        // Clear the current shared buffer before processing each target pixel
                        bins.Memory.Span.Clear();

                        for (int fy = 0; fy <= this.radius; fy++)
                        {
                            int fyr = fy - this.radius;
                            int offsetY = y + fyr;
                            offsetY = Numerics.Clamp(offsetY, 0, maxY);

                            Span<TPixel> sourceOffsetRow = this.source.GetPixelRowSpan(offsetY);

                            for (int fx = 0; fx <= this.radius; fx++)
                            {
                                int fxr = fx - this.radius;
                                int offsetX = x + fxr;
                                offsetX = Numerics.Clamp(offsetX, 0, maxX);

                                var vector = sourceOffsetRow[offsetX].ToVector4();

                                float sourceRed = vector.X;
                                float sourceBlue = vector.Z;
                                float sourceGreen = vector.Y;

                                int currentIntensity = (int)MathF.Round((sourceBlue + sourceGreen + sourceRed) / 3F * (this.levels - 1));

                                Unsafe.Add(ref intensityBinRef, currentIntensity)++;
                                Unsafe.Add(ref redBinRef, currentIntensity) += sourceRed;
                                Unsafe.Add(ref blueBinRef, currentIntensity) += sourceBlue;
                                Unsafe.Add(ref greenBinRef, currentIntensity) += sourceGreen;

                                if (Unsafe.Add(ref intensityBinRef, currentIntensity) > maxIntensity)
                                {
                                    maxIntensity = Unsafe.Add(ref intensityBinRef, currentIntensity);
                                    maxIndex = currentIntensity;
                                }
                            }

                            float red = MathF.Abs(Unsafe.Add(ref redBinRef, maxIndex) / maxIntensity);
                            float blue = MathF.Abs(Unsafe.Add(ref blueBinRef, maxIndex) / maxIntensity);
                            float green = MathF.Abs(Unsafe.Add(ref greenBinRef, maxIndex) / maxIntensity);
                            float alpha = sourceRowVector4Span[x].W;

                            targetRowVector4Span[x] = new Vector4(red, green, blue, alpha);
                        }
                    }

                    Span<TPixel> targetRowAreaPixelSpan = this.targetPixels.GetRowSpan(y).Slice(this.bounds.X, this.bounds.Width);

                    PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, targetRowAreaVector4Span, targetRowAreaPixelSpan);
                }
            }
        }
    }
}

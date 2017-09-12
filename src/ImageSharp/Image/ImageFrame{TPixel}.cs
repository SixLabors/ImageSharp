// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.MetaData;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Represents a single frame in a animation.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public sealed class ImageFrame<TPixel> : IPixelSource<TPixel>, IDisposable
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// The image pixels. Not private as Buffer2D requires an array in its constructor.
        /// </summary>
        private Buffer2D<TPixel> pixelBuffer;

        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrame{TPixel}" /> class.
        /// </summary>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        internal ImageFrame(int width, int height)
            : this(width, height, new ImageFrameMetaData())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrame{TPixel}" /> class.
        /// </summary>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        /// <param name="metaData">The meta data.</param>
        internal ImageFrame(int width, int height, ImageFrameMetaData metaData)
        {
            Guard.MustBeGreaterThan(width, 0, nameof(width));
            Guard.MustBeGreaterThan(height, 0, nameof(height));
            Guard.NotNull(metaData, nameof(metaData));

            this.pixelBuffer = Buffer2D<TPixel>.CreateClean(width, height);
            this.MetaData = metaData;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrame{TPixel}" /> class.
        /// </summary>
        /// <param name="source">The source.</param>
        internal ImageFrame(ImageFrame<TPixel> source)
        {
            this.pixelBuffer = new Buffer2D<TPixel>(source.pixelBuffer.Width, source.pixelBuffer.Height);
            source.pixelBuffer.Span.CopyTo(this.pixelBuffer.Span);
            this.MetaData = source.MetaData.Clone();
        }

        /// <inheritdoc/>
        Buffer2D<TPixel> IPixelSource<TPixel>.PixelBuffer => this.pixelBuffer;

        /// <summary>
        /// Gets the width.
        /// </summary>
        public int Width => this.pixelBuffer.Width;

        /// <summary>
        /// Gets the height.
        /// </summary>
        public int Height => this.pixelBuffer.Height;

        /// <summary>
        /// Gets the meta data of the frame.
        /// </summary>
        public ImageFrameMetaData MetaData { get; private set; }

        /// <summary>
        /// Gets or sets the pixel at the specified position.
        /// </summary>
        /// <param name="x">The x-coordinate of the pixel. Must be greater than or equal to zero and less than the width of the image.</param>
        /// <param name="y">The y-coordinate of the pixel. Must be greater than or equal to zero and less than the height of the image.</param>
        /// <returns>The <see typeparam="TPixel"/> at the specified position.</returns>
        public TPixel this[int x, int y]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return this.pixelBuffer[x, y];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                this.pixelBuffer[x, y] = value;
            }
        }

        /// <summary>
        /// Performs an explicit conversion from <see cref="Image{TPixel}"/> to <see cref="ImageFrame{TPixel}"/>.
        /// </summary>
        /// <param name="image">The image.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static implicit operator ImageFrame<TPixel>(Image<TPixel> image) => image.Frames[0];

        /// <summary>
        /// Gets a reference to the pixel at the specified position.
        /// </summary>
        /// <param name="x">The x-coordinate of the pixel. Must be greater than or equal to zero and less than the width of the image.</param>
        /// <param name="y">The y-coordinate of the pixel. Must be greater than or equal to zero and less than the height of the image.</param>
        /// <returns>The <see typeparam="TPixel"/> at the specified position.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref TPixel GetPixelReference(int x, int y)
        {
            return ref this.pixelBuffer[x, y];
        }

        /// <summary>
        /// Locks the image providing access to the pixels.
        /// <remarks>
        /// It is imperative that the accessor is correctly disposed off after use.
        /// </remarks>
        /// </summary>
        /// <returns>The <see cref="PixelAccessor{TPixel}"/></returns>
        internal PixelAccessor<TPixel> Lock()
        {
            return new PixelAccessor<TPixel>(this);
        }

        /// <summary>
        /// Copies the pixels to another <see cref="PixelAccessor{TPixel}"/> of the same size.
        /// </summary>
        /// <param name="target">The target pixel buffer accessor.</param>
        internal void CopyTo(PixelAccessor<TPixel> target)
        {
            SpanHelper.Copy(this.GetPixelSpan(), target.PixelBuffer.Span);
        }

        /// <summary>
        /// Switches the buffers used by the image and the PixelAccessor meaning that the Image will "own" the buffer from the PixelAccessor and the PixelAccessor will now own the Images buffer.
        /// </summary>
        /// <param name="pixelSource">The pixel source.</param>
        internal void SwapPixelsBuffers(PixelAccessor<TPixel> pixelSource)
        {
            Guard.NotNull(pixelSource, nameof(pixelSource));

            // Push my memory into the accessor (which in turn unpins the old buffer ready for the images use)
            Buffer2D<TPixel> newPixels = pixelSource.SwapBufferOwnership(this.pixelBuffer);
            this.pixelBuffer = newPixels;
        }

        /// <summary>
        /// Switches the buffers used by the image and the pixelSource meaning that the Image will "own" the buffer from the pixelSource and the pixelSource will now own the Images buffer.
        /// </summary>
        /// <param name="pixelSource">The pixel source.</param>
        internal void SwapPixelsBuffers(ImageFrame<TPixel> pixelSource)
        {
            Guard.NotNull(pixelSource, nameof(pixelSource));

            ComparableExtensions.Swap(ref this.pixelBuffer, ref pixelSource.pixelBuffer);
        }

        /// <summary>
        /// Disposes the object and frees resources for the Garbage Collector.
        /// </summary>
        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            this.pixelBuffer?.Dispose();
            this.pixelBuffer = null;

            // Note disposing is done.
            this.isDisposed = true;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"ImageFrame<{typeof(TPixel).Name}>: {this.Width}x{this.Height}";
        }

        /// <summary>
        /// Returns a copy of the image frame in the given pixel format.
        /// </summary>
        /// <typeparam name="TPixel2">The pixel format.</typeparam>
        /// <returns>The <see cref="ImageFrame{TPixel2}"/></returns>
        public ImageFrame<TPixel2> CloneAs<TPixel2>()
            where TPixel2 : struct, IPixel<TPixel2>
        {
            if (typeof(TPixel2) == typeof(TPixel))
            {
                return this.Clone() as ImageFrame<TPixel2>;
            }

            Func<Vector4, Vector4> scaleFunc = PackedPixelConverterHelper.ComputeScaleFunction<TPixel, TPixel2>();

            var target = new ImageFrame<TPixel2>(this.Width, this.Height, this.MetaData.Clone());

            using (PixelAccessor<TPixel> pixels = this.Lock())
            using (PixelAccessor<TPixel2> targetPixels = target.Lock())
            {
                Parallel.For(
                    0,
                    target.Height,
                    Configuration.Default.ParallelOptions,
                    y =>
                    {
                        for (int x = 0; x < target.Width; x++)
                        {
                            var color = default(TPixel2);
                            color.PackFromVector4(scaleFunc(pixels[x, y].ToVector4()));
                            targetPixels[x, y] = color;
                        }
                    });
            }

            return target;
        }

        /// <summary>
        /// Clones the current instance.
        /// </summary>
        /// <returns>The <see cref="ImageFrame{TPixel}"/></returns>
        public ImageFrame<TPixel> Clone()
        {
            return new ImageFrame<TPixel>(this);
        }
    }
}
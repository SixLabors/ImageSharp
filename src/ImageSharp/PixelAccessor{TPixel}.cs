// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// A delegate to be executed on a <see cref="PixelAccessor{TPixel}"/>.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    public delegate void PixelAccessorAction<TPixel>(PixelAccessor<TPixel> pixelAccessor)
        where TPixel : unmanaged, IPixel<TPixel>;

    /// <summary>
    /// A delegate to be executed on two instances of <see cref="PixelAccessor{TPixel}"/>.
    /// </summary>
    /// <typeparam name="TPixel1">The first pixel type.</typeparam>
    /// <typeparam name="TPixel2">The second pixel type.</typeparam>
    public delegate void PixelAccessorAction<TPixel1, TPixel2>(
        PixelAccessor<TPixel1> pixelAccessor1,
        PixelAccessor<TPixel2> pixelAccessor2)
        where TPixel1 : unmanaged, IPixel<TPixel1>
        where TPixel2 : unmanaged, IPixel<TPixel2>;

    /// <summary>
    /// A delegate to be executed on three instances of <see cref="PixelAccessor{TPixel}"/>.
    /// </summary>
    /// <typeparam name="TPixel1">The first pixel type.</typeparam>
    /// <typeparam name="TPixel2">The second pixel type.</typeparam>
    /// <typeparam name="TPixel3">The third pixel type.</typeparam>
    public delegate void PixelAccessorAction<TPixel1, TPixel2, TPixel3>(
        PixelAccessor<TPixel1> pixelAccessor1,
        PixelAccessor<TPixel2> pixelAccessor2,
        PixelAccessor<TPixel3> pixelAccessor3)
        where TPixel1 : unmanaged, IPixel<TPixel1>
        where TPixel2 : unmanaged, IPixel<TPixel2>
        where TPixel3 : unmanaged, IPixel<TPixel3>;

    /// <summary>
    /// Provides efficient access the pixel buffers of an <see cref="Image{TPixel}"/>.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    public ref struct PixelAccessor<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private Buffer2D<TPixel> buffer;

        internal PixelAccessor(Buffer2D<TPixel> buffer) => this.buffer = buffer;

        /// <summary>
        /// Gets the width of the backing <see cref="Image{TPixel}"/>.
        /// </summary>
        public int Width => this.buffer.Width;

        /// <summary>
        /// Gets the height of the backing <see cref="Image{TPixel}"/>.
        /// </summary>
        public int Height => this.buffer.Height;

        /// <summary>
        /// Gets the representation of the pixels as a <see cref="Span{T}"/> of contiguous memory
        /// at row <paramref name="rowIndex"/> beginning from the first pixel on that row.
        /// </summary>
        /// <param name="rowIndex">The row index.</param>
        /// <returns>The <see cref="Span{TPixel}"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when row index is out of range.</exception>
        public Span<TPixel> GetRowSpan(int rowIndex) => this.buffer.DangerousGetRowSpan(rowIndex);
    }
}

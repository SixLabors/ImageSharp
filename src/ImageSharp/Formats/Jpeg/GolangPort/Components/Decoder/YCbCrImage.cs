// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Formats.Jpeg.GolangPort.Components.Decoder
{
    using SixLabors.ImageSharp.Formats.Jpeg.Common;

    /// <summary>
    /// Represents an image made up of three color components (luminance, blue chroma, red chroma)
    /// </summary>
    internal class YCbCrImage : IDisposable
    {
        // Complex value type field + mutable + available to other classes = the field MUST NOT be private :P
#pragma warning disable SA1401 // FieldsMustBePrivate
        /// <summary>
        /// Gets the luminance components channel as <see cref="OrigJpegPixelArea" />.
        /// </summary>
        public Buffer2D<byte> YChannel;

        /// <summary>
        /// Gets the blue chroma components channel as <see cref="OrigJpegPixelArea" />.
        /// </summary>
        public Buffer2D<byte> CbChannel;

        /// <summary>
        /// Gets an offseted <see cref="OrigJpegPixelArea" /> to the Cr channel
        /// </summary>
        public Buffer2D<byte> CrChannel;
#pragma warning restore SA1401

        /// <summary>
        /// Initializes a new instance of the <see cref="YCbCrImage" /> class.
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="ratio">The ratio.</param>
        public YCbCrImage(int width, int height, SubsampleRatio ratio)
        {
            Size cSize = ratio.CalculateChrominanceSize(width, height);

            this.Ratio = ratio;
            this.YStride = width;
            this.CStride = cSize.Width;

            this.YChannel = Buffer2D<byte>.CreateClean(width, height);
            this.CbChannel = Buffer2D<byte>.CreateClean(cSize.Width, cSize.Height);
            this.CrChannel = Buffer2D<byte>.CreateClean(cSize.Width, cSize.Height);
        }

        /// <summary>
        /// Gets the Y slice index delta between vertically adjacent pixels.
        /// </summary>
        public int YStride { get; }

        /// <summary>
        /// Gets the red and blue chroma slice index delta between vertically adjacent pixels
        /// that map to separate chroma samples.
        /// </summary>
        public int CStride { get; }

        /// <summary>
        /// Gets or sets the subsampling ratio.
        /// </summary>
        public SubsampleRatio Ratio { get; set; }

        /// <summary>
        /// Disposes the <see cref="YCbCrImage" /> returning rented arrays to the pools.
        /// </summary>
        public void Dispose()
        {
            this.YChannel.Dispose();
            this.CbChannel.Dispose();
            this.CrChannel.Dispose();
        }

        /// <summary>
        /// Returns the offset of the first chroma component at the given row
        /// </summary>
        /// <param name="y">The row number.</param>
        /// <returns>
        /// The <see cref="int" />.
        /// </returns>
        public int GetRowCOffset(int y)
        {
            switch (this.Ratio)
            {
                case SubsampleRatio.Ratio422:
                    return y * this.CStride;
                case SubsampleRatio.Ratio420:
                    return (y / 2) * this.CStride;
                case SubsampleRatio.Ratio440:
                    return (y / 2) * this.CStride;
                case SubsampleRatio.Ratio411:
                    return y * this.CStride;
                case SubsampleRatio.Ratio410:
                    return (y / 2) * this.CStride;
                default:
                    return y * this.CStride;
            }
        }

        /// <summary>
        /// Returns the offset of the first luminance component at the given row
        /// </summary>
        /// <param name="y">The row number.</param>
        /// <returns>
        /// The <see cref="int" />.
        /// </returns>
        public int GetRowYOffset(int y)
        {
            return y * this.YStride;
        }
    }
}
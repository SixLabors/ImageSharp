// <copyright file="YCbCrImage.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>
namespace ImageSharp.Formats.Jpg
{
    using System;
    using System.Buffers;

    /// <summary>
    /// Represents an image made up of three color components (luminance, blue chroma, red chroma)
    /// </summary>
    internal class YCbCrImage : IDisposable
    {
        // Complex value type field + mutable + available to other classes = the field MUST NOT be private :P
#pragma warning disable SA1401 // FieldsMustBePrivate
        /// <summary>
        /// Gets the luminance components channel as <see cref="JpegPixelArea" />.
        /// </summary>
        public JpegPixelArea YChannel;

        /// <summary>
        /// Gets the blue chroma components channel as <see cref="JpegPixelArea" />.
        /// </summary>
        public JpegPixelArea CbChannel;

        /// <summary>
        /// Gets an offseted <see cref="JpegPixelArea" /> to the Cr channel
        /// </summary>
        public JpegPixelArea CrChannel;
#pragma warning restore SA1401

        /// <summary>
        /// Initializes a new instance of the <see cref="YCbCrImage" /> class.
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="ratio">The ratio.</param>
        public YCbCrImage(int width, int height, YCbCrSubsampleRatio ratio)
        {
            Size cSize = CalculateChrominanceSize(width, height, ratio);

            this.Ratio = ratio;
            this.YStride = width;
            this.CStride = cSize.Width;

            this.YChannel = JpegPixelArea.CreatePooled(width, height);
            this.CbChannel = JpegPixelArea.CreatePooled(cSize.Width, cSize.Height);
            this.CrChannel = JpegPixelArea.CreatePooled(cSize.Width, cSize.Height);
        }

        /// <summary>
        /// Provides enumeration of the various available subsample ratios.
        /// </summary>
        public enum YCbCrSubsampleRatio
        {
            /// <summary>
            /// YCbCrSubsampleRatio444
            /// </summary>
            YCbCrSubsampleRatio444,

            /// <summary>
            /// YCbCrSubsampleRatio422
            /// </summary>
            YCbCrSubsampleRatio422,

            /// <summary>
            /// YCbCrSubsampleRatio420
            /// </summary>
            YCbCrSubsampleRatio420,

            /// <summary>
            /// YCbCrSubsampleRatio440
            /// </summary>
            YCbCrSubsampleRatio440,

            /// <summary>
            /// YCbCrSubsampleRatio411
            /// </summary>
            YCbCrSubsampleRatio411,

            /// <summary>
            /// YCbCrSubsampleRatio410
            /// </summary>
            YCbCrSubsampleRatio410,
        }

        private static ArrayPool<byte> BytePool => ArrayPool<byte>.Shared;

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
        public YCbCrSubsampleRatio Ratio { get; set; }

        /// <summary>
        /// Disposes the <see cref="YCbCrImage" /> returning rented arrays to the pools.
        /// </summary>
        public void Dispose()
        {
            this.YChannel.ReturnPooled();
            this.CbChannel.ReturnPooled();
            this.CrChannel.ReturnPooled();
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
                case YCbCrSubsampleRatio.YCbCrSubsampleRatio422:
                    return y * this.CStride;
                case YCbCrSubsampleRatio.YCbCrSubsampleRatio420:
                    return (y / 2) * this.CStride;
                case YCbCrSubsampleRatio.YCbCrSubsampleRatio440:
                    return (y / 2) * this.CStride;
                case YCbCrSubsampleRatio.YCbCrSubsampleRatio411:
                    return y * this.CStride;
                case YCbCrSubsampleRatio.YCbCrSubsampleRatio410:
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

        /// <summary>
        /// Returns the height and width of the chroma components
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="ratio">The subsampling ratio.</param>
        /// <returns>The <see cref="Size"/> of the chrominance channel</returns>
        internal static Size CalculateChrominanceSize(
            int width,
            int height,
            YCbCrSubsampleRatio ratio)
        {
            switch (ratio)
            {
                case YCbCrSubsampleRatio.YCbCrSubsampleRatio422:
                    return new Size((width + 1) / 2, height);
                case YCbCrSubsampleRatio.YCbCrSubsampleRatio420:
                    return new Size((width + 1) / 2, (height + 1) / 2);
                case YCbCrSubsampleRatio.YCbCrSubsampleRatio440:
                    return new Size(width, (height + 1) / 2);
                case YCbCrSubsampleRatio.YCbCrSubsampleRatio411:
                    return new Size((width + 3) / 4, height);
                case YCbCrSubsampleRatio.YCbCrSubsampleRatio410:
                    return new Size((width + 3) / 4, (height + 1) / 2);
                default:
                    // Default to 4:4:4 subsampling.
                    return new Size(width, height);
            }
        }
    }
}
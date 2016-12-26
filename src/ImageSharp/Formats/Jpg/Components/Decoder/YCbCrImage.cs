﻿// <copyright file="YCbCrImage.cs" company="James Jackson-South">
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
        /// <summary>
        /// Initializes a new instance of the <see cref="YCbCrImage"/> class.
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="ratio">The ratio.</param>
        public YCbCrImage(int width, int height, YCbCrSubsampleRatio ratio, int yOffset, int cOffset)
        {
            int cw, ch;
            YCbCrSize(width, height, ratio, out cw, out ch);
            this.YPixels = CleanPooler<byte>.RentCleanArray(width * height);
            this.CbPixels = CleanPooler<byte>.RentCleanArray(cw * ch);
            this.CrPixels = CleanPooler<byte>.RentCleanArray(cw * ch);
            this.Ratio = ratio;
            this.YOffset = yOffset;
            this.COffset = cOffset;
            this.YStride = width;
            this.CStride = cw;
            this.X = 0;
            this.Y = 0;
        }

        public JpegPixelArea YChannel => new JpegPixelArea(this.YPixels, this.YStride, this.YOffset);

        public JpegPixelArea CbChannel => new JpegPixelArea(this.CbPixels, this.CStride, this.COffset); 

        public JpegPixelArea CrChannel => new JpegPixelArea(this.CrPixels, this.CStride, this.COffset);

        /// <summary>
        /// Prevents a default instance of the <see cref="YCbCrImage"/> class from being created.
        /// </summary>
        private YCbCrImage(int yOffset, int cOffset)
        {
            this.YOffset = yOffset;
            this.COffset = cOffset;
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

        /// <summary>
        /// Gets or sets the luminance components channel.
        /// </summary>
        public byte[] YPixels { get; }

        /// <summary>
        /// Gets or sets the blue chroma components channel.
        /// </summary>
        public byte[] CbPixels { get; }

        /// <summary>
        /// Gets or sets the red chroma components channel.
        /// </summary>
        public byte[] CrPixels { get; }

        /// <summary>
        /// Gets or sets the Y slice index delta between vertically adjacent pixels.
        /// </summary>
        public int YStride { get; }

        /// <summary>
        /// Gets or sets the red and blue chroma slice index delta between vertically adjacent pixels
        /// that map to separate chroma samples.
        /// </summary>
        public int CStride { get; }

        /// <summary>
        /// Gets or sets the index of the first luminance element.
        /// </summary>
        public int YOffset { get; }

        /// <summary>
        /// Gets or sets the index of the first element of red or blue chroma.
        /// </summary>
        public int COffset { get; }

        /// <summary>
        /// Gets or sets the horizontal position.
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// Gets or sets the vertical position.
        /// </summary>
        public int Y { get; set; }
        
        /// <summary>
        /// Gets or sets the subsampling ratio.
        /// </summary>
        public YCbCrSubsampleRatio Ratio { get; set; }

        /// <summary>
        /// Returns the offset of the first luminance component at the given row
        /// </summary>
        /// <param name="y">The row number.</param>
        /// <returns>
        /// The <see cref="int"/>.
        /// </returns>
        public int GetRowYOffset(int y)
        {
            return y * this.YStride;
        }

        /// <summary>
        /// Returns the offset of the first chroma component at the given row
        /// </summary>
        /// <param name="y">The row number.</param>
        /// <returns>
        /// The <see cref="int"/>.
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
        /// Returns the height and width of the chroma components
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="ratio">The subsampling ratio.</param>
        /// <param name="chromaWidth">The chroma width.</param>
        /// <param name="chromaHeight">The chroma height.</param>
        private static void YCbCrSize(int width, int height, YCbCrSubsampleRatio ratio, out int chromaWidth, out int chromaHeight)
        {
            switch (ratio)
            {
                case YCbCrSubsampleRatio.YCbCrSubsampleRatio422:
                    chromaWidth = (width + 1) / 2;
                    chromaHeight = height;
                    break;
                case YCbCrSubsampleRatio.YCbCrSubsampleRatio420:
                    chromaWidth = (width + 1) / 2;
                    chromaHeight = (height + 1) / 2;
                    break;
                case YCbCrSubsampleRatio.YCbCrSubsampleRatio440:
                    chromaWidth = width;
                    chromaHeight = (height + 1) / 2;
                    break;
                case YCbCrSubsampleRatio.YCbCrSubsampleRatio411:
                    chromaWidth = (width + 3) / 4;
                    chromaHeight = height;
                    break;
                case YCbCrSubsampleRatio.YCbCrSubsampleRatio410:
                    chromaWidth = (width + 3) / 4;
                    chromaHeight = (height + 1) / 2;
                    break;
                default:

                    // Default to 4:4:4 subsampling.
                    chromaWidth = width;
                    chromaHeight = height;
                    break;
            }
        }

        public void Dispose()
        {
            CleanPooler<byte>.ReturnArray(this.YPixels);
            CleanPooler<byte>.ReturnArray(this.CrPixels);
            CleanPooler<byte>.ReturnArray(this.CbPixels);
        }
    }
}

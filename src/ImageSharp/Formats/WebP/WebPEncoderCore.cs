// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using System;
using System.Collections.Generic;
using System.IO;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.WebP.BitWriter;
using SixLabors.ImageSharp.Formats.WebP.Lossless;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// Image encoder for writing an image to a stream in the WebP format.
    /// </summary>
    internal sealed class WebPEncoderCore
    {
        /// <summary>
        /// Used for allocating memory during processing operations.
        /// </summary>
        private readonly MemoryAllocator memoryAllocator;

        /// <summary>
        /// The global configuration.
        /// </summary>
        private Configuration configuration;

        /// <summary>
        /// A bit writer for writing lossless webp streams.
        /// </summary>
        private Vp8LBitWriter bitWriter;

        /// <summary>
        /// Initializes a new instance of the <see cref="WebPEncoderCore"/> class.
        /// </summary>
        /// <param name="options">The encoder options.</param>
        /// <param name="memoryAllocator">The memory manager.</param>
        public WebPEncoderCore(IWebPEncoderOptions options, MemoryAllocator memoryAllocator)
        {
            this.memoryAllocator = memoryAllocator;
        }

        /// <summary>
        /// Encodes the image to the specified stream from the <see cref="ImageFrame{TPixel}"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The <see cref="ImageFrame{TPixel}"/> to encode from.</param>
        /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
        public void Encode<TPixel>(Image<TPixel> image, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(image, nameof(image));
            Guard.NotNull(stream, nameof(stream));

            this.configuration = image.GetConfiguration();
            ImageMetadata metadata = image.Metadata;

            int width = image.Width;
            int height = image.Height;
            int initialSize = width * height;
            this.bitWriter = new Vp8LBitWriter(initialSize);

            // Write image size.
            this.WriteImageSize(width, height);

            // Write the non-trivial Alpha flag and lossless version.
            bool hasAlpha = false; // TODO: for the start, this will be always false.
            this.WriteRealAlphaAndVersion(hasAlpha);

            // Encode the main image stream.
            this.EncodeStream(image);
        }

        private void WriteImageSize(int inputImgWidth, int inputImgHeight)
        {
            Guard.MustBeLessThan(inputImgWidth, WebPConstants.MaxDimension, nameof(inputImgWidth));
            Guard.MustBeLessThan(inputImgHeight, WebPConstants.MaxDimension, nameof(inputImgHeight));

            uint width = (uint)inputImgWidth - 1;
            uint height = (uint)inputImgHeight - 1;

            this.bitWriter.PutBits(width, WebPConstants.Vp8LImageSizeBits);
            this.bitWriter.PutBits(height, WebPConstants.Vp8LImageSizeBits);
        }

        private void WriteRealAlphaAndVersion(bool hasAlpha)
        {
            this.bitWriter.PutBits(hasAlpha ? 1U : 0, 1);
            this.bitWriter.PutBits(WebPConstants.Vp8LVersion, WebPConstants.Vp8LVersionBits);
        }

        private void EncodeStream<TPixel>(Image<TPixel> image)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var encoder = new Vp8LEncoder();

            // Analyze image (entropy, num_palettes etc).
            this.EncoderAnalyze(image);
        }

        /// <summary>
        /// Analyzes the image and decides what transforms should be used.
        /// </summary>
        private void EncoderAnalyze<TPixel>(Image<TPixel> image)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // TODO: low effort is always false for now.
            bool lowEffort = false;

            // Check if we only deal with a small number of colors and should use a palette.
            var usePalette = this.AnalyzeAndCreatePalette(image, lowEffort);
        }

        /// <summary>
        /// If number of colors in the image is less than or equal to MAX_PALETTE_SIZE,
        /// creates a palette and returns true, else returns false.
        /// </summary>
        /// <returns>true, if a palette should be used.</returns>
        private bool AnalyzeAndCreatePalette<TPixel>(Image<TPixel> image, bool lowEffort)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int numColors = this.GetColorPalette(image, out uint[] palette);

            if (numColors > WebPConstants.MaxPaletteSize)
            {
                return false;
            }

            // TODO: figure out how the palette needs to be sorted.
            Array.Sort(palette);

            if (!lowEffort && PaletteHasNonMonotonousDeltas(palette, numColors))
            {
                GreedyMinimizeDeltas(palette, numColors);
            }

            return true;
        }

        private int GetColorPalette<TPixel>(Image<TPixel> image, out uint[] palette)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Rgba32 color = default;
            palette = null;
            var colors = new HashSet<TPixel>();
            for (int y = 0; y < image.Height; y++)
            {
                System.Span<TPixel> rowSpan = image.GetPixelRowSpan(y);
                for (int x = 0; x < rowSpan.Length; x++)
                {
                    colors.Add(rowSpan[x]);
                    if (colors.Count > WebPConstants.MaxPaletteSize)
                    {
                        // Exact count is not needed, because a palette will not be used then anyway.
                        return WebPConstants.MaxPaletteSize + 1;
                    }
                }
            }

            // Fill the colors into the palette.
            palette = new uint[colors.Count];
            using HashSet<TPixel>.Enumerator colorEnumerator = colors.GetEnumerator();
            int idx = 0;
            while (colorEnumerator.MoveNext())
            {
                colorEnumerator.Current.ToRgba32(ref color);
                var bgra = new Bgra32(color.R, color.G, color.B, color.A);
                palette[idx++] = bgra.PackedValue;
            }

            return colors.Count;
        }

        /// <summary>
        /// The palette has been sorted by alpha. This function checks if the other components of the palette
        /// have a monotonic development with regards to position in the palette.
        /// If all have monotonic development, there is no benefit to re-organize them greedily. A monotonic development
        /// would be spotted in green-only situations (like lossy alpha) or gray-scale images.
        /// </summary>
        /// <param name="palette">The palette.</param>
        /// <param name="numColors">Number of colors in the palette.</param>
        /// <returns>True, if the palette has no monotonous deltas.</returns>
        private static bool PaletteHasNonMonotonousDeltas(uint[] palette, int numColors)
        {
            uint predict = 0x000000;
            byte signFound = 0x00;
            for (int i = 0; i < numColors; ++i)
            {
                uint diff = LosslessUtils.SubPixels(palette[i], predict);
                byte rd = (byte)((diff >> 16) & 0xff);
                byte gd = (byte)((diff >> 8) & 0xff);
                byte bd = (byte)((diff >> 0) & 0xff);
                if (rd != 0x00)
                {
                    signFound |= (byte)((rd < 0x80) ? 1 : 2);
                }

                if (gd != 0x00)
                {
                    signFound |= (byte)((gd < 0x80) ? 8 : 16);
                }

                if (bd != 0x00)
                {
                    signFound |= (byte)((bd < 0x80) ? 64 : 128);
                }
            }

            return (signFound & (signFound << 1)) != 0;  // two consequent signs.
        }

        /// <summary>
        /// Find greedily always the closest color of the predicted color to minimize
        /// deltas in the palette. This reduces storage needs since the palette is stored with delta encoding.
        /// </summary>
        /// <param name="palette">The palette.</param>
        /// <param name="numColors">The number of colors in the palette.</param>
        private static void GreedyMinimizeDeltas(uint[] palette, int numColors)
        {
            uint predict = 0x00000000;
            for (int i = 0; i < numColors; ++i)
            {
                int bestIdx = i;
                uint bestScore = ~0U;
                for (int k = i; k < numColors; ++k)
                {
                    uint curScore = PaletteColorDistance(palette[k], predict);
                    if (bestScore > curScore)
                    {
                        bestScore = curScore;
                        bestIdx = k;
                    }
                }

                // swap color(palette[bestIdx], palette[i]);
                uint best = palette[bestIdx];
                palette[bestIdx] = palette[i];
                palette[i] = best;
                predict = palette[i];
            }
        }

        /// <summary>
        /// Computes a value that is related to the entropy created by the
        /// palette entry diff.
        ///
        /// Note that the last & 0xff is a no-operation in the next statement, but
        /// removed by most compilers and is here only for regularity of the code.
        /// </summary>
        /// <param name="col1">First color.</param>
        /// <param name="col2">Second color.</param>
        /// <returns>The color distance.</returns>
        private static uint PaletteColorDistance(uint col1, uint col2)
        {
            uint diff = LosslessUtils.SubPixels(col1, col2);
            int moreWeightForRGBThanForAlpha = 9;
            uint score = PaletteComponentDistance((diff >> 0) & 0xff);
            score += PaletteComponentDistance((diff >> 8) & 0xff);
            score += PaletteComponentDistance((diff >> 16) & 0xff);
            score *= moreWeightForRGBThanForAlpha;
            score += PaletteComponentDistance((diff >> 24) & 0xff);

            return score;
        }

        private static uint PaletteComponentDistance(uint v)
        {
            return (v <= 128) ? v : (256 - v);
        }
    }
}

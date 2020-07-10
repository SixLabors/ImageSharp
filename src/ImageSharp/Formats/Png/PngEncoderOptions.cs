// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// The options structure for the <see cref="PngEncoderCore"/>.
    /// </summary>
    internal class PngEncoderOptions : IPngEncoderOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PngEncoderOptions"/> class.
        /// </summary>
        /// <param name="source">The source.</param>
        public PngEncoderOptions(IPngEncoderOptions source)
        {
            this.BitDepth = source.BitDepth;
            this.ColorType = source.ColorType;

            // Specification recommends default filter method None for paletted images and Paeth for others.
            this.FilterMethod = source.FilterMethod ?? (source.ColorType == PngColorType.Palette
                ? PngFilterMethod.None
                : PngFilterMethod.Paeth);
            this.CompressionLevel = source.CompressionLevel;
            this.TextCompressionThreshold = source.TextCompressionThreshold;
            this.Gamma = source.Gamma;
            this.Quantizer = source.Quantizer;
            this.Threshold = source.Threshold;
            this.InterlaceMethod = source.InterlaceMethod;
            this.ChunkFilter = source.ChunkFilter;
            this.IgnoreMetadata = source.IgnoreMetadata;
            this.TransparentColorMode = source.TransparentColorMode;
        }

        /// <inheritdoc/>
        public PngBitDepth? BitDepth { get; set; }

        /// <inheritdoc/>
        public PngColorType? ColorType { get; set; }

        /// <inheritdoc/>
        public PngFilterMethod? FilterMethod { get; }

        /// <inheritdoc/>
        public PngCompressionLevel CompressionLevel { get; } = PngCompressionLevel.DefaultCompression;

        /// <inheritdoc/>
        public int TextCompressionThreshold { get; }

        /// <inheritdoc/>
        public float? Gamma { get; set; }

        /// <inheritdoc/>
        public IQuantizer Quantizer { get; set; }

        /// <inheritdoc/>
        public byte Threshold { get; }

        /// <inheritdoc/>
        public PngInterlaceMode? InterlaceMethod { get; set; }

        /// <inheritdoc/>
        public PngChunkFilter? ChunkFilter { get; set; }

        /// <inheritdoc/>
        public bool IgnoreMetadata { get; set; }

        /// <inheritdoc/>
        public PngTransparentColorMode TransparentColorMode { get; set; }
    }
}

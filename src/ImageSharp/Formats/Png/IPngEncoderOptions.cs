// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// The options available for manipulating the encoder pipeline.
    /// </summary>
    internal interface IPngEncoderOptions
    {
        /// <summary>
        /// Gets the number of bits per sample or per palette index (not per pixel).
        /// Not all values are allowed for all <see cref="ColorType"/> values.
        /// </summary>
        PngBitDepth? BitDepth { get; }

        /// <summary>
        /// Gets the color type.
        /// </summary>
        PngColorType? ColorType { get; }

        /// <summary>
        /// Gets the filter method.
        /// </summary>
        PngFilterMethod? FilterMethod { get; }

        /// <summary>
        /// Gets the compression level 1-9.
        /// <remarks>Defaults to <see cref="PngCompressionLevel.DefaultCompression"/>.</remarks>
        /// </summary>
        PngCompressionLevel CompressionLevel { get; }

        /// <summary>
        /// Gets the threshold of characters in text metadata, when compression should be used.
        /// </summary>
        int TextCompressionThreshold { get; }

        /// <summary>
        /// Gets the gamma value, that will be written the image.
        /// </summary>
        /// <value>The gamma value of the image.</value>
        float? Gamma { get; }

        /// <summary>
        /// Gets the quantizer for reducing the color count.
        /// </summary>
        IQuantizer Quantizer { get; }

        /// <summary>
        /// Gets the transparency threshold.
        /// </summary>
        byte Threshold { get; }

        /// <summary>
        /// Gets a value indicating whether this instance should write an Adam7 interlaced image.
        /// </summary>
        PngInterlaceMode? InterlaceMethod { get; }

        /// <summary>
        /// Gets a value indicating whether the metadata should be ignored when the image is being encoded.
        /// When set to true, all ancillary chunks will be skipped.
        /// </summary>
        bool IgnoreMetadata { get; }

        /// <summary>
        /// Gets the chunk filter method. This allows to filter ancillary chunks.
        /// </summary>
        PngChunkFilter? ChunkFilter { get; }

        /// <summary>
        /// Gets a value indicating whether fully transparent pixels that may contain R, G, B values which are not 0,
        /// should be converted to transparent black, which can yield in better compression in some cases.
        /// </summary>
        PngTransparentColorMode TransparentColorMode { get; }
    }
}

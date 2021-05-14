// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Tiff.Compression;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Provides Tiff specific metadata information for the frame.
    /// </summary>
    internal class TiffFrameMetadata : IDeepCloneable
    {
        private const TiffPlanarConfiguration DefaultPlanarConfiguration = TiffPlanarConfiguration.Chunky;

        private const TiffPredictor DefaultPredictor = TiffPredictor.None;

        /// <summary>
        /// Initializes a new instance of the <see cref="TiffFrameMetadata"/> class.
        /// </summary>
        public TiffFrameMetadata()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TiffFrameMetadata"/> class.
        /// </summary>
        /// <param name="frameTags">The Tiff frame directory tags.</param>
        public TiffFrameMetadata(ExifProfile frameTags) => this.Initialize(frameTags);

        /// <summary>
        /// Gets or sets a general indication of the kind of data contained in this subfile.
        /// </summary>
        public TiffNewSubfileType? SubfileType { get; set; }

        /// <summary>
        /// Gets or sets a general indication of the kind of data contained in this subfile.
        /// </summary>
        public TiffSubfileType? OldSubfileType { get; set; }

        /// <summary>
        /// Gets or sets  the number of bits per component.
        /// </summary>
        public TiffBitsPerSample? BitsPerSample { get; set; }

        /// <summary>
        /// Gets or sets the bits per pixel.
        /// </summary>
        public int BitsPerPixel { get; set; }

        /// <summary>
        /// Gets or sets the compression scheme used on the image data.
        /// </summary>
        public TiffCompression Compression { get; set; }

        /// <summary>
        /// Gets or sets the fax compression options.
        /// </summary>
        public FaxCompressionOptions FaxCompressionOptions { get; set; }

        /// <summary>
        /// Gets or sets the color space of the image data.
        /// </summary>
        public TiffPhotometricInterpretation PhotometricInterpretation { get; set; }

        /// <summary>
        /// Gets or sets the logical order of bits within a byte.
        /// </summary>
        internal TiffFillOrder FillOrder { get; set; }

        /// <summary>
        /// Gets or sets for each strip, the byte offset of that strip.
        /// </summary>
        public Number[] StripOffsets { get; set; }

        /// <summary>
        /// Gets or sets the strip row counts.
        /// </summary>
        public uint[] StripRowCounts { get; set; }

        /// <summary>
        /// Gets or sets the number of components per pixel.
        /// </summary>
        public ushort? SamplesPerPixel { get; set; }

        /// <summary>
        /// Gets or sets the number of rows per strip.
        /// </summary>
        public Number RowsPerStrip { get; set; }

        /// <summary>
        /// Gets or sets for each strip, the number of bytes in the strip after compression.
        /// </summary>
        public Number[] StripByteCounts { get; set; }

        /// <summary>
        /// Gets or sets the resolution of the image in x-direction.
        /// </summary>
        public double? HorizontalResolution { get; set; }

        /// <summary>
        /// Gets or sets the resolution of the image in y-direction.
        /// </summary>
        public double? VerticalResolution { get; set; }

        /// <summary>
        /// Gets or sets how the components of each pixel are stored.
        /// </summary>
        public TiffPlanarConfiguration PlanarConfiguration { get; set; }

        /// <summary>
        /// Gets or sets the unit of measurement for XResolution and YResolution.
        /// </summary>
        public PixelResolutionUnit ResolutionUnit { get; set; }

        /// <summary>
        /// Gets or sets a color map for palette color images.
        /// </summary>
        public ushort[] ColorMap { get; set; }

        /// <summary>
        /// Gets or sets the description of extra components.
        /// </summary>
        public ushort[] ExtraSamples { get; set; }

        /// <summary>
        /// Gets or sets the tile offsets.
        /// </summary>
        public uint[] TileOffsets { get; set; }

        /// <summary>
        /// Gets or sets a mathematical operator that is applied to the image data before an encoding scheme is applied.
        /// </summary>
        public TiffPredictor Predictor { get; set; }

        /// <summary>
        /// Gets or sets the specifies how to interpret each data sample in a pixel.
        /// </summary>
        public TiffSampleFormat[] SampleFormat { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TiffFrameMetadata"/> class with a given ExifProfile.
        /// </summary>
        /// <param name="frameTags">The Tiff frame directory tags.</param>
        public void Initialize(ExifProfile frameTags)
        {
            this.FillOrder = (TiffFillOrder?)frameTags.GetValue(ExifTag.FillOrder)?.Value ?? TiffFillOrder.MostSignificantBitFirst;
            this.Compression = frameTags.GetValue(ExifTag.Compression) != null ? (TiffCompression)frameTags.GetValue(ExifTag.Compression).Value : TiffCompression.None;
            this.FaxCompressionOptions = frameTags.GetValue(ExifTag.T4Options) != null ? (FaxCompressionOptions)frameTags.GetValue(ExifTag.T4Options).Value : FaxCompressionOptions.None;
            this.SubfileType = (TiffNewSubfileType?)frameTags.GetValue(ExifTag.SubfileType)?.Value ?? TiffNewSubfileType.FullImage;
            this.OldSubfileType = (TiffSubfileType?)frameTags.GetValue(ExifTag.OldSubfileType)?.Value;
            this.HorizontalResolution = frameTags.GetValue(ExifTag.XResolution)?.Value.ToDouble();
            this.VerticalResolution = frameTags.GetValue(ExifTag.YResolution)?.Value.ToDouble();
            this.ResolutionUnit = UnitConverter.ExifProfileToResolutionUnit(frameTags);
            this.PlanarConfiguration = (TiffPlanarConfiguration?)frameTags.GetValue(ExifTag.PlanarConfiguration)?.Value ?? DefaultPlanarConfiguration;
            this.ColorMap = frameTags.GetValue(ExifTag.ColorMap)?.Value;
            this.ExtraSamples = frameTags.GetValue(ExifTag.ExtraSamples)?.Value;
            this.Predictor = (TiffPredictor?)frameTags.GetValue(ExifTag.Predictor)?.Value ?? DefaultPredictor;
            this.SampleFormat = frameTags.GetValue(ExifTag.SampleFormat)?.Value?.Select(a => (TiffSampleFormat)a).ToArray();
            this.SamplesPerPixel = frameTags.GetValue(ExifTag.SamplesPerPixel)?.Value;
            this.StripRowCounts = frameTags.GetValue(ExifTag.StripRowCounts)?.Value;
            this.RowsPerStrip = frameTags.GetValue(ExifTag.RowsPerStrip) != null ? frameTags.GetValue(ExifTag.RowsPerStrip).Value : TiffConstants.RowsPerStripInfinity;
            this.TileOffsets = frameTags.GetValue(ExifTag.TileOffsets)?.Value;

            this.PhotometricInterpretation = frameTags.GetValue(ExifTag.PhotometricInterpretation) != null ?
                (TiffPhotometricInterpretation)frameTags.GetValue(ExifTag.PhotometricInterpretation).Value : TiffPhotometricInterpretation.WhiteIsZero;

            // Required Fields for decoding the image.
            this.StripOffsets = frameTags.GetValue(ExifTag.StripOffsets)?.Value;
            this.StripByteCounts = frameTags.GetValue(ExifTag.StripByteCounts)?.Value;

            ushort[] bits = frameTags.GetValue(ExifTag.BitsPerSample)?.Value;
            if (bits == null)
            {
                if (this.PhotometricInterpretation == TiffPhotometricInterpretation.WhiteIsZero || this.PhotometricInterpretation == TiffPhotometricInterpretation.BlackIsZero)
                {
                    this.BitsPerSample = TiffBitsPerSample.Bit1;
                }

                this.BitsPerSample = null;
            }
            else
            {
                this.BitsPerSample = bits.GetBitsPerSample();
            }

            this.BitsPerPixel = this.BitsPerSample.GetValueOrDefault().BitsPerPixel();
        }

        /// <summary>
        /// Verifies that the required fields for decoding an image are present.
        /// If not, a ImageFormatException will be thrown.
        /// </summary>
        public void VerifyRequiredFieldsArePresent()
        {
            if (this.StripOffsets == null)
            {
                TiffThrowHelper.ThrowImageFormatException("StripOffsets are missing and are required for decoding the TIFF image!");
            }

            if (this.StripByteCounts == null)
            {
                TiffThrowHelper.ThrowImageFormatException("StripByteCounts are missing and are required for decoding the TIFF image!");
            }

            if (this.BitsPerSample == null)
            {
                TiffThrowHelper.ThrowNotSupported("The TIFF BitsPerSample entry is missing which is required to decode the image!");
            }
        }

        /// <inheritdoc/>
        public IDeepCloneable DeepClone()
        {
            var clone = new TiffFrameMetadata();

            clone.FillOrder = this.FillOrder;
            clone.Compression = this.Compression;
            clone.FaxCompressionOptions = this.FaxCompressionOptions;
            clone.SubfileType = this.SubfileType ?? TiffNewSubfileType.FullImage;
            clone.OldSubfileType = this.OldSubfileType ?? TiffSubfileType.FullImage;
            clone.HorizontalResolution = this.HorizontalResolution ?? ImageMetadata.DefaultHorizontalResolution;
            clone.VerticalResolution = this.VerticalResolution ?? ImageMetadata.DefaultVerticalResolution;
            clone.ResolutionUnit = this.ResolutionUnit;
            clone.PlanarConfiguration = this.PlanarConfiguration;

            if (this.ColorMap != null)
            {
                clone.ColorMap = new ushort[this.ColorMap.Length];
                this.ColorMap.AsSpan().CopyTo(clone.ColorMap);
            }

            if (this.ExtraSamples != null)
            {
                clone.ExtraSamples = new ushort[this.ExtraSamples.Length];
                this.ExtraSamples.AsSpan().CopyTo(clone.ExtraSamples);
            }

            clone.Predictor = this.Predictor;

            if (this.SampleFormat != null)
            {
                clone.SampleFormat = new TiffSampleFormat[this.SampleFormat.Length];
                this.SampleFormat.AsSpan().CopyTo(clone.SampleFormat);
            }

            clone.SamplesPerPixel = this.SamplesPerPixel;

            if (this.StripRowCounts != null)
            {
                clone.StripRowCounts = new uint[this.StripRowCounts.Length];
                this.StripRowCounts.AsSpan().CopyTo(clone.StripRowCounts);
            }

            clone.RowsPerStrip = this.RowsPerStrip;

            if (this.TileOffsets != null)
            {
                clone.TileOffsets = new uint[this.TileOffsets.Length];
                this.TileOffsets.AsSpan().CopyTo(clone.TileOffsets);
            }

            clone.PhotometricInterpretation = this.PhotometricInterpretation;

            if (this.StripOffsets != null)
            {
                clone.StripOffsets = new Number[this.StripOffsets.Length];
                this.StripOffsets.AsSpan().CopyTo(clone.StripOffsets);
            }

            if (this.StripByteCounts != null)
            {
                clone.StripByteCounts = new Number[this.StripByteCounts.Length];
                this.StripByteCounts.AsSpan().CopyTo(clone.StripByteCounts);
            }

            clone.BitsPerSample = this.BitsPerSample;
            clone.BitsPerPixel = this.BitsPerPixel;

            return clone;
        }
    }
}

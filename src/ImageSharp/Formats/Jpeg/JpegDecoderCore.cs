// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Iptc;
using SixLabors.ImageSharp.Metadata.Profiles.Xmp;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jpeg
{
    /// <summary>
    /// Performs the jpeg decoding operation.
    /// Originally ported from <see href="https://github.com/mozilla/pdf.js/blob/master/src/core/jpg.js"/>
    /// with additional fixes for both performance and common encoding errors.
    /// </summary>
    internal sealed class JpegDecoderCore : IRawJpegData, IImageDecoderInternals
    {
        /// <summary>
        /// The only supported precision
        /// </summary>
        private readonly byte[] supportedPrecisions = { 8, 12 };

        /// <summary>
        /// The buffer used to temporarily store bytes read from the stream.
        /// </summary>
        private readonly byte[] temp = new byte[2 * 16 * 4];

        /// <summary>
        /// The buffer used to read markers from the stream.
        /// </summary>
        private readonly byte[] markerBuffer = new byte[2];

        /// <summary>
        /// Whether the image has an EXIF marker.
        /// </summary>
        private bool hasExif;

        /// <summary>
        /// Contains exif data.
        /// </summary>
        private byte[] exifData;

        /// <summary>
        /// Whether the image has an ICC marker.
        /// </summary>
        private bool hasIcc;

        /// <summary>
        /// Contains ICC data.
        /// </summary>
        private byte[] iccData;

        /// <summary>
        /// Whether the image has a IPTC data.
        /// </summary>
        private bool hasIptc;

        /// <summary>
        /// Contains IPTC data.
        /// </summary>
        private byte[] iptcData;

        /// <summary>
        /// Whether the image has a XMP data.
        /// </summary>
        private bool hasXmp;

        /// <summary>
        /// Contains XMP data.
        /// </summary>
        private byte[] xmpData;

        /// <summary>
        /// Whether the image has a APP14 adobe marker. This is needed to determine image encoded colorspace.
        /// </summary>
        private bool hasAdobeMarker;

        /// <summary>
        /// Contains information about the JFIF marker.
        /// </summary>
        private JFifMarker jFif;

        /// <summary>
        /// Contains information about the Adobe marker.
        /// </summary>
        private AdobeMarker adobe;

        /// <summary>
        /// Scan decoder.
        /// </summary>
        private IJpegScanDecoder scanDecoder;

        /// <summary>
        /// The arithmetic decoding tables.
        /// </summary>
        private List<ArithmeticDecodingTable> arithmeticDecodingTables;

        /// <summary>
        /// The restart interval.
        /// </summary>
        private int? resetInterval;

        /// <summary>
        /// Initializes a new instance of the <see cref="JpegDecoderCore" /> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="options">The options.</param>
        public JpegDecoderCore(Configuration configuration, IJpegDecoderOptions options)
        {
            this.Configuration = configuration ?? Configuration.Default;
            this.IgnoreMetadata = options.IgnoreMetadata;
        }

        /// <inheritdoc />
        public Configuration Configuration { get; }

        /// <summary>
        /// Gets the frame
        /// </summary>
        public JpegFrame Frame { get; private set; }

        /// <inheritdoc/>
        Size IImageDecoderInternals.Dimensions => this.Frame.PixelSize;

        /// <summary>
        /// Gets a value indicating whether the metadata should be ignored when the image is being decoded.
        /// </summary>
        public bool IgnoreMetadata { get; }

        /// <summary>
        /// Gets the <see cref="ImageMetadata"/> decoded by this decoder instance.
        /// </summary>
        public ImageMetadata Metadata { get; private set; }

        /// <inheritdoc/>
        public JpegColorSpace ColorSpace { get; private set; }

        /// <summary>
        /// Gets the components.
        /// </summary>
        public JpegComponent[] Components => this.Frame.Components;

        /// <inheritdoc/>
        JpegComponent[] IRawJpegData.Components => this.Components;

        /// <inheritdoc/>
        public Block8x8F[] QuantizationTables { get; private set; }

        /// <summary>
        /// Finds the next file marker within the byte stream.
        /// </summary>
        /// <param name="stream">The input stream.</param>
        /// <returns>The <see cref="JpegFileMarker"/>.</returns>
        public static JpegFileMarker FindNextFileMarker(BufferedReadStream stream)
        {
            while (true)
            {
                int b = stream.ReadByte();
                if (b == -1)
                {
                    return new JpegFileMarker(JpegConstants.Markers.EOI, stream.Length - 2);
                }

                // Found a marker.
                if (b == JpegConstants.Markers.XFF)
                {
                    while (b == JpegConstants.Markers.XFF)
                    {
                        // Loop here to discard any padding FF bytes on terminating marker.
                        b = stream.ReadByte();
                        if (b == -1)
                        {
                            return new JpegFileMarker(JpegConstants.Markers.EOI, stream.Length - 2);
                        }
                    }

                    // Found a valid marker. Exit loop
                    if (b is not 0 and (< JpegConstants.Markers.RST0 or > JpegConstants.Markers.RST7))
                    {
                        return new JpegFileMarker((byte)(uint)b, stream.Position - 2);
                    }
                }
            }
        }

        /// <inheritdoc/>
        public Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
            => this.Decode<TPixel>(stream, targetSize: null, cancellationToken);

        /// <inheritdoc/>
        public IImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
        {
            this.ParseStream(stream, spectralConverter: null, cancellationToken);
            this.InitExifProfile();
            this.InitIccProfile();
            this.InitIptcProfile();
            this.InitXmpProfile();
            this.InitDerivedMetadataProperties();

            Size pixelSize = this.Frame.PixelSize;
            return new ImageInfo(new PixelTypeInfo(this.Frame.BitsPerPixel), pixelSize.Width, pixelSize.Height, this.Metadata);
        }

        /// <summary>
        /// Decodes and downscales the image from the specified stream if possible.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="stream">Stream.</param>
        /// <param name="targetSize">Target size.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        internal Image<TPixel> DecodeInto<TPixel>(BufferedReadStream stream, Size targetSize, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
            => this.Decode<TPixel>(stream, targetSize, cancellationToken);

        private Image<TPixel> Decode<TPixel>(BufferedReadStream stream, Size? targetSize, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using var spectralConverter = new SpectralConverter<TPixel>(this.Configuration, targetSize);
            this.ParseStream(stream, spectralConverter, cancellationToken);
            this.InitExifProfile();
            this.InitIccProfile();
            this.InitIptcProfile();
            this.InitXmpProfile();
            this.InitDerivedMetadataProperties();

            return new Image<TPixel>(
                this.Configuration,
                spectralConverter.GetPixelBuffer(cancellationToken),
                this.Metadata);
        }

        /// <summary>
        /// Load quantization and/or Huffman tables for subsequent use for jpeg's embedded in tiff's,
        /// so those tables do not need to be duplicated with segmented tiff's (tiff's with multiple strips).
        /// </summary>
        /// <param name="tableBytes">The table bytes.</param>
        /// <param name="scanDecoder">The scan decoder.</param>
        public void LoadTables(byte[] tableBytes, IJpegScanDecoder scanDecoder)
        {
            this.Metadata = new ImageMetadata();
            this.QuantizationTables = new Block8x8F[4];
            this.scanDecoder = scanDecoder;
            if (tableBytes.Length < 4)
            {
                JpegThrowHelper.ThrowInvalidImageContentException("Not enough data to read marker");
            }

            using var ms = new MemoryStream(tableBytes);
            using var stream = new BufferedReadStream(this.Configuration, ms);

            // Check for the Start Of Image marker.
            int bytesRead = stream.Read(this.markerBuffer, 0, 2);
            var fileMarker = new JpegFileMarker(this.markerBuffer[1], 0);
            if (fileMarker.Marker != JpegConstants.Markers.SOI)
            {
                JpegThrowHelper.ThrowInvalidImageContentException("Missing SOI marker.");
            }

            // Read next marker.
            bytesRead = stream.Read(this.markerBuffer, 0, 2);
            fileMarker = new JpegFileMarker(this.markerBuffer[1], (int)stream.Position - 2);

            while (fileMarker.Marker != JpegConstants.Markers.EOI || (fileMarker.Marker == JpegConstants.Markers.EOI && fileMarker.Invalid))
            {
                if (!fileMarker.Invalid)
                {
                    // Get the marker length.
                    int markerContentByteSize = this.ReadUint16(stream) - 2;

                    // Check whether stream actually has enought bytes to read
                    // markerContentByteSize is always positive so we cast
                    // to uint to avoid sign extension
                    if (stream.RemainingBytes < (uint)markerContentByteSize)
                    {
                        JpegThrowHelper.ThrowNotEnoughBytesForMarker(fileMarker.Marker);
                    }

                    switch (fileMarker.Marker)
                    {
                        case JpegConstants.Markers.SOI:
                            break;
                        case JpegConstants.Markers.RST0:
                        case JpegConstants.Markers.RST7:
                            break;
                        case JpegConstants.Markers.DHT:
                            this.ProcessDefineHuffmanTablesMarker(stream, markerContentByteSize);
                            break;
                        case JpegConstants.Markers.DQT:
                            this.ProcessDefineQuantizationTablesMarker(stream, markerContentByteSize);
                            break;
                        case JpegConstants.Markers.DRI:
                            this.ProcessDefineRestartIntervalMarker(stream, markerContentByteSize);
                            break;
                        case JpegConstants.Markers.EOI:
                            return;
                    }
                }

                // Read next marker.
                bytesRead = stream.Read(this.markerBuffer, 0, 2);
                if (bytesRead != 2)
                {
                    JpegThrowHelper.ThrowInvalidImageContentException("Not enough data to read marker");
                }

                fileMarker = new JpegFileMarker(this.markerBuffer[1], 0);
            }
        }

        /// <summary>
        /// Parses the input stream for file markers.
        /// </summary>
        /// <param name="stream">The input stream.</param>
        /// <param name="spectralConverter">The spectral converter to use.</param>
        /// <param name="cancellationToken">The token to monitor cancellation.</param>
        internal void ParseStream(BufferedReadStream stream, SpectralConverter spectralConverter, CancellationToken cancellationToken)
        {
            bool metadataOnly = spectralConverter == null;

            this.scanDecoder ??= new HuffmanScanDecoder(stream, spectralConverter, cancellationToken);

            this.Metadata = new ImageMetadata();

            // Check for the Start Of Image marker.
            stream.Read(this.markerBuffer, 0, 2);
            var fileMarker = new JpegFileMarker(this.markerBuffer[1], 0);
            if (fileMarker.Marker != JpegConstants.Markers.SOI)
            {
                JpegThrowHelper.ThrowInvalidImageContentException("Missing SOI marker.");
            }

            fileMarker = FindNextFileMarker(stream);
            this.QuantizationTables ??= new Block8x8F[4];

            // Break only when we discover a valid EOI marker.
            // https://github.com/SixLabors/ImageSharp/issues/695
            while (fileMarker.Marker != JpegConstants.Markers.EOI)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!fileMarker.Invalid)
                {
                    // Get the marker length.
                    int markerContentByteSize = this.ReadUint16(stream) - 2;

                    // Check whether stream actually has enough bytes to read
                    // markerContentByteSize is always positive so we cast
                    // to uint to avoid sign extension.
                    if (stream.RemainingBytes < (uint)markerContentByteSize)
                    {
                        JpegThrowHelper.ThrowNotEnoughBytesForMarker(fileMarker.Marker);
                    }

                    switch (fileMarker.Marker)
                    {
                        case JpegConstants.Markers.SOF0:
                        case JpegConstants.Markers.SOF1:
                        case JpegConstants.Markers.SOF2:

                            this.ProcessStartOfFrameMarker(stream, markerContentByteSize, fileMarker, ComponentType.Huffman, metadataOnly);
                            break;

                        case JpegConstants.Markers.SOF9:
                        case JpegConstants.Markers.SOF10:
                        case JpegConstants.Markers.SOF13:
                        case JpegConstants.Markers.SOF14:
                            this.scanDecoder = new ArithmeticScanDecoder(stream, spectralConverter, cancellationToken);
                            if (this.resetInterval.HasValue)
                            {
                                this.scanDecoder.ResetInterval = this.resetInterval.Value;
                            }

                            this.ProcessStartOfFrameMarker(stream, markerContentByteSize, fileMarker, ComponentType.Arithmetic, metadataOnly);
                            break;

                        case JpegConstants.Markers.SOF5:
                            JpegThrowHelper.ThrowNotSupportedException("Decoding jpeg files with differential sequential DCT is not supported.");
                            break;

                        case JpegConstants.Markers.SOF6:
                            JpegThrowHelper.ThrowNotSupportedException("Decoding jpeg files with differential progressive DCT is not supported.");
                            break;

                        case JpegConstants.Markers.SOF3:
                        case JpegConstants.Markers.SOF7:
                            JpegThrowHelper.ThrowNotSupportedException("Decoding lossless jpeg files is not supported.");
                            break;

                        case JpegConstants.Markers.SOF11:
                        case JpegConstants.Markers.SOF15:
                            JpegThrowHelper.ThrowNotSupportedException("Decoding jpeg files with lossless arithmetic coding is not supported.");
                            break;

                        case JpegConstants.Markers.SOS:
                            if (!metadataOnly)
                            {
                                this.ProcessStartOfScanMarker(stream, markerContentByteSize);
                                break;
                            }
                            else
                            {
                                // It's highly unlikely that APPn related data will be found after the SOS marker
                                // We should have gathered everything we need by now.
                                return;
                            }

                        case JpegConstants.Markers.DHT:

                            if (metadataOnly)
                            {
                                stream.Skip(markerContentByteSize);
                            }
                            else
                            {
                                this.ProcessDefineHuffmanTablesMarker(stream, markerContentByteSize);
                            }

                            break;

                        case JpegConstants.Markers.DQT:
                            this.ProcessDefineQuantizationTablesMarker(stream, markerContentByteSize);
                            break;

                        case JpegConstants.Markers.DRI:
                            if (metadataOnly)
                            {
                                stream.Skip(markerContentByteSize);
                            }
                            else
                            {
                                this.ProcessDefineRestartIntervalMarker(stream, markerContentByteSize);
                            }

                            break;

                        case JpegConstants.Markers.APP0:
                            this.ProcessApplicationHeaderMarker(stream, markerContentByteSize);
                            break;

                        case JpegConstants.Markers.APP1:
                            this.ProcessApp1Marker(stream, markerContentByteSize);
                            break;

                        case JpegConstants.Markers.APP2:
                            this.ProcessApp2Marker(stream, markerContentByteSize);
                            break;

                        case JpegConstants.Markers.APP3:
                        case JpegConstants.Markers.APP4:
                        case JpegConstants.Markers.APP5:
                        case JpegConstants.Markers.APP6:
                        case JpegConstants.Markers.APP7:
                        case JpegConstants.Markers.APP8:
                        case JpegConstants.Markers.APP9:
                        case JpegConstants.Markers.APP10:
                        case JpegConstants.Markers.APP11:
                        case JpegConstants.Markers.APP12:
                            stream.Skip(markerContentByteSize);
                            break;

                        case JpegConstants.Markers.APP13:
                            this.ProcessApp13Marker(stream, markerContentByteSize);
                            break;

                        case JpegConstants.Markers.APP14:
                            this.ProcessApp14Marker(stream, markerContentByteSize);
                            break;

                        case JpegConstants.Markers.APP15:
                        case JpegConstants.Markers.COM:
                            stream.Skip(markerContentByteSize);
                            break;

                        case JpegConstants.Markers.DAC:
                            if (metadataOnly)
                            {
                                stream.Skip(markerContentByteSize);
                            }
                            else
                            {
                                this.ProcessArithmeticTable(stream, markerContentByteSize);
                            }

                            break;
                    }
                }

                // Read on.
                fileMarker = FindNextFileMarker(stream);
            }

            this.Metadata.GetJpegMetadata().Interleaved = this.Frame.Interleaved;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Frame?.Dispose();

            // Set large fields to null.
            this.Frame = null;
            this.scanDecoder = null;
        }

        /// <summary>
        /// Returns encoded colorspace based on the adobe APP14 marker.
        /// </summary>
        /// <param name="componentCount">Number of components.</param>
        /// <param name="adobeMarker">Parsed adobe APP14 marker.</param>
        /// <returns>The <see cref="JpegColorSpace"/></returns>
        internal static JpegColorSpace DeduceJpegColorSpace(byte componentCount, ref AdobeMarker adobeMarker)
        {
            if (componentCount == 1)
            {
                return JpegColorSpace.Grayscale;
            }

            if (componentCount == 3)
            {
                if (adobeMarker.ColorTransform == JpegConstants.Adobe.ColorTransformUnknown)
                {
                    return JpegColorSpace.RGB;
                }

                return JpegColorSpace.YCbCr;
            }

            if (componentCount == 4)
            {
                if (adobeMarker.ColorTransform == JpegConstants.Adobe.ColorTransformYcck)
                {
                    return JpegColorSpace.Ycck;
                }

                return JpegColorSpace.Cmyk;
            }

            JpegThrowHelper.ThrowNotSupportedComponentCount(componentCount);
            return default;
        }

        /// <summary>
        /// Returns encoded colorspace based on the component count.
        /// </summary>
        /// <param name="componentCount">Number of components.</param>
        /// <returns>The <see cref="JpegColorSpace"/></returns>
        internal static JpegColorSpace DeduceJpegColorSpace(byte componentCount)
        {
            if (componentCount == 1)
            {
                return JpegColorSpace.Grayscale;
            }

            if (componentCount == 3)
            {
                return JpegColorSpace.YCbCr;
            }

            if (componentCount == 4)
            {
                return JpegColorSpace.Cmyk;
            }

            JpegThrowHelper.ThrowNotSupportedComponentCount(componentCount);
            return default;
        }

        /// <summary>
        /// Returns the jpeg color type based on the colorspace and subsampling used.
        /// </summary>
        /// <returns>Jpeg color type.</returns>
        private JpegEncodingColor DeduceJpegColorType()
        {
            switch (this.ColorSpace)
            {
                case JpegColorSpace.Grayscale:
                    return JpegEncodingColor.Luminance;

                case JpegColorSpace.RGB:
                    return JpegEncodingColor.Rgb;

                case JpegColorSpace.YCbCr:
                    if (this.Frame.Components[0].HorizontalSamplingFactor == 1 && this.Frame.Components[0].VerticalSamplingFactor == 1 &&
                        this.Frame.Components[1].HorizontalSamplingFactor == 1 && this.Frame.Components[1].VerticalSamplingFactor == 1 &&
                        this.Frame.Components[2].HorizontalSamplingFactor == 1 && this.Frame.Components[2].VerticalSamplingFactor == 1)
                    {
                        return JpegEncodingColor.YCbCrRatio444;
                    }
                    else if (this.Frame.Components[0].HorizontalSamplingFactor == 2 && this.Frame.Components[0].VerticalSamplingFactor == 1 &&
                        this.Frame.Components[1].HorizontalSamplingFactor == 1 && this.Frame.Components[1].VerticalSamplingFactor == 1 &&
                        this.Frame.Components[2].HorizontalSamplingFactor == 1 && this.Frame.Components[2].VerticalSamplingFactor == 1)
                    {
                        return JpegEncodingColor.YCbCrRatio422;
                    }
                    else if (this.Frame.Components[0].HorizontalSamplingFactor == 2 && this.Frame.Components[0].VerticalSamplingFactor == 2 &&
                        this.Frame.Components[1].HorizontalSamplingFactor == 1 && this.Frame.Components[1].VerticalSamplingFactor == 1 &&
                        this.Frame.Components[2].HorizontalSamplingFactor == 1 && this.Frame.Components[2].VerticalSamplingFactor == 1)
                    {
                        return JpegEncodingColor.YCbCrRatio420;
                    }
                    else if (this.Frame.Components[0].HorizontalSamplingFactor == 4 && this.Frame.Components[0].VerticalSamplingFactor == 1 &&
                             this.Frame.Components[1].HorizontalSamplingFactor == 1 && this.Frame.Components[1].VerticalSamplingFactor == 1 &&
                             this.Frame.Components[2].HorizontalSamplingFactor == 1 && this.Frame.Components[2].VerticalSamplingFactor == 1)
                    {
                        return JpegEncodingColor.YCbCrRatio411;
                    }
                    else if (this.Frame.Components[0].HorizontalSamplingFactor == 4 && this.Frame.Components[0].VerticalSamplingFactor == 2 &&
                             this.Frame.Components[1].HorizontalSamplingFactor == 1 && this.Frame.Components[1].VerticalSamplingFactor == 1 &&
                             this.Frame.Components[2].HorizontalSamplingFactor == 1 && this.Frame.Components[2].VerticalSamplingFactor == 1)
                    {
                        return JpegEncodingColor.YCbCrRatio410;
                    }
                    else
                    {
                        return JpegEncodingColor.YCbCrRatio420;
                    }

                case JpegColorSpace.Cmyk:
                    return JpegEncodingColor.Cmyk;
                case JpegColorSpace.Ycck:
                    return JpegEncodingColor.Ycck;
                default:
                    return JpegEncodingColor.YCbCrRatio420;
            }
        }

        /// <summary>
        /// Initializes the EXIF profile.
        /// </summary>
        private void InitExifProfile()
        {
            if (this.hasExif)
            {
                this.Metadata.ExifProfile = new ExifProfile(this.exifData);
            }
        }

        /// <summary>
        /// Initializes the ICC profile.
        /// </summary>
        private void InitIccProfile()
        {
            if (this.hasIcc)
            {
                var profile = new IccProfile(this.iccData);
                if (profile.CheckIsValid())
                {
                    this.Metadata.IccProfile = profile;
                }
            }
        }

        /// <summary>
        /// Initializes the IPTC profile.
        /// </summary>
        private void InitIptcProfile()
        {
            if (this.hasIptc)
            {
                var profile = new IptcProfile(this.iptcData);
                this.Metadata.IptcProfile = profile;
            }
        }

        /// <summary>
        /// Initializes the XMP profile.
        /// </summary>
        private void InitXmpProfile()
        {
            if (this.hasXmp)
            {
                var profile = new XmpProfile(this.xmpData);
                this.Metadata.XmpProfile = profile;
            }
        }

        /// <summary>
        /// Assigns derived metadata properties to <see cref="Metadata"/>, eg. horizontal and vertical resolution if it has a JFIF header.
        /// </summary>
        private void InitDerivedMetadataProperties()
        {
            if (this.jFif.XDensity > 0 && this.jFif.YDensity > 0)
            {
                this.Metadata.HorizontalResolution = this.jFif.XDensity;
                this.Metadata.VerticalResolution = this.jFif.YDensity;
                this.Metadata.ResolutionUnits = this.jFif.DensityUnits;
            }
            else if (this.hasExif)
            {
                double horizontalValue = this.GetExifResolutionValue(ExifTag.XResolution);
                double verticalValue = this.GetExifResolutionValue(ExifTag.YResolution);

                if (horizontalValue > 0 && verticalValue > 0)
                {
                    this.Metadata.HorizontalResolution = horizontalValue;
                    this.Metadata.VerticalResolution = verticalValue;
                    this.Metadata.ResolutionUnits = UnitConverter.ExifProfileToResolutionUnit(this.Metadata.ExifProfile);
                }
            }
        }

        private double GetExifResolutionValue(ExifTag<Rational> tag)
        {
            IExifValue<Rational> resolution = this.Metadata.ExifProfile.GetValue(tag);

            return resolution is null ? 0 : resolution.Value.ToDouble();
        }

        /// <summary>
        /// Extends the profile with additional data.
        /// </summary>
        /// <param name="profile">The profile data array.</param>
        /// <param name="extension">The array containing addition profile data.</param>
        private void ExtendProfile(ref byte[] profile, byte[] extension)
        {
            int currentLength = profile.Length;

            Array.Resize(ref profile, currentLength + extension.Length);
            Buffer.BlockCopy(extension, 0, profile, currentLength, extension.Length);
        }

        /// <summary>
        /// Processes the application header containing the JFIF identifier plus extra data.
        /// </summary>
        /// <param name="stream">The input stream.</param>
        /// <param name="remaining">The remaining bytes in the segment block.</param>
        private void ProcessApplicationHeaderMarker(BufferedReadStream stream, int remaining)
        {
            // We can only decode JFif identifiers.
            // Some images contain multiple JFIF markers (Issue 1932) so we check to see
            // if it's already been read.
            if (remaining < JFifMarker.Length || (!this.jFif.Equals(default)))
            {
                // Skip the application header length
                stream.Skip(remaining);
                return;
            }

            stream.Read(this.temp, 0, JFifMarker.Length);
            remaining -= JFifMarker.Length;

            JFifMarker.TryParse(this.temp, out this.jFif);

            // TODO: thumbnail
            if (remaining > 0)
            {
                if (stream.Position + remaining >= stream.Length)
                {
                    JpegThrowHelper.ThrowInvalidImageContentException("Bad App0 Marker length.");
                }

                stream.Skip(remaining);
            }
        }

        /// <summary>
        /// Processes the App1 marker retrieving any stored metadata.
        /// </summary>
        /// <param name="stream">The input stream.</param>
        /// <param name="remaining">The remaining bytes in the segment block.</param>
        private void ProcessApp1Marker(BufferedReadStream stream, int remaining)
        {
            const int ExifMarkerLength = 6;
            const int XmpMarkerLength = 29;
            if (remaining < ExifMarkerLength || this.IgnoreMetadata)
            {
                // Skip the application header length.
                stream.Skip(remaining);
                return;
            }

            if (stream.Position + remaining >= stream.Length)
            {
                JpegThrowHelper.ThrowInvalidImageContentException("Bad App1 Marker length.");
            }

            // XMP marker is the longer then the EXIF marker, so first try read the EXIF marker bytes.
            stream.Read(this.temp, 0, ExifMarkerLength);
            remaining -= ExifMarkerLength;

            if (ProfileResolver.IsProfile(this.temp, ProfileResolver.ExifMarker))
            {
                this.hasExif = true;
                byte[] profile = new byte[remaining];
                stream.Read(profile, 0, remaining);

                if (this.exifData is null)
                {
                    this.exifData = profile;
                }
                else
                {
                    // If the EXIF information exceeds 64K, it will be split over multiple APP1 markers.
                    this.ExtendProfile(ref this.exifData, profile);
                }

                remaining = 0;
            }

            if (ProfileResolver.IsProfile(this.temp, ProfileResolver.XmpMarker.Slice(0, ExifMarkerLength)))
            {
                const int remainingXmpMarkerBytes = XmpMarkerLength - ExifMarkerLength;
                if (remaining < remainingXmpMarkerBytes || this.IgnoreMetadata)
                {
                    // Skip the application header length.
                    stream.Skip(remaining);
                    return;
                }

                stream.Read(this.temp, ExifMarkerLength, remainingXmpMarkerBytes);
                remaining -= remainingXmpMarkerBytes;
                if (ProfileResolver.IsProfile(this.temp, ProfileResolver.XmpMarker))
                {
                    this.hasXmp = true;
                    byte[] profile = new byte[remaining];
                    stream.Read(profile, 0, remaining);

                    if (this.xmpData is null)
                    {
                        this.xmpData = profile;
                    }
                    else
                    {
                        // If the XMP information exceeds 64K, it will be split over multiple APP1 markers.
                        this.ExtendProfile(ref this.xmpData, profile);
                    }

                    remaining = 0;
                }
            }

            // Skip over any remaining bytes of this header.
            stream.Skip(remaining);
        }

        /// <summary>
        /// Processes the App2 marker retrieving any stored ICC profile information
        /// </summary>
        /// <param name="stream">The input stream.</param>
        /// <param name="remaining">The remaining bytes in the segment block.</param>
        private void ProcessApp2Marker(BufferedReadStream stream, int remaining)
        {
            // Length is 14 though we only need to check 12.
            const int Icclength = 14;
            if (remaining < Icclength || this.IgnoreMetadata)
            {
                stream.Skip(remaining);
                return;
            }

            byte[] identifier = new byte[Icclength];
            stream.Read(identifier, 0, Icclength);
            remaining -= Icclength; // We have read it by this point

            if (ProfileResolver.IsProfile(identifier, ProfileResolver.IccMarker))
            {
                this.hasIcc = true;
                byte[] profile = new byte[remaining];
                stream.Read(profile, 0, remaining);

                if (this.iccData is null)
                {
                    this.iccData = profile;
                }
                else
                {
                    // If the ICC information exceeds 64K, it will be split over multiple APP2 markers
                    this.ExtendProfile(ref this.iccData, profile);
                }
            }
            else
            {
                // Not an ICC profile we can handle. Skip the remaining bytes so we can carry on and ignore this.
                stream.Skip(remaining);
            }
        }

        /// <summary>
        /// Processes a App13 marker, which contains IPTC data stored with Adobe Photoshop.
        /// The tableBytes of an APP13 segment is formed by an identifier string followed by a sequence of resource data blocks.
        /// </summary>
        /// <param name="stream">The input stream.</param>
        /// <param name="remaining">The remaining bytes in the segment block.</param>
        private void ProcessApp13Marker(BufferedReadStream stream, int remaining)
        {
            if (remaining < ProfileResolver.AdobePhotoshopApp13Marker.Length || this.IgnoreMetadata)
            {
                stream.Skip(remaining);
                return;
            }

            stream.Read(this.temp, 0, ProfileResolver.AdobePhotoshopApp13Marker.Length);
            remaining -= ProfileResolver.AdobePhotoshopApp13Marker.Length;
            if (ProfileResolver.IsProfile(this.temp, ProfileResolver.AdobePhotoshopApp13Marker))
            {
                byte[] resourceBlockData = new byte[remaining];
                stream.Read(resourceBlockData, 0, remaining);
                Span<byte> blockDataSpan = resourceBlockData.AsSpan();

                while (blockDataSpan.Length > 12)
                {
                    if (!ProfileResolver.IsProfile(blockDataSpan.Slice(0, 4), ProfileResolver.AdobeImageResourceBlockMarker))
                    {
                        return;
                    }

                    blockDataSpan = blockDataSpan.Slice(4);
                    Span<byte> imageResourceBlockId = blockDataSpan.Slice(0, 2);
                    if (ProfileResolver.IsProfile(imageResourceBlockId, ProfileResolver.AdobeIptcMarker))
                    {
                        int resourceBlockNameLength = ReadImageResourceNameLength(blockDataSpan);
                        int resourceDataSize = ReadResourceDataLength(blockDataSpan, resourceBlockNameLength);
                        int dataStartIdx = 2 + resourceBlockNameLength + 4;
                        if (resourceDataSize > 0 && blockDataSpan.Length >= dataStartIdx + resourceDataSize)
                        {
                            this.hasIptc = true;
                            this.iptcData = blockDataSpan.Slice(dataStartIdx, resourceDataSize).ToArray();
                            break;
                        }
                    }
                    else
                    {
                        int resourceBlockNameLength = ReadImageResourceNameLength(blockDataSpan);
                        int resourceDataSize = ReadResourceDataLength(blockDataSpan, resourceBlockNameLength);
                        int dataStartIdx = 2 + resourceBlockNameLength + 4;
                        if (blockDataSpan.Length < dataStartIdx + resourceDataSize)
                        {
                            // Not enough data or the resource data size is wrong.
                            break;
                        }

                        blockDataSpan = blockDataSpan.Slice(dataStartIdx + resourceDataSize);
                    }
                }
            }
            else
            {
                // If the profile is unknown skip over the rest of it.
                stream.Skip(remaining);
            }
        }

        /// <summary>
        /// Processes a DAC marker, decoding the arithmetic tables.
        /// </summary>
        /// <param name="stream">The input stream.</param>
        /// <param name="remaining">The remaining bytes in the segment block.</param>
        private void ProcessArithmeticTable(BufferedReadStream stream, int remaining)
        {
            this.arithmeticDecodingTables ??= new List<ArithmeticDecodingTable>(4);

            while (remaining > 0)
            {
                int tableClassAndIdentifier = stream.ReadByte();
                remaining--;
                byte tableClass = (byte)(tableClassAndIdentifier >> 4);
                byte identifier = (byte)(tableClassAndIdentifier & 0xF);

                byte conditioningTableValue = (byte)stream.ReadByte();
                remaining--;

                var arithmeticTable = new ArithmeticDecodingTable(tableClass, identifier);
                arithmeticTable.Configure(conditioningTableValue);

                bool tableEntryReplaced = false;
                for (int i = 0; i < this.arithmeticDecodingTables.Count; i++)
                {
                    ArithmeticDecodingTable item = this.arithmeticDecodingTables[i];
                    if (item.TableClass == arithmeticTable.TableClass && item.Identifier == arithmeticTable.Identifier)
                    {
                        this.arithmeticDecodingTables[i] = arithmeticTable;
                        tableEntryReplaced = true;
                        break;
                    }
                }

                if (!tableEntryReplaced)
                {
                    this.arithmeticDecodingTables.Add(arithmeticTable);
                }
            }
        }

        /// <summary>
        /// Reads the adobe image resource block name: a Pascal string (padded to make size even).
        /// </summary>
        /// <param name="blockDataSpan">The span holding the block resource data.</param>
        /// <returns>The length of the name.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        private static int ReadImageResourceNameLength(Span<byte> blockDataSpan)
        {
            byte nameLength = blockDataSpan[2];
            int nameDataSize = nameLength == 0 ? 2 : nameLength;
            if (nameDataSize % 2 != 0)
            {
                nameDataSize++;
            }

            return nameDataSize;
        }

        /// <summary>
        /// Reads the length of a adobe image resource data block.
        /// </summary>
        /// <param name="blockDataSpan">The span holding the block resource data.</param>
        /// <param name="resourceBlockNameLength">The length of the block name.</param>
        /// <returns>The block length.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        private static int ReadResourceDataLength(Span<byte> blockDataSpan, int resourceBlockNameLength)
            => BinaryPrimitives.ReadInt32BigEndian(blockDataSpan.Slice(2 + resourceBlockNameLength, 4));

        /// <summary>
        /// Processes the application header containing the Adobe identifier
        /// which stores image encoding information for DCT filters.
        /// </summary>
        /// <param name="stream">The input stream.</param>
        /// <param name="remaining">The remaining bytes in the segment block.</param>
        private void ProcessApp14Marker(BufferedReadStream stream, int remaining)
        {
            const int MarkerLength = AdobeMarker.Length;
            if (remaining < MarkerLength)
            {
                // Skip the application header length
                stream.Skip(remaining);
                return;
            }

            stream.Read(this.temp, 0, MarkerLength);
            remaining -= MarkerLength;

            if (AdobeMarker.TryParse(this.temp, out this.adobe))
            {
                this.hasAdobeMarker = true;
            }

            if (remaining > 0)
            {
                stream.Skip(remaining);
            }
        }

        /// <summary>
        /// Processes the Define Quantization Marker and tables. Specified in section B.2.4.1.
        /// </summary>
        /// <param name="stream">The input stream.</param>
        /// <param name="remaining">The remaining bytes in the segment block.</param>
        /// <exception cref="ImageFormatException">
        /// Thrown if the tables do not match the header.
        /// </exception>
        private void ProcessDefineQuantizationTablesMarker(BufferedReadStream stream, int remaining)
        {
            JpegMetadata jpegMetadata = this.Metadata.GetFormatMetadata(JpegFormat.Instance);

            while (remaining > 0)
            {
                // 1 byte: quantization table spec
                // bit 0..3: table index (0..3)
                // bit 4..7: table precision (0 = 8 bit, 1 = 16 bit)
                int quantizationTableSpec = stream.ReadByte();
                int tableIndex = quantizationTableSpec & 15;
                int tablePrecision = quantizationTableSpec >> 4;

                // Validate:
                if (tableIndex > 3)
                {
                    JpegThrowHelper.ThrowBadQuantizationTableIndex(tableIndex);
                }

                remaining--;

                // Decoding single 8x8 table
                ref Block8x8F table = ref this.QuantizationTables[tableIndex];
                switch (tablePrecision)
                {
                    // 8 bit values
                    case 0:
                    {
                        // Validate: 8 bit table needs exactly 64 bytes
                        if (remaining < 64)
                        {
                            JpegThrowHelper.ThrowBadMarker(nameof(JpegConstants.Markers.DQT), remaining);
                        }

                        stream.Read(this.temp, 0, 64);
                        remaining -= 64;

                        // Parsing quantization table & saving it in natural order
                        for (int j = 0; j < 64; j++)
                        {
                            table[ZigZag.ZigZagOrder[j]] = this.temp[j];
                        }

                        break;
                    }

                    // 16 bit values
                    case 1:
                    {
                        // Validate: 16 bit table needs exactly 128 bytes
                        if (remaining < 128)
                        {
                            JpegThrowHelper.ThrowBadMarker(nameof(JpegConstants.Markers.DQT), remaining);
                        }

                        stream.Read(this.temp, 0, 128);
                        remaining -= 128;

                        // Parsing quantization table & saving it in natural order
                        for (int j = 0; j < 64; j++)
                        {
                            table[ZigZag.ZigZagOrder[j]] = (this.temp[2 * j] << 8) | this.temp[(2 * j) + 1];
                        }

                        break;
                    }

                    // Unknown precision - error
                    default:
                    {
                        JpegThrowHelper.ThrowBadQuantizationTablePrecision(tablePrecision);
                        break;
                    }
                }

                // Estimating quality
                switch (tableIndex)
                {
                    // luminance table
                    case 0:
                    {
                        jpegMetadata.LuminanceQuality = Quantization.EstimateLuminanceQuality(ref table);
                        break;
                    }

                    // chrominance table
                    case 1:
                    {
                        jpegMetadata.ChrominanceQuality = Quantization.EstimateChrominanceQuality(ref table);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Processes the Start of Frame marker. Specified in section B.2.2.
        /// </summary>
        /// <param name="stream">The input stream.</param>
        /// <param name="remaining">The remaining bytes in the segment block.</param>
        /// <param name="frameMarker">The current frame marker.</param>
        /// <param name="decodingComponentType">The jpeg decoding component type.</param>
        /// <param name="metadataOnly">Whether to parse metadata only.</param>
        private void ProcessStartOfFrameMarker(BufferedReadStream stream, int remaining, in JpegFileMarker frameMarker, ComponentType decodingComponentType, bool metadataOnly)
        {
            if (this.Frame != null)
            {
                if (metadataOnly)
                {
                    return;
                }

                JpegThrowHelper.ThrowInvalidImageContentException("Multiple SOF markers. Only single frame jpegs supported.");
            }

            // Read initial marker definitions.
            const int length = 6;
            int bytesRead = stream.Read(this.temp, 0, length);
            if (bytesRead != length)
            {
                JpegThrowHelper.ThrowInvalidImageContentException("SOF marker does not contain enough data.");
            }

            // 1 byte: Bits/sample precision.
            byte precision = this.temp[0];

            // Validate: only 8-bit and 12-bit precisions are supported.
            if (Array.IndexOf(this.supportedPrecisions, precision) == -1)
            {
                JpegThrowHelper.ThrowInvalidImageContentException("Only 8-Bit and 12-Bit precision is supported.");
            }

            // 2 byte: Height
            int frameHeight = (this.temp[1] << 8) | this.temp[2];

            // 2 byte: Width
            int frameWidth = (this.temp[3] << 8) | this.temp[4];

            // Validate: width/height > 0 (they are upper-bounded by 2 byte max value so no need to check that).
            if (frameHeight == 0 || frameWidth == 0)
            {
                JpegThrowHelper.ThrowInvalidImageDimensions(frameWidth, frameHeight);
            }

            // 1 byte: Number of components.
            byte componentCount = this.temp[5];

            // Validate: componentCount more than 4 can lead to a buffer overflow during stream
            // reading so we must limit it to 4.
            // We do not support jpeg images with more than 4 components anyway.
            if (componentCount > 4)
            {
                JpegThrowHelper.ThrowNotSupportedComponentCount(componentCount);
            }

            this.Frame = new JpegFrame(frameMarker, precision, frameWidth, frameHeight, componentCount);
            this.Metadata.GetJpegMetadata().Progressive = this.Frame.Progressive;

            remaining -= length;

            // Validate: remaining part must be equal to components * 3
            const int componentBytes = 3;
            if (remaining != componentCount * componentBytes)
            {
                JpegThrowHelper.ThrowBadMarker("SOFn", remaining);
            }

            // components*3 bytes: component data
            stream.Read(this.temp, 0, remaining);

            // No need to pool this. They max out at 4
            this.Frame.ComponentIds = new byte[componentCount];
            this.Frame.ComponentOrder = new byte[componentCount];
            this.Frame.Components = new JpegComponent[componentCount];

            int maxH = 0;
            int maxV = 0;
            int index = 0;
            for (int i = 0; i < this.Frame.Components.Length; i++)
            {
                // 1 byte: component identifier
                byte componentId = this.temp[index];

                // 1 byte: component sampling factors
                byte hv = this.temp[index + 1];
                int h = (hv >> 4) & 15;
                int v = hv & 15;

                // Validate: 1-4 range
                if (Numerics.IsOutOfRange(h, 1, 4))
                {
                    JpegThrowHelper.ThrowBadSampling(h);
                }

                // Validate: 1-4 range
                if (Numerics.IsOutOfRange(v, 1, 4))
                {
                    JpegThrowHelper.ThrowBadSampling(v);
                }

                if (maxH < h)
                {
                    maxH = h;
                }

                if (maxV < v)
                {
                    maxV = v;
                }

                // 1 byte: quantization table destination selector
                byte quantTableIndex = this.temp[index + 2];

                // Validate: 0-3 range
                if (quantTableIndex > 3)
                {
                    JpegThrowHelper.ThrowBadQuantizationTableIndex(quantTableIndex);
                }

                IJpegComponent component = decodingComponentType is ComponentType.Huffman ?
                            new JpegComponent(this.Configuration.MemoryAllocator, this.Frame, componentId, h, v, quantTableIndex, i) :
                            new ArithmeticDecodingComponent(this.Configuration.MemoryAllocator, this.Frame, componentId, h, v, quantTableIndex, i);

                this.Frame.Components[i] = (JpegComponent)component;
                this.Frame.ComponentIds[i] = componentId;

                index += componentBytes;
            }

            this.ColorSpace = this.hasAdobeMarker
                ? DeduceJpegColorSpace(componentCount, ref this.adobe)
                : DeduceJpegColorSpace(componentCount);
            this.Metadata.GetJpegMetadata().ColorType = this.DeduceJpegColorType();

            if (!metadataOnly)
            {
                this.Frame.Init(maxH, maxV);
                this.scanDecoder.InjectFrameData(this.Frame, this);
            }
        }

        /// <summary>
        /// Processes a Define Huffman Table marker, and initializes a huffman
        /// struct from its contents. Specified in section B.2.4.2.
        /// </summary>
        /// <param name="stream">The input stream.</param>
        /// <param name="remaining">The remaining bytes in the segment block.</param>
        private void ProcessDefineHuffmanTablesMarker(BufferedReadStream stream, int remaining)
        {
            const int codeLengthsByteSize = 17;
            const int codeValuesMaxByteSize = 256;
            const int totalBufferSize = codeLengthsByteSize + codeValuesMaxByteSize + HuffmanTable.WorkspaceByteSize;

            var huffmanScanDecoder = this.scanDecoder as HuffmanScanDecoder;
            if (huffmanScanDecoder is null)
            {
                JpegThrowHelper.ThrowInvalidImageContentException("missing huffman table data");
            }

            int length = remaining;
            using (IMemoryOwner<byte> buffer = this.Configuration.MemoryAllocator.Allocate<byte>(totalBufferSize))
            {
                Span<byte> bufferSpan = buffer.GetSpan();
                Span<byte> huffmanLengthsSpan = bufferSpan.Slice(0, codeLengthsByteSize);
                Span<byte> huffmanValuesSpan = bufferSpan.Slice(codeLengthsByteSize, codeValuesMaxByteSize);
                Span<uint> tableWorkspace = MemoryMarshal.Cast<byte, uint>(bufferSpan.Slice(codeLengthsByteSize + codeValuesMaxByteSize));

                for (int i = 2; i < remaining;)
                {
                    byte huffmanTableSpec = (byte)stream.ReadByte();
                    int tableType = huffmanTableSpec >> 4;
                    int tableIndex = huffmanTableSpec & 15;

                    // Types 0..1 DC..AC
                    if (tableType > 1)
                    {
                        JpegThrowHelper.ThrowInvalidImageContentException($"Bad huffman table type: {tableType}.");
                    }

                    // Max tables of each type
                    if (tableIndex > 3)
                    {
                        JpegThrowHelper.ThrowInvalidImageContentException($"Bad huffman table index: {tableIndex}.");
                    }

                    stream.Read(huffmanLengthsSpan, 1, 16);

                    int codeLengthSum = 0;
                    for (int j = 1; j < 17; j++)
                    {
                        codeLengthSum += huffmanLengthsSpan[j];
                    }

                    length -= 17;

                    if (codeLengthSum > 256 || codeLengthSum > length)
                    {
                        JpegThrowHelper.ThrowInvalidImageContentException("Huffman table has excessive length.");
                    }

                    stream.Read(huffmanValuesSpan, 0, codeLengthSum);

                    i += 17 + codeLengthSum;

                    huffmanScanDecoder!.BuildHuffmanTable(
                        tableType,
                        tableIndex,
                        huffmanLengthsSpan,
                        huffmanValuesSpan.Slice(0, codeLengthSum),
                        tableWorkspace);
                }
            }
        }

        /// <summary>
        /// Processes the DRI (Define Restart Interval Marker) Which specifies the interval between RSTn markers,
        /// in macroblocks.
        /// </summary>
        /// <param name="stream">The input stream.</param>
        /// <param name="remaining">The remaining bytes in the segment block.</param>
        private void ProcessDefineRestartIntervalMarker(BufferedReadStream stream, int remaining)
        {
            if (remaining != 2)
            {
                JpegThrowHelper.ThrowBadMarker(nameof(JpegConstants.Markers.DRI), remaining);
            }

            // Save the reset interval, because it can come before or after the SOF marker.
            // If the reset interval comes after the SOF marker, the scanDecoder has not been created.
            this.resetInterval = this.ReadUint16(stream);

            if (this.scanDecoder != null)
            {
                this.scanDecoder.ResetInterval = this.resetInterval.Value;
            }
        }

        /// <summary>
        /// Processes the SOS (Start of scan marker).
        /// </summary>
        private void ProcessStartOfScanMarker(BufferedReadStream stream, int remaining)
        {
            if (this.Frame is null)
            {
                JpegThrowHelper.ThrowInvalidImageContentException("No readable SOFn (Start Of Frame) marker found.");
            }

            // 1 byte: Number of components in scan.
            int selectorsCount = stream.ReadByte();

            // Validate: 0 < count <= totalComponents
            if (selectorsCount == 0 || selectorsCount > this.Frame.ComponentCount)
            {
                // TODO: extract as separate method?
                JpegThrowHelper.ThrowInvalidImageContentException($"Invalid number of components in scan: {selectorsCount}.");
            }

            // Validate: Marker must contain exactly (4 + selectorsCount*2) bytes
            int selectorsBytes = selectorsCount * 2;
            if (remaining != 4 + selectorsBytes)
            {
                JpegThrowHelper.ThrowBadMarker(nameof(JpegConstants.Markers.SOS), remaining);
            }

            // selectorsCount*2 bytes: component index + huffman tables indices
            stream.Read(this.temp, 0, selectorsBytes);

            this.Frame.Interleaved = this.Frame.ComponentCount == selectorsCount;
            for (int i = 0; i < selectorsBytes; i += 2)
            {
                // 1 byte: Component id
                int componentSelectorId = this.temp[i];

                int componentIndex = -1;
                for (int j = 0; j < this.Frame.ComponentIds.Length; j++)
                {
                    byte id = this.Frame.ComponentIds[j];
                    if (componentSelectorId == id)
                    {
                        componentIndex = j;
                        break;
                    }
                }

                // Validate: Must be found among registered components.
                if (componentIndex == -1)
                {
                    // TODO: extract as separate method?
                    JpegThrowHelper.ThrowInvalidImageContentException($"Unknown component id in scan: {componentSelectorId}.");
                }

                this.Frame.ComponentOrder[i / 2] = (byte)componentIndex;

                IJpegComponent component = this.Frame.Components[componentIndex];

                // 1 byte: Huffman table selectors.
                // 4 bits - dc
                // 4 bits - ac
                int tableSpec = this.temp[i + 1];
                int dcTableIndex = tableSpec >> 4;
                int acTableIndex = tableSpec & 15;

                // Validate: both must be < 4
                if (dcTableIndex >= 4 || acTableIndex >= 4)
                {
                    // TODO: extract as separate method?
                    JpegThrowHelper.ThrowInvalidImageContentException($"Invalid huffman table for component:{componentSelectorId}: dc={dcTableIndex}, ac={acTableIndex}");
                }

                component.DcTableId = dcTableIndex;
                component.AcTableId = acTableIndex;
            }

            // 3 bytes: Progressive scan decoding data.
            int bytesRead = stream.Read(this.temp, 0, 3);
            if (bytesRead != 3)
            {
                JpegThrowHelper.ThrowInvalidImageContentException("Not enough data to read progressive scan decoding data");
            }

            int spectralStart = this.temp[0];
            this.scanDecoder.SpectralStart = spectralStart;

            int spectralEnd = this.temp[1];
            this.scanDecoder.SpectralEnd = spectralEnd;

            int successiveApproximation = this.temp[2];
            this.scanDecoder.SuccessiveHigh = successiveApproximation >> 4;
            this.scanDecoder.SuccessiveLow = successiveApproximation & 15;

            if (this.scanDecoder is ArithmeticScanDecoder arithmeticScanDecoder)
            {
                arithmeticScanDecoder.InitDecodingTables(this.arithmeticDecodingTables);
            }

            this.scanDecoder.ParseEntropyCodedData(selectorsCount);
        }

        /// <summary>
        /// Reads a <see cref="ushort"/> from the stream advancing it by two bytes.
        /// </summary>
        /// <param name="stream">The input stream.</param>
        /// <returns>The <see cref="ushort"/></returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        private ushort ReadUint16(BufferedReadStream stream)
        {
            int bytesRead = stream.Read(this.markerBuffer, 0, 2);
            if (bytesRead != 2)
            {
                JpegThrowHelper.ThrowInvalidImageContentException("jpeg stream does not contain enough data, could not read ushort.");
            }

            return BinaryPrimitives.ReadUInt16BigEndian(this.markerBuffer);
        }
    }
}

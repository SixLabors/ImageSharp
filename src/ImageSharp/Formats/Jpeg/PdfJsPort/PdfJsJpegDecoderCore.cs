// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Jpeg.Common;
using SixLabors.ImageSharp.Formats.Jpeg.Common.Decoder;
using SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort.Components;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.MetaData;
using SixLabors.ImageSharp.MetaData.Profiles.Exif;
using SixLabors.ImageSharp.MetaData.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort
{
    /// <summary>
    /// Performs the jpeg decoding operation.
    /// Ported from <see href="https://github.com/mozilla/pdf.js/blob/master/src/core/jpg.js"/> with additional fixes to handle common encoding errors
    /// </summary>
    internal sealed class PdfJsJpegDecoderCore : IRawJpegData
    {
        /// <summary>
        /// The only supported precision
        /// </summary>
        public const int SupportedPrecision = 8;

#pragma warning disable SA1401 // Fields should be private
        /// <summary>
        /// The global configuration
        /// </summary>
        private readonly Configuration configuration;

        /// <summary>
        /// The buffer used to temporarily store bytes read from the stream.
        /// </summary>
        private readonly byte[] temp = new byte[2 * 16 * 4];

        /// <summary>
        /// The buffer used to read markers from the stream.
        /// </summary>
        private readonly byte[] markerBuffer = new byte[2];

        /// <summary>
        /// The DC HUffman tables
        /// </summary>
        private PdfJsHuffmanTables dcHuffmanTables;

        /// <summary>
        /// The AC HUffman tables
        /// </summary>
        private PdfJsHuffmanTables acHuffmanTables;

        /// <summary>
        /// The reset interval determined by RST markers
        /// </summary>
        private ushort resetInterval;

        /// <summary>
        /// Whether the image has a EXIF header
        /// </summary>
        private bool isExif;

        /// <summary>
        /// Contains information about the JFIF marker
        /// </summary>
        private JFifMarker jFif;

        /// <summary>
        /// Contains information about the Adobe marker
        /// </summary>
        private AdobeMarker adobe;

        /// <summary>
        /// Initializes a new instance of the <see cref="PdfJsJpegDecoderCore" /> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="options">The options.</param>
        public PdfJsJpegDecoderCore(Configuration configuration, IJpegDecoderOptions options)
        {
            this.configuration = configuration ?? Configuration.Default;
            this.IgnoreMetadata = options.IgnoreMetadata;
        }

        /// <summary>
        /// Gets the frame
        /// </summary>
        public PdfJsFrame Frame { get; private set; }

        /// <summary>
        /// Gets the image width
        /// </summary>
        public int ImageWidth { get; private set; }

        /// <summary>
        /// Gets the image height
        /// </summary>
        public int ImageHeight { get; private set; }

        /// <summary>
        /// Gets the color depth, in number of bits per pixel.
        /// </summary>
        public int BitsPerPixel => this.ComponentCount * SupportedPrecision;

        /// <summary>
        /// Gets the input stream.
        /// </summary>
        public Stream InputStream { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the metadata should be ignored when the image is being decoded.
        /// </summary>
        public bool IgnoreMetadata { get; }

        /// <summary>
        /// Gets the <see cref="ImageMetaData"/> decoded by this decoder instance.
        /// </summary>
        public ImageMetaData MetaData { get; private set; }

        /// <inheritdoc/>
        public Size ImageSizeInPixels => new Size(this.ImageWidth, this.ImageHeight);

        /// <inheritdoc/>
        public int ComponentCount { get; private set; }

        /// <inheritdoc/>
        public JpegColorSpace ColorSpace { get; private set; }

        /// <inheritdoc/>
        public IEnumerable<IJpegComponent> Components => this.Frame.Components;

        /// <inheritdoc/>
        public Block8x8F[] QuantizationTables { get; private set; }

        /// <summary>
        /// Finds the next file marker within the byte stream.
        /// </summary>
        /// <param name="marker">The buffer to read file markers to</param>
        /// <param name="stream">The input stream</param>
        /// <returns>The <see cref="PdfJsFileMarker"/></returns>
        public static PdfJsFileMarker FindNextFileMarker(byte[] marker, Stream stream)
        {
            int value = stream.Read(marker, 0, 2);

            if (value == 0)
            {
                return new PdfJsFileMarker(PdfJsJpegConstants.Markers.EOI, stream.Length - 2);
            }

            if (marker[0] == PdfJsJpegConstants.Markers.Prefix)
            {
                // According to Section B.1.1.2:
                // "Any marker may optionally be preceded by any number of fill bytes, which are bytes assigned code 0xFF."
                while (marker[1] == PdfJsJpegConstants.Markers.Prefix)
                {
                    int suffix = stream.ReadByte();
                    if (suffix == -1)
                    {
                        return new PdfJsFileMarker(PdfJsJpegConstants.Markers.EOI, stream.Length - 2);
                    }

                    marker[1] = (byte)suffix;
                }

                return new PdfJsFileMarker(BinaryPrimitives.ReadUInt16BigEndian(marker), stream.Position - 2);
            }

            return new PdfJsFileMarker(BinaryPrimitives.ReadUInt16BigEndian(marker), stream.Position - 2, true);
        }

        /// <summary>
        /// Decodes the image from the specified <see cref="Stream"/>  and sets the data to image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="stream">The stream, where the image should be.</param>
        /// <returns>The decoded image.</returns>
        public Image<TPixel> Decode<TPixel>(Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            this.ParseStream(stream);
            return this.PostProcessIntoImage<TPixel>();
        }

        /// <summary>
        /// Reads the raw image information from the specified stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        public IImageInfo Identify(Stream stream)
        {
            this.ParseStream(stream, true);
            this.AssignResolution();
            return new ImageInfo(new PixelTypeInfo(this.BitsPerPixel), this.ImageWidth, this.ImageHeight, this.MetaData);
        }

        /// <summary>
        /// Parses the input stream for file markers
        /// </summary>
        /// <param name="stream">The input stream</param>
        /// <param name="metadataOnly">Whether to decode metadata only.</param>
        public void ParseStream(Stream stream, bool metadataOnly = false)
        {
            this.MetaData = new ImageMetaData();
            this.InputStream = stream;

            // Check for the Start Of Image marker.
            var fileMarker = new PdfJsFileMarker(this.ReadUint16(), 0);
            if (fileMarker.Marker != PdfJsJpegConstants.Markers.SOI)
            {
                throw new ImageFormatException("Missing SOI marker.");
            }

            ushort marker = this.ReadUint16();
            fileMarker = new PdfJsFileMarker(marker, (int)this.InputStream.Position - 2);

            this.QuantizationTables = new Block8x8F[4];

            // this.quantizationTables = new PdfJsQuantizationTables(this.configuration.MemoryManager);
            this.dcHuffmanTables = new PdfJsHuffmanTables();
            this.acHuffmanTables = new PdfJsHuffmanTables();

            while (fileMarker.Marker != PdfJsJpegConstants.Markers.EOI)
            {
                // Get the marker length
                int remaining = this.ReadUint16() - 2;

                switch (fileMarker.Marker)
                {
                    case PdfJsJpegConstants.Markers.APP0:
                        this.ProcessApplicationHeaderMarker(remaining);
                        break;

                    case PdfJsJpegConstants.Markers.APP1:
                        this.ProcessApp1Marker(remaining);
                        break;

                    case PdfJsJpegConstants.Markers.APP2:
                        this.ProcessApp2Marker(remaining);
                        break;
                    case PdfJsJpegConstants.Markers.APP3:
                    case PdfJsJpegConstants.Markers.APP4:
                    case PdfJsJpegConstants.Markers.APP5:
                    case PdfJsJpegConstants.Markers.APP6:
                    case PdfJsJpegConstants.Markers.APP7:
                    case PdfJsJpegConstants.Markers.APP8:
                    case PdfJsJpegConstants.Markers.APP9:
                    case PdfJsJpegConstants.Markers.APP10:
                    case PdfJsJpegConstants.Markers.APP11:
                    case PdfJsJpegConstants.Markers.APP12:
                    case PdfJsJpegConstants.Markers.APP13:
                        this.InputStream.Skip(remaining);
                        break;

                    case PdfJsJpegConstants.Markers.APP14:
                        this.ProcessApp14Marker(remaining);
                        break;

                    case PdfJsJpegConstants.Markers.APP15:
                    case PdfJsJpegConstants.Markers.COM:
                        this.InputStream.Skip(remaining);
                        break;

                    case PdfJsJpegConstants.Markers.DQT:
                        if (metadataOnly)
                        {
                            this.InputStream.Skip(remaining);
                        }
                        else
                        {
                            this.ProcessDefineQuantizationTablesMarker(remaining);
                        }

                        break;

                    case PdfJsJpegConstants.Markers.SOF0:
                    case PdfJsJpegConstants.Markers.SOF1:
                    case PdfJsJpegConstants.Markers.SOF2:
                        this.ProcessStartOfFrameMarker(remaining, fileMarker);
                        if (metadataOnly && !this.jFif.Equals(default))
                        {
                            this.InputStream.Skip(remaining);
                        }

                        break;

                    case PdfJsJpegConstants.Markers.DHT:
                        if (metadataOnly)
                        {
                            this.InputStream.Skip(remaining);
                        }
                        else
                        {
                            this.ProcessDefineHuffmanTablesMarker(remaining);
                        }

                        break;

                    case PdfJsJpegConstants.Markers.DRI:
                        if (metadataOnly)
                        {
                            this.InputStream.Skip(remaining);
                        }
                        else
                        {
                            this.ProcessDefineRestartIntervalMarker(remaining);
                        }

                        break;

                    case PdfJsJpegConstants.Markers.SOS:
                        if (!metadataOnly)
                        {
                            this.ProcessStartOfScanMarker();
                        }

                        break;
                }

                // Read on.
                fileMarker = FindNextFileMarker(this.markerBuffer, this.InputStream);
            }

            this.ImageWidth = this.Frame.SamplesPerLine;
            this.ImageHeight = this.Frame.Scanlines;
            this.ComponentCount = this.Frame.ComponentCount;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Frame?.Dispose();

            // Set large fields to null.
            this.Frame = null;
            this.dcHuffmanTables = null;
            this.acHuffmanTables = null;
        }

        /// <summary>
        /// Returns the correct colorspace based on the image component count
        /// </summary>
        /// <returns>The <see cref="JpegColorSpace"/></returns>
        private JpegColorSpace DeduceJpegColorSpace()
        {
            if (this.ComponentCount == 1)
            {
                return JpegColorSpace.Grayscale;
            }

            if (this.ComponentCount == 3)
            {
                if (this.adobe.Equals(default) || this.adobe.ColorTransform == PdfJsJpegConstants.Markers.Adobe.ColorTransformYCbCr)
                {
                    return JpegColorSpace.YCbCr;
                }
                else if (this.adobe.ColorTransform == PdfJsJpegConstants.Markers.Adobe.ColorTransformUnknown)
                {
                    return JpegColorSpace.RGB;
                }
            }

            if (this.ComponentCount == 4)
            {
                if (this.adobe.ColorTransform == PdfJsJpegConstants.Markers.Adobe.ColorTransformYcck)
                {
                    return JpegColorSpace.Ycck;
                }
                else
                {
                    return JpegColorSpace.Cmyk;
                }
            }

            throw new ImageFormatException($"Unsupported color mode. Max components 4; found {this.ComponentCount}");
        }

        /// <summary>
        /// Assigns the horizontal and vertical resolution to the image if it has a JFIF header or EXIF metadata.
        /// </summary>
        private void AssignResolution()
        {
            if (this.isExif)
            {
                double horizontalValue = this.MetaData.ExifProfile.TryGetValue(ExifTag.XResolution, out ExifValue horizontalTag)
                    ? ((Rational)horizontalTag.Value).ToDouble()
                    : 0;

                double verticalValue = this.MetaData.ExifProfile.TryGetValue(ExifTag.YResolution, out ExifValue verticalTag)
                    ? ((Rational)verticalTag.Value).ToDouble()
                    : 0;

                if (horizontalValue > 0 && verticalValue > 0)
                {
                    this.MetaData.HorizontalResolution = horizontalValue;
                    this.MetaData.VerticalResolution = verticalValue;
                }
            }
            else if (this.jFif.XDensity > 0 && this.jFif.YDensity > 0)
            {
                this.MetaData.HorizontalResolution = this.jFif.XDensity;
                this.MetaData.VerticalResolution = this.jFif.YDensity;
            }
        }

        /// <summary>
        /// Processes the application header containing the JFIF identifier plus extra data.
        /// </summary>
        /// <param name="remaining">The remaining bytes in the segment block.</param>
        private void ProcessApplicationHeaderMarker(int remaining)
        {
            if (remaining < 5)
            {
                // Skip the application header length
                this.InputStream.Skip(remaining);
                return;
            }

            this.InputStream.Read(this.temp, 0, JFifMarker.Length);
            remaining -= JFifMarker.Length;

            JFifMarker.TryParse(this.temp, out this.jFif);

            // TODO: thumbnail
            if (remaining > 0)
            {
                this.InputStream.Skip(remaining);
            }
        }

        /// <summary>
        /// Processes the App1 marker retrieving any stored metadata
        /// </summary>
        /// <param name="remaining">The remaining bytes in the segment block.</param>
        private void ProcessApp1Marker(int remaining)
        {
            if (remaining < 6 || this.IgnoreMetadata)
            {
                // Skip the application header length
                this.InputStream.Skip(remaining);
                return;
            }

            byte[] profile = new byte[remaining];
            this.InputStream.Read(profile, 0, remaining);

            if (ProfileResolver.IsProfile(profile, ProfileResolver.ExifMarker))
            {
                this.isExif = true;
                this.MetaData.ExifProfile = new ExifProfile(profile);
            }
        }

        /// <summary>
        /// Processes the App2 marker retrieving any stored ICC profile information
        /// </summary>
        /// <param name="remaining">The remaining bytes in the segment block.</param>
        private void ProcessApp2Marker(int remaining)
        {
            // Length is 14 though we only need to check 12.
            const int Icclength = 14;
            if (remaining < Icclength || this.IgnoreMetadata)
            {
                this.InputStream.Skip(remaining);
                return;
            }

            byte[] identifier = new byte[Icclength];
            this.InputStream.Read(identifier, 0, Icclength);
            remaining -= Icclength; // We have read it by this point

            if (ProfileResolver.IsProfile(identifier, ProfileResolver.IccMarker))
            {
                byte[] profile = new byte[remaining];
                this.InputStream.Read(profile, 0, remaining);

                if (this.MetaData.IccProfile == null)
                {
                    this.MetaData.IccProfile = new IccProfile(profile);
                }
                else
                {
                    this.MetaData.IccProfile.Extend(profile);
                }
            }
            else
            {
                // Not an ICC profile we can handle. Skip the remaining bytes so we can carry on and ignore this.
                this.InputStream.Skip(remaining);
            }
        }

        /// <summary>
        /// Processes the application header containing the Adobe identifier
        /// which stores image encoding information for DCT filters.
        /// </summary>
        /// <param name="remaining">The remaining bytes in the segment block.</param>
        private void ProcessApp14Marker(int remaining)
        {
            const int MarkerLength = AdobeMarker.Length;
            if (remaining < MarkerLength)
            {
                // Skip the application header length
                this.InputStream.Skip(remaining);
                return;
            }

            this.InputStream.Read(this.temp, 0, MarkerLength);
            remaining -= MarkerLength;

            AdobeMarker.TryParse(this.temp, out this.adobe);

            if (remaining > 0)
            {
                this.InputStream.Skip(remaining);
            }
        }

        /// <summary>
        /// Processes the Define Quantization Marker and tables. Specified in section B.2.4.1.
        /// </summary>
        /// <param name="remaining">The remaining bytes in the segment block.</param>
        /// <exception cref="ImageFormatException">
        /// Thrown if the tables do not match the header
        /// </exception>
        private void ProcessDefineQuantizationTablesMarker(int remaining)
        {
            while (remaining > 0)
            {
                bool done = false;
                remaining--;
                int quantizationTableSpec = this.InputStream.ReadByte();

                switch (quantizationTableSpec >> 4)
                {
                    case 0:
                        {
                            // 8 bit values
                            if (remaining < 64)
                            {
                                done = true;
                                break;
                            }

                            this.InputStream.Read(this.temp, 0, 64);
                            remaining -= 64;

                            ref Block8x8F table = ref this.QuantizationTables[quantizationTableSpec & 15];
                            for (int j = 0; j < 64; j++)
                            {
                                table[j] = this.temp[j];
                            }
                        }

                        break;
                    case 1:
                        {
                            // 16 bit values
                            if (remaining < 128)
                            {
                                done = true;
                                break;
                            }

                            this.InputStream.Read(this.temp, 0, 128);
                            remaining -= 128;

                            ref Block8x8F table = ref this.QuantizationTables[quantizationTableSpec & 15];
                            for (int j = 0; j < 64; j++)
                            {
                                table[j] = (this.temp[2 * j] << 8) | this.temp[(2 * j) + 1];
                            }
                        }

                        break;
                    default:
                        throw new ImageFormatException("Bad Tq index value");
                }

                if (done)
                {
                    break;
                }
            }

            if (remaining != 0)
            {
                throw new ImageFormatException("DQT has wrong length");
            }
        }

        /// <summary>
        /// Processes the Start of Frame marker.  Specified in section B.2.2.
        /// </summary>
        /// <param name="remaining">The remaining bytes in the segment block.</param>
        /// <param name="frameMarker">The current frame marker.</param>
        private void ProcessStartOfFrameMarker(int remaining, PdfJsFileMarker frameMarker)
        {
            if (this.Frame != null)
            {
                throw new ImageFormatException("Multiple SOF markers. Only single frame jpegs supported.");
            }

            this.InputStream.Read(this.temp, 0, remaining);

            // We only support 8-bit precision.
            if (this.temp[0] != SupportedPrecision)
            {
                throw new ImageFormatException("Only 8-Bit precision supported.");
            }

            this.Frame = new PdfJsFrame
            {
                Extended = frameMarker.Marker == PdfJsJpegConstants.Markers.SOF1,
                Progressive = frameMarker.Marker == PdfJsJpegConstants.Markers.SOF2,
                Precision = this.temp[0],
                Scanlines = (short)((this.temp[1] << 8) | this.temp[2]),
                SamplesPerLine = (short)((this.temp[3] << 8) | this.temp[4]),
                ComponentCount = this.temp[5]
            };

            int maxH = 0;
            int maxV = 0;
            int index = 6;

            // No need to pool this. They max out at 4
            this.Frame.ComponentIds = new byte[this.Frame.ComponentCount];
            this.Frame.Components = new PdfJsFrameComponent[this.Frame.ComponentCount];

            for (int i = 0; i < this.Frame.Components.Length; i++)
            {
                byte hv = this.temp[index + 1];
                int h = hv >> 4;
                int v = hv & 15;

                if (maxH < h)
                {
                    maxH = h;
                }

                if (maxV < v)
                {
                    maxV = v;
                }

                var component = new PdfJsFrameComponent(this.configuration.MemoryManager, this.Frame, this.temp[index], h, v, this.temp[index + 2], i);

                this.Frame.Components[i] = component;
                this.Frame.ComponentIds[i] = component.Id;

                index += 3;
            }

            this.Frame.MaxHorizontalFactor = maxH;
            this.Frame.MaxVerticalFactor = maxV;
            this.Frame.InitComponents();
        }

        /// <summary>
        /// Processes a Define Huffman Table marker, and initializes a huffman
        /// struct from its contents. Specified in section B.2.4.2.
        /// </summary>
        /// <param name="remaining">The remaining bytes in the segment block.</param>
        private void ProcessDefineHuffmanTablesMarker(int remaining)
        {
            if (remaining < 17)
            {
                throw new ImageFormatException($"DHT has wrong length: {remaining}");
            }

            using (IManagedByteBuffer huffmanData = this.configuration.MemoryManager.AllocateCleanManagedByteBuffer(256))
            {
                ref byte huffmanDataRef = ref MemoryMarshal.GetReference(huffmanData.Span);
                for (int i = 2; i < remaining;)
                {
                    byte huffmanTableSpec = (byte)this.InputStream.ReadByte();
                    this.InputStream.Read(huffmanData.Array, 0, 16);

                    using (IManagedByteBuffer codeLengths = this.configuration.MemoryManager.AllocateCleanManagedByteBuffer(17))
                    {
                        ref byte codeLengthsRef = ref MemoryMarshal.GetReference(codeLengths.Span);
                        int codeLengthSum = 0;

                        for (int j = 1; j < 17; j++)
                        {
                            codeLengthSum += Unsafe.Add(ref codeLengthsRef, j) = Unsafe.Add(ref huffmanDataRef, j - 1);
                        }

                        using (IManagedByteBuffer huffmanValues = this.configuration.MemoryManager.AllocateCleanManagedByteBuffer(256))
                        {
                            this.InputStream.Read(huffmanValues.Array, 0, codeLengthSum);

                            i += 17 + codeLengthSum;

                            this.BuildHuffmanTable(
                                huffmanTableSpec >> 4 == 0 ? this.dcHuffmanTables : this.acHuffmanTables,
                                huffmanTableSpec & 15,
                                codeLengths.Span,
                                huffmanValues.Span);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Processes the DRI (Define Restart Interval Marker) Which specifies the interval between RSTn markers, in
        /// macroblocks
        /// </summary>
        /// <param name="remaining">The remaining bytes in the segment block.</param>
        private void ProcessDefineRestartIntervalMarker(int remaining)
        {
            if (remaining != 2)
            {
                throw new ImageFormatException($"DRI has wrong length: {remaining}");
            }

            this.resetInterval = this.ReadUint16();
        }

        /// <summary>
        /// Processes the SOS (Start of scan marker).
        /// </summary>
        private void ProcessStartOfScanMarker()
        {
            int selectorsCount = this.InputStream.ReadByte();
            int componentIndex = -1;
            for (int i = 0; i < selectorsCount; i++)
            {
                componentIndex = -1;
                int selector = this.InputStream.ReadByte();

                for (int j = 0; j < this.Frame.ComponentIds.Length; j++)
                {
                    byte id = this.Frame.ComponentIds[j];
                    if (selector == id)
                    {
                        componentIndex = j;
                    }
                }

                if (componentIndex < 0)
                {
                    throw new ImageFormatException("Unknown component selector");
                }

                ref PdfJsFrameComponent component = ref this.Frame.Components[componentIndex];
                int tableSpec = this.InputStream.ReadByte();
                component.DCHuffmanTableId = tableSpec >> 4;
                component.ACHuffmanTableId = tableSpec & 15;
            }

            this.InputStream.Read(this.temp, 0, 3);

            int spectralStart = this.temp[0];
            int spectralEnd = this.temp[1];
            int successiveApproximation = this.temp[2];
            var scanDecoder = default(PdfJsScanDecoder);

            scanDecoder.DecodeScan(
                 this.Frame,
                 this.InputStream,
                 this.dcHuffmanTables,
                 this.acHuffmanTables,
                 this.Frame.Components,
                 componentIndex,
                 selectorsCount,
                 this.resetInterval,
                 spectralStart,
                 spectralEnd,
                 successiveApproximation >> 4,
                 successiveApproximation & 15);
        }

        /// <summary>
        /// Builds the huffman tables
        /// </summary>
        /// <param name="tables">The tables</param>
        /// <param name="index">The table index</param>
        /// <param name="codeLengths">The codelengths</param>
        /// <param name="values">The values</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void BuildHuffmanTable(PdfJsHuffmanTables tables, int index, ReadOnlySpan<byte> codeLengths, ReadOnlySpan<byte> values)
        {
            tables[index] = new PdfJsHuffmanTable(this.configuration.MemoryManager, codeLengths, values);
        }

        /// <summary>
        /// Reads a <see cref="ushort"/> from the stream advancing it by two bytes
        /// </summary>
        /// <returns>The <see cref="ushort"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort ReadUint16()
        {
            this.InputStream.Read(this.markerBuffer, 0, 2);
            return BinaryPrimitives.ReadUInt16BigEndian(this.markerBuffer);
        }

        private Image<TPixel> PostProcessIntoImage<TPixel>()
            where TPixel : struct, IPixel<TPixel>
        {
            this.ColorSpace = this.DeduceJpegColorSpace();
            this.AssignResolution();
            using (var postProcessor = new JpegImagePostProcessor(this.configuration.MemoryManager, this))
            {
                var image = new Image<TPixel>(this.configuration, this.ImageWidth, this.ImageHeight, this.MetaData);
                postProcessor.PostProcess(image.Frames.RootFrame);
                return image;
            }
        }
    }
}
// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort.Components;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.MetaData;
using SixLabors.ImageSharp.MetaData.Profiles.Exif;
using SixLabors.ImageSharp.MetaData.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort
{
    /// <summary>
    /// Performs the jpeg decoding operation.
    /// Ported from <see href="https://github.com/mozilla/pdf.js/blob/master/src/core/jpg.js"/> with additional fixes to handle common encoding errors
    /// </summary>
    internal sealed class PdfJsJpegDecoderCore : IDisposable
    {
#pragma warning disable SA1401 // Fields should be private
        /// <summary>
        /// The global configuration
        /// </summary>
        private readonly Configuration configuration;

        /// <summary>
        /// Gets the temporary buffer used to store bytes read from the stream.
        /// </summary>
        private readonly byte[] temp = new byte[2 * 16 * 4];

        private readonly byte[] markerBuffer = new byte[2];

        private PdfJsQuantizationTables quantizationTables;

        private PdfJsHuffmanTables dcHuffmanTables;

        private PdfJsHuffmanTables acHuffmanTables;

        private PdfJsComponentBlocks components;

        private PdfJsJpegPixelArea pixelArea;

        private ushort resetInterval;

        /// <summary>
        /// Whether the image has a EXIF header
        /// </summary>
        private bool isExif;

        /// <summary>
        /// Contains information about the JFIF marker
        /// </summary>
        private PdfJsJFif jFif;

        /// <summary>
        /// Contains information about the Adobe marker
        /// </summary>
        private PdfJsAdobe adobe;

        /// <summary>
        /// Initializes static members of the <see cref="PdfJsJpegDecoderCore"/> class.
        /// </summary>
        static PdfJsJpegDecoderCore()
        {
            PdfJsYCbCrToRgbTables.Create();
        }

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
        /// Gets the number of components
        /// </summary>
        public int NumberOfComponents { get; private set; }

        /// <summary>
        /// Gets the input stream.
        /// </summary>
        public Stream InputStream { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the metadata should be ignored when the image is being decoded.
        /// </summary>
        public bool IgnoreMetadata { get; }

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
                return new PdfJsFileMarker(PdfJsJpegConstants.Markers.EOI, (int)stream.Length - 2);
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
                        return new PdfJsFileMarker(PdfJsJpegConstants.Markers.EOI, (int)stream.Length - 2);
                    }

                    marker[1] = (byte)value;
                }

                return new PdfJsFileMarker((ushort)((marker[0] << 8) | marker[1]), (int)(stream.Position - 2));
            }

            return new PdfJsFileMarker((ushort)((marker[0] << 8) | marker[1]), (int)(stream.Position - 2), true);
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
            ImageMetaData metadata = this.ParseStream(stream);

            this.QuantizeAndInverseAllComponents();

            var image = new Image<TPixel>(this.configuration, this.ImageWidth, this.ImageHeight, metadata);
            this.FillPixelData(image.Frames.RootFrame);
            this.AssignResolution(image);
            return image;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Frame?.Dispose();
            this.components?.Dispose();
            this.quantizationTables?.Dispose();
            this.dcHuffmanTables?.Dispose();
            this.acHuffmanTables?.Dispose();
            this.pixelArea.Dispose();

            // Set large fields to null.
            this.Frame = null;
            this.components = null;
            this.quantizationTables = null;
            this.dcHuffmanTables = null;
            this.acHuffmanTables = null;
        }

        internal ImageMetaData ParseStream(Stream stream)
        {
            this.InputStream = stream;

            var metadata = new ImageMetaData();
            this.ParseStream(metadata, false);
            return metadata;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetBlockBufferOffset(ref PdfJsComponent component, int row, int col)
        {
            return 64 * (((component.BlocksPerLine + 1) * row) + col);
        }

        /// <summary>
        /// Parses the input stream for file markers
        /// </summary>
        /// <param name="metaData">Contains the metadata for an image</param>
        /// <param name="metadataOnly">Whether to decode metadata only.</param>
        private void ParseStream(ImageMetaData metaData, bool metadataOnly)
        {
            // TODO: metadata only logic
            // Check for the Start Of Image marker.
            var fileMarker = new PdfJsFileMarker(this.ReadUint16(), 0);
            if (fileMarker.Marker != PdfJsJpegConstants.Markers.SOI)
            {
                throw new ImageFormatException("Missing SOI marker.");
            }

            ushort marker = this.ReadUint16();
            fileMarker = new PdfJsFileMarker(marker, (int)this.InputStream.Position - 2);

            this.quantizationTables = new PdfJsQuantizationTables();
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
                        this.ProcessApp1Marker(remaining, metaData);
                        break;

                    case PdfJsJpegConstants.Markers.APP2:
                        this.ProcessApp2Marker(remaining, metaData);
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
                        this.ProcessDefineQuantizationTablesMarker(remaining);
                        break;

                    case PdfJsJpegConstants.Markers.SOF0:
                    case PdfJsJpegConstants.Markers.SOF1:
                    case PdfJsJpegConstants.Markers.SOF2:
                        this.ProcessStartOfFrameMarker(remaining, fileMarker);
                        break;

                    case PdfJsJpegConstants.Markers.DHT:
                        this.ProcessDefineHuffmanTablesMarker(remaining);
                        break;

                    case PdfJsJpegConstants.Markers.DRI:
                        this.ProcessDefineRestartIntervalMarker(remaining);
                        break;

                    case PdfJsJpegConstants.Markers.SOS:
                        this.ProcessStartOfScanMarker();
                        break;
                }

                // Read on.
                fileMarker = FindNextFileMarker(this.markerBuffer, this.InputStream);
            }

            this.ImageWidth = this.Frame.SamplesPerLine;
            this.ImageHeight = this.Frame.Scanlines;
            this.components = new PdfJsComponentBlocks { Components = new PdfJsComponent[this.Frame.ComponentCount] };

            for (int i = 0; i < this.components.Components.Length; i++)
            {
                PdfJsFrameComponent frameComponent = this.Frame.Components[i];
                var component = new PdfJsComponent
                {
                    Scale = new System.Numerics.Vector2(
                        frameComponent.HorizontalSamplingFactor / (float)this.Frame.MaxHorizontalFactor,
                        frameComponent.VerticalSamplingFactor / (float)this.Frame.MaxVerticalFactor),
                    BlocksPerLine = frameComponent.WidthInBlocks,
                    BlocksPerColumn = frameComponent.HeightInBlocks
                };

                // this.QuantizeAndInverseComponentData(ref component, frameComponent);
                this.components.Components[i] = component;
            }

            this.NumberOfComponents = this.components.Components.Length;
        }

        internal void QuantizeAndInverseAllComponents()
        {
            for (int i = 0; i < this.components.Components.Length; i++)
            {
                PdfJsFrameComponent frameComponent = this.Frame.Components[i];
                PdfJsComponent component = this.components.Components[i];

                this.QuantizeAndInverseComponentData(component, frameComponent);
            }
        }

        /// <summary>
        /// Fills the given image with the color data
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The image</param>
        private void FillPixelData<TPixel>(ImageFrame<TPixel> image)
            where TPixel : struct, IPixel<TPixel>
        {
            if (this.NumberOfComponents > 4)
            {
                throw new ImageFormatException($"Unsupported color mode. Max components 4; found {this.NumberOfComponents}");
            }

            this.pixelArea = new PdfJsJpegPixelArea(image.Width, image.Height, this.NumberOfComponents);
            this.pixelArea.LinearizeBlockData(this.components, image.Width, image.Height);

            if (this.NumberOfComponents == 1)
            {
                this.FillGrayScaleImage(image);
                return;
            }

            if (this.NumberOfComponents == 3)
            {
                if (this.adobe.Equals(default(PdfJsAdobe)) || this.adobe.ColorTransform == PdfJsJpegConstants.Markers.Adobe.ColorTransformYCbCr)
                {
                    this.FillYCbCrImage(image);
                }
                else if (this.adobe.ColorTransform == PdfJsJpegConstants.Markers.Adobe.ColorTransformUnknown)
                {
                    this.FillRgbImage(image);
                }
            }

            if (this.NumberOfComponents == 4)
            {
                if (this.adobe.ColorTransform == PdfJsJpegConstants.Markers.Adobe.ColorTransformYcck)
                {
                    this.FillYcckImage(image);
                }
                else
                {
                    this.FillCmykImage(image);
                }
            }
        }

        /// <summary>
        /// Assigns the horizontal and vertical resolution to the image if it has a JFIF header or EXIF metadata.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The image to assign the resolution to.</param>
        private void AssignResolution<TPixel>(Image<TPixel> image)
            where TPixel : struct, IPixel<TPixel>
        {
            if (this.isExif)
            {
                ExifValue horizontal = image.MetaData.ExifProfile.GetValue(ExifTag.XResolution);
                ExifValue vertical = image.MetaData.ExifProfile.GetValue(ExifTag.YResolution);
                double horizontalValue = horizontal != null ? ((Rational)horizontal.Value).ToDouble() : 0;
                double verticalValue = vertical != null ? ((Rational)vertical.Value).ToDouble() : 0;

                if (horizontalValue > 0 && verticalValue > 0)
                {
                    image.MetaData.HorizontalResolution = horizontalValue;
                    image.MetaData.VerticalResolution = verticalValue;
                }
            }
            else if (this.jFif.XDensity > 0 && this.jFif.YDensity > 0)
            {
                image.MetaData.HorizontalResolution = this.jFif.XDensity;
                image.MetaData.VerticalResolution = this.jFif.YDensity;
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

            this.InputStream.Read(this.temp, 0, 13);
            remaining -= 13;

            bool isJfif = this.temp[0] == PdfJsJpegConstants.Markers.JFif.J &&
                          this.temp[1] == PdfJsJpegConstants.Markers.JFif.F &&
                          this.temp[2] == PdfJsJpegConstants.Markers.JFif.I &&
                          this.temp[3] == PdfJsJpegConstants.Markers.JFif.F &&
                          this.temp[4] == PdfJsJpegConstants.Markers.JFif.Null;

            if (isJfif)
            {
                this.jFif = new PdfJsJFif
                {
                    MajorVersion = this.temp[5],
                    MinorVersion = this.temp[6],
                    DensityUnits = this.temp[7],
                    XDensity = (short)((this.temp[8] << 8) | this.temp[9]),
                    YDensity = (short)((this.temp[10] << 8) | this.temp[11])
                };
            }

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
        /// <param name="metadata">The image.</param>
        private void ProcessApp1Marker(int remaining, ImageMetaData metadata)
        {
            if (remaining < 6 || this.IgnoreMetadata)
            {
                // Skip the application header length
                this.InputStream.Skip(remaining);
                return;
            }

            byte[] profile = new byte[remaining];
            this.InputStream.Read(profile, 0, remaining);

            if (profile[0] == PdfJsJpegConstants.Markers.Exif.E &&
                profile[1] == PdfJsJpegConstants.Markers.Exif.X &&
                profile[2] == PdfJsJpegConstants.Markers.Exif.I &&
                profile[3] == PdfJsJpegConstants.Markers.Exif.F &&
                profile[4] == PdfJsJpegConstants.Markers.Exif.Null &&
                profile[5] == PdfJsJpegConstants.Markers.Exif.Null)
            {
                this.isExif = true;
                metadata.ExifProfile = new ExifProfile(profile);
            }
        }

        /// <summary>
        /// Processes the App2 marker retrieving any stored ICC profile information
        /// </summary>
        /// <param name="remaining">The remaining bytes in the segment block.</param>
        /// <param name="metadata">The image.</param>
        private void ProcessApp2Marker(int remaining, ImageMetaData metadata)
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

            if (identifier[0] == PdfJsJpegConstants.Markers.ICC.I &&
                identifier[1] == PdfJsJpegConstants.Markers.ICC.C &&
                identifier[2] == PdfJsJpegConstants.Markers.ICC.C &&
                identifier[3] == PdfJsJpegConstants.Markers.ICC.UnderScore &&
                identifier[4] == PdfJsJpegConstants.Markers.ICC.P &&
                identifier[5] == PdfJsJpegConstants.Markers.ICC.R &&
                identifier[6] == PdfJsJpegConstants.Markers.ICC.O &&
                identifier[7] == PdfJsJpegConstants.Markers.ICC.F &&
                identifier[8] == PdfJsJpegConstants.Markers.ICC.I &&
                identifier[9] == PdfJsJpegConstants.Markers.ICC.L &&
                identifier[10] == PdfJsJpegConstants.Markers.ICC.E &&
                identifier[11] == PdfJsJpegConstants.Markers.ICC.Null)
            {
                byte[] profile = new byte[remaining];
                this.InputStream.Read(profile, 0, remaining);

                if (metadata.IccProfile == null)
                {
                    metadata.IccProfile = new IccProfile(profile);
                }
                else
                {
                    metadata.IccProfile.Extend(profile);
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
            if (remaining < 12)
            {
                // Skip the application header length
                this.InputStream.Skip(remaining);
                return;
            }

            this.InputStream.Read(this.temp, 0, 12);
            remaining -= 12;

            bool isAdobe = this.temp[0] == PdfJsJpegConstants.Markers.Adobe.A &&
                           this.temp[1] == PdfJsJpegConstants.Markers.Adobe.D &&
                           this.temp[2] == PdfJsJpegConstants.Markers.Adobe.O &&
                           this.temp[3] == PdfJsJpegConstants.Markers.Adobe.B &&
                           this.temp[4] == PdfJsJpegConstants.Markers.Adobe.E;

            if (isAdobe)
            {
                this.adobe = new PdfJsAdobe
                {
                    DCTEncodeVersion = (short)((this.temp[5] << 8) | this.temp[6]),
                    APP14Flags0 = (short)((this.temp[7] << 8) | this.temp[8]),
                    APP14Flags1 = (short)((this.temp[9] << 8) | this.temp[10]),
                    ColorTransform = this.temp[11]
                };
            }

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

                            Span<short> tableSpan = this.quantizationTables.Tables.GetRowSpan(quantizationTableSpec & 15);
                            for (int j = 0; j < 64; j++)
                            {
                                tableSpan[PdfJsQuantizationTables.DctZigZag[j]] = this.temp[j];
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

                            Span<short> tableSpan = this.quantizationTables.Tables.GetRowSpan(quantizationTableSpec & 15);
                            for (int j = 0; j < 64; j++)
                            {
                                tableSpan[PdfJsQuantizationTables.DctZigZag[j]] = (short)((this.temp[2 * j] << 8) | this.temp[(2 * j) + 1]);
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
                int h = this.temp[index + 1] >> 4;
                int v = this.temp[index + 1] & 15;

                if (maxH < h)
                {
                    maxH = h;
                }

                if (maxV < v)
                {
                    maxV = v;
                }

                var component = new PdfJsFrameComponent(this.Frame, this.temp[index], h, v, this.temp[index + 2], i);

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

            using (var huffmanData = Buffer<byte>.CreateClean(256))
            {
                for (int i = 2; i < remaining;)
                {
                    byte huffmanTableSpec = (byte)this.InputStream.ReadByte();
                    this.InputStream.Read(huffmanData.Array, 0, 16);

                    using (var codeLengths = Buffer<byte>.CreateClean(17))
                    {
                        int codeLengthSum = 0;

                        for (int j = 1; j < 17; j++)
                        {
                            codeLengthSum += codeLengths[j] = huffmanData[j - 1];
                        }

                        using (var huffmanValues = Buffer<byte>.CreateClean(256))
                        {
                            this.InputStream.Read(huffmanValues.Array, 0, codeLengthSum);

                            i += 17 + codeLengthSum;

                            this.BuildHuffmanTable(
                                huffmanTableSpec >> 4 == 0 ? this.dcHuffmanTables : this.acHuffmanTables,
                                huffmanTableSpec & 15,
                                codeLengths.Array,
                                huffmanValues.Array);
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
        /// Build the data for the given component
        /// </summary>
        /// <param name="component">The component</param>
        /// <param name="frameComponent">The frame component</param>
        private void QuantizeAndInverseComponentData(PdfJsComponent component, PdfJsFrameComponent frameComponent)
        {
            int blocksPerLine = component.BlocksPerLine;
            int blocksPerColumn = component.BlocksPerColumn;
            using (var computationBuffer = Buffer<short>.CreateClean(64))
            using (var multiplicationBuffer = Buffer<short>.CreateClean(64))
            {
                Span<short> quantizationTable = this.quantizationTables.Tables.GetRowSpan(frameComponent.QuantizationTableIndex);
                Span<short> computationBufferSpan = computationBuffer;

                // For AA&N IDCT method, multiplier are equal to quantization
                // coefficients scaled by scalefactor[row]*scalefactor[col], where
                //   scalefactor[0] = 1
                //   scalefactor[k] = cos(k*PI/16) * sqrt(2)    for k=1..7
                // For integer operation, the multiplier table is to be scaled by 12.
                Span<short> multiplierSpan = multiplicationBuffer;

                // for (int i = 0; i < 64; i++)
                // {
                //    multiplierSpan[i] = (short)IDCT.Descale(quantizationTable[i] * IDCT.Aanscales[i], 12);
                // }
                for (int blockRow = 0; blockRow < blocksPerColumn; blockRow++)
                {
                    for (int blockCol = 0; blockCol < blocksPerLine; blockCol++)
                    {
                        int offset = GetBlockBufferOffset(ref component, blockRow, blockCol);
                        PdfJsIDCT.QuantizeAndInverse(frameComponent, offset, ref computationBufferSpan, ref quantizationTable);
                    }
                }
            }

            component.Output = frameComponent.BlockData;
        }

        /// <summary>
        /// Builds the huffman tables
        /// </summary>
        /// <param name="tables">The tables</param>
        /// <param name="index">The table index</param>
        /// <param name="codeLengths">The codelengths</param>
        /// <param name="values">The values</param>
        private void BuildHuffmanTable(PdfJsHuffmanTables tables, int index, byte[] codeLengths, byte[] values)
        {
            tables[index] = new PdfJsHuffmanTable(codeLengths, values);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FillGrayScaleImage<TPixel>(ImageFrame<TPixel> image)
            where TPixel : struct, IPixel<TPixel>
        {
            for (int y = 0; y < image.Height; y++)
            {
                Span<TPixel> imageRowSpan = image.GetPixelRowSpan(y);
                Span<byte> areaRowSpan = this.pixelArea.GetRowSpan(y);

                for (int x = 0; x < image.Width; x++)
                {
                    ref byte luminance = ref areaRowSpan[x];
                    ref TPixel pixel = ref imageRowSpan[x];
                    var rgba = new Rgba32(luminance, luminance, luminance);
                    pixel.PackFromRgba32(rgba);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FillYCbCrImage<TPixel>(ImageFrame<TPixel> image)
            where TPixel : struct, IPixel<TPixel>
        {
            for (int y = 0; y < image.Height; y++)
            {
                Span<TPixel> imageRowSpan = image.GetPixelRowSpan(y);
                Span<byte> areaRowSpan = this.pixelArea.GetRowSpan(y);
                for (int x = 0, o = 0; x < image.Width; x++, o += 3)
                {
                    ref byte yy = ref areaRowSpan[o];
                    ref byte cb = ref areaRowSpan[o + 1];
                    ref byte cr = ref areaRowSpan[o + 2];
                    ref TPixel pixel = ref imageRowSpan[x];
                    PdfJsYCbCrToRgbTables.PackYCbCr(ref pixel, yy, cb, cr);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FillYcckImage<TPixel>(ImageFrame<TPixel> image)
            where TPixel : struct, IPixel<TPixel>
        {
            for (int y = 0; y < image.Height; y++)
            {
                Span<TPixel> imageRowSpan = image.GetPixelRowSpan(y);
                Span<byte> areaRowSpan = this.pixelArea.GetRowSpan(y);
                for (int x = 0, o = 0; x < image.Width; x++, o += 4)
                {
                    ref byte yy = ref areaRowSpan[o];
                    ref byte cb = ref areaRowSpan[o + 1];
                    ref byte cr = ref areaRowSpan[o + 2];
                    ref byte k = ref areaRowSpan[o + 3];

                    ref TPixel pixel = ref imageRowSpan[x];
                    PdfJsYCbCrToRgbTables.PackYccK(ref pixel, yy, cb, cr, k);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FillCmykImage<TPixel>(ImageFrame<TPixel> image)
            where TPixel : struct, IPixel<TPixel>
        {
            for (int y = 0; y < image.Height; y++)
            {
                Span<TPixel> imageRowSpan = image.GetPixelRowSpan(y);
                Span<byte> areaRowSpan = this.pixelArea.GetRowSpan(y);
                for (int x = 0, o = 0; x < image.Width; x++, o += 4)
                {
                    ref byte c = ref areaRowSpan[o];
                    ref byte m = ref areaRowSpan[o + 1];
                    ref byte cy = ref areaRowSpan[o + 2];
                    ref byte k = ref areaRowSpan[o + 3];

                    byte r = (byte)((c * k) / 255);
                    byte g = (byte)((m * k) / 255);
                    byte b = (byte)((cy * k) / 255);

                    ref TPixel pixel = ref imageRowSpan[x];
                    var rgba = new Rgba32(r, g, b);
                    pixel.PackFromRgba32(rgba);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FillRgbImage<TPixel>(ImageFrame<TPixel> image)
            where TPixel : struct, IPixel<TPixel>
        {
            for (int y = 0; y < image.Height; y++)
            {
                Span<TPixel> imageRowSpan = image.GetPixelRowSpan(y);
                Span<byte> areaRowSpan = this.pixelArea.GetRowSpan(y);

                PixelOperations<TPixel>.Instance.PackFromRgb24Bytes(areaRowSpan, imageRowSpan, image.Width);
            }
        }

        /// <summary>
        /// Reads a <see cref="ushort"/> from the stream advancing it by two bytes
        /// </summary>
        /// <returns>The <see cref="ushort"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort ReadUint16()
        {
            this.InputStream.Read(this.markerBuffer, 0, 2);
            return (ushort)((this.markerBuffer[0] << 8) | this.markerBuffer[1]);
        }
    }
}
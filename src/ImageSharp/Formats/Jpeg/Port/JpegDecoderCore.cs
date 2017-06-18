// <copyright file="JpegDecoderCore.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats.Jpeg.Port
{
    using System;
    using System.Buffers;
    using System.IO;

    using ImageSharp.Formats.Jpeg.Port.Components;
    using ImageSharp.Memory;
    using ImageSharp.PixelFormats;

    /// <summary>
    /// Performs the jpeg decoding operation.
    /// Ported from <see href="https://github.com/mozilla/pdf.js/blob/master/src/core/jpg.js"/>
    /// </summary>
    internal class JpegDecoderCore
    {
        /// <summary>
        /// The decoder options.
        /// </summary>
        private readonly IDecoderOptions options;

        /// <summary>
        /// The global configuration
        /// </summary>
        private readonly Configuration configuration;

        /// <summary>
        /// Gets the temporary buffer used to store bytes read from the stream.
        /// </summary>
        private readonly byte[] temp = new byte[2 * 16 * 4];

        private readonly byte[] uint16Buffer = new byte[2];

        private QuantizationTables quantizationTables;

        private HuffmanTables dcHuffmanTables;

        private HuffmanTables acHuffmanTables;

        private Frame frame;

        /// <summary>
        /// COntains information about the jFIF marker
        /// </summary>
        private JFif jFif;

        /// <summary>
        /// Whether the image has a EXIF header
        /// </summary>
        private bool isExif;

        private int offset;

        /// <summary>
        /// Initializes a new instance of the <see cref="JpegDecoderCore" /> class.
        /// </summary>
        /// <param name="options">The decoder options.</param>
        /// <param name="configuration">The configuration.</param>
        public JpegDecoderCore(IDecoderOptions options, Configuration configuration)
        {
            this.configuration = configuration ?? Configuration.Default;
            this.options = options ?? new DecoderOptions();
        }

        /// <summary>
        /// Gets the input stream.
        /// </summary>
        public Stream InputStream { get; private set; }

        /// <summary>
        /// Decodes the image from the specified <see cref="Stream"/>  and sets
        /// the data to image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="stream">The stream, where the image should be.</param>
        /// <returns>The decoded image.</returns>
        public Image<TPixel> Decode<TPixel>(Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            this.InputStream = stream;
            this.ParseStream();

            var image = new Image<TPixel>(1, 1);
            return image;
        }

        private void ParseStream()
        {
            // Check for the Start Of Image marker.
            ushort fileMarker = this.ReadUint16();
            if (fileMarker != JpegConstants.Markers.SOI)
            {
                throw new ImageFormatException("Missing SOI marker.");
            }

            fileMarker = this.ReadUint16();

            this.quantizationTables = new QuantizationTables();
            this.dcHuffmanTables = new HuffmanTables();
            this.acHuffmanTables = new HuffmanTables();

            while (fileMarker != JpegConstants.Markers.EOI)
            {
                // Get the marker length
                int remaining = this.ReadUint16() - 2;

                switch (fileMarker)
                {
                    case JpegConstants.Markers.APP0:
                        this.ProcessApplicationHeaderMarker(remaining);
                        break;

                    case JpegConstants.Markers.APP1:
                    case JpegConstants.Markers.APP2:
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
                    case JpegConstants.Markers.APP13:
                    case JpegConstants.Markers.APP14:
                    case JpegConstants.Markers.APP15:
                    case JpegConstants.Markers.COM:
                        break;

                    case JpegConstants.Markers.DQT:
                        this.ProcessDqtMarker(remaining);
                        break;

                    case JpegConstants.Markers.SOF0:
                    case JpegConstants.Markers.SOF1:
                    case JpegConstants.Markers.SOF2:
                        this.ProcessStartOfFrameMarker(remaining, fileMarker);
                        break;

                    case JpegConstants.Markers.DHT:
                        this.ProcessDefineHuffmanTablesMarker(remaining);
                        break;
                }

                // Read on
                fileMarker = this.FindNextFileMarker();
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

            bool isJfif = this.temp[0] == JpegConstants.Markers.JFif.J &&
                          this.temp[1] == JpegConstants.Markers.JFif.F &&
                          this.temp[2] == JpegConstants.Markers.JFif.I &&
                          this.temp[3] == JpegConstants.Markers.JFif.F &&
                          this.temp[4] == JpegConstants.Markers.JFif.Null;

            if (isJfif)
            {
                this.jFif = new JFif
                {
                    MajorVersion = this.temp[5],
                    MinorVersion = this.temp[6],
                    DensityUnits = this.temp[7],
                    XDensity = (short)((this.temp[8] << 8) | this.temp[9]),
                    YDensity = (short)((this.temp[10] << 8) | this.temp[11])
                };
            }

            // Skip thumbnails for now.
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
        private void ProcessDqtMarker(int remaining)
        {
            while (remaining > 0)
            {
                bool done = false;
                remaining--;
                int quantizationTableSpec = this.InputStream.ReadByte();

                if (quantizationTableSpec > 3)
                {
                    throw new ImageFormatException("Bad Tq index value");
                }

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

                            Span<short> tableSpan = this.quantizationTables.Tables.GetRowSpan(quantizationTableSpec);
                            for (int j = 0; j < 64; j++)
                            {
                                tableSpan[QuantizationTables.DctZigZag[j]] = this.temp[j];
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

                            Span<short> tableSpan = this.quantizationTables.Tables.GetRowSpan(quantizationTableSpec);
                            for (int j = 0; j < 64; j++)
                            {
                                tableSpan[QuantizationTables.DctZigZag[j]] = (short)((this.temp[2 * j] << 8) | this.temp[(2 * j) + 1]);
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
        private void ProcessStartOfFrameMarker(int remaining, ushort frameMarker)
        {
            if (this.frame != null)
            {
                throw new ImageFormatException("Multiple SOF markers. Only single frame jpegs supported.");
            }

            this.InputStream.Read(this.temp, 0, remaining);

            this.frame = new Frame
            {
                Extended = frameMarker == JpegConstants.Markers.SOF1,
                Progressive = frameMarker == JpegConstants.Markers.SOF1,
                Precision = this.temp[0],
                Scanlines = (short)((this.temp[1] << 8) | this.temp[2]),
                SamplesPerLine = (short)((this.temp[3] << 8) | this.temp[4]),
                ComponentCount = this.temp[5]
            };

            int maxH = 0;
            int maxV = 0;
            int index = 6;

            // TODO: Pool this.
            this.frame.ComponentIds = new byte[this.frame.ComponentCount];
            this.frame.Components = new Component[this.frame.ComponentCount];

            for (int i = 0; i < this.frame.ComponentCount; i++)
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

                ref var component = ref this.frame.Components[i];
                component.Id = this.temp[index];
                component.HorizontalFactor = h;
                component.VerticalFactor = v;
                component.QuantizationIdentifier = this.temp[index + 2];

                // Don't assign the table yet.
                index += 3;
            }

            this.frame.MaxHorizontalFactor = maxH;
            this.frame.MaxVerticalFactor = maxV;
            this.PrepareComponents();
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
                throw new ImageFormatException("DHT has wrong length");
            }

            using (var huffmanData = new Buffer<byte>(remaining))
            {
                this.InputStream.Read(huffmanData.Array, 1, remaining);

                int o = 0;
                for (int i = 0; i < remaining;)
                {
                    byte huffmanTableSpec = huffmanData[i];
                    byte[] codeLengths = new byte[16];
                    int codeLengthSum = 0;

                    for (int j = 0; j < 16; j++)
                    {
                        codeLengthSum += codeLengths[j] = huffmanData[o++];
                    }

                    short[] huffmanValues = new short[codeLengthSum];

                    byte[] values = null;
                    try
                    {
                        values = ArrayPool<byte>.Shared.Rent(256);
                        this.InputStream.Read(values, 0, codeLengthSum);

                        for (int j = 0; j < codeLengthSum; j++)
                        {
                            huffmanValues[j] = values[o++];
                        }
                    }
                    finally
                    {
                        if (values != null)
                        {
                            ArrayPool<byte>.Shared.Return(values);
                        }
                    }

                    i += 17 + codeLengthSum;

                    this.BuildHuffmanTable(
                        huffmanTableSpec >> 4 == 0 ? this.dcHuffmanTables : this.acHuffmanTables,
                        huffmanTableSpec & 15,
                        codeLengths,
                        huffmanValues);
                }
            }
        }

        private void BuildHuffmanTable(HuffmanTables tables, int index, byte[] codeLengths, short[] values)
        {
            int length = 16;
            while (length > 0 && codeLengths[length - 1] == 0)
            {
                length--;
            }

            Span<short> tableSpan = tables.Tables.GetRowSpan(index);
        }

        /// <summary>
        /// Allocates the frame component blocks
        /// </summary>
        private void PrepareComponents()
        {
            int mcusPerLine = this.frame.SamplesPerLine / 8 / this.frame.MaxHorizontalFactor;
            int mcusPerColumn = this.frame.Scanlines / 8 / this.frame.MaxVerticalFactor;

            for (int i = 0; i < this.frame.ComponentCount; i++)
            {
                ref var component = ref this.frame.Components[i];
                int blocksPerLine = this.frame.SamplesPerLine / 8 * component.HorizontalFactor / this.frame.MaxHorizontalFactor;
                int blocksPerColumn = this.frame.Scanlines / 8 * component.VerticalFactor / this.frame.MaxVerticalFactor;
                int blocksPerLineForMcu = mcusPerLine * component.HorizontalFactor;
                int blocksPerColumnForMcu = mcusPerColumn * component.VerticalFactor;

                int blocksBufferSize = 64 * blocksPerColumnForMcu * (blocksPerLineForMcu + 1);

                // TODO: Pool this
                component.BlockData = new short[blocksBufferSize];
                component.BlocksPerLine = blocksPerLine;
                component.BlocksPerColumn = blocksPerColumn;
            }

            this.frame.McusPerLine = mcusPerLine;
            this.frame.McusPerColumn = mcusPerColumn;
        }

        /// <summary>
        /// Finds the next file marker within the byte stream
        /// </summary>
        /// <returns>The <see cref="ushort"/></returns>
        private ushort FindNextFileMarker()
        {
            while (true)
            {
                int value = this.InputStream.Read(this.uint16Buffer, 0, 2);

                if (value == 0)
                {
                    return JpegConstants.Markers.EOI;
                }

                while (this.uint16Buffer[0] != JpegConstants.Markers.Prefix)
                {
                    // Strictly speaking, this is a format error. However, libjpeg is
                    // liberal in what it accepts. As of version 9, next_marker in
                    // jdmarker.c treats this as a warning (JWRN_EXTRANEOUS_DATA) and
                    // continues to decode the stream. Even before next_marker sees
                    // extraneous data, jpeg_fill_bit_buffer in jdhuff.c reads as many
                    // bytes as it can, possibly past the end of a scan's data. It
                    // effectively puts back any markers that it overscanned (e.g. an
                    // "\xff\xd9" EOI marker), but it does not put back non-marker data,
                    // and thus it can silently ignore a small number of extraneous
                    // non-marker bytes before next_marker has a chance to see them (and
                    // print a warning).
                    // We are therefore also liberal in what we accept. Extraneous data
                    // is silently ignore
                    // This is similar to, but not exactly the same as, the restart
                    // mechanism within a scan (the RST[0-7] markers).
                    // Note that extraneous 0xff bytes in e.g. SOS data are escaped as
                    // "\xff\x00", and so are detected a little further down below.
                    this.uint16Buffer[0] = this.uint16Buffer[1];

                    value = this.InputStream.ReadByte();
                    if (value == -1)
                    {
                        return JpegConstants.Markers.EOI;
                    }

                    this.uint16Buffer[1] = (byte)value;
                }

                return (ushort)((this.uint16Buffer[0] << 8) | this.uint16Buffer[1]);
            }
        }

        /// <summary>
        /// Reads a <see cref="ushort"/> from the stream advancing it by two bytes
        /// </summary>
        /// <returns>The <see cref="ushort"/></returns>
        private ushort ReadUint16()
        {
            this.InputStream.Read(this.uint16Buffer, 0, 2);
            ushort value = (ushort)((this.uint16Buffer[0] << 8) | this.uint16Buffer[1]);
            this.offset += 2;
            return value;
        }

        /// <summary>
        /// Provides information about the JFIF marker segment
        /// </summary>
        internal struct JFif
        {
            /// <summary>
            /// The major version
            /// </summary>
            public byte MajorVersion;

            /// <summary>
            /// The minor version
            /// </summary>
            public byte MinorVersion;

            /// <summary>
            /// Units for the following pixel density fields
            ///  00 : No units; width:height pixel aspect ratio = Ydensity:Xdensity
            ///  01 : Pixels per inch (2.54 cm)
            ///  02 : Pixels per centimeter
            /// </summary>
            public byte DensityUnits;

            /// <summary>
            /// Horizontal pixel density. Must not be zero.
            /// </summary>
            public short XDensity;

            /// <summary>
            /// Vertical pixel density. Must not be zero.
            /// </summary>
            public short YDensity;

            // TODO: Thumbnail?
        }
    }
}

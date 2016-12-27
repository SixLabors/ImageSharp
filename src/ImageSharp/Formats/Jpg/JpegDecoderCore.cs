// <copyright file="JpegDecoderCore.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>
namespace ImageSharp.Formats
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;

    using ImageSharp.Formats.Jpg;
    
    /// <summary>
    ///     Performs the jpeg decoding operation.
    /// </summary>
    internal unsafe class JpegDecoderCore : IDisposable
    {
        /// <summary>
        ///     The AC table index
        /// </summary>
        internal const int AcTable = 1;

        /// <summary>
        ///     The DC table index
        /// </summary>
        internal const int DcTable = 0;

        /// <summary>
        ///     The maximum number of color components
        /// </summary>
        internal const int MaxComponents = 4;

        /// <summary>
        ///     The maximum number of quantization tables
        /// </summary>
        private const int MaxTq = 3;

        /// <summary>
        ///     The component array
        /// </summary>
        internal Component[] ComponentArray { get; }

        /// <summary>
        ///     The huffman trees
        /// </summary>
        internal HuffmanTree[] HuffmanTrees { get; }

        /// <summary>
        ///     Saved state between progressive-mode scans.
        /// </summary>
        internal Block8x8F[][] ProgCoeffs { get; }

        /// <summary>
        ///     Quantization tables, in zigzag order.
        /// </summary>
        internal Block8x8F[] QuantizationTables { get; }

        /// <summary>
        ///     A temporary buffer for holding pixels
        /// </summary>
        internal byte[] Temp { get; }

        // TODO: the usage of this buffer is unclean + need to move it to the stack for performance

        /// <summary>
        ///     The App14 marker color-space
        /// </summary>
        private byte adobeTransform;

        /// <summary>
        ///     Whether the image is in CMYK format with an App14 marker
        /// </summary>
        private bool adobeTransformValid;

        /// <summary>
        ///     Holds the unprocessed bits that have been taken from the byte-stream.
        /// </summary>
        internal Bits Bits;

        private JpegPixelArea blackImage;

        //private int blockIndex;

        /// <summary>
        ///     The byte buffer.
        /// </summary>
        private Bytes bytes;

        /// <summary>
        ///     The number of color components within the image.
        /// </summary>
        internal int ComponentCount { get; private set; }

        /// <summary>
        ///     End-of-Band run, specified in section G.1.2.2.
        /// </summary>
        internal ushort EobRun;

        /// <summary>
        ///     A grayscale image to decode to.
        /// </summary>
        private JpegPixelArea grayImage;

        /// <summary>
        ///     The horizontal resolution. Calculated if the image has a JFIF header.
        /// </summary>
        private short horizontalResolution;

        /// <summary>
        ///     The image height
        /// </summary>
        internal int ImageHeight { get; private set; }

        /// <summary>
        ///     The image width
        /// </summary>
        internal int ImageWidth { get; private set; }

        /// <summary>
        ///     The byte buffer.
        /// </summary>
        private Stream inputStream;

        /// <summary>
        ///     Whether the image has a JFIF header
        /// </summary>
        private bool isJfif;

        /// <summary>
        ///     Whether the image is interlaced (progressive)
        /// </summary>
        public bool IsProgressive { get; private set; }

        /// <summary>
        ///     The restart interval
        /// </summary>
        internal int RestartInterval { get; private set; }

        /// <summary>
        ///     The vertical resolution. Calculated if the image has a JFIF header.
        /// </summary>
        private short verticalResolution;

        /// <summary>
        ///     The full color image to decode to.
        /// </summary>
        private YCbCrImage ycbcrImage;

        /// <summary>
        ///     Initializes a new instance of the <see cref="JpegDecoderCore" /> class.
        /// </summary>
        public JpegDecoderCore()
        {
            this.HuffmanTrees = HuffmanTree.CreateHuffmanTrees();
            this.QuantizationTables = new Block8x8F[MaxTq + 1];
            this.Temp = new byte[2 * Block8x8F.ScalarCount];
            this.ComponentArray = new Component[MaxComponents];
            this.ProgCoeffs = new Block8x8F[MaxComponents][];
            this.Bits = default(Bits);
            this.bytes = Bytes.Create();
        }
        
        /// <summary>
        ///     ReadByteStuffedByte was throwing exceptions on normal execution path (very inefficent)
        ///     It's better tho have an error code for this!
        /// </summary>
        internal enum ErrorCodes
        {
            /// <summary>
            ///     NoError
            /// </summary>
            NoError,

            /// <summary>
            ///     MissingFF00
            /// </summary>
            MissingFF00
        }

        /// <summary>
        ///     Gets or sets the byte buffer.
        /// </summary>
        public Bytes Bytes
        {
            get
            {
                return this.bytes;
            }

            set
            {
                this.bytes = value;
            }
        }

        /// <summary>
        ///     Gets the input stream.
        /// </summary>
        public Stream InputStream
        {
            get
            {
                return this.inputStream;
            }
        }

        /// <summary>
        ///     Decodes the image from the specified this._stream and sets
        ///     the data to image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="image">The image, where the data should be set to.</param>
        /// <param name="stream">The stream, where the image should be.</param>
        /// <param name="configOnly">Whether to decode metadata only.</param>
        public void Decode<TColor>(Image<TColor> image, Stream stream, bool configOnly)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            this.inputStream = stream;

            // Check for the Start Of Image marker.
            this.ReadFull(this.Temp, 0, 2);
            if (this.Temp[0] != JpegConstants.Markers.XFF || this.Temp[1] != JpegConstants.Markers.SOI)
            {
                throw new ImageFormatException("Missing SOI marker.");
            }

            // Process the remaining segments until the End Of Image marker.
            while (true)
            {
                this.ReadFull(this.Temp, 0, 2);
                while (this.Temp[0] != 0xff)
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
                    this.Temp[0] = this.Temp[1];
                    this.Temp[1] = this.ReadByte();
                }

                byte marker = this.Temp[1];
                if (marker == 0)
                {
                    // Treat "\xff\x00" as extraneous data.
                    continue;
                }

                while (marker == 0xff)
                {
                    // Section B.1.1.2 says, "Any marker may optionally be preceded by any
                    // number of fill bytes, which are bytes assigned code X'FF'".
                    marker = this.ReadByte();
                }

                // End Of Image.
                if (marker == JpegConstants.Markers.EOI)
                {
                    break;
                }

                if (marker >= JpegConstants.Markers.RST0 && marker <= JpegConstants.Markers.RST7)
                {
                    // Figures B.2 and B.16 of the specification suggest that restart markers should
                    // only occur between Entropy Coded Segments and not after the final ECS.
                    // However, some encoders may generate incorrect JPEGs with a final restart
                    // marker. That restart marker will be seen here instead of inside the ProcessSOS
                    // method, and is ignored as a harmless error. Restart markers have no extra data,
                    // so we check for this before we read the 16-bit length of the segment.
                    continue;
                }

                // Read the 16-bit length of the segment. The value includes the 2 bytes for the
                // length itself, so we subtract 2 to get the number of remaining bytes.
                this.ReadFull(this.Temp, 0, 2);
                int remaining = (this.Temp[0] << 8) + this.Temp[1] - 2;
                if (remaining < 0)
                {
                    throw new ImageFormatException("Short segment length.");
                }

                switch (marker)
                {
                    case JpegConstants.Markers.SOF0:
                    case JpegConstants.Markers.SOF1:
                    case JpegConstants.Markers.SOF2:
                        this.IsProgressive = marker == JpegConstants.Markers.SOF2;
                        this.ProcessStartOfFrameMarker(remaining);
                        if (configOnly && this.isJfif)
                        {
                            return;
                        }

                        break;
                    case JpegConstants.Markers.DHT:
                        if (configOnly)
                        {
                            this.Skip(remaining);
                        }
                        else
                        {
                            this.ProcessDefineHuffmanTablesMarker(remaining);
                        }

                        break;
                    case JpegConstants.Markers.DQT:
                        if (configOnly)
                        {
                            this.Skip(remaining);
                        }
                        else
                        {
                            this.ProcessDqt(remaining);
                        }

                        break;
                    case JpegConstants.Markers.SOS:
                        if (configOnly)
                        {
                            return;
                        }

                        this.ProcessStartOfScan22(remaining);
                        break;
                    case JpegConstants.Markers.DRI:
                        if (configOnly)
                        {
                            this.Skip(remaining);
                        }
                        else
                        {
                            this.ProcessDefineRestartIntervalMarker(remaining);
                        }

                        break;
                    case JpegConstants.Markers.APP0:
                        this.ProcessApplicationHeader(remaining);
                        break;
                    case JpegConstants.Markers.APP1:
                        this.ProcessApp1Marker(remaining, image);
                        break;
                    case JpegConstants.Markers.APP14:
                        this.ProcessApp14Marker(remaining);
                        break;
                    default:
                        if ((marker >= JpegConstants.Markers.APP0 && marker <= JpegConstants.Markers.APP15)
                            || marker == JpegConstants.Markers.COM)
                        {
                            this.Skip(remaining);
                        }
                        else if (marker < JpegConstants.Markers.SOF0)
                        {
                            // See Table B.1 "Marker code assignments".
                            throw new ImageFormatException("Unknown marker");
                        }
                        else
                        {
                            throw new ImageFormatException("Unknown marker");
                        }

                        break;
                }
            }

            if (this.grayImage.IsInitialized)
            {
                this.ConvertFromGrayScale(this.ImageWidth, this.ImageHeight, image);
            }
            else if (this.ycbcrImage != null)
            {
                if (this.ComponentCount == 4)
                {
                    if (!this.adobeTransformValid)
                    {
                        throw new ImageFormatException(
                                  "Unknown color model: 4-component JPEG doesn't have Adobe APP14 metadata");
                    }

                    // See http://www.sno.phy.queensu.ca/~phil/exiftool/TagNames/JPEG.html#Adobe
                    // See https://docs.oracle.com/javase/8/docs/api/javax/imageio/metadata/doc-files/jpeg_metadata.html
                    // TODO: YCbCrA?
                    if (this.adobeTransform == JpegConstants.Adobe.ColorTransformYcck)
                    {
                        this.ConvertFromYcck(this.ImageWidth, this.ImageHeight, image);
                    }
                    else if (this.adobeTransform == JpegConstants.Adobe.ColorTransformUnknown)
                    {
                        // Assume CMYK
                        this.ConvertFromCmyk(this.ImageWidth, this.ImageHeight, image);
                    }

                    return;
                }

                if (this.ComponentCount == 3)
                {
                    if (this.IsRGB())
                    {
                        this.ConvertFromRGB(this.ImageWidth, this.ImageHeight, image);
                        return;
                    }

                    this.ConvertFromYCbCr(this.ImageWidth, this.ImageHeight, image);
                    return;
                }

                throw new ImageFormatException("JpegDecoder only supports RGB, CMYK and Grayscale color spaces.");
            }
            else
            {
                throw new ImageFormatException("Missing SOS marker.");
            }
        }

        /// <summary>
        ///     Dispose
        /// </summary>
        public void Dispose()
        {
            for (int i = 0; i < this.HuffmanTrees.Length; i++)
            {
                this.HuffmanTrees[i].Dispose();
            }

            this.ycbcrImage?.Dispose();
            this.bytes.Dispose();
            this.grayImage.ReturnPooled();
            this.blackImage.ReturnPooled();
        }
        
        /// <summary>
        ///     Returns the next byte, whether buffered or not buffered. It does not care about byte stuffing.
        /// </summary>
        /// <returns>The <see cref="byte" /></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal byte ReadByte()
        {
            return this.bytes.ReadByte(this.inputStream);
        }

        /// <summary>
        ///     Reads exactly length bytes into data. It does not care about byte stuffing.
        /// </summary>
        /// <param name="data">The data to write to.</param>
        /// <param name="offset">The offset in the source buffer</param>
        /// <param name="length">The number of bytes to read</param>
        internal void ReadFull(byte[] data, int offset, int length)
        {
            // Unread the overshot bytes, if any.
            if (this.bytes.UnreadableBytes != 0)
            {
                if (this.Bits.UnreadBits >= 8)
                {
                    this.UnreadByteStuffedByte();
                }

                this.bytes.UnreadableBytes = 0;
            }

            while (length > 0)
            {
                if (this.bytes.J - this.bytes.I >= length)
                {
                    Array.Copy(this.bytes.Buffer, this.bytes.I, data, offset, length);
                    this.bytes.I += length;
                    length -= length;
                }
                else
                {
                    Array.Copy(this.bytes.Buffer, this.bytes.I, data, offset, this.bytes.J - this.bytes.I);
                    offset += this.bytes.J - this.bytes.I;
                    length -= this.bytes.J - this.bytes.I;
                    this.bytes.I += this.bytes.J - this.bytes.I;

                    this.bytes.Fill(this.inputStream);
                }
            }
        }

        /// <summary>
        ///     Optimized method to pack bytes to the image from the YCbCr color space.
        ///     This is faster than implicit casting as it avoids double packing.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="packed">The packed pixel.</param>
        /// <param name="y">The y luminance component.</param>
        /// <param name="cb">The cb chroma component.</param>
        /// <param name="cr">The cr chroma component.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void PackYcbCr<TColor>(ref TColor packed, byte y, byte cb, byte cr)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            int ccb = cb - 128;
            int ccr = cr - 128;

            byte r = (byte)(y + (1.402F * ccr)).Clamp(0, 255);
            byte g = (byte)(y - (0.34414F * ccb) - (0.71414F * ccr)).Clamp(0, 255);
            byte b = (byte)(y + (1.772F * ccb)).Clamp(0, 255);

            packed.PackFromBytes(r, g, b, 255);
        }

        /// <summary>
        ///     Assigns the horizontal and vertical resolution to the image if it has a JFIF header.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="image">The image to assign the resolution to.</param>
        private void AssignResolution<TColor>(Image<TColor> image)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            if (this.isJfif && this.horizontalResolution > 0 && this.verticalResolution > 0)
            {
                image.HorizontalResolution = this.horizontalResolution;
                image.VerticalResolution = this.verticalResolution;
            }
        }

        /// <summary>
        ///     Converts the image from the original CMYK image pixels.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="width">The image width.</param>
        /// <param name="height">The image height.</param>
        /// <param name="image">The image.</param>
        private void ConvertFromCmyk<TColor>(int width, int height, Image<TColor> image)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            int scale = this.ComponentArray[0].HorizontalFactor / this.ComponentArray[1].HorizontalFactor;

            image.InitPixels(width, height);

            using (PixelAccessor<TColor> pixels = image.Lock())
            {
                Parallel.For(
                    0,
                    height,
                    y =>
                        {
                            int yo = this.ycbcrImage.GetRowYOffset(y);
                            int co = this.ycbcrImage.GetRowCOffset(y);

                            for (int x = 0; x < width; x++)
                            {
                                byte cyan = this.ycbcrImage.YPixels[yo + x];
                                byte magenta = this.ycbcrImage.CbPixels[co + (x / scale)];
                                byte yellow = this.ycbcrImage.CrPixels[co + (x / scale)];

                                TColor packed = default(TColor);
                                this.PackCmyk<TColor>(ref packed, cyan, magenta, yellow, x, y);
                                pixels[x, y] = packed;
                            }
                        });
            }

            this.AssignResolution(image);
        }

        /// <summary>
        ///     Converts the image from the original grayscale image pixels.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="width">The image width.</param>
        /// <param name="height">The image height.</param>
        /// <param name="image">The image.</param>
        private void ConvertFromGrayScale<TColor>(int width, int height, Image<TColor> image)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            image.InitPixels(width, height);

            using (PixelAccessor<TColor> pixels = image.Lock())
            {
                Parallel.For(
                    0,
                    height,
                    Bootstrapper.ParallelOptions,
                    y =>
                        {
                            int yoff = this.grayImage.GetRowOffset(y);
                            for (int x = 0; x < width; x++)
                            {
                                byte rgb = this.grayImage.Pixels[yoff + x];

                                TColor packed = default(TColor);
                                packed.PackFromBytes(rgb, rgb, rgb, 255);
                                pixels[x, y] = packed;
                            }
                        });
            }

            this.AssignResolution(image);
        }

        /// <summary>
        ///     Converts the image from the original RBG image pixels.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="width">The image width.</param>
        /// <param name="height">The height.</param>
        /// <param name="image">The image.</param>
        private void ConvertFromRGB<TColor>(int width, int height, Image<TColor> image)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            int scale = this.ComponentArray[0].HorizontalFactor / this.ComponentArray[1].HorizontalFactor;
            image.InitPixels(width, height);

            using (PixelAccessor<TColor> pixels = image.Lock())
            {
                Parallel.For(
                    0,
                    height,
                    Bootstrapper.ParallelOptions,
                    y =>
                        {
                            int yo = this.ycbcrImage.GetRowYOffset(y);
                            int co = this.ycbcrImage.GetRowCOffset(y);

                            for (int x = 0; x < width; x++)
                            {
                                byte red = this.ycbcrImage.YPixels[yo + x];
                                byte green = this.ycbcrImage.CbPixels[co + (x / scale)];
                                byte blue = this.ycbcrImage.CrPixels[co + (x / scale)];

                                TColor packed = default(TColor);
                                packed.PackFromBytes(red, green, blue, 255);
                                pixels[x, y] = packed;
                            }
                        });
            }

            this.AssignResolution(image);
        }

        /// <summary>
        ///     Converts the image from the original YCbCr image pixels.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="width">The image width.</param>
        /// <param name="height">The image height.</param>
        /// <param name="image">The image.</param>
        private void ConvertFromYCbCr<TColor>(int width, int height, Image<TColor> image)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            int scale = this.ComponentArray[0].HorizontalFactor / this.ComponentArray[1].HorizontalFactor;
            image.InitPixels(width, height);

            using (PixelAccessor<TColor> pixels = image.Lock())
            {
                Parallel.For(
                    0,
                    height,
                    Bootstrapper.ParallelOptions,
                    y =>
                        {
                            int yo = this.ycbcrImage.GetRowYOffset(y);
                            int co = this.ycbcrImage.GetRowCOffset(y);

                            for (int x = 0; x < width; x++)
                            {
                                byte yy = this.ycbcrImage.YPixels[yo + x];
                                byte cb = this.ycbcrImage.CbPixels[co + (x / scale)];
                                byte cr = this.ycbcrImage.CrPixels[co + (x / scale)];

                                TColor packed = default(TColor);
                                PackYcbCr<TColor>(ref packed, yy, cb, cr);
                                pixels[x, y] = packed;
                            }
                        });
            }

            this.AssignResolution(image);
        }

        /// <summary>
        ///     Converts the image from the original YCCK image pixels.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="width">The image width.</param>
        /// <param name="height">The image height.</param>
        /// <param name="image">The image.</param>
        private void ConvertFromYcck<TColor>(int width, int height, Image<TColor> image)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            int scale = this.ComponentArray[0].HorizontalFactor / this.ComponentArray[1].HorizontalFactor;

            image.InitPixels(width, height);

            using (PixelAccessor<TColor> pixels = image.Lock())
            {
                Parallel.For(
                    0,
                    height,
                    y =>
                        {
                            int yo = this.ycbcrImage.GetRowYOffset(y);
                            int co = this.ycbcrImage.GetRowCOffset(y);

                            for (int x = 0; x < width; x++)
                            {
                                byte yy = this.ycbcrImage.YPixels[yo + x];
                                byte cb = this.ycbcrImage.CbPixels[co + (x / scale)];
                                byte cr = this.ycbcrImage.CrPixels[co + (x / scale)];

                                TColor packed = default(TColor);
                                this.PackYcck<TColor>(ref packed, yy, cb, cr, x, y);
                                pixels[x, y] = packed;
                            }
                        });
            }

            this.AssignResolution(image);
        }

        /// <summary>
        ///     Decodes a single bit
        /// </summary>
        /// <returns>The <see cref="bool" /></returns>
        internal bool DecodeBit()
        {
            if (this.Bits.UnreadBits == 0)
            {
                ErrorCodes errorCode = this.Bits.EnsureNBits(1, this);
                if (errorCode != ErrorCodes.NoError)
                {
                    throw new MissingFF00Exception();
                }
            }

            bool ret = (this.Bits.Accumulator & this.Bits.Mask) != 0;
            this.Bits.UnreadBits--;
            this.Bits.Mask >>= 1;
            return ret;
        }

        /// <summary>
        ///     Decodes the given number of bits
        /// </summary>
        /// <param name="count">The number of bits to decode.</param>
        /// <returns>The <see cref="uint" /></returns>
        internal uint DecodeBits(int count)
        {
            if (this.Bits.UnreadBits < count)
            {
                ErrorCodes errorCode = this.Bits.EnsureNBits(count, this);
                if (errorCode != ErrorCodes.NoError)
                {
                    throw new MissingFF00Exception();
                }
            }

            uint ret = this.Bits.Accumulator >> (this.Bits.UnreadBits - count);
            ret = (uint)(ret & ((1 << count) - 1));
            this.Bits.UnreadBits -= count;
            this.Bits.Mask >>= count;
            return ret;
        }

        /// <summary>
        ///     Returns the next Huffman-coded value from the bit-stream, decoded according to the given value.
        /// </summary>
        /// <param name="huffmanTree">The huffman value</param>
        /// <returns>The <see cref="byte" /></returns>
        internal byte DecodeHuffman(ref HuffmanTree huffmanTree)
        {
            // Copy stuff to the stack:
            if (huffmanTree.Length == 0)
            {
                throw new ImageFormatException("Uninitialized Huffman table");
            }

            if (this.Bits.UnreadBits < 8)
            {
                ErrorCodes errorCode = this.Bits.EnsureNBits(8, this);

                if (errorCode == ErrorCodes.NoError)
                {
                    ushort v =
                        huffmanTree.Lut[(this.Bits.Accumulator >> (this.Bits.UnreadBits - HuffmanTree.LutSize)) & 0xff];

                    if (v != 0)
                    {
                        byte n = (byte)((v & 0xff) - 1);
                        this.Bits.UnreadBits -= n;
                        this.Bits.Mask >>= n;
                        return (byte)(v >> 8);
                    }
                }
                else
                {
                    this.UnreadByteStuffedByte();
                }
            }

            int code = 0;
            for (int i = 0; i < HuffmanTree.MaxCodeLength; i++)
            {
                if (this.Bits.UnreadBits == 0)
                {
                    ErrorCodes errorCode = this.Bits.EnsureNBits(1, this);
                    if (errorCode != ErrorCodes.NoError)
                    {
                        throw new MissingFF00Exception();
                    }
                }

                if ((this.Bits.Accumulator & this.Bits.Mask) != 0)
                {
                    code |= 1;
                }

                this.Bits.UnreadBits--;
                this.Bits.Mask >>= 1;

                if (code <= huffmanTree.MaxCodes[i])
                {
                    return huffmanTree.Values[huffmanTree.Indices[i] + code - huffmanTree.MinCodes[i]];
                }

                code <<= 1;
            }

            throw new ImageFormatException("Bad Huffman code");
        }

        internal JpegPixelArea GetDestinationChannel(int compIndex)
        {
            if (this.ComponentCount == 1)
            {
                return this.grayImage;
            }
            else
            {
                switch (compIndex)
                {
                    case 0:
                        return this.ycbcrImage.YChannel;
                    case 1:
                        return this.ycbcrImage.CbChannel;
                    case 2:
                        return this.ycbcrImage.CrChannel;
                    case 3:
                        return this.blackImage;
                    default:
                        throw new ImageFormatException("Too many components");
                }
            }
        }

        /// <summary>
        ///     Returns a value indicating whether the image in an RGB image.
        /// </summary>
        /// <returns>
        ///     The <see cref="bool" />.
        /// </returns>
        private bool IsRGB()
        {
            if (this.isJfif)
            {
                return false;
            }

            if (this.adobeTransformValid && this.adobeTransform == JpegConstants.Adobe.ColorTransformUnknown)
            {
                // http://www.sno.phy.queensu.ca/~phil/exiftool/TagNames/JPEG.html#Adobe
                // says that 0 means Unknown (and in practice RGB) and 1 means YCbCr.
                return true;
            }

            return this.ComponentArray[0].Identifier == 'R' && this.ComponentArray[1].Identifier == 'G'
                   && this.ComponentArray[2].Identifier == 'B';
        }

        /// <summary>
        ///     Makes the image from the buffer.
        /// </summary>
        /// <param name="mxx">The horizontal MCU count</param>
        /// <param name="myy">The vertical MCU count</param>
        private void MakeImage(int mxx, int myy)
        {
            if (this.grayImage.IsInitialized || this.ycbcrImage != null)
            {
                return;
            }

            if (this.ComponentCount == 1)
            {
                this.grayImage = JpegPixelArea.CreatePooled(8 * mxx, 8 * myy);
            }
            else
            {
                int h0 = this.ComponentArray[0].HorizontalFactor;
                int v0 = this.ComponentArray[0].VerticalFactor;
                int horizontalRatio = h0 / this.ComponentArray[1].HorizontalFactor;
                int verticalRatio = v0 / this.ComponentArray[1].VerticalFactor;

                YCbCrImage.YCbCrSubsampleRatio ratio = YCbCrImage.YCbCrSubsampleRatio.YCbCrSubsampleRatio444;
                switch ((horizontalRatio << 4) | verticalRatio)
                {
                    case 0x11:
                        ratio = YCbCrImage.YCbCrSubsampleRatio.YCbCrSubsampleRatio444;
                        break;
                    case 0x12:
                        ratio = YCbCrImage.YCbCrSubsampleRatio.YCbCrSubsampleRatio440;
                        break;
                    case 0x21:
                        ratio = YCbCrImage.YCbCrSubsampleRatio.YCbCrSubsampleRatio422;
                        break;
                    case 0x22:
                        ratio = YCbCrImage.YCbCrSubsampleRatio.YCbCrSubsampleRatio420;
                        break;
                    case 0x41:
                        ratio = YCbCrImage.YCbCrSubsampleRatio.YCbCrSubsampleRatio411;
                        break;
                    case 0x42:
                        ratio = YCbCrImage.YCbCrSubsampleRatio.YCbCrSubsampleRatio410;
                        break;
                }

                this.ycbcrImage = new YCbCrImage(8 * h0 * mxx, 8 * v0 * myy, ratio);

                if (this.ComponentCount == 4)
                {
                    int h3 = this.ComponentArray[3].HorizontalFactor;
                    int v3 = this.ComponentArray[3].VerticalFactor;

                    this.blackImage = JpegPixelArea.CreatePooled(8 * h3 * mxx, 8 * v3 * myy);
                }
            }
        }

        /// <summary>
        ///     Optimized method to pack bytes to the image from the CMYK color space.
        ///     This is faster than implicit casting as it avoids double packing.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="packed">The packed pixel.</param>
        /// <param name="c">The cyan component.</param>
        /// <param name="m">The magenta component.</param>
        /// <param name="y">The yellow component.</param>
        /// <param name="xx">The x-position within the image.</param>
        /// <param name="yy">The y-position within the image.</param>
        private void PackCmyk<TColor>(ref TColor packed, byte c, byte m, byte y, int xx, int yy)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            // Get keyline
            float keyline = (255 - this.blackImage[xx, yy]) / 255F;

            // Convert back to RGB. CMY are not inverted
            byte r = (byte)(((c / 255F) * (1F - keyline)).Clamp(0, 1) * 255);
            byte g = (byte)(((m / 255F) * (1F - keyline)).Clamp(0, 1) * 255);
            byte b = (byte)(((y / 255F) * (1F - keyline)).Clamp(0, 1) * 255);

            packed.PackFromBytes(r, g, b, 255);
        }

        /// <summary>
        ///     Optimized method to pack bytes to the image from the YCCK color space.
        ///     This is faster than implicit casting as it avoids double packing.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="packed">The packed pixel.</param>
        /// <param name="y">The y luminance component.</param>
        /// <param name="cb">The cb chroma component.</param>
        /// <param name="cr">The cr chroma component.</param>
        /// <param name="xx">The x-position within the image.</param>
        /// <param name="yy">The y-position within the image.</param>
        private void PackYcck<TColor>(ref TColor packed, byte y, byte cb, byte cr, int xx, int yy)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            // Convert the YCbCr part of the YCbCrK to RGB, invert the RGB to get
            // CMY, and patch in the original K. The RGB to CMY inversion cancels
            // out the 'Adobe inversion' described in the applyBlack doc comment
            // above, so in practice, only the fourth channel (black) is inverted.
            // TODO: We can speed this up further with Vector4
            int ccb = cb - 128;
            int ccr = cr - 128;

            // First convert from YCbCr to CMY
            float cyan = (y + (1.402F * ccr)).Clamp(0, 255) / 255F;
            float magenta = (y - (0.34414F * ccb) - (0.71414F * ccr)).Clamp(0, 255) / 255F;
            float yellow = (y + (1.772F * ccb)).Clamp(0, 255) / 255F;

            // Get keyline
            float keyline = (255 - this.blackImage[xx, yy]) / 255F;

            // Convert back to RGB
            byte r = (byte)(((1 - cyan) * (1 - keyline)).Clamp(0, 1) * 255);
            byte g = (byte)(((1 - magenta) * (1 - keyline)).Clamp(0, 1) * 255);
            byte b = (byte)(((1 - yellow) * (1 - keyline)).Clamp(0, 1) * 255);

            packed.PackFromBytes(r, g, b, 255);
        }

        /// <summary>
        ///     Processes the "Adobe" APP14 segment stores image encoding information for DCT filters.
        ///     This segment may be copied or deleted as a block using the Extra "Adobe" tag, but note that it is not
        ///     deleted by default when deleting all metadata because it may affect the appearance of the image.
        /// </summary>
        /// <param name="remaining">The remaining number of bytes in the stream.</param>
        private void ProcessApp14Marker(int remaining)
        {
            if (remaining < 12)
            {
                this.Skip(remaining);
                return;
            }

            this.ReadFull(this.Temp, 0, 12);
            remaining -= 12;

            if (this.Temp[0] == 'A' && this.Temp[1] == 'd' && this.Temp[2] == 'o' && this.Temp[3] == 'b'
                && this.Temp[4] == 'e')
            {
                this.adobeTransformValid = true;
                this.adobeTransform = this.Temp[11];
            }

            if (remaining > 0)
            {
                this.Skip(remaining);
            }
        }

        /// <summary>
        ///     Processes the App1 marker retrieving any stored metadata
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="remaining">The remaining bytes in the segment block.</param>
        /// <param name="image">The image.</param>
        private void ProcessApp1Marker<TColor>(int remaining, Image<TColor> image)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            if (remaining < 6)
            {
                this.Skip(remaining);
                return;
            }

            byte[] profile = new byte[remaining];
            this.ReadFull(profile, 0, remaining);

            if (profile[0] == 'E' && profile[1] == 'x' && profile[2] == 'i' && profile[3] == 'f' && profile[4] == '\0'
                && profile[5] == '\0')
            {
                image.ExifProfile = new ExifProfile(profile);
            }
        }

        /// <summary>
        ///     Processes the application header containing the JFIF identifier plus extra data.
        /// </summary>
        /// <param name="remaining">The remaining bytes in the segment block.</param>
        private void ProcessApplicationHeader(int remaining)
        {
            if (remaining < 5)
            {
                this.Skip(remaining);
                return;
            }

            this.ReadFull(this.Temp, 0, 13);
            remaining -= 13;

            // TODO: We should be using constants for this.
            this.isJfif = this.Temp[0] == 'J' && this.Temp[1] == 'F' && this.Temp[2] == 'I' && this.Temp[3] == 'F'
                          && this.Temp[4] == '\x00';

            if (this.isJfif)
            {
                this.horizontalResolution = (short)(this.Temp[9] + (this.Temp[10] << 8));
                this.verticalResolution = (short)(this.Temp[11] + (this.Temp[12] << 8));
            }

            if (remaining > 0)
            {
                this.Skip(remaining);
            }
        }

        internal void ProcessBlockImpl(
            int ah,
            Block8x8F* b,
            Block8x8F* temp1,
            Block8x8F* temp2,
            int* unzigPtr,
            Scan[] scan,
            int i,
            int zigStart,
            int zigEnd,
            int al,
            int[] dc,
            int compIndex,
            int @by,
            int mxx,
            int hi,
            int bx,
            Block8x8F* qt)
        {
            int huffmannIdx = (AcTable * HuffmanTree.ThRowSize) + scan[i].AcTableSelector;
            if (ah != 0)
            {
                this.Refine(b, ref this.HuffmanTrees[huffmannIdx], unzigPtr, zigStart, zigEnd, 1 << al);
            }
            else
            {
                int zig = zigStart;
                if (zig == 0)
                {
                    zig++;

                    // Decode the DC coefficient, as specified in section F.2.2.1.
                    byte value =
                        this.DecodeHuffman(
                            ref this.HuffmanTrees[(DcTable * HuffmanTree.ThRowSize) + scan[i].DcTableSelector]);
                    if (value > 16)
                    {
                        throw new ImageFormatException("Excessive DC component");
                    }

                    int deltaDC = this.Bits.ReceiveExtend(value, this);
                    dc[compIndex] += deltaDC;

                    // b[0] = dc[compIndex] << al;
                    Block8x8F.SetScalarAt(b, 0, dc[compIndex] << al);
                }

                if (zig <= zigEnd && this.EobRun > 0)
                {
                    this.EobRun--;
                }
                else
                {
                    // Decode the AC coefficients, as specified in section F.2.2.2.
                    // Huffman huffv = ;
                    for (; zig <= zigEnd; zig++)
                    {
                        byte value = this.DecodeHuffman(ref this.HuffmanTrees[huffmannIdx]);
                        byte val0 = (byte)(value >> 4);
                        byte val1 = (byte)(value & 0x0f);
                        if (val1 != 0)
                        {
                            zig += val0;
                            if (zig > zigEnd)
                            {
                                break;
                            }

                            int ac = this.Bits.ReceiveExtend(val1, this);

                            // b[Unzig[zig]] = ac << al;
                            Block8x8F.SetScalarAt(b, unzigPtr[zig], ac << al);
                        }
                        else
                        {
                            if (val0 != 0x0f)
                            {
                                this.EobRun = (ushort)(1 << val0);
                                if (val0 != 0)
                                {
                                    this.EobRun |= (ushort)this.DecodeBits(val0);
                                }

                                this.EobRun--;
                                break;
                            }

                            zig += 0x0f;
                        }
                    }
                }
            }

            if (this.IsProgressive)
            {
                if (zigEnd != Block8x8F.ScalarCount - 1 || al != 0)
                {
                    // We haven't completely decoded this 8x8 block. Save the coefficients.

                    // TODO!!!
                    // throw new NotImplementedException();
                    // this.ProgCoeffs[compIndex][((@by * mxx) * hi) + bx] = b.Clone();
                    this.ProgCoeffs[compIndex][((@by * mxx) * hi) + bx] = *b;

                    // At this point, we could execute the rest of the loop body to dequantize and
                    // perform the inverse DCT, to save early stages of a progressive image to the
                    // *image.YCbCr buffers (the whole point of progressive encoding), but in Go,
                    // the jpeg.Decode function does not return until the entire image is decoded,
                    // so we "continue" here to avoid wasted computation.
                    return;
                }
            }

            // Dequantize, perform the inverse DCT and store the block to the image.
            Block8x8F.UnZig(b, qt, unzigPtr);

            DCT.TransformIDCT(ref *b, ref *temp1, ref *temp2);

            var destChannel = this.GetDestinationChannel(compIndex);
            var destArea = destChannel.GetOffsetedSubAreaForBlock(bx, by);
            destArea.LoadColorsFrom(temp1, temp2);
        }

        private void ProcessComponentImpl(
            int i,
            ref Scan currentScan,
            Scan[] scan,
            ref int totalHv,
            ref Component currentComponent)
        {
            // Section B.2.3 states that "the value of Cs_j shall be different from
            // the values of Cs_1 through Cs_(j-1)". Since we have previously
            // verified that a frame's component identifiers (C_i values in section
            // B.2.2) are unique, it suffices to check that the implicit indexes
            // into comp are unique.
            for (int j = 0; j < i; j++)
            {
                if (currentScan.Index == scan[j].Index)
                {
                    throw new ImageFormatException("Repeated component selector");
                }
            }

            totalHv += currentComponent.HorizontalFactor * currentComponent.VerticalFactor;

            currentScan.DcTableSelector = (byte)(this.Temp[2 + (2 * i)] >> 4);
            if (currentScan.DcTableSelector > HuffmanTree.MaxTh)
            {
                throw new ImageFormatException("Bad DC table selector value");
            }

            currentScan.AcTableSelector = (byte)(this.Temp[2 + (2 * i)] & 0x0f);
            if (currentScan.AcTableSelector > HuffmanTree.MaxTh)
            {
                throw new ImageFormatException("Bad AC table selector  value");
            }
        }

        /// <summary>
        ///     Processes a Define Huffman Table marker, and initializes a huffman
        ///     struct from its contents. Specified in section B.2.4.2.
        /// </summary>
        /// <param name="remaining">The remaining bytes in the segment block.</param>
        private void ProcessDefineHuffmanTablesMarker(int remaining)
        {
            while (remaining > 0)
            {
                if (remaining < 17)
                {
                    throw new ImageFormatException("DHT has wrong length");
                }

                this.ReadFull(this.Temp, 0, 17);

                int tc = this.Temp[0] >> 4;
                if (tc > HuffmanTree.MaxTc)
                {
                    throw new ImageFormatException("Bad Tc value");
                }

                int th = this.Temp[0] & 0x0f;
                if (th > HuffmanTree.MaxTh || (!this.IsProgressive && (th > 1)))
                {
                    throw new ImageFormatException("Bad Th value");
                }

                int huffTreeIndex = (tc * HuffmanTree.ThRowSize) + th;
                this.HuffmanTrees[huffTreeIndex].ProcessDefineHuffmanTablesMarkerLoop(this, this.Temp, ref remaining);
            }
        }

        /// <summary>
        ///     Processes the DRI (Define Restart Interval Marker) Which specifies the interval between RSTn markers, in
        ///     macroblocks
        /// </summary>
        /// <param name="remaining">The remaining bytes in the segment block.</param>
        private void ProcessDefineRestartIntervalMarker(int remaining)
        {
            if (remaining != 2)
            {
                throw new ImageFormatException("DRI has wrong length");
            }

            this.ReadFull(this.Temp, 0, 2);
            this.RestartInterval = ((int)this.Temp[0] << 8) + (int)this.Temp[1];
        }

        /// <summary>
        ///     Processes the Define Quantization Marker and tables. Specified in section B.2.4.1.
        /// </summary>
        /// <param name="remaining">The remaining bytes in the segment block.</param>
        /// <exception cref="ImageFormatException">
        ///     Thrown if the tables do not match the header
        /// </exception>
        private void ProcessDqt(int remaining)
        {
            while (remaining > 0)
            {
                bool done = false;

                remaining--;
                byte x = this.ReadByte();
                byte tq = (byte)(x & 0x0f);
                if (tq > MaxTq)
                {
                    throw new ImageFormatException("Bad Tq value");
                }

                switch (x >> 4)
                {
                    case 0:
                        if (remaining < Block8x8F.ScalarCount)
                        {
                            done = true;
                            break;
                        }

                        remaining -= Block8x8F.ScalarCount;
                        this.ReadFull(this.Temp, 0, Block8x8F.ScalarCount);

                        for (int i = 0; i < Block8x8F.ScalarCount; i++)
                        {
                            this.QuantizationTables[tq][i] = this.Temp[i];
                        }

                        break;
                    case 1:
                        if (remaining < 2 * Block8x8F.ScalarCount)
                        {
                            done = true;
                            break;
                        }

                        remaining -= 2 * Block8x8F.ScalarCount;
                        this.ReadFull(this.Temp, 0, 2 * Block8x8F.ScalarCount);

                        for (int i = 0; i < Block8x8F.ScalarCount; i++)
                        {
                            this.QuantizationTables[tq][i] = (this.Temp[2 * i] << 8) | this.Temp[(2 * i) + 1];
                        }

                        break;
                    default:
                        throw new ImageFormatException("Bad Pq value");
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

        private void ProcessScanImpl(int i, ref Scan currentScan, Scan[] scan, ref int totalHv)
        {
            // Component selector.
            int cs = this.Temp[1 + (2 * i)];
            int compIndex = -1;
            for (int j = 0; j < this.ComponentCount; j++)
            {
                // Component compv = ;
                if (cs == this.ComponentArray[j].Identifier)
                {
                    compIndex = j;
                }
            }

            if (compIndex < 0)
            {
                throw new ImageFormatException("Unknown component selector");
            }

            currentScan.Index = (byte)compIndex;

            this.ProcessComponentImpl(i, ref currentScan, scan, ref totalHv, ref this.ComponentArray[compIndex]);
        }

        /// <summary>
        ///     Processes the Start of Frame marker.  Specified in section B.2.2.
        /// </summary>
        /// <param name="remaining">The remaining bytes in the segment block.</param>
        private void ProcessStartOfFrameMarker(int remaining)
        {
            if (this.ComponentCount != 0)
            {
                throw new ImageFormatException("Multiple SOF markers");
            }

            switch (remaining)
            {
                case 6 + (3 * 1): // Grayscale image.
                    this.ComponentCount = 1;
                    break;
                case 6 + (3 * 3): // YCbCr or RGB image.
                    this.ComponentCount = 3;
                    break;
                case 6 + (3 * 4): // YCbCrK or CMYK image.
                    this.ComponentCount = 4;
                    break;
                default:
                    throw new ImageFormatException("Incorrect number of components");
            }

            this.ReadFull(this.Temp, 0, remaining);

            // We only support 8-bit precision.
            if (this.Temp[0] != 8)
            {
                throw new ImageFormatException("Only 8-Bit precision supported.");
            }

            this.ImageHeight = (this.Temp[1] << 8) + this.Temp[2];
            this.ImageWidth = (this.Temp[3] << 8) + this.Temp[4];
            if (this.Temp[5] != this.ComponentCount)
            {
                throw new ImageFormatException("SOF has wrong length");
            }

            for (int i = 0; i < this.ComponentCount; i++)
            {
                this.ComponentArray[i].Identifier = this.Temp[6 + (3 * i)];

                // Section B.2.2 states that "the value of C_i shall be different from
                // the values of C_1 through C_(i-1)".
                for (int j = 0; j < i; j++)
                {
                    if (this.ComponentArray[i].Identifier == this.ComponentArray[j].Identifier)
                    {
                        throw new ImageFormatException("Repeated component identifier");
                    }
                }

                this.ComponentArray[i].Selector = this.Temp[8 + (3 * i)];
                if (this.ComponentArray[i].Selector > MaxTq)
                {
                    throw new ImageFormatException("Bad Tq value");
                }

                byte hv = this.Temp[7 + (3 * i)];
                int h = hv >> 4;
                int v = hv & 0x0f;
                if (h < 1 || h > 4 || v < 1 || v > 4)
                {
                    throw new ImageFormatException("Unsupported Luma/chroma subsampling ratio");
                }

                if (h == 3 || v == 3)
                {
                    throw new ImageFormatException("Lnsupported subsampling ratio");
                }

                switch (this.ComponentCount)
                {
                    case 1:

                        // If a JPEG image has only one component, section A.2 says "this data
                        // is non-interleaved by definition" and section A.2.2 says "[in this
                        // case...] the order of data units within a scan shall be left-to-right
                        // and top-to-bottom... regardless of the values of H_1 and V_1". Section
                        // 4.8.2 also says "[for non-interleaved data], the MCU is defined to be
                        // one data unit". Similarly, section A.1.1 explains that it is the ratio
                        // of H_i to max_j(H_j) that matters, and similarly for V. For grayscale
                        // images, H_1 is the maximum H_j for all components j, so that ratio is
                        // always 1. The component's (h, v) is effectively always (1, 1): even if
                        // the nominal (h, v) is (2, 1), a 20x5 image is encoded in three 8x8
                        // MCUs, not two 16x8 MCUs.
                        h = 1;
                        v = 1;
                        break;

                    case 3:

                        // For YCbCr images, we only support 4:4:4, 4:4:0, 4:2:2, 4:2:0,
                        // 4:1:1 or 4:1:0 chroma subsampling ratios. This implies that the
                        // (h, v) values for the Y component are either (1, 1), (1, 2),
                        // (2, 1), (2, 2), (4, 1) or (4, 2), and the Y component's values
                        // must be a multiple of the Cb and Cr component's values. We also
                        // assume that the two chroma components have the same subsampling
                        // ratio.
                        switch (i)
                        {
                            case 0:
                                {
                                    // Y.
                                    // We have already verified, above, that h and v are both
                                    // either 1, 2 or 4, so invalid (h, v) combinations are those
                                    // with v == 4.
                                    if (v == 4)
                                    {
                                        throw new ImageFormatException("Unsupported subsampling ratio");
                                    }

                                    break;
                                }

                            case 1:
                                {
                                    // Cb.
                                    if (this.ComponentArray[0].HorizontalFactor % h != 0
                                        || this.ComponentArray[0].VerticalFactor % v != 0)
                                    {
                                        throw new ImageFormatException("Unsupported subsampling ratio");
                                    }

                                    break;
                                }

                            case 2:
                                {
                                    // Cr.
                                    if (this.ComponentArray[1].HorizontalFactor != h
                                        || this.ComponentArray[1].VerticalFactor != v)
                                    {
                                        throw new ImageFormatException("Unsupported subsampling ratio");
                                    }

                                    break;
                                }
                        }

                        break;

                    case 4:

                        // For 4-component images (either CMYK or YCbCrK), we only support two
                        // hv vectors: [0x11 0x11 0x11 0x11] and [0x22 0x11 0x11 0x22].
                        // Theoretically, 4-component JPEG images could mix and match hv values
                        // but in practice, those two combinations are the only ones in use,
                        // and it simplifies the applyBlack code below if we can assume that:
                        // - for CMYK, the C and K channels have full samples, and if the M
                        // and Y channels subsample, they subsample both horizontally and
                        // vertically.
                        // - for YCbCrK, the Y and K channels have full samples.
                        switch (i)
                        {
                            case 0:
                                if (hv != 0x11 && hv != 0x22)
                                {
                                    throw new ImageFormatException("Unsupported subsampling ratio");
                                }

                                break;
                            case 1:
                            case 2:
                                if (hv != 0x11)
                                {
                                    throw new ImageFormatException("Unsupported subsampling ratio");
                                }

                                break;
                            case 3:
                                if (this.ComponentArray[0].HorizontalFactor != h
                                    || this.ComponentArray[0].VerticalFactor != v)
                                {
                                    throw new ImageFormatException("Unsupported subsampling ratio");
                                }

                                break;
                        }

                        break;
                }

                this.ComponentArray[i].HorizontalFactor = h;
                this.ComponentArray[i].VerticalFactor = v;
            }
        }

        private void ProcessStartOfScan22(int remaining)
        {
            Scan[] scan = new Scan[MaxComponents];
            int[] dc = new int[MaxComponents];

            DecoderScanProcessor p = default(DecoderScanProcessor);
            DecoderScanProcessor.Init(&p, this, remaining, scan);
            this.Bits = default(Bits);
            this.MakeImage(p.mxx, p.myy);
            p.ProcessBlocks(this, scan, ref dc);
            
        }

        /// <summary>
        ///     Processes the SOS (Start of scan marker).
        /// </summary>
        /// <remarks>
        ///     TODO: This also needs some significant refactoring to follow a more OO format.
        /// </remarks>
        /// <param name="remaining">The remaining bytes in the segment block.</param>
        /// <exception cref="ImageFormatException">
        ///     Missing SOF Marker
        ///     SOS has wrong length
        /// </exception>
        private void ProcessStartOfScan(int remaining)
        {
            if (this.ComponentCount == 0)
            {
                throw new ImageFormatException("Missing SOF marker");
            }

            if (remaining < 6 || 4 + (2 * this.ComponentCount) < remaining || remaining % 2 != 0)
            {
                throw new ImageFormatException("SOS has wrong length");
            }

            this.ReadFull(this.Temp, 0, remaining);
            byte scanComponentCount = this.Temp[0];

            int scanComponentCountX2 = 2 * scanComponentCount;
            if (remaining != 4 + scanComponentCountX2)
            {
                throw new ImageFormatException("SOS length inconsistent with number of components");
            }

            Scan[] scan = new Scan[MaxComponents];
            int totalHv = 0;

            for (int i = 0; i < scanComponentCount; i++)
            {
                this.ProcessScanImpl(i, ref scan[i], scan, ref totalHv);
            }

            // Section B.2.3 states that if there is more than one component then the
            // total H*V values in a scan must be <= 10.
            if (this.ComponentCount > 1 && totalHv > 10)
            {
                throw new ImageFormatException("Total sampling factors too large.");
            }

            // zigStart and zigEnd are the spectral selection bounds.
            // ah and al are the successive approximation high and low values.
            // The spec calls these values Ss, Se, Ah and Al.
            // For progressive JPEGs, these are the two more-or-less independent
            // aspects of progression. Spectral selection progression is when not
            // all of a block's 64 DCT coefficients are transmitted in one pass.
            // For example, three passes could transmit coefficient 0 (the DC
            // component), coefficients 1-5, and coefficients 6-63, in zig-zag
            // order. Successive approximation is when not all of the bits of a
            // band of coefficients are transmitted in one pass. For example,
            // three passes could transmit the 6 most significant bits, followed
            // by the second-least significant bit, followed by the least
            // significant bit.
            // For baseline JPEGs, these parameters are hard-coded to 0/63/0/0.
            int zigStart = 0;
            int zigEnd = Block8x8F.ScalarCount - 1;
            int ah = 0;
            int al = 0;

            if (this.IsProgressive)
            {
                zigStart = this.Temp[1 + scanComponentCountX2];
                zigEnd = this.Temp[2 + scanComponentCountX2];
                ah = this.Temp[3 + scanComponentCountX2] >> 4;
                al = this.Temp[3 + scanComponentCountX2] & 0x0f;

                if ((zigStart == 0 && zigEnd != 0) || zigStart > zigEnd || zigEnd >= Block8x8F.ScalarCount)
                {
                    throw new ImageFormatException("Bad spectral selection bounds");
                }

                if (zigStart != 0 && scanComponentCount != 1)
                {
                    throw new ImageFormatException("Progressive AC coefficients for more than one component");
                }

                if (ah != 0 && ah != al + 1)
                {
                    throw new ImageFormatException("Bad successive approximation values");
                }
            }

            // mxx and myy are the number of MCUs (Minimum Coded Units) in the image.
            int h0 = this.ComponentArray[0].HorizontalFactor;
            int v0 = this.ComponentArray[0].VerticalFactor;
            int mxx = (this.ImageWidth + (8 * h0) - 1) / (8 * h0);
            int myy = (this.ImageHeight + (8 * v0) - 1) / (8 * v0);

            if (this.IsProgressive)
            {
                for (int i = 0; i < scanComponentCount; i++)
                {
                    int compIndex = scan[i].Index;
                    if (this.ProgCoeffs[compIndex] == null)
                    {
                        int size = mxx * myy * this.ComponentArray[compIndex].HorizontalFactor
                                   * this.ComponentArray[compIndex].VerticalFactor;

                        this.ProgCoeffs[compIndex] = new Block8x8F[size];
                    }
                }
            }

            this.Bits = default(Bits);

            int mcu = 0;
            byte expectedRst = JpegConstants.Markers.RST0;

            // b is the decoded coefficients block, in natural (not zig-zag) order.
            // Block b;
            int[] dc = new int[MaxComponents];

            // bx and by are the location of the current block, in units of 8x8
            // blocks: the third block in the first row has (bx, by) = (2, 0).
            int bx, by, blockCount = 0;

            // TODO: A DecoderScanProcessor struct could clean up this mess
            Block8x8F b = default(Block8x8F);
            Block8x8F temp1 = default(Block8x8F);
            Block8x8F temp2 = default(Block8x8F);

            UnzigData unzig = UnzigData.Create();

            int* unzigPtr = unzig.Data;


            this.MakeImage(mxx, myy);


            for (int my = 0; my < myy; my++)
            {
                for (int mx = 0; mx < mxx; mx++)
                {
                    for (int i = 0; i < scanComponentCount; i++)
                    {
                        int compIndex = scan[i].Index;
                        int hi = this.ComponentArray[compIndex].HorizontalFactor;
                        int vi = this.ComponentArray[compIndex].VerticalFactor;

                        for (int j = 0; j < hi * vi; j++)
                        {
                            // The blocks are traversed one MCU at a time. For 4:2:0 chroma
                            // subsampling, there are four Y 8x8 blocks in every 16x16 MCU.
                            // For a baseline 32x16 pixel image, the Y blocks visiting order is:
                            // 0 1 4 5
                            // 2 3 6 7
                            // For progressive images, the interleaved scans (those with component count > 1)
                            // are traversed as above, but non-interleaved scans are traversed left
                            // to right, top to bottom:
                            // 0 1 2 3
                            // 4 5 6 7
                            // Only DC scans (zigStart == 0) can be interleave AC scans must have
                            // only one component.
                            // To further complicate matters, for non-interleaved scans, there is no
                            // data for any blocks that are inside the image at the MCU level but
                            // outside the image at the pixel level. For example, a 24x16 pixel 4:2:0
                            // progressive image consists of two 16x16 MCUs. The interleaved scans
                            // will process 8 Y blocks:
                            // 0 1 4 5
                            // 2 3 6 7
                            // The non-interleaved scans will process only 6 Y blocks:
                            // 0 1 2
                            // 3 4 5
                            if (scanComponentCount != 1)
                            {
                                bx = (hi * mx) + (j % hi);
                                by = (vi * my) + (j / hi);
                            }
                            else
                            {
                                int q = mxx * hi;
                                bx = blockCount % q;
                                by = blockCount / q;
                                blockCount++;
                                if (bx * 8 >= this.ImageWidth || by * 8 >= this.ImageHeight)
                                {
                                    continue;
                                }
                            }

                            int qtIndex = this.ComponentArray[compIndex].Selector;

                            // TODO: A DecoderScanProcessor struct could clean up this mess
                            // TODO: Reading & processing blocks should be done in 2 separate loops. The second one could be parallelized. The first one could be async.
                            fixed (Block8x8F* qtp = &this.QuantizationTables[qtIndex])
                            {
                                // Load the previous partially decoded coefficients, if applicable.
                                if (this.IsProgressive)
                                {
                                    int blockIndex = ((@by * mxx) * hi) + bx;
                                    
                                    fixed (Block8x8F* bp = &this.ProgCoeffs[compIndex][blockIndex])
                                    {
                                        Unsafe.CopyBlock(&b, bp, (uint) sizeof(Block8x8F) );
                                    }
                                }
                                else
                                {
                                    b.Clear();
                                }

                                this.ProcessBlockImpl(
                                        ah,
                                        &b,
                                        &temp1,
                                        &temp2,
                                        unzigPtr,
                                        scan,
                                        i,
                                        zigStart,
                                        zigEnd,
                                        al,
                                        dc,
                                        compIndex,
                                        @by,
                                        mxx,
                                        hi,
                                        bx,
                                        qtp);
                            }
                        }

                        // for j
                    }

                    // for i
                    mcu++;

                    if (this.RestartInterval > 0 && mcu % this.RestartInterval == 0 && mcu < mxx * myy)
                    {
                        // A more sophisticated decoder could use RST[0-7] markers to resynchronize from corrupt input,
                        // but this one assumes well-formed input, and hence the restart marker follows immediately.
                        this.ReadFull(this.Temp, 0, 2);
                        if (this.Temp[0] != 0xff || this.Temp[1] != expectedRst)
                        {
                            throw new ImageFormatException("Bad RST marker");
                        }

                        expectedRst++;
                        if (expectedRst == JpegConstants.Markers.RST7 + 1)
                        {
                            expectedRst = JpegConstants.Markers.RST0;
                        }

                        // Reset the Huffman decoder.
                        this.Bits = default(Bits);

                        // Reset the DC components, as per section F.2.1.3.1.
                        dc = new int[MaxComponents];

                        // Reset the progressive decoder state, as per section G.1.2.2.
                        this.EobRun = 0;
                    }
                }

                // for mx
            }

            // for my
        }

        /// <summary>
        ///     Decodes a successive approximation refinement block, as specified in section G.1.2.
        /// </summary>
        /// <param name="b">The block of coefficients</param>
        /// <param name="h">The Huffman tree</param>
        /// <param name="unzigPtr">Unzig ptr</param>
        /// <param name="zigStart">The zig-zag start index</param>
        /// <param name="zigEnd">The zig-zag end index</param>
        /// <param name="delta">The low transform offset</param>
        private void Refine(Block8x8F* b, ref HuffmanTree h, int* unzigPtr, int zigStart, int zigEnd, int delta)
        {
            // Refining a DC component is trivial.
            if (zigStart == 0)
            {
                if (zigEnd != 0)
                {
                    throw new ImageFormatException("Invalid state for zig DC component");
                }

                bool bit = this.DecodeBit();
                if (bit)
                {
                    int stuff = (int)Block8x8F.GetScalarAt(b, 0);

                    // int stuff = (int)b[0];
                    stuff |= delta;

                    // b[0] = stuff;
                    Block8x8F.SetScalarAt(b, 0, stuff);
                }

                return;
            }

            // Refining AC components is more complicated; see sections G.1.2.2 and G.1.2.3.
            int zig = zigStart;
            if (this.EobRun == 0)
            {
                for (; zig <= zigEnd; zig++)
                {
                    bool done = false;
                    int z = 0;
                    byte val = this.DecodeHuffman(ref h);
                    int val0 = val >> 4;
                    int val1 = val & 0x0f;

                    switch (val1)
                    {
                        case 0:
                            if (val0 != 0x0f)
                            {
                                this.EobRun = (ushort)(1 << val0);
                                if (val0 != 0)
                                {
                                    this.EobRun |= (ushort)this.DecodeBits(val0);
                                }

                                done = true;
                            }

                            break;
                        case 1:
                            z = delta;
                            bool bit = this.DecodeBit();
                            if (!bit)
                            {
                                z = -z;
                            }

                            break;
                        default:
                            throw new ImageFormatException("Unexpected Huffman code");
                    }

                    if (done)
                    {
                        break;
                    }

                    int blah = zig;

                    zig = this.RefineNonZeroes(b, zig, zigEnd, val0, delta, unzigPtr);
                    if (zig > zigEnd)
                    {
                        throw new ImageFormatException($"Too many coefficients {zig} > {zigEnd}");
                    }

                    if (z != 0)
                    {
                        // b[Unzig[zig]] = z;
                        Block8x8F.SetScalarAt(b, unzigPtr[zig], z);
                    }
                }
            }

            if (this.EobRun > 0)
            {
                this.EobRun--;
                this.RefineNonZeroes(b, zig, zigEnd, -1, delta, unzigPtr);
            }
        }

        /// <summary>
        ///     Refines non-zero entries of b in zig-zag order.
        ///     If <paramref name="nz" /> >= 0, the first <paramref name="nz" /> zero entries are skipped over.
        /// </summary>
        /// <param name="b">The block of coefficients</param>
        /// <param name="zig">The zig-zag start index</param>
        /// <param name="zigEnd">The zig-zag end index</param>
        /// <param name="nz">The non-zero entry</param>
        /// <param name="delta">The low transform offset</param>
        /// <param name="unzigPtr">Pointer to the  Jpeg Unzig data (data part of <see cref="UnzigData" />)</param>
        /// <returns>The <see cref="int" /></returns>
        private int RefineNonZeroes(Block8x8F* b, int zig, int zigEnd, int nz, int delta, int* unzigPtr)
        {
            for (; zig <= zigEnd; zig++)
            {
                int u = unzigPtr[zig];
                float bu = Block8x8F.GetScalarAt(b, u);

                // TODO: Are the equality comparsions OK with floating point values? Isn't an epsilon value necessary?
                if (bu == 0)
                {
                    if (nz == 0)
                    {
                        break;
                    }

                    nz--;
                    continue;
                }

                bool bit = this.DecodeBit();
                if (!bit)
                {
                    continue;
                }

                if (bu >= 0)
                {
                    // b[u] += delta;
                    Block8x8F.SetScalarAt(b, u, bu + delta);
                }
                else
                {
                    // b[u] -= delta;
                    Block8x8F.SetScalarAt(b, u, bu - delta);
                }
            }

            return zig;
        }

        /// <summary>
        ///     Skips the next n bytes.
        /// </summary>
        /// <param name="count">The number of bytes to ignore.</param>
        private void Skip(int count)
        {
            // Unread the overshot bytes, if any.
            if (this.bytes.UnreadableBytes != 0)
            {
                if (this.Bits.UnreadBits >= 8)
                {
                    this.UnreadByteStuffedByte();
                }

                this.bytes.UnreadableBytes = 0;
            }

            while (true)
            {
                int m = this.bytes.J - this.bytes.I;
                if (m > count)
                {
                    m = count;
                }

                this.bytes.I += m;
                count -= m;
                if (count == 0)
                {
                    break;
                }

                this.bytes.Fill(this.inputStream);
            }
        }

        /// <summary>
        ///     Undoes the most recent ReadByteStuffedByte call,
        ///     giving a byte of data back from bits to bytes. The Huffman look-up table
        ///     requires at least 8 bits for look-up, which means that Huffman decoding can
        ///     sometimes overshoot and read one or two too many bytes. Two-byte overshoot
        ///     can happen when expecting to read a 0xff 0x00 byte-stuffed byte.
        /// </summary>
        private void UnreadByteStuffedByte()
        {
            this.bytes.I -= this.bytes.UnreadableBytes;
            this.bytes.UnreadableBytes = 0;
            if (this.Bits.UnreadBits >= 8)
            {
                this.Bits.Accumulator >>= 8;
                this.Bits.UnreadBits -= 8;
                this.Bits.Mask >>= 8;
            }
        }

        /// <summary>
        ///     The EOF (End of File exception).
        ///     Thrown when the decoder encounters an EOF marker without a proceeding EOI (End Of Image) marker
        /// </summary>
        internal class EOFException : Exception
        {
        }

        /// <summary>
        ///     The missing ff00 exception.
        /// </summary>
        internal class MissingFF00Exception : Exception
        {
        }

        /// <summary>
        ///     The short huffman data exception.
        /// </summary>
        private class ShortHuffmanDataException : Exception
        {
        }
    }
}
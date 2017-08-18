// <copyright file="JpegDecoderCore.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;

    using ImageSharp.Formats.Jpg;
    using ImageSharp.Memory;
    using ImageSharp.PixelFormats;

    /// <summary>
    /// Performs the jpeg decoding operation.
    /// </summary>
    internal sealed unsafe class JpegDecoderCore : IDisposable
    {
        /// <summary>
        /// The maximum number of color components
        /// </summary>
        public const int MaxComponents = 4;

        /// <summary>
        /// The maximum number of quantization tables
        /// </summary>
        public const int MaxTq = 3;

        // Complex value type field + mutable + available to other classes = the field MUST NOT be private :P
#pragma warning disable SA1401 // FieldsMustBePrivate

        /// <summary>
        /// Encapsulates stream reading and processing data and operations for <see cref="JpegDecoderCore"/>.
        /// It's a value type for imporved data locality, and reduced number of CALLVIRT-s
        /// </summary>
        public InputProcessor InputProcessor;
#pragma warning restore SA401

        /// <summary>
        /// Lookup tables for converting YCbCr to Rgb
        /// </summary>
        private static YCbCrToRgbTables yCbCrToRgbTables = YCbCrToRgbTables.Create();

        /// <summary>
        /// The global configuration
        /// </summary>
        private readonly Configuration configuration;

        /// <summary>
        /// The App14 marker color-space
        /// </summary>
        private byte adobeTransform;

        /// <summary>
        /// Whether the image is in CMYK format with an App14 marker
        /// </summary>
        private bool adobeTransformValid;

        /// <summary>
        /// The black image to decode to.
        /// </summary>
        private JpegPixelArea blackImage;

        /// <summary>
        /// A grayscale image to decode to.
        /// </summary>
        private JpegPixelArea grayImage;

        /// <summary>
        /// The horizontal resolution. Calculated if the image has a JFIF header.
        /// </summary>
        private short horizontalResolution;

        /// <summary>
        /// Whether the image has a JFIF header
        /// </summary>
        private bool isJfif;

        /// <summary>
        /// Whether the image has a EXIF header
        /// </summary>
        private bool isExif;

        /// <summary>
        /// The vertical resolution. Calculated if the image has a JFIF header.
        /// </summary>
        private short verticalResolution;

        /// <summary>
        /// The full color image to decode to.
        /// </summary>
        private YCbCrImage ycbcrImage;

        /// <summary>
        /// Initializes a new instance of the <see cref="JpegDecoderCore" /> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="options">The options.</param>
        public JpegDecoderCore(Configuration configuration, IJpegDecoderOptions options)
        {
            this.IgnoreMetadata = options.IgnoreMetadata;
            this.configuration = configuration ?? Configuration.Default;
            this.HuffmanTrees = HuffmanTree.CreateHuffmanTrees();
            this.QuantizationTables = new Block8x8F[MaxTq + 1];
            this.Temp = new byte[2 * Block8x8F.ScalarCount];
            this.ComponentArray = new Component[MaxComponents];
            this.DecodedBlocks = new Buffer<DecodedBlock>[MaxComponents];
        }

        /// <summary>
        /// Gets the component array
        /// </summary>
        public Component[] ComponentArray { get; }

        /// <summary>
        /// Gets the huffman trees
        /// </summary>
        public HuffmanTree[] HuffmanTrees { get; }

        /// <summary>
        /// Gets the array of <see cref="Buffer{T}"/>-s storing the "raw" frequency-domain decoded blocks.
        /// We need to apply IDCT, dequantiazition and unzigging to transform them into color-space blocks.
        /// This is done by <see cref="ProcessBlocksIntoJpegImageChannels{TPixel}"/>.
        /// When <see cref="IsProgressive"/>==true, we are touching these blocks multiple times - each time we process a Scan.
        /// </summary>
        public Buffer<DecodedBlock>[] DecodedBlocks { get; }

        /// <summary>
        /// Gets the quantization tables, in zigzag order.
        /// </summary>
        public Block8x8F[] QuantizationTables { get; }

        /// <summary>
        /// Gets the temporary buffer used to store bytes read from the stream.
        /// TODO: Should be stack allocated, fixed sized buffer!
        /// </summary>
        public byte[] Temp { get; }

        /// <summary>
        /// Gets the number of color components within the image.
        /// </summary>
        public int ComponentCount { get; private set; }

        /// <summary>
        /// Gets the image height
        /// </summary>
        public int ImageHeight { get; private set; }

        /// <summary>
        /// Gets the image width
        /// </summary>
        public int ImageWidth { get; private set; }

        /// <summary>
        /// Gets the input stream.
        /// </summary>
        public Stream InputStream { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the image is interlaced (progressive)
        /// </summary>
        public bool IsProgressive { get; private set; }

        /// <summary>
        /// Gets the restart interval
        /// </summary>
        public int RestartInterval { get; private set; }

        /// <summary>
        /// Gets the number of MCU-s (Minimum Coded Units) in the image along the X axis
        /// </summary>
        public int MCUCountX { get; private set; }

        /// <summary>
        /// Gets the number of MCU-s (Minimum Coded Units) in the image along the Y axis
        /// </summary>
        public int MCUCountY { get; private set; }

        /// <summary>
        /// Gets the the total number of MCU-s (Minimum Coded Units) in the image.
        /// </summary>
        public int TotalMCUCount => this.MCUCountX * this.MCUCountY;

        /// <summary>
        /// Gets a value indicating whether the metadata should be ignored when the image is being decoded.
        /// </summary>
        public bool IgnoreMetadata { get; private set; }

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
            ImageMetaData metadata = new ImageMetaData();
            this.ProcessStream(metadata, stream, false);
            this.ProcessBlocksIntoJpegImageChannels<TPixel>();
            Image<TPixel> image = this.ConvertJpegPixelsToImagePixels<TPixel>(metadata);

            return image;
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            for (int i = 0; i < this.HuffmanTrees.Length; i++)
            {
                this.HuffmanTrees[i].Dispose();
            }

            foreach (Buffer<DecodedBlock> blockArray in this.DecodedBlocks)
            {
                blockArray?.Dispose();
            }

            this.ycbcrImage?.Dispose();
            this.InputProcessor.Dispose();
            this.grayImage.Pixels?.Dispose();
            this.blackImage.Pixels?.Dispose();
        }

        /// <summary>
        /// Gets the <see cref="JpegPixelArea"/> representing the channel at a given component index
        /// </summary>
        /// <param name="compIndex">The component index</param>
        /// <returns>The <see cref="JpegPixelArea"/> of the channel</returns>
        public JpegPixelArea GetDestinationChannel(int compIndex)
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
                        return new JpegPixelArea(this.ycbcrImage.YChannel);
                    case 1:
                        return new JpegPixelArea(this.ycbcrImage.CbChannel);
                    case 2:
                        return new JpegPixelArea(this.ycbcrImage.CrChannel);
                    case 3:
                        return this.blackImage;
                    default:
                        throw new ImageFormatException("Too many components");
                }
            }
        }

        /// <summary>
        /// Read metadata from stream and read the blocks in the scans into <see cref="DecodedBlocks"/>.
        /// </summary>
        /// <param name="metadata">The metadata</param>
        /// <param name="stream">The stream</param>
        /// <param name="metadataOnly">Whether to decode metadata only.</param>
        private void ProcessStream(ImageMetaData metadata, Stream stream, bool metadataOnly)
        {
            this.InputStream = stream;
            this.InputProcessor = new InputProcessor(stream, this.Temp);

            // Check for the Start Of Image marker.
            this.InputProcessor.ReadFull(this.Temp, 0, 2);
            if (this.Temp[0] != JpegConstants.Markers.XFF || this.Temp[1] != JpegConstants.Markers.SOI)
            {
                throw new ImageFormatException("Missing SOI marker.");
            }

            // Process the remaining segments until the End Of Image marker.
            bool processBytes = true;

            // we can't currently short circute progressive images so don't try.
            while (processBytes)
            {
                this.InputProcessor.ReadFull(this.Temp, 0, 2);
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
                    this.Temp[1] = this.InputProcessor.ReadByte();
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
                    marker = this.InputProcessor.ReadByte();
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
                this.InputProcessor.ReadFull(this.Temp, 0, 2);
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
                        if (metadataOnly && this.isJfif)
                        {
                            return;
                        }

                        break;
                    case JpegConstants.Markers.DHT:
                        if (metadataOnly)
                        {
                            this.InputProcessor.Skip(remaining);
                        }
                        else
                        {
                            this.ProcessDefineHuffmanTablesMarker(remaining);
                        }

                        break;
                    case JpegConstants.Markers.DQT:
                        if (metadataOnly)
                        {
                            this.InputProcessor.Skip(remaining);
                        }
                        else
                        {
                            this.ProcessDqt(remaining);
                        }

                        break;
                    case JpegConstants.Markers.SOS:
                        if (metadataOnly)
                        {
                            return;
                        }

                        // when this is a progressive image this gets called a number of times
                        // need to know how many times this should be called in total.
                        this.ProcessStartOfScan(remaining);
                        if (this.InputProcessor.UnexpectedEndOfStreamReached || !this.IsProgressive)
                        {
                            // if unexpeced EOF reached or this is not a progressive image we can stop processing bytes as we now have the image data.
                            processBytes = false;
                        }

                        break;
                    case JpegConstants.Markers.DRI:
                        if (metadataOnly)
                        {
                            this.InputProcessor.Skip(remaining);
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
                        this.ProcessApp1Marker(remaining, metadata);
                        break;
                    case JpegConstants.Markers.APP2:
                        this.ProcessApp2Marker(remaining, metadata);
                        break;
                    case JpegConstants.Markers.APP14:
                        this.ProcessApp14Marker(remaining);
                        break;
                    default:
                        if ((marker >= JpegConstants.Markers.APP0 && marker <= JpegConstants.Markers.APP15)
                            || marker == JpegConstants.Markers.COM)
                        {
                            this.InputProcessor.Skip(remaining);
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
        }

        /// <summary>
        /// Processes the SOS (Start of scan marker).
        /// </summary>
        /// <param name="remaining">The remaining bytes in the segment block.</param>
        /// <exception cref="ImageFormatException">
        /// Missing SOF Marker
        /// SOS has wrong length
        /// </exception>
        private void ProcessStartOfScan(int remaining)
        {
            JpegScanDecoder scan = default(JpegScanDecoder);
            JpegScanDecoder.InitStreamReading(&scan, this, remaining);
            this.InputProcessor.Bits = default(Bits);
            this.MakeImage();
            scan.DecodeBlocks(this);
        }

        /// <summary>
        /// Process the blocks in <see cref="DecodedBlocks"/> into Jpeg image channels (<see cref="YCbCrImage"/> and <see cref="JpegPixelArea"/>)
        /// <see cref="DecodedBlocks"/> are in a "raw" frequency-domain form. We need to apply IDCT, dequantization and unzigging to transform them into color-space blocks.
        /// We can copy these blocks into <see cref="JpegPixelArea"/>-s afterwards.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type</typeparam>
        private void ProcessBlocksIntoJpegImageChannels<TPixel>()
            where TPixel : struct, IPixel<TPixel>
        {
            Parallel.For(
                0,
                this.ComponentCount,
                componentIndex =>
                    {
                        JpegBlockProcessor processor = default(JpegBlockProcessor);
                        JpegBlockProcessor.Init(&processor, componentIndex);
                        processor.ProcessAllBlocks(this);
                    });
        }

        /// <summary>
        /// Convert the pixel data in <see cref="YCbCrImage"/> and/or <see cref="JpegPixelArea"/> into pixels of <see cref="Image{TPixel}"/>
        /// </summary>
        /// <typeparam name="TPixel">The pixel type</typeparam>
        /// <param name="metadata">The metadata for the image.</param>
        /// <returns>The decoded image.</returns>
        private Image<TPixel> ConvertJpegPixelsToImagePixels<TPixel>(ImageMetaData metadata)
            where TPixel : struct, IPixel<TPixel>
        {
            Image<TPixel> image = new Image<TPixel>(this.configuration, this.ImageWidth, this.ImageHeight, metadata);

            if (this.grayImage.IsInitialized)
            {
                this.ConvertFromGrayScale(image);
                return image;
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
                        this.ConvertFromYcck(image);
                    }
                    else if (this.adobeTransform == JpegConstants.Adobe.ColorTransformUnknown)
                    {
                        // Assume CMYK
                        this.ConvertFromCmyk(image);
                    }

                    return image;
                }

                if (this.ComponentCount == 3)
                {
                    if (this.IsRGB())
                    {
                        this.ConvertFromRGB(image);
                        return image;
                    }

                    this.ConvertFromYCbCr(image);
                    return image;
                }

                throw new ImageFormatException("JpegDecoder only supports RGB, CMYK and Grayscale color spaces.");
            }
            else
            {
                throw new ImageFormatException("Missing SOS marker.");
            }
        }

        /// <summary>
        /// Assigns the horizontal and vertical resolution to the image if it has a JFIF header.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The image to assign the resolution to.</param>
        private void AssignResolution<TPixel>(Image<TPixel> image)
            where TPixel : struct, IPixel<TPixel>
        {
            if (this.isJfif && this.horizontalResolution > 0 && this.verticalResolution > 0)
            {
                image.MetaData.HorizontalResolution = this.horizontalResolution;
                image.MetaData.VerticalResolution = this.verticalResolution;
            }
            else if (this.isExif)
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
        }

        /// <summary>
        /// Converts the image from the original CMYK image pixels.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The image.</param>
        private void ConvertFromCmyk<TPixel>(Image<TPixel> image)
            where TPixel : struct, IPixel<TPixel>
        {
            int scale = this.ComponentArray[0].HorizontalFactor / this.ComponentArray[1].HorizontalFactor;

            using (PixelAccessor<TPixel> pixels = image.Lock())
            {
                Parallel.For(
                    0,
                    image.Height,
                    y =>
                    {
                        // TODO: Simplify + optimize + share duplicate code across converter methods
                        int yo = this.ycbcrImage.GetRowYOffset(y);
                        int co = this.ycbcrImage.GetRowCOffset(y);

                        for (int x = 0; x < image.Width; x++)
                        {
                            byte cyan = this.ycbcrImage.YChannel[yo + x];
                            byte magenta = this.ycbcrImage.CbChannel[co + (x / scale)];
                            byte yellow = this.ycbcrImage.CrChannel[co + (x / scale)];

                            TPixel packed = default(TPixel);
                            this.PackCmyk(ref packed, cyan, magenta, yellow, x, y);
                            pixels[x, y] = packed;
                        }
                    });
            }

            this.AssignResolution(image);
        }

        /// <summary>
        /// Converts the image from the original grayscale image pixels.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The image.</param>
        private void ConvertFromGrayScale<TPixel>(Image<TPixel> image)
            where TPixel : struct, IPixel<TPixel>
        {
            Parallel.For(
                0,
                image.Height,
                image.Configuration.ParallelOptions,
                y =>
                    {
                        ref TPixel pixelRowBaseRef = ref image.GetPixelReference(0, y);

                        int yoff = this.grayImage.GetRowOffset(y);

                        for (int x = 0; x < image.Width; x++)
                        {
                            byte rgb = this.grayImage.Pixels[yoff + x];
                            ref TPixel pixel = ref Unsafe.Add(ref pixelRowBaseRef, x);
                            pixel.PackFromRgba32(new Rgba32(rgb, rgb, rgb, 255));
                        }
                    });

            this.AssignResolution(image);
        }

        /// <summary>
        /// Converts the image from the original RBG image pixels.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The image.</param>
        private void ConvertFromRGB<TPixel>(Image<TPixel> image)
            where TPixel : struct, IPixel<TPixel>
        {
            int scale = this.ComponentArray[0].HorizontalFactor / this.ComponentArray[1].HorizontalFactor;

            Parallel.For(
                0,
                image.Height,
                image.Configuration.ParallelOptions,
                y =>
                    {
                        // TODO: Simplify + optimize + share duplicate code across converter methods
                        int yo = this.ycbcrImage.GetRowYOffset(y);
                        int co = this.ycbcrImage.GetRowCOffset(y);
                        ref TPixel pixelRowBaseRef = ref image.GetPixelReference(0, y);

                        Rgba32 rgba = new Rgba32(0, 0, 0, 255);

                        for (int x = 0; x < image.Width; x++)
                        {
                            rgba.R = this.ycbcrImage.YChannel[yo + x];
                            rgba.G = this.ycbcrImage.CbChannel[co + (x / scale)];
                            rgba.B = this.ycbcrImage.CrChannel[co + (x / scale)];

                            ref TPixel pixel = ref Unsafe.Add(ref pixelRowBaseRef, x);
                            pixel.PackFromRgba32(rgba);
                        }
                    });

            this.AssignResolution(image);
        }

        /// <summary>
        /// Converts the image from the original YCbCr image pixels.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The image.</param>
        private void ConvertFromYCbCr<TPixel>(Image<TPixel> image)
            where TPixel : struct, IPixel<TPixel>
        {
            int scale = this.ComponentArray[0].HorizontalFactor / this.ComponentArray[1].HorizontalFactor;
            using (PixelAccessor<TPixel> pixels = image.Lock())
            {
                Parallel.For(
                 0,
                 image.Height,
                 image.Configuration.ParallelOptions,
                 y =>
                 {
                     // TODO. This Parallel loop doesn't give us the boost it should.
                     ref byte ycRef = ref this.ycbcrImage.YChannel[0];
                     ref byte cbRef = ref this.ycbcrImage.CbChannel[0];
                     ref byte crRef = ref this.ycbcrImage.CrChannel[0];
                     fixed (YCbCrToRgbTables* tables = &yCbCrToRgbTables)
                     {
                         // TODO: Simplify + optimize + share duplicate code across converter methods
                         int yo = this.ycbcrImage.GetRowYOffset(y);
                         int co = this.ycbcrImage.GetRowCOffset(y);

                         for (int x = 0; x < image.Width; x++)
                         {
                             int cOff = co + (x / scale);
                             byte yy = Unsafe.Add(ref ycRef, yo + x);
                             byte cb = Unsafe.Add(ref cbRef, cOff);
                             byte cr = Unsafe.Add(ref crRef, cOff);

                             TPixel packed = default(TPixel);
                             YCbCrToRgbTables.Pack(ref packed, tables, yy, cb, cr);
                             pixels[x, y] = packed;
                         }
                     }
                 });
            }

            this.AssignResolution(image);
        }

        /// <summary>
        /// Converts the image from the original YCCK image pixels.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The image.</param>
        private void ConvertFromYcck<TPixel>(Image<TPixel> image)
            where TPixel : struct, IPixel<TPixel>
        {
            int scale = this.ComponentArray[0].HorizontalFactor / this.ComponentArray[1].HorizontalFactor;

            Parallel.For(
                0,
                image.Height,
                y =>
                    {
                        // TODO: Simplify + optimize + share duplicate code across converter methods
                        int yo = this.ycbcrImage.GetRowYOffset(y);
                        int co = this.ycbcrImage.GetRowCOffset(y);
                        ref TPixel pixelRowBaseRef = ref image.GetPixelReference(0, y);

                        for (int x = 0; x < image.Width; x++)
                        {
                            byte yy = this.ycbcrImage.YChannel[yo + x];
                            byte cb = this.ycbcrImage.CbChannel[co + (x / scale)];
                            byte cr = this.ycbcrImage.CrChannel[co + (x / scale)];

                            ref TPixel pixel = ref Unsafe.Add(ref pixelRowBaseRef, x);
                            this.PackYcck(ref pixel, yy, cb, cr, x, y);
                        }
                    });

            this.AssignResolution(image);
        }

        /// <summary>
        /// Returns a value indicating whether the image in an RGB image.
        /// </summary>
        /// <returns>
        /// The <see cref="bool" />.
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
        /// Makes the image from the buffer.
        /// </summary>
        private void MakeImage()
        {
            if (this.grayImage.IsInitialized || this.ycbcrImage != null)
            {
                return;
            }

            if (this.ComponentCount == 1)
            {
                Buffer2D<byte> buffer = Buffer2D<byte>.CreateClean(8 * this.MCUCountX, 8 * this.MCUCountY);
                this.grayImage = new JpegPixelArea(buffer);
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

                this.ycbcrImage = new YCbCrImage(8 * h0 * this.MCUCountX, 8 * v0 * this.MCUCountY, ratio);

                if (this.ComponentCount == 4)
                {
                    int h3 = this.ComponentArray[3].HorizontalFactor;
                    int v3 = this.ComponentArray[3].VerticalFactor;

                    Buffer2D<byte> buffer = Buffer2D<byte>.CreateClean(8 * h3 * this.MCUCountX, 8 * v3 * this.MCUCountY);
                    this.blackImage = new JpegPixelArea(buffer);
                }
            }
        }

        /// <summary>
        /// Optimized method to pack bytes to the image from the CMYK color space.
        /// This is faster than implicit casting as it avoids double packing.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="packed">The packed pixel.</param>
        /// <param name="c">The cyan component.</param>
        /// <param name="m">The magenta component.</param>
        /// <param name="y">The yellow component.</param>
        /// <param name="xx">The x-position within the image.</param>
        /// <param name="yy">The y-position within the image.</param>
        private void PackCmyk<TPixel>(ref TPixel packed, byte c, byte m, byte y, int xx, int yy)
            where TPixel : struct, IPixel<TPixel>
        {
            // Get keyline
            float keyline = (255 - this.blackImage[xx, yy]) / 255F;

            // Convert back to RGB. CMY are not inverted
            byte r = (byte)(((c / 255F) * (1F - keyline)).Clamp(0, 1) * 255);
            byte g = (byte)(((m / 255F) * (1F - keyline)).Clamp(0, 1) * 255);
            byte b = (byte)(((y / 255F) * (1F - keyline)).Clamp(0, 1) * 255);

            packed.PackFromRgba32(new Rgba32(r, g, b));
        }

        /// <summary>
        /// Optimized method to pack bytes to the image from the YCCK color space.
        /// This is faster than implicit casting as it avoids double packing.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="packed">The packed pixel.</param>
        /// <param name="y">The y luminance component.</param>
        /// <param name="cb">The cb chroma component.</param>
        /// <param name="cr">The cr chroma component.</param>
        /// <param name="xx">The x-position within the image.</param>
        /// <param name="yy">The y-position within the image.</param>
        private void PackYcck<TPixel>(ref TPixel packed, byte y, byte cb, byte cr, int xx, int yy)
            where TPixel : struct, IPixel<TPixel>
        {
            // Convert the YCbCr part of the YCbCrK to RGB, invert the RGB to get
            // CMY, and patch in the original K. The RGB to CMY inversion cancels
            // out the 'Adobe inversion' described in the applyBlack doc comment
            // above, so in practice, only the fourth channel (black) is inverted.
            int ccb = cb - 128;
            int ccr = cr - 128;

            // Speed up the algorithm by removing floating point calculation
            // Scale by 65536, add .5F and truncate value. We use bit shifting to divide the result
            int r0 = 91881 * ccr; // (1.402F * 65536) + .5F
            int g0 = 22554 * ccb; // (0.34414F * 65536) + .5F
            int g1 = 46802 * ccr; // (0.71414F  * 65536) + .5F
            int b0 = 116130 * ccb; // (1.772F * 65536) + .5F

            // First convert from YCbCr to CMY
            float cyan = (y + (r0 >> 16)).Clamp(0, 255) / 255F;
            float magenta = (byte)(y - (g0 >> 16) - (g1 >> 16)).Clamp(0, 255) / 255F;
            float yellow = (byte)(y + (b0 >> 16)).Clamp(0, 255) / 255F;

            // Get keyline
            float keyline = (255 - this.blackImage[xx, yy]) / 255F;

            // Convert back to RGB
            byte r = (byte)(((1 - cyan) * (1 - keyline)).Clamp(0, 1) * 255);
            byte g = (byte)(((1 - magenta) * (1 - keyline)).Clamp(0, 1) * 255);
            byte b = (byte)(((1 - yellow) * (1 - keyline)).Clamp(0, 1) * 255);

            packed.PackFromRgba32(new Rgba32(r, g, b));
        }

        /// <summary>
        /// Processes the "Adobe" APP14 segment stores image encoding information for DCT filters.
        /// This segment may be copied or deleted as a block using the Extra "Adobe" tag, but note that it is not
        /// deleted by default when deleting all metadata because it may affect the appearance of the image.
        /// </summary>
        /// <param name="remaining">The remaining number of bytes in the stream.</param>
        private void ProcessApp14Marker(int remaining)
        {
            if (remaining < 12)
            {
                this.InputProcessor.Skip(remaining);
                return;
            }

            this.InputProcessor.ReadFull(this.Temp, 0, 12);
            remaining -= 12;

            if (this.Temp[0] == 'A' && this.Temp[1] == 'd' && this.Temp[2] == 'o' && this.Temp[3] == 'b'
                && this.Temp[4] == 'e')
            {
                this.adobeTransformValid = true;
                this.adobeTransform = this.Temp[11];
            }

            if (remaining > 0)
            {
                this.InputProcessor.Skip(remaining);
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
                this.InputProcessor.Skip(remaining);
                return;
            }

            byte[] profile = new byte[remaining];
            this.InputProcessor.ReadFull(profile, 0, remaining);

            if (profile[0] == 'E' &&
                profile[1] == 'x' &&
                profile[2] == 'i' &&
                profile[3] == 'f' &&
                profile[4] == '\0' &&
                profile[5] == '\0')
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
                this.InputProcessor.Skip(remaining);
                return;
            }

            byte[] identifier = new byte[Icclength];
            this.InputProcessor.ReadFull(identifier, 0, Icclength);
            remaining -= Icclength; // we have read it by this point

            if (identifier[0] == 'I' &&
                identifier[1] == 'C' &&
                identifier[2] == 'C' &&
                identifier[3] == '_' &&
                identifier[4] == 'P' &&
                identifier[5] == 'R' &&
                identifier[6] == 'O' &&
                identifier[7] == 'F' &&
                identifier[8] == 'I' &&
                identifier[9] == 'L' &&
                identifier[10] == 'E' &&
                identifier[11] == '\0')
            {
                byte[] profile = new byte[remaining];
                this.InputProcessor.ReadFull(profile, 0, remaining);

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
                // not an ICC profile we can handle read the remaining so we can carry on and ignore this.
                this.InputProcessor.Skip(remaining);
            }
        }

        /// <summary>
        /// Processes the application header containing the JFIF identifier plus extra data.
        /// </summary>
        /// <param name="remaining">The remaining bytes in the segment block.</param>
        private void ProcessApplicationHeader(int remaining)
        {
            if (remaining < 5)
            {
                this.InputProcessor.Skip(remaining);
                return;
            }

            this.InputProcessor.ReadFull(this.Temp, 0, 13);
            remaining -= 13;

            // TODO: We should be using constants for this.
            this.isJfif = this.Temp[0] == 'J' &&
                          this.Temp[1] == 'F' &&
                          this.Temp[2] == 'I' &&
                          this.Temp[3] == 'F' &&
                          this.Temp[4] == '\x00';

            if (this.isJfif)
            {
                this.horizontalResolution = (short)(this.Temp[9] + (this.Temp[8] << 8));
                this.verticalResolution = (short)(this.Temp[11] + (this.Temp[10] << 8));
            }

            if (remaining > 0)
            {
                this.InputProcessor.Skip(remaining);
            }
        }

        /// <summary>
        /// Processes a Define Huffman Table marker, and initializes a huffman
        /// struct from its contents. Specified in section B.2.4.2.
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

                this.InputProcessor.ReadFull(this.Temp, 0, 17);

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
                this.HuffmanTrees[huffTreeIndex].ProcessDefineHuffmanTablesMarkerLoop(
                    ref this.InputProcessor,
                    this.Temp,
                    ref remaining);
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
                throw new ImageFormatException("DRI has wrong length");
            }

            this.InputProcessor.ReadFull(this.Temp, 0, remaining);
            this.RestartInterval = (this.Temp[0] << 8) + this.Temp[1];
        }

        /// <summary>
        /// Processes the Define Quantization Marker and tables. Specified in section B.2.4.1.
        /// </summary>
        /// <param name="remaining">The remaining bytes in the segment block.</param>
        /// <exception cref="ImageFormatException">
        /// Thrown if the tables do not match the header
        /// </exception>
        private void ProcessDqt(int remaining)
        {
            while (remaining > 0)
            {
                bool done = false;

                remaining--;
                byte x = this.InputProcessor.ReadByte();
                int tq = x & 0x0F;
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
                        this.InputProcessor.ReadFull(this.Temp, 0, Block8x8F.ScalarCount);

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
                        this.InputProcessor.ReadFull(this.Temp, 0, 2 * Block8x8F.ScalarCount);

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

        /// <summary>
        /// Processes the Start of Frame marker.  Specified in section B.2.2.
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

            this.InputProcessor.ReadFull(this.Temp, 0, remaining);

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

            int h0 = this.ComponentArray[0].HorizontalFactor;
            int v0 = this.ComponentArray[0].VerticalFactor;
            this.MCUCountX = (this.ImageWidth + (8 * h0) - 1) / (8 * h0);
            this.MCUCountY = (this.ImageHeight + (8 * v0) - 1) / (8 * v0);

            // As a preparation for parallelizing Scan decoder, we also allocate DecodedBlocks in the non-progressive case!
            for (int i = 0; i < this.ComponentCount; i++)
            {
                int count = this.TotalMCUCount * this.ComponentArray[i].HorizontalFactor
                           * this.ComponentArray[i].VerticalFactor;
                this.DecodedBlocks[i] = Buffer<DecodedBlock>.CreateClean(count);
            }
        }
    }
}

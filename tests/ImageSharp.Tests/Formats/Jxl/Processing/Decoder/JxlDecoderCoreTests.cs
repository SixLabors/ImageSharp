// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

#nullable enable

using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Jxl.Cms;
using SixLabors.ImageSharp.Formats.Jxl.IO;
using SixLabors.ImageSharp.Formats.Jxl.IO.Container;
using SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;
using SixLabors.ImageSharp.Formats.Jxl.IO.Jpeg;
using SixLabors.ImageSharp.Formats.Jxl.IO.Jpeg.Data;
using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.Processing.Decoder;

public class JxlDecoderCoreTests
{
    // Unknown boxes and their data for testing.
    private const string Unk1BoxType = "unk1";
    private const string Unk1BoxContents = "abcdefghijklmnopqrstuvwxyz";
    private const string Unk2BoxType = "unk2";
    private const string Unk2BoxContents = "0123456789";
    private const string Unk3BoxType = "unk3";
    private const string Unk3BoxContents = "ABCDEF123456";

    private const int BoxBrobExifSize = 64;
    private const int ExifUncompressedSize = 94;

    private static ReadOnlySpan<byte> BoxBrobExif => "\0\0\0@brobExif\xA1\xE8\x02\xC0\x7F\xA4v\xAA5\xC4\xF0\x17=?\xB7{\x1B\x1F\xCC\xDA\x8CQX17PT\"\xAE\0\0\x82s\x8C\xCBt\xDB\xC8\xD0k\x10\xBE\x18\x84\xBFl$\xD6c#\x01\b"u8;

    private static ReadOnlySpan<byte> ExifUncompressed => "\"\\0\\0\\0\\0MM\\0*\\0\\0\\0\\b\\0\\5\\1\\22\\0\\3\\0\\0\\0\\1\\0\\5\\0\\0\\1\\32\\0\\5\\0\\0\\0\\1\\0\\0\\0J\\1\\33\\0\\5\\0\\0\\0\\1\\0\\0\\0R\\1(\\0\\3\\0\\0\\0\\1\\0\\1\\0\\0\\2\\23\\0\\3\\0\\0\\0\\1\\0\\1\\0\\0\\0\\0\\0\\0\\0\\0\\0\\1\\0\\0\\0\\1\\0\\0\\0\\1\\0\\0\\0\\1\""u8;

    /// <summary>
    /// What type of codestream format in the boxes to use for testing.
    /// </summary>
    private enum CodeStreamBoxFormat : byte
    {
        /// <summary>
        /// Do not use box format at all. Only pure codestream.
        /// </summary>
        None,

        /// <summary>
        /// Have a single codestream box, with its actual size given in
        /// the box header.
        /// </summary>
        Single,

        /// <summary>
        /// Have a single codestream box, with box size 0 (final box
        /// running to end).
        /// </summary>
        SingleZeroTerminated,

        /// <summary>
        /// Single codestream box, with another unknown box behind it
        /// </summary>
        SingleOther,

        /// <summary>
        /// Have multiple partial codestream boxes
        /// </summary>
        Multi,

        /// <summary>
        /// Have multiple partial codestream boxes, with final box size 0
        /// (running to end)
        /// </summary>
        MultiZeroTerminated,

        /// <summary>
        /// Have multiple partial codestream boxes, terminated by
        /// non-codestream box
        /// </summary>
        MultiOtherTerminated,

        /// <summary>
        /// Have multiple partial codestream boxes, terminated by non-codestream
        /// box that has its size set to 0 (running to end)
        /// </summary>
        MultiOtherZeroTerminated,

        /// <summary>
        /// Have multiple partial codestream boxes, and the first one has a content
        /// of zero length
        /// </summary>
        MultiFirstEmpty,

        /// <summary>
        /// Have multiple partial codestream boxes, and the last one has a content
        /// of zero length and there is an unknown empty box at the end
        /// </summary>
        MultiLastEmptyOther,

        /// <summary>
        /// Have a compressed exif box before a regular codestream box.
        /// </summary>
        BrobExif,

        /// <summary>
        /// Not a value but used for counting amount of enum entries
        /// </summary>
        NumEntries
    }

    private struct PixelTestConfiguration
    {
        private static readonly string[] Colors = [string.Empty, "G", "GA", "RGB", "RGBA"];

        public bool Grayscale { get; set; }

        public bool IncludeAlpha { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public PreviewMode PreviewMode { get; set; }

        public bool AddIntrinsicSize { get; set; }

        public ByteOrder Endianness { get; set; }

        public JxlDataType DataType { get; set; }

        public uint OutputChannels { get; set; }

        public CodeStreamBoxFormat AddContainer { get; set; }

        public bool UseCallback { get; set; }

        public bool SetBufferEarly { get; set; }

        public bool UseResizableRunner { get; set; }

        public JxlExifOrientation Orientation { get; set; }

        public bool KeepOrientation { get; set; }

        public int Upsampling { get; set; }

        // For debugging purposes
        public void Format(StringBuilder sb)
        {
            // Dimensions
            sb.Append("Dimensions: ");
            sb.Append(this.Width);
            sb.Append('x');
            sb.Append(this.Height);
            sb.AppendLine();

            // Colors
            sb.Append("Colors: ");
            sb.Append(Colors[(this.Grayscale ? 1 : 3) + (this.IncludeAlpha ? 1 : 0)]);
            sb.Append(" to ");
            sb.Append(Colors[this.OutputChannels]);
            sb.AppendLine();

            // Data type
            sb.Append("Data type: ");
            sb.Append(this.DataType switch
            {
                JxlDataType.Byte => "u8",
                JxlDataType.UInt16 => "u16",
                JxlDataType.Single => "f32",
                JxlDataType.Half => "f16",
                _ => "???"
            });
            sb.AppendLine();

            // Endianness
            sb.Append("Endianness: ");
            sb.Append(this.Endianness is ByteOrder.BigEndian ? "Big Endian" : "Little Endian");
            sb.AppendLine();

            // File type
            sb.Append("File type: ");
            sb.Append(this.AddContainer == CodeStreamBoxFormat.None ? "Plain codestream" : "Container");
            sb.AppendLine();

            // Preview
            sb.Append("Preview type: ");
            sb.Append(this.PreviewMode switch
            {
                PreviewMode.NoPreview => "No preview",
                PreviewMode.SmallPreview => "Small preview",
                PreviewMode.BigPreview => "Big preview",
                _ => "???"
            });
            sb.AppendLine();

            // Other flags
            sb.Append("Other flags: ");

            if (this.AddIntrinsicSize)
            {
                sb.Append("Intrinsic Size; ");
            }

            if (this.UseCallback)
            {
                sb.Append("Use Callback; ");
            }

            if (this.SetBufferEarly)
            {
                sb.Append("Set Buffer Early; ");
            }

            if (this.UseResizableRunner)
            {
                sb.Append("Use Resizable Runner; ");
            }

            if (this.Orientation != (JxlExifOrientation)1)
            {
                sb.Append("Orientation=" + this.Orientation + "; ");
            }

            if (this.KeepOrientation)
            {
                sb.Append("Keep Orientation; ");
            }

            if (this.Upsampling > 1)
            {
                sb.Append(null, $"Upsampling x{this.Upsampling}; ");
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new();
            this.Format(sb);
            return sb.ToString();
        }
    }

    private enum PreviewMode : byte
    {
        NoPreview,
        SmallPreview,
        BigPreview,
        NumPreviewModes
    }

    private static void AppendU32BE(uint u32, List<byte> bytes)
    {
        bytes.Add((byte)(u32 >> 24));
        bytes.Add((byte)(u32 >> 16));
        bytes.Add((byte)(u32 >> 8));
        bytes.Add((byte)(u32 >> 0));
    }

    private static void AppendTestBox(ReadOnlySpan<char> type, ReadOnlySpan<char> contents, bool unbounded, List<byte> bytes)
    {
        AppendU32BE((uint)(contents.Length + 8), bytes);
        bytes.Add((byte)type[0]);
        bytes.Add((byte)type[1]);
        bytes.Add((byte)type[2]);
        bytes.Add((byte)type[3]);

        ReadOnlySpan<byte> contentsU = MemoryMarshal.Cast<char, byte>(type);
        for (int i = 0; i < contentsU.Length; i++)
        {
            bytes.Add(contentsU[i]);
        }
    }

    private static void GeneratePreview(Configuration configuration, PreviewMode previewMode, JxlImageBundle ib)
    {
        if (previewMode == PreviewMode.SmallPreview)
        {
            Assert.True(ib.ShrinkTo(ib.XSize / 7, ib.YSize / 7));
        }
        else if (previewMode == PreviewMode.BigPreview)
        {
            static void Upsample7(JxlPlane<float> input, JxlPlane<float> output)
            {
                for (int y = 0; y < output.YSize; ++y)
                {
                    Span<float> rowY = output.GetRow(y);
                    Span<float> rowYDiv7 = input.GetRow(y / 7);

                    for (int x = 0; x < output.XSize; ++x)
                    {
                        rowY[x] = rowYDiv7[x / 7];
                    }
                }
            }

            JxlImage3F preview = new(configuration, ib.XSize * 7, ib.YSize * 7);

            for (int c = 0; c < 3; c++)
            {
                Upsample7(ib.Color!.Plane(c), preview.Plane(c));
            }

            List<JxlImageF> extraChannels = [];

            foreach (JxlImageF extraChannel in ib.EnumerateExtraChannels())
            {
                JxlImageF ec = new(configuration, ib.XSize * 7, ib.YSize * 7);

                Upsample7(extraChannel, ec);
                extraChannels.Add(ec);
            }

            ib.RemoveColor();
            ib.ClearExtraChannels();

            Assert.True(ib.SetFromImage(preview, ib.CurrentColorEncoding!));
            Assert.True(ib.TrySetExtraChannels(extraChannels));
        }
    }

    private static List<byte> CreateTestJpegXLCodeStream(List<byte> pixels, int width, int height, int numChannels, TestCodestreamParameters parameters)
        => CreateTestJpegXLCodeStream(TestEnvironment.Configuration, CollectionsMarshal.AsSpan(pixels), width, height, numChannels, parameters);

    // Input pixels always given as 16-bit RGBA, 8 bytes per pixel.
    // include_alpha determines if the encoded image should contain the alpha
    // channel.
    // add_icc_profile: if false, encodes the image as sRGB using the JXL fields,
    // for grayscale or RGB images. If true, encodes the image using the ICC profile
    // returned by GetIccTestProfile, without the JXL fields, this requires the
    // image is RGB, not grayscale.
    // Providing jpeg_codestream will populate the jpeg_codestream with compressed
    // JPEG bytes, and make it possible to reconstruct those exact JPEG bytes using
    // the return value _if_ add_container indicates a box format.
    //
    // (comment from libjxl source)
    private static List<byte> CreateTestJpegXLCodeStream(Configuration configuration, Span<byte> pixels, int width, int height, int numChannels, TestCodestreamParameters parameters)
    {
        bool grayscale = numChannels <= 2;
        bool haveAlpha = (numChannels & 1) == 0;
        bool includeAlpha = haveAlpha && parameters.JpegCodestream is null;
        int bitDepth = parameters.JpegCodestream is null ? 16 : 8;

        JxlColorEncoding colorEncoding = new();

        if (parameters.AddIccProfile)
        {
            Assert.False(grayscale);
            Assert.Equal(0, parameters.ColorSpace?.Length);
            Assert.True(colorEncoding.SetIcc(GetIccTestProfile(), JxlGetDefaultCms()));
        }
        else if (parameters.ColorSpace?.Length > 0)
        {
            JxlColorEncoding c = new();
            Assert.True(ParseDescription(parameters.ColorSpace!, c));
            Assert.True(colorEncoding.FromExternal(c));
            Assert.Equal(grayscale, colorEncoding.IsGray);
        }
        else
        {
            colorEncoding = JxlColorEncoding.SRgb(grayscale);
        }

        JxlCodecIO io = new();
        io.SetSize(width, height);

        io.CodecMetadata.ImageMetadata!.SetUIntSamples(bitDepth);

        if (includeAlpha)
        {
            io.CodecMetadata.ImageMetadata!.SetAlphaBits(bitDepth);
        }

        if (parameters.IntensityTarget != 0)
        {
            io.CodecMetadata.ImageMetadata!.SetIntensityTarget(parameters.IntensityTarget);
        }

        JxlPixelFormat format = new()
        {
            Channels = numChannels,
            DataType = JxlDataType.UInt16,
            Endianness = ByteOrder.BigEndian,
            Align = 0
        };

        io.CodecMetadata.ImageMetadata.ColorEncoding = colorEncoding;

        JxlExternalImageDecoder.ConvertFromExternal(configuration, pixels, width, height, colorEncoding, 16, format, io.Main, includeAlpha);

        List<byte> encodedJpegBytes = [];

        if (parameters.JpegCodestream is not null)
        {
            if (CanDecode(JxlCodecType.Jpeg))
            {
                List<byte> jpegBytes = [];

                // We encode the JPEG and ensure that the JPEG writer in JPEG XL
                // codec for use in JPEG<->JPEG XL Lossless coding can properly
                // code data.
                // First let's set up the JPEG encoder parameters, really the only
                // thing we have to configure there is set the quality to 70%.
                JpegEncoder encoder = new()
                {
                    Quality = 70
                };

                // The following chain of if's is just encoding the image with input pixel format
                // depending on the number of channels. Since our data type is always
                // 16-bits per channel, that means a single grayscale channel maps
                // very well into L16. 3 channels map into Rgb48. We can have up to
                // 4 channels in this context, and that's Rgba64.
                if (numChannels == 1)
                {
                    // Each sample is 16-bit and we have one grayscale channel, so
                    // feed the encoder an L16.
                    // Total bit depth is 1 * 16.
                    EncodeJpeg<L16>(pixels);
                }
                else if (numChannels == 2)
                {
                    // 2 channel is not a thing. 🚫
                    throw new InvalidOperationException("Cannot encode a 2-channel JPEG");
                }
                else if (numChannels == 3)
                {
                    // RGB with each component being 16-bit.
                    // Total bit depth is 3 * 16.
                    EncodeJpeg<Rgb48>(pixels);
                }
                else if (numChannels == 4)
                {
                    // RGBA with each component being 16-bit.
                    // Total bit depth is 4 * 16.
                    EncodeJpeg<Rgba64>(pixels);
                }
                else
                {
                    // Only 1, 3, and 4 channels are allowed.
                    throw new InvalidOperationException($"Invalid number of channels: {numChannels}");
                }

                // JPEG encoded successfully. So parse its components.
                JpegData jpegData = new();
                Assert.True(JpegReader.ReadJpeg(CollectionsMarshal.AsSpan(encodedJpegBytes), JpegReadMode.ReadEverything, jpegData));
                Assert.True(EncodeJpegData(io.Main.JpegData!, encodedJpegBytes, parameters.CompressParameters));
                io.CodecMetadata.ImageMetadata!.XybEncoded = false;

                // This is a simple helper so we don't have to duplicate
                // code. It just takes in a pixel format and encodes a
                // JPEG using ImageSharp existing JPEG codec. Very simple.
                // We should also pass the pixels Span explicitly because
                // a local function cannot use a ByRef variable.
                void EncodeJpeg<TPixelFormat>(Span<byte> pixels)
                    where TPixelFormat : unmanaged, IPixel<TPixelFormat>
                {
                    using Image<TPixelFormat> data = new(width, height);

                    // Just some copying from `pixels` variable to the data image.
                    Assert.True(data.DangerousTryGetSinglePixelMemory(out Memory<TPixelFormat> memory));
                    Span<byte> bytes = MemoryMarshal.Cast<TPixelFormat, byte>(memory.Span);
                    Assert.Equal(bytes.Length, pixels.Length);
                    pixels.CopyTo(bytes);

                    // We store a temporary MemoryStream here and this is where the
                    // encoded JPEG file output will go.
                    using MemoryStream memoryStream = new();
                    data.Save(memoryStream, encoder);

                    // JPEG file is saved inside memoryStream, now we have
                    // to copy it to a List<T>.
                    // encodedJpegBytes is initially empty, so we can construct
                    // a new List<T> on top. Note that memoryStream.ToArray()
                    // allocates a new array for us, but in theory it should
                    // be faster than calling .Add() elementwise.
                    encodedJpegBytes = [.. memoryStream.ToArray()];
                }
            }
            else
            {
                throw new InvalidOperationException("JPEG cannot be encoded");
            }
        }

        if (parameters.PreviewMode != PreviewMode.NoPreview)
        {
            io.PreviewFrame = io.Main.Copy(configuration);
            GeneratePreview(configuration, parameters.PreviewMode, io.PreviewFrame);
            io.CodecMetadata.ImageMetadata!.HavePreview = true;

            // This will throw on failure.
            io.CodecMetadata.ImageMetadata.PreviewSize.Set(io.PreviewFrame.XSize, io.PreviewFrame.YSize);
        }

        if (parameters.AddIntrinsicSize)
        {
            // This will also throw on failure.
            io.CodecMetadata.ImageMetadata!.PreviewSize.Set(io.PreviewFrame.XSize / 3, io.PreviewFrame.YSize / 3);
        }

        io.CodecMetadata.ImageMetadata.Orientation = (int)parameters.Orientation;

        List<byte> compressed = [];
        EncodeFile(parameters.CompressParameters, io, compressed);

        CodeStreamBoxFormat addContainer = parameters.BoxFormat;

        if (addContainer != CodeStreamBoxFormat.None)
        {
            // We can encode the container format.
            //
            // Header with signature box and ftyp box
            Span<byte> header =
            [
                0,    0,    0,    0xc,  0x4a, 0x58, 0x4c, 0x20,
                0xd,  0xa,  0x87, 0xa,  0,    0,    0,    0x14,
                0x66, 0x74, 0x79, 0x70, 0x6a, 0x78, 0x6c, 0x20,
                0,    0,    0,    0,    0x6a, 0x78, 0x6c, 0x20
            ];

            bool isMulti = addContainer is CodeStreamBoxFormat.Multi
                or CodeStreamBoxFormat.MultiZeroTerminated
                or CodeStreamBoxFormat.MultiOtherTerminated
                or CodeStreamBoxFormat.MultiOtherZeroTerminated
                or CodeStreamBoxFormat.MultiFirstEmpty
                or CodeStreamBoxFormat.MultiLastEmptyOther;

            if (isMulti)
            {
                int third = compressed.Count / 3;
                Span<byte> compressedData = CollectionsMarshal.AsSpan(compressed);

                Span<byte> compressed0 = compressedData[..third];
                Span<byte> compressed1 = compressedData[third..(2 * third)];
                Span<byte> compressed2 = compressedData[(2 * third)..];

                List<byte> c = [];
                c.AddRange(header);

                if (parameters.JpegCodestream is not null)
                {
                    AppendBoxHeader(JxlBoxHeader.TypeFromString("jbrd"), encodedJpegBytes.Count, false, c);
                    c.AddRange(encodedJpegBytes);
                }

                uint jxlpIndex = 0;

                if (addContainer == CodeStreamBoxFormat.MultiFirstEmpty)
                {
                    AppendU32BE(12, c);
                    c.Add((byte)'j');
                    c.Add((byte)'x');
                    c.Add((byte)'l');
                    c.Add((byte)'p');
                    AppendU32BE(jxlpIndex++, c);
                }

                // First codestream part
                AppendU32BE((uint)compressed0.Length + 12, c);
                c.Add((byte)'j');
                c.Add((byte)'x');
                c.Add((byte)'l');
                c.Add((byte)'p');
                AppendU32BE(jxlpIndex++, c);
                c.AddRange(compressed0);

                // A few non-codestream boxes in between
                AppendTestBox(Unk1BoxType, Unk1BoxContents, false, c);
                AppendTestBox(Unk2BoxType, Unk2BoxContents, false, c);

                // Empty placeholder codestream part
                AppendU32BE(12, c);
                c.Add((byte)'j');
                c.Add((byte)'x');
                c.Add((byte)'l');
                c.Add((byte)'p');
                AppendU32BE(jxlpIndex++, c);

                // Second codestream part
                AppendU32BE((uint)compressed1.Length + 12, c);
                c.Add((byte)'j');
                c.Add((byte)'x');
                c.Add((byte)'l');
                c.Add((byte)'p');
                AppendU32BE(jxlpIndex++, c);
                c.AddRange(compressed1);

                // Third (last) codestream part
                AppendU32BE(
                    addContainer == CodeStreamBoxFormat.MultiZeroTerminated
                        ? 0u
                        : ((uint)compressed2.Length + 12),
                    c);
                c.Add((byte)'j');
                c.Add((byte)'x');
                c.Add((byte)'l');
                c.Add((byte)'p');

                if (addContainer != CodeStreamBoxFormat.MultiLastEmptyOther)
                {
                    AppendU32BE(jxlpIndex++ | 0x80000000u, c);
                }
                else
                {
                    AppendU32BE(jxlpIndex++, c);
                }

                c.AddRange(compressed2);
                if (addContainer == CodeStreamBoxFormat.MultiLastEmptyOther)
                {
                    // Empty placeholder codestream part
                    AppendU32BE(12, c);
                    c.Add((byte)'j');
                    c.Add((byte)'x');
                    c.Add((byte)'l');
                    c.Add((byte)'p');
                    AppendU32BE(jxlpIndex++ | 0x80000000, c);
                    AppendTestBox(Unk3BoxType, Unk3BoxContents, false, c);
                }

                if (addContainer == CodeStreamBoxFormat.MultiOtherTerminated)
                {
                    AppendTestBox(Unk3BoxType, Unk3BoxContents, false, c);
                }

                if (addContainer == CodeStreamBoxFormat.MultiOtherZeroTerminated)
                {
                    AppendTestBox(Unk3BoxType, Unk3BoxContents, true, c);
                }

                RuntimeUtility.Swap(ref compressed, ref c);
            }
            else
            {
                List<byte> c = [];
                c.AddRange(header);

                if (parameters.JpegCodestream is not null)
                {
                    AppendBoxHeader(JxlBoxHeader.TypeFromString("jbrd"), encodedJpegBytes.Count, c);
                    c.AddRange(encodedJpegBytes);
                }

                if (addContainer == CodeStreamBoxFormat.BrobExif)
                {
                    c.AddRange(BoxBrobExif);
                }

                AppendU32BE(
                    addContainer == CodeStreamBoxFormat.SingleZeroTerminated
                      ? 0
                      : ((uint)compressed.Count + 8),
                    c);

                c.Add((byte)'j');
                c.Add((byte)'x');
                c.Add((byte)'l');
                c.Add((byte)'c');

                c.AddRange(compressed);

                if (addContainer == CodeStreamBoxFormat.SingleOther)
                {
                    AppendTestBox(Unk1BoxType, Unk1BoxContents, false, c);
                }

                RuntimeUtility.Swap(ref compressed, ref c);
            }
        }

        return compressed;
    }

    /// <summary>
    /// Tests to make sure that JXLP boxes that are out of order
    /// don't result in the decoder crashing.
    /// </summary>
    [Fact]
    public void TestOutOfOrderJxlpDuplicateIndexTest()
    {
        static void AppendTag(ReadOnlySpan<byte> tagData, List<byte> output) => output.AddRange(tagData);

        List<byte> data = [];

        // Adding the JPEG XL signature box
        AppendU32BE(12, data);
        AppendTag("JXL "u8, data);
        data.AddRange([0x0D, 0x0A, 0x87, 0x0A]);

        // Adding the FTYP box declaring file format version 1, which
        // enables out-of-order JXLP
        AppendU32BE(20, data);
        AppendTag("ftyp"u8, data);
        AppendTag("jxl "u8, data);
        AppendU32BE(1, data);
        AppendTag("jxl "u8, data);

        // Two jxlp boxes that both carry out-of-order index 1 (the expected first
        // index is 0). The first is buffered; the second is a duplicate.
        for (int i = 0; i < 2; i++)
        {
            // box size: 8-byte header + 8-byte contents
            AppendU32BE(16, data);
            AppendTag("jxlp"u8, data);

            // jxlp index 1, high bit unset (not the last box)
            AppendU32BE(1, data);

            // payload
            data.AddRange([0xAA, 0xBB, 0xCC, 0xDD]);
        }

        byte[] byteArrayData = [.. data];
        using MemoryStream ms = new(byteArrayData);
        ms.Position = 0;

        using JxlDecoderCore decoderCore = new(DecoderOptions.Default);
        decoderCore.SubscribeEvents(JxlDecoderStatus.BasicInfo);

        decoderCore.DecodeInput(ms);

        // We just have to make sure that processing out-of-order (OOO) JXLP
        // boxes don't throw.
    }

    [Fact]
    public void TestSpotColors()
    {
        JxlCodecIO io = new();
        int width = 55;
        int height = 257;

        io.CodecMetadata.ImageMetadata!.ColorEncoding = JxlColorEncoding.LinearSRgb();

        using JxlImage3F main = new(TestEnvironment.Configuration, width, height);
        using JxlImageF spot = new(TestEnvironment.Configuration, width, height);
        JxlImageOperations.ZeroFillImage(main);
        JxlImageOperations.ZeroFillImage(spot);

        for (int y = 0; y < height; y++)
        {
            Span<float> rowm = main.PlaneRow(1, y);
            Span<float> rows = spot.GetRow(y);

            for (int x = 0; x < width; x++)
            {
                rowm[x] = (x + y) * (1.0f / 255.0f);
                rows[x] = ((x ^ y) & 255) * (1.0f / 255.0f);
            }
        }

        io.SetFromImage(main, JxlColorEncoding.LinearSRgb());

        JxlExtraChannelInfo info = new()
        {
            BitDepth = new()
            {
                BitsPerSample = 8
            },
            DimensionShift = 0,
            Type = JxlExtraChannel.SpotColor
        };

        info.SpotColor[0] = 0.5f;
        info.SpotColor[1] = 0.2f;
        info.SpotColor[2] = 1.0f;
        info.SpotColor[3] = 0.5f;

        io.CodecMetadata.ImageMetadata!.ExtraChannels.Add(info);

        List<JxlImageF> ec = [spot];
        Assert.True(io.Frames[0].TrySetExtraChannels(ec));

        JxlCompressParameters compressParameters = new()
        {
            SpeedTier = JxlSpeedTier.Lightning,
            ModularMode = true,
            ColorTransform = JxlColorTransform.None,
            ButteraugliDistance = 0f
        };

        List<byte> compressed = [];
        Assert.True(EncodeFile(compressParameters, io, compressed));

        for (int renderSpot = 0; renderSpot < 2; renderSpot++)
        {
            JxlPixelFormat format = new()
            {
                Channels = 3,
                DataType = JxlDataType.Byte,
                Endianness = ByteOrder.LittleEndian,
                Align = 0
            };

            using JxlDecoderCore decoder = new(DecoderOptions.Default);
            decoder.SubscribeEvents(JxlDecoderStatus.BasicInfo | JxlDecoderStatus.FullImage);

            if (renderSpot == 0)
            {
                decoder.RenderSpotcolors = false;
            }

            using MemoryStream ms = new([.. compressed]);
            decoder.DecodeInput(ms);

            JxlBasicInfo basicInfo = decoder.GetBasicInfo();
            Assert.Equal(1u, basicInfo.ExtraChannelCount);
            Assert.Equal(width, basicInfo.XSize);
            Assert.Equal(height, basicInfo.YSize);

            JxlExtraChannelInfo extraInfo = decoder.GetExtraChannelInfo(0);
            Assert.Equal(JxlExtraChannel.SpotColor, extraInfo.Type);

            // Should throw as image output buffer wasn't provided.
            Assert.Throws<InvalidOperationException>(() => decoder.DecodeInput(ms));

            long bufferSize = decoder.GetImageOutputBufferSize();
            long extraSize = decoder.GetExtraChannelBufferSize(0);

            using Image<Rgb24> image = new(basicInfo.XSize, basicInfo.YSize);
            IMemoryOwner<byte> extra = TestEnvironment.Configuration.MemoryAllocator.Allocate<byte>(basicInfo.XSize * basicInfo.YSize);

            Assert.True(image.DangerousTryGetSinglePixelMemory(out Memory<Rgb24> imageMemory));

            decoder.SetOutputBuffer(JxlTestingTools.ChangeMemoryType<Rgb24, byte>(imageMemory));
            decoder.SetExtraChannelBuffer(0, extra.Memory);

            decoder.DecodeInput(ms);
            decoder.StatusMustBeFullImage();

            decoder.DecodeInput(ms);
            decoder.StatusMustBeSuccess();

            Span<Rgb24> imageSpan = imageMemory.Span;
            Span<byte> extraSpan = extra.Memory.Span;

            int stride = basicInfo.XSize;

            for (int y = 0; y < height; y++)
            {
                Span<Rgb24> rowm = imageSpan[(stride * y)..];
                Span<byte> rows = extraSpan[(stride * y)..];

                for (int x = 0; x < width; x++)
                {
                    if (renderSpot == 0)
                    {
                        // if spot color isn't rendered, main image should be as we made it
                        // (red and blue are all zeroes)
                        ref Rgb24 rgb = ref rowm[x];
                        Assert.Equal(0, rgb.R);
                        Assert.Equal(x + y > 255 ? 255 : x + y, rgb.G);
                        Assert.Equal(0, rgb.B);
                    }

                    if (renderSpot != 0)
                    {
                        // if spot color is rendered, expect red and blue to look like the
                        // spot color channel
                        Assert.True(MathF.Abs(rowm[x].R - (rows[x] * 0.25f)) < 1.0f);
                        Assert.True(MathF.Abs(rowm[x].B - (rows[x] * 0.5f)) < 1.0f);
                    }

                    Assert.Equal(rows[x], (x ^ y) & 255);
                }
            }
        }
    }

    /// <summary>
    /// The DC event still exists, but is no longer implemented, it is deprecated.
    /// </summary>
    [Fact]
    public void TestDcNotGettable()
    {
        ReadOnlySpan<byte> compressed = "\xFF\n\0\x10\xB0\x13\0H\x80(\0\xDC\0U\x0F\0\0\xA8P\x19e\xDC\xE0\xE5\\\xCF\x97\x1F:,\xA6m\\gh\xABm\vK\x12E\xC6\xB1I\xAAC&pH\x12\xEB \xF3\x06\x12\bp\0\x80\x9F\x1C\x99W2d\xAD$\x01"u8;
        using JxlDecoderCore decoder = new(DecoderOptions.Default);
        decoder.SubscribeEvents(JxlDecoderStatus.BasicInfo);

        using MemoryStream ms = new(compressed.ToArray());

        decoder.DecodeInput(ms);
        decoder.StatusMustBeBasicInfo();

        decoder.DecodeInput(ms);
        decoder.StatusMustBeSuccess();
    }

    private class TestCodestreamParameters
    {
        public JxlCompressParameters CompressParameters { get; set; } = new();

        public CodeStreamBoxFormat BoxFormat { get; set; } = CodeStreamBoxFormat.None;

        public JxlExifOrientation Orientation { get; set; } = JxlExifOrientation.Identity;

        public PreviewMode PreviewMode { get; set; } = PreviewMode.NoPreview;

        public bool AddIntrinsicSize { get; set; }

        public bool AddIccProfile { get; set; }

        public float IntensityTarget { get; set; }

        public string? ColorSpace { get; set; }

        public List<byte>? JpegCodestream { get; set; }
    }
}

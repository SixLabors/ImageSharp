// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// TEMPORARY LOCAL TOOLING. Compares streams from the managed encoder between the libaom reference decoder
/// and the managed decoder. The reference decoder is the authority: a stream that it rejects is an encoder
/// defect, and a sample that differs is a managed decoder defect.
/// </summary>
/// <remarks>
/// The tests need the path of an optimized <c>aomdec</c> in the <c>IMAGESHARP_AOMDEC</c> environment variable and
/// do nothing without it. They belong to the temporary native material that the implementation plan removes at
/// final cleanup.
/// </remarks>
[Trait("Format", "Heif")]
public class Av1ReferenceDecoderVerificationTests
{
    private const string DecoderVariable = "IMAGESHARP_AOMDEC";

    /// <summary>
    /// Verifies that the reference decoder accepts a managed still-picture stream and reproduces the managed decode.
    /// </summary>
    /// <param name="imagePath">The source image.</param>
    /// <param name="width">The encoded width.</param>
    /// <param name="height">The encoded height.</param>
    /// <param name="colorFormatValue">The component layout.</param>
    /// <param name="bitDepth">The component precision.</param>
    /// <param name="qIndex">The base quantizer index; zero selects lossless coding.</param>
    /// <param name="speed">The encoder speed.</param>
    [Theory]
    [InlineData(TestImages.Png.Bike, 385, 137, (int)Av1ColorFormat.Yuv420, 8, 37, HeifEncodingSpeed.Level0)]
    [InlineData(TestImages.Png.Bike, 385, 137, (int)Av1ColorFormat.Yuv420, 8, 128, HeifEncodingSpeed.Level0)]
    [InlineData(TestImages.Png.Bike, 385, 137, (int)Av1ColorFormat.Yuv422, 8, 128, HeifEncodingSpeed.Level0)]
    [InlineData(TestImages.Png.Bike, 385, 137, (int)Av1ColorFormat.Yuv444, 8, 128, HeifEncodingSpeed.Level0)]
    [InlineData(TestImages.Png.Bike, 385, 137, (int)Av1ColorFormat.Yuv400, 8, 128, HeifEncodingSpeed.Level0)]
    [InlineData(TestImages.Png.Bike, 385, 137, (int)Av1ColorFormat.Yuv420, 10, 128, HeifEncodingSpeed.Level0)]
    [InlineData(TestImages.Png.Bike, 385, 137, (int)Av1ColorFormat.Yuv444, 12, 128, HeifEncodingSpeed.Level0)]
    [InlineData(TestImages.Png.Bike, 256, 256, (int)Av1ColorFormat.Yuv420, 8, 64, HeifEncodingSpeed.Level3)]
    [InlineData(TestImages.Png.Bike, 512, 512, (int)Av1ColorFormat.Yuv420, 8, 64, HeifEncodingSpeed.Level6)]
    [InlineData(TestImages.Png.Bike, 512, 512, (int)Av1ColorFormat.Yuv420, 8, 64, HeifEncodingSpeed.Level9)]
    [InlineData(TestImages.Png.Bike, 512, 512, (int)Av1ColorFormat.Yuv444, 10, 200, HeifEncodingSpeed.Level6)]
    [InlineData(TestImages.Png.Bike, 131, 67, (int)Av1ColorFormat.Yuv420, 8, 0, HeifEncodingSpeed.Level6)]
    [InlineData(TestImages.Png.Bike, 131, 67, (int)Av1ColorFormat.Yuv444, 10, 0, HeifEncodingSpeed.Level0)]
    [InlineData(TestImages.Png.CalliphoraPartial, 300, 200, (int)Av1ColorFormat.Yuv420, 8, 96, HeifEncodingSpeed.Level0)]
    [InlineData(TestImages.Png.CalliphoraPartial, 300, 200, (int)Av1ColorFormat.Yuv420, 8, 96, HeifEncodingSpeed.Level6)]
    [InlineData(TestImages.Png.Splash, 500, 483, (int)Av1ColorFormat.Yuv444, 8, 0, HeifEncodingSpeed.Level0)]
    [InlineData(TestImages.Png.Splash, 500, 483, (int)Av1ColorFormat.Yuv400, 8, 0, HeifEncodingSpeed.Level0)]
    [InlineData(TestImages.Png.Splash, 500, 483, (int)Av1ColorFormat.Yuv444, 8, 0, HeifEncodingSpeed.Level0, true)]
    [InlineData(TestImages.Png.Splash, 500, 483, (int)Av1ColorFormat.Yuv400, 8, 0, HeifEncodingSpeed.Level0, false, true)]
    public void ReferenceDecoderReproducesManagedDecode(
        string imagePath,
        int width,
        int height,
        int colorFormatValue,
        int bitDepth,
        int qIndex,
        HeifEncodingSpeed speed,
        bool identity = false,
        bool alpha = false)
    {
        string decoderPath = Environment.GetEnvironmentVariable(DecoderVariable);
        if (string.IsNullOrEmpty(decoderPath))
        {
            return;
        }

        Av1ColorFormat colorFormat = (Av1ColorFormat)colorFormatValue;
        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = colorFormat == Av1ColorFormat.Yuv400,
            SubSamplingX = colorFormat is Av1ColorFormat.Yuv400 or Av1ColorFormat.Yuv420 or Av1ColorFormat.Yuv422,
            SubSamplingY = colorFormat is Av1ColorFormat.Yuv400 or Av1ColorFormat.Yuv420,
            BitDepth = bitDepth == 8 ? Av1BitDepth.EightBit : bitDepth == 10 ? Av1BitDepth.TenBit : Av1BitDepth.TwelveBit,
            MatrixCoefficients = identity ? ObuMatrixCoefficients.Identity : ObuMatrixCoefficients.Bt709,
            ColorRange = true
        };

        if (identity)
        {
            colorConfig.IsColorDescriptionPresent = true;
            colorConfig.ColorPrimaries = ObuColorPrimaries.Bt709;
            colorConfig.TransferCharacteristics = ObuTransferCharacteristics.Srgb;
        }

        using Image<Rgba64> image = Image.Load<Rgba64>(TestFile.Create(imagePath).Bytes);
        image.Mutate(context => context.Resize(width, height));
        using MemoryStream stream = new();
        _ = alpha
            ? Av1FrameEncoder.EncodeAlpha(Configuration.Default, image.Frames.RootFrame, stream, colorConfig, qIndex, speed)
            : Av1FrameEncoder.Encode(Configuration.Default, image.Frames.RootFrame, stream, colorConfig, qIndex, speed);
        byte[] payload = stream.ToArray();
        string dump = Environment.GetEnvironmentVariable("IMAGESHARP_AV1_DUMP");
        if (!string.IsNullOrEmpty(dump))
        {
            File.WriteAllBytes(Path.Combine(dump, $"harness-{Path.GetFileNameWithoutExtension(imagePath)}-{colorFormat}-{bitDepth}-q{qIndex}-{speed}{(alpha ? "-alpha" : string.Empty)}.obu"), payload);
        }

        byte[] reference = DecodeWithReference(decoderPath, payload, out string log);
        using Av1Decoder decoder = new(Configuration.Default);
        using Av1FrameBuffer<byte> managed = decoder.DecodeFrameBuffer(payload, null, null, out _);

        int bytesPerSample = bitDepth == 8 ? 1 : 2;
        (int subX, int subY) = colorFormat switch
        {
            Av1ColorFormat.Yuv420 => (1, 1),
            Av1ColorFormat.Yuv422 => (1, 0),
            _ => (0, 0)
        };

        int planeCount = colorFormat == Av1ColorFormat.Yuv400 ? 1 : 3;
        int offset = 0;
        int mismatchCount = 0;
        StringBuilder description = new();
        for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
        {
            Av1Plane plane = (Av1Plane)planeIndex;
            int planeSubX = planeIndex == 0 ? 0 : subX;
            int planeSubY = planeIndex == 0 ? 0 : subY;
            int planeWidth = (width + planeSubX) >> planeSubX;
            int planeHeight = (height + planeSubY) >> planeSubY;
            Buffer2DRegion<byte> managedPlane = managed.DeriveBlockPointer(plane, planeSubX, planeSubY);
            for (int y = 0; y < planeHeight; y++)
            {
                ReadOnlySpan<byte> expected = reference.AsSpan(offset, planeWidth * bytesPerSample);
                ReadOnlySpan<byte> actual = managedPlane.DangerousGetRowSpan(y)[..(planeWidth * bytesPerSample)];
                offset += planeWidth * bytesPerSample;
                if (expected.SequenceEqual(actual))
                {
                    continue;
                }

                for (int x = 0; x < planeWidth; x++)
                {
                    int expectedSample = bytesPerSample == 1
                        ? expected[x]
                        : MemoryMarshal.Cast<byte, ushort>(expected)[x];
                    int actualSample = bytesPerSample == 1
                        ? actual[x]
                        : MemoryMarshal.Cast<byte, ushort>(actual)[x];
                    if (expectedSample != actualSample)
                    {
                        if (mismatchCount < 12)
                        {
                            description.Append(CultureInfo.InvariantCulture, $" {plane}({x},{y})={expectedSample}/{actualSample}");
                        }

                        mismatchCount++;
                    }
                }
            }
        }

        // A monochrome reference decode still writes neutral chroma planes. Only the coded planes are compared,
        // so the reference file must hold at least those samples.
        Assert.True(offset <= reference.Length, $"The reference decode is shorter than the coded planes. {log}");
        Assert.True(mismatchCount == 0, $"{mismatchCount} samples differ (reference/managed):{description}");
    }

    /// <summary>
    /// Verifies that the reference decoder reproduces a lossless monochrome source exactly.
    /// </summary>
    /// <param name="width">The source width.</param>
    /// <param name="height">The source height.</param>
    /// <param name="speed">The encoder speed.</param>
    [Theory]
    [InlineData(21, 77, HeifEncodingSpeed.Level0)]
    [InlineData(13, 21, HeifEncodingSpeed.Level7)]
    [InlineData(77, 21, HeifEncodingSpeed.Level8)]
    [InlineData(21, 77, HeifEncodingSpeed.Level9)]
    public void ReferenceDecoderReproducesLosslessSource(int width, int height, HeifEncodingSpeed speed)
    {
        string decoderPath = Environment.GetEnvironmentVariable(DecoderVariable);
        if (string.IsNullOrEmpty(decoderPath))
        {
            return;
        }

        ReadOnlySpan<int> period = [0, 28, 40, 28, 0, -28, -40, -12];
        using Image<L8> source = new(width, height);
        for (int y = 0; y < height; y++)
        {
            Span<L8> row = source.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            for (int x = 0; x < width; x++)
            {
                row[x] = new L8((byte)(128 + period[x % period.Length] + period[y % period.Length]));
            }
        }

        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = true,
            SubSamplingX = true,
            SubSamplingY = true,
            BitDepth = Av1BitDepth.EightBit,
            ColorRange = true
        };

        using MemoryStream stream = new();
        _ = Av1FrameEncoder.Encode(Configuration.Default, source.Frames.RootFrame, stream, colorConfig, qIndex: 0, speed);
        byte[] reference = DecodeWithReference(decoderPath, stream.ToArray(), out string log);
        int mismatchCount = 0;
        StringBuilder description = new();
        for (int y = 0; y < height; y++)
        {
            ReadOnlySpan<byte> expected = MemoryMarshal.AsBytes(source.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y));
            ReadOnlySpan<byte> actual = reference.AsSpan(y * width, width);
            for (int x = 0; x < width; x++)
            {
                if (expected[x] != actual[x])
                {
                    if (mismatchCount < 12)
                    {
                        description.Append(CultureInfo.InvariantCulture, $" ({x},{y})={expected[x]}/{actual[x]}");
                    }

                    mismatchCount++;
                }
            }
        }

        Assert.True(mismatchCount == 0, $"{mismatchCount} samples differ (source/reference decode):{description} {log}");
    }

    /// <summary>
    /// Writes the temporary work counters of one managed encode for the comparison with libaom.
    /// </summary>
    /// <param name="name">The case name.</param>
    /// <param name="speed">The encoder speed.</param>
    [Theory]
    [InlineData("bike512-s6", HeifEncodingSpeed.Level6)]
    [InlineData("bike512-s0", HeifEncodingSpeed.Level0)]
    public void WriteWorkCounters(string name, HeifEncodingSpeed speed)
    {
        string directory = Environment.GetEnvironmentVariable("IMAGESHARP_AV1_WORK");
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = false,
            SubSamplingX = true,
            SubSamplingY = true,
            BitDepth = Av1BitDepth.EightBit,
            MatrixCoefficients = ObuMatrixCoefficients.Bt709,
            ColorRange = true
        };

        using Image<Rgba64> image = Image.Load<Rgba64>(TestFile.Create(TestImages.Png.Bike).Bytes);
        image.Mutate(context => context.Resize(512, 512));
        using MemoryStream lossy = new();

        // The first encode warms the JIT; the report covers the second one.
        _ = Av1FrameEncoder.Encode(Configuration.Default, image.Frames.RootFrame, lossy, colorConfig, 64, speed);
        lossy.SetLength(0);
        Av1WorkCounters.Reset();
        Stopwatch stopwatch = Stopwatch.StartNew();
        long workTotal = Av1WorkCounters.Start();
        _ = Av1FrameEncoder.Encode(Configuration.Default, image.Frames.RootFrame, lossy, colorConfig, 64, speed);
        Av1WorkCounters.Stop(Av1WorkCounters.Total, workTotal);
        Av1WorkCounters.Count(Av1WorkCounters.Total);
        stopwatch.Stop();
        File.WriteAllText(
            Path.Combine(directory, name + ".work.txt"),
            Av1WorkCounters.Report() + "MS " + stopwatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
    }

    /// <summary>
    /// Writes a lossless and a lossy managed encode of the same source for the size and PSNR comparison script.
    /// </summary>
    /// <remarks>
    /// The reference decoder turns the lossless stream into the exact source planes. The comparison script gives
    /// those planes to the reference encoder, so both encoders see identical samples. The output directory comes
    /// from the <c>IMAGESHARP_AV1_COMPARE</c> environment variable; the test does nothing without it.
    /// </remarks>
    /// <param name="name">The case name.</param>
    /// <param name="imagePath">The source image.</param>
    /// <param name="width">The encoded width.</param>
    /// <param name="height">The encoded height.</param>
    /// <param name="colorFormatValue">The component layout.</param>
    /// <param name="bitDepth">The component precision.</param>
    /// <param name="qIndex">The base quantizer index of the lossy encode.</param>
    /// <param name="speed">The encoder speed.</param>
    [Theory]
    [InlineData("bike512-420-8-q64-s0", TestImages.Png.Bike, 512, 512, (int)Av1ColorFormat.Yuv420, 8, 64, HeifEncodingSpeed.Level0)]
    [InlineData("bike512-420-8-q64-s3", TestImages.Png.Bike, 512, 512, (int)Av1ColorFormat.Yuv420, 8, 64, HeifEncodingSpeed.Level3)]
    [InlineData("bike512-420-8-q64-s6", TestImages.Png.Bike, 512, 512, (int)Av1ColorFormat.Yuv420, 8, 64, HeifEncodingSpeed.Level6)]
    [InlineData("bike512-420-8-q64-s9", TestImages.Png.Bike, 512, 512, (int)Av1ColorFormat.Yuv420, 8, 64, HeifEncodingSpeed.Level9)]
    [InlineData("bike512-420-8-q128-s6", TestImages.Png.Bike, 512, 512, (int)Av1ColorFormat.Yuv420, 8, 128, HeifEncodingSpeed.Level6)]
    [InlineData("bike512-444-10-q64-s6", TestImages.Png.Bike, 512, 512, (int)Av1ColorFormat.Yuv444, 10, 64, HeifEncodingSpeed.Level6)]
    [InlineData("calliphora-420-8-q64-s6", TestImages.Png.CalliphoraPartial, 300, 200, (int)Av1ColorFormat.Yuv420, 8, 64, HeifEncodingSpeed.Level6)]
    [InlineData("calliphora-420-8-q64-s0", TestImages.Png.CalliphoraPartial, 300, 200, (int)Av1ColorFormat.Yuv420, 8, 64, HeifEncodingSpeed.Level0)]
    public void WriteComparisonStreams(
        string name,
        string imagePath,
        int width,
        int height,
        int colorFormatValue,
        int bitDepth,
        int qIndex,
        HeifEncodingSpeed speed)
    {
        string directory = Environment.GetEnvironmentVariable("IMAGESHARP_AV1_COMPARE");
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        Av1ColorFormat colorFormat = (Av1ColorFormat)colorFormatValue;
        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = colorFormat == Av1ColorFormat.Yuv400,
            SubSamplingX = colorFormat is Av1ColorFormat.Yuv400 or Av1ColorFormat.Yuv420 or Av1ColorFormat.Yuv422,
            SubSamplingY = colorFormat is Av1ColorFormat.Yuv400 or Av1ColorFormat.Yuv420,
            BitDepth = bitDepth == 8 ? Av1BitDepth.EightBit : bitDepth == 10 ? Av1BitDepth.TenBit : Av1BitDepth.TwelveBit,
            MatrixCoefficients = ObuMatrixCoefficients.Bt709,
            ColorRange = true
        };

        using Image<Rgba64> image = Image.Load<Rgba64>(TestFile.Create(imagePath).Bytes);
        image.Mutate(context => context.Resize(width, height));
        using (MemoryStream lossless = new())
        {
            _ = Av1FrameEncoder.Encode(Configuration.Default, image.Frames.RootFrame, lossless, colorConfig, 0, HeifEncodingSpeed.Level6);
            File.WriteAllBytes(Path.Combine(directory, name + ".lossless.obu"), lossless.ToArray());
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        using (MemoryStream lossy = new())
        {
            _ = Av1FrameEncoder.Encode(Configuration.Default, image.Frames.RootFrame, lossy, colorConfig, qIndex, speed);
            stopwatch.Stop();
            File.WriteAllBytes(Path.Combine(directory, name + ".lossy.obu"), lossy.ToArray());
        }

        string chroma = colorFormat switch
        {
            Av1ColorFormat.Yuv400 => "400",
            Av1ColorFormat.Yuv420 => "420",
            Av1ColorFormat.Yuv422 => "422",
            _ => "444"
        };

        File.WriteAllText(
            Path.Combine(directory, name + ".txt"),
            string.Create(CultureInfo.InvariantCulture, $"{width} {height} {bitDepth} {chroma} {qIndex} {(int)speed}"));

        File.WriteAllText(
            Path.Combine(directory, name + ".managed-ms"),
            stopwatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Temporary comparison tooling: decodes every OBU stream in the <c>IMAGESHARP_AV1_DUMP</c> directory and
    /// writes each coded block's decisions in decode order, so the managed and reference encodes can be diffed.
    /// </summary>
    [Fact]
    public void WriteDecodedBlockDumps()
    {
        string directory = Environment.GetEnvironmentVariable("IMAGESHARP_AV1_DUMP");
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        SixLabors.ImageSharp.Formats.Heif.Av1.Entropy.Av1SymbolReader.SkipTrailingBitValidationForDiagnostics = true;
        foreach (string path in Directory.GetFiles(directory, "*.obu"))
        {
            if (path.EndsWith(".td.obu", StringComparison.Ordinal) || path.EndsWith(".lossless.obu", StringComparison.Ordinal))
            {
                continue;
            }

            byte[] payload = File.ReadAllBytes(path);
            using Av1Decoder decoder = new(Configuration.Default);
            try
            {
                decoder.DecodeSequenceReference(payload, null, null);
            }
            catch (InvalidImageContentException exception)
            {
                // A stream that fails the final trailing-bit check still holds every parsed block.
                File.WriteAllText(path + ".error.txt", exception.Message);
            }

            ObuSequenceHeader sequenceHeader = decoder.SequenceHeader;
            ObuFrameHeader frameHeader = decoder.FrameHeader;
            SixLabors.ImageSharp.Formats.Heif.Av1.Tiling.Av1FrameInfo frameInfo = decoder.FrameInfo;
            int superblockModeInfoLog2 = sequenceHeader.SuperblockSizeLog2 - 2;
            int superblockColumns = (frameHeader.ModeInfoColumnCount + (1 << superblockModeInfoLog2) - 1) >> superblockModeInfoLog2;
            int superblockRows = (frameHeader.ModeInfoRowCount + (1 << superblockModeInfoLog2) - 1) >> superblockModeInfoLog2;
            StringBuilder builder = new();
            for (int row = 0; row < superblockRows; row++)
            {
                for (int column = 0; column < superblockColumns; column++)
                {
                    Point index = new(column, row);
                    int count = frameInfo.GetModeInfoCount(index);
                    foreach (SixLabors.ImageSharp.Formats.Heif.Av1.Tiling.Av1BlockModeInfo mode in frameInfo.GetModeInfos(index, count))
                    {
                        Point position = new(
                            (column << (superblockModeInfoLog2 + 2)) + (mode.PositionInSuperblock.X << 2),
                            (row << (superblockModeInfoLog2 + 2)) + (mode.PositionInSuperblock.Y << 2));
                        builder.Append(CultureInfo.InvariantCulture, $"blk {position.X},{position.Y} bsize {(int)mode.BlockSize}")
                            .Append(CultureInfo.InvariantCulture, $" y {(int)mode.YMode} {mode.GetAngleDelta(Av1Plane.Y)} fi {(mode.UseFilterIntra ? 1 : 0)}:{(int)mode.FilterIntraMode}")
                            .Append(CultureInfo.InvariantCulture, $" uv {(int)mode.UvMode} {mode.GetAngleDelta(Av1Plane.U)} cfl {mode.ChromaFromLumaAlphaIndex}:{mode.ChromaFromLumaAlphaSign}")
                            .Append(CultureInfo.InvariantCulture, $" pal {mode.GetPaletteSize(Av1Plane.Y)}:{mode.GetPaletteSize(Av1Plane.U)}")
                            .Append(CultureInfo.InvariantCulture, $" tx {(int)mode.TransformSize} skip {(mode.Skip ? 1 : 0)} ibc {(mode.UseIntraBlockCopy ? 1 : 0)}")
                            .Append(CultureInfo.InvariantCulture, $" dv {mode.DisplacementVector.Column},{mode.DisplacementVector.Row}")
                            .Append('\n');
                    }
                }
            }

            File.WriteAllText(path + ".blocks.txt", builder.ToString());
        }
    }

    /// <summary>
    /// Temporary comparison tooling: encodes the splash alpha channel losslessly, decodes it without the final
    /// trailing-bit check, and writes the first block in decode order whose samples differ from the source.
    /// </summary>
    [Fact]
    public void LocateLosslessAlphaMismatch()
    {
        string directory = Environment.GetEnvironmentVariable("IMAGESHARP_AV1_DUMP");
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        using Image<Rgba32> image = Image.Load<Rgba32>(TestFile.Create(TestImages.Png.Splash).Bytes);
        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = true,
            SubSamplingX = true,
            SubSamplingY = true,
            BitDepth = Av1BitDepth.EightBit,
            ColorRange = true
        };

        using MemoryStream stream = new();
        List<string> written = [];
        SixLabors.ImageSharp.Formats.Heif.Av1.Entropy.Av1SymbolWriter.DiagnosticSymbolTrace = written;
        try
        {
            _ = Av1FrameEncoder.EncodeAlpha(Configuration.Default, image.Frames.RootFrame, stream, colorConfig, 0, HeifEncodingSpeed.Level0);
        }
        finally
        {
            SixLabors.ImageSharp.Formats.Heif.Av1.Entropy.Av1SymbolWriter.DiagnosticSymbolTrace = null;
        }

        File.WriteAllBytes(Path.Combine(directory, "locate-alpha.obu"), stream.ToArray());
        List<string> read = [];
        SixLabors.ImageSharp.Formats.Heif.Av1.Entropy.Av1SymbolReader.SkipTrailingBitValidationForDiagnostics = true;
        SixLabors.ImageSharp.Formats.Heif.Av1.Entropy.Av1SymbolReader.DiagnosticSymbolTrace = read;
        try
        {
            using Av1Decoder decoder = new(Configuration.Default);
            using Av1FrameBuffer<byte> frame = decoder.DecodeFrameBuffer(stream.ToArray(), null, null, out _);
            SixLabors.ImageSharp.Formats.Heif.Av1.Entropy.Av1SymbolReader.DiagnosticSymbolTrace = null;
            File.WriteAllLines(Path.Combine(directory, "locate-alpha.written.txt"), written);
            File.WriteAllLines(Path.Combine(directory, "locate-alpha.read.txt"), read);
            Buffer2DRegion<byte> decoded = frame.DeriveBlockPointer(Av1Plane.Y, 0, 0);
            SixLabors.ImageSharp.Formats.Heif.Av1.Tiling.Av1FrameInfo frameInfo = decoder.FrameInfo;
            ObuSequenceHeader sequenceHeader = decoder.SequenceHeader;
            ObuFrameHeader frameHeader = decoder.FrameHeader;
            int superblockModeInfoLog2 = sequenceHeader.SuperblockSizeLog2 - 2;
            int superblockColumns = (frameHeader.ModeInfoColumnCount + (1 << superblockModeInfoLog2) - 1) >> superblockModeInfoLog2;
            int superblockRows = (frameHeader.ModeInfoRowCount + (1 << superblockModeInfoLog2) - 1) >> superblockModeInfoLog2;
            StringBuilder report = new();
            int blockIndex = 0;
            for (int row = 0; row < superblockRows && report.Length == 0; row++)
            {
                for (int column = 0; column < superblockColumns && report.Length == 0; column++)
                {
                    Point index = new(column, row);
                    foreach (SixLabors.ImageSharp.Formats.Heif.Av1.Tiling.Av1BlockModeInfo mode in frameInfo.GetModeInfos(index, frameInfo.GetModeInfoCount(index)))
                    {
                        int x0 = (column << (superblockModeInfoLog2 + 2)) + (mode.PositionInSuperblock.X << 2);
                        int y0 = (row << (superblockModeInfoLog2 + 2)) + (mode.PositionInSuperblock.Y << 2);
                        int mismatches = 0;
                        for (int y = y0; y < Math.Min(y0 + mode.BlockSize.GetHeight(), image.Height); y++)
                        {
                            for (int x = x0; x < Math.Min(x0 + mode.BlockSize.GetWidth(), image.Width); x++)
                            {
                                mismatches += decoded.DangerousGetRowSpan(y)[x] != image[x, y].A ? 1 : 0;
                            }
                        }

                        if (mismatches > 0)
                        {
                            report.Append(CultureInfo.InvariantCulture, $"block {blockIndex} at {x0},{y0} bsize {mode.BlockSize} mismatches {mismatches}")
                                .Append(CultureInfo.InvariantCulture, $" y {mode.YMode} pal {mode.GetPaletteSize(Av1Plane.Y)} ibc {mode.UseIntraBlockCopy} dv {mode.DisplacementVector.Column},{mode.DisplacementVector.Row} tx {mode.TransformSize} skip {mode.Skip}");
                            break;
                        }

                        blockIndex++;
                    }
                }
            }

            File.WriteAllText(Path.Combine(directory, "locate-alpha.txt"), report.Length == 0 ? "no mismatch" : report.ToString());
        }
        finally
        {
            SixLabors.ImageSharp.Formats.Heif.Av1.Entropy.Av1SymbolReader.SkipTrailingBitValidationForDiagnostics = false;
            SixLabors.ImageSharp.Formats.Heif.Av1.Entropy.Av1SymbolReader.DiagnosticSymbolTrace = null;
        }
    }

    /// <summary>
    /// Temporary comparison tooling: encodes one case with the symbol trace enabled and writes the trace.
    /// </summary>
    [Fact]
    public void TraceEncoderDecisions()
    {
        string directory = Environment.GetEnvironmentVariable("IMAGESHARP_AV1_DUMP");
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        ObuColorConfig colorConfig = new()
        {
            SubSamplingX = true,
            SubSamplingY = true,
            BitDepth = Av1BitDepth.EightBit,
            MatrixCoefficients = ObuMatrixCoefficients.Bt709,
            ColorRange = true
        };

        string subject = Environment.GetEnvironmentVariable("IMAGESHARP_AV1_TRACE_IMAGE") ?? "bike";
        HeifEncodingSpeed speed = Enum.Parse<HeifEncodingSpeed>(
            Environment.GetEnvironmentVariable("IMAGESHARP_AV1_TRACE_SPEED") ?? "Level6");

        using Image<Rgba64> image = Image.Load<Rgba64>(
            TestFile.Create(subject == "bike" ? TestImages.Png.Bike : TestImages.Png.CalliphoraPartial).Bytes);
        image.Mutate(context => context.Resize(subject == "bike" ? 512 : 300, subject == "bike" ? 512 : 200));
        using MemoryStream stream = new();
        List<string> trace = [];
        SixLabors.ImageSharp.Formats.Heif.Av1.Entropy.Av1SymbolWriter.DiagnosticSymbolTrace = trace;
        try
        {
            _ = Av1FrameEncoder.Encode(Configuration.Default, image.Frames.RootFrame, stream, colorConfig, 64, speed);
        }
        finally
        {
            SixLabors.ImageSharp.Formats.Heif.Av1.Entropy.Av1SymbolWriter.DiagnosticSymbolTrace = null;
        }

        File.WriteAllLines(
            Path.Combine(directory, $"trace-{subject}-{speed}.txt"),
            trace.Where(line => !line.StartsWith('S') && !line.StartsWith('B')));
    }

    /// <summary>
    /// Decodes one low-overhead OBU stream with the reference decoder.
    /// </summary>
    /// <param name="decoderPath">The reference decoder executable.</param>
    /// <param name="payload">The sequence header and frame OBUs of one still picture.</param>
    /// <param name="log">The reference decoder diagnostics.</param>
    /// <returns>The planar samples at the coded precision.</returns>
    private static byte[] DecodeWithReference(string decoderPath, byte[] payload, out string log)
    {
        string directory = Path.Combine(Path.GetTempPath(), "imagesharp-aomdec", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string input = Path.Combine(directory, "stream.obu");
            string output = Path.Combine(directory, "stream.yuv");

            // The reference tool identifies a low-overhead stream by exactly one leading temporal delimiter.
            // Add it only when the stream does not already start with one.
            using (FileStream file = File.Create(input))
            {
                if (payload[0] != 0x12)
                {
                    file.Write([0x12, 0x00]);
                }

                file.Write(payload);
            }

            ProcessStartInfo start = new(decoderPath)
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            start.ArgumentList.Add("--rawvideo");
            start.ArgumentList.Add("-o");
            start.ArgumentList.Add(output);
            start.ArgumentList.Add(input);
            using Process process = Process.Start(start);
            string error = process.StandardError.ReadToEnd();
            string standard = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            log = $"exit={process.ExitCode} {error} {standard}";
            Assert.True(process.ExitCode == 0 && File.Exists(output), $"The reference decoder rejected the stream. {log}");
            return File.ReadAllBytes(output);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}

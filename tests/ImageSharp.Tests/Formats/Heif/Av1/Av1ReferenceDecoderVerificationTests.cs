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
    [Fact]
    public void ReferenceDecoderReproducesManagedDecode()
    {
        this.ReferenceDecoderReproducesManagedDecodeCase(TestImages.Png.Bike, 385, 137, (int)Av1ColorFormat.Yuv420, 8, 37, HeifEncodingSpeed.Level0);
        this.ReferenceDecoderReproducesManagedDecodeCase(TestImages.Png.Bike, 385, 137, (int)Av1ColorFormat.Yuv420, 8, 128, HeifEncodingSpeed.Level0);
        this.ReferenceDecoderReproducesManagedDecodeCase(TestImages.Png.Bike, 385, 137, (int)Av1ColorFormat.Yuv422, 8, 128, HeifEncodingSpeed.Level0);
        this.ReferenceDecoderReproducesManagedDecodeCase(TestImages.Png.Bike, 385, 137, (int)Av1ColorFormat.Yuv444, 8, 128, HeifEncodingSpeed.Level0);
        this.ReferenceDecoderReproducesManagedDecodeCase(TestImages.Png.Bike, 385, 137, (int)Av1ColorFormat.Yuv400, 8, 128, HeifEncodingSpeed.Level0);
        this.ReferenceDecoderReproducesManagedDecodeCase(TestImages.Png.Bike, 385, 137, (int)Av1ColorFormat.Yuv420, 10, 128, HeifEncodingSpeed.Level0);
        this.ReferenceDecoderReproducesManagedDecodeCase(TestImages.Png.Bike, 385, 137, (int)Av1ColorFormat.Yuv444, 12, 128, HeifEncodingSpeed.Level0);
        this.ReferenceDecoderReproducesManagedDecodeCase(TestImages.Png.Bike, 256, 256, (int)Av1ColorFormat.Yuv420, 8, 64, HeifEncodingSpeed.Level3);
        this.ReferenceDecoderReproducesManagedDecodeCase(TestImages.Png.Bike, 512, 512, (int)Av1ColorFormat.Yuv420, 8, 64, HeifEncodingSpeed.Level6);
        this.ReferenceDecoderReproducesManagedDecodeCase(TestImages.Png.Bike, 512, 512, (int)Av1ColorFormat.Yuv420, 8, 64, HeifEncodingSpeed.Level9);
        this.ReferenceDecoderReproducesManagedDecodeCase(TestImages.Png.Bike, 512, 512, (int)Av1ColorFormat.Yuv444, 10, 200, HeifEncodingSpeed.Level6);
        this.ReferenceDecoderReproducesManagedDecodeCase(TestImages.Png.Bike, 131, 67, (int)Av1ColorFormat.Yuv420, 8, 0, HeifEncodingSpeed.Level6);
        this.ReferenceDecoderReproducesManagedDecodeCase(TestImages.Png.Bike, 131, 67, (int)Av1ColorFormat.Yuv444, 10, 0, HeifEncodingSpeed.Level0);
        this.ReferenceDecoderReproducesManagedDecodeCase(TestImages.Png.CalliphoraPartial, 300, 200, (int)Av1ColorFormat.Yuv420, 8, 96, HeifEncodingSpeed.Level0);
        this.ReferenceDecoderReproducesManagedDecodeCase(TestImages.Png.CalliphoraPartial, 300, 200, (int)Av1ColorFormat.Yuv420, 8, 96, HeifEncodingSpeed.Level6);
        this.ReferenceDecoderReproducesManagedDecodeCase(TestImages.Png.Splash, 500, 483, (int)Av1ColorFormat.Yuv444, 8, 0, HeifEncodingSpeed.Level0);
        this.ReferenceDecoderReproducesManagedDecodeCase(TestImages.Png.Splash, 500, 483, (int)Av1ColorFormat.Yuv400, 8, 0, HeifEncodingSpeed.Level0);
        this.ReferenceDecoderReproducesManagedDecodeCase(TestImages.Png.Splash, 500, 483, (int)Av1ColorFormat.Yuv444, 8, 0, HeifEncodingSpeed.Level0, true);
        this.ReferenceDecoderReproducesManagedDecodeCase(TestImages.Png.Splash, 500, 483, (int)Av1ColorFormat.Yuv400, 8, 0, HeifEncodingSpeed.Level0, false, true);
    }

    private void ReferenceDecoderReproducesManagedDecodeCase(
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
    [InlineData(77, 21, HeifEncodingSpeed.Level8)]
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

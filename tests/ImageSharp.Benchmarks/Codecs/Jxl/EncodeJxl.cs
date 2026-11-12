// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using JxlNet;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jxl;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;
using ManagedEncoder = SixLabors.ImageSharp.Formats.Jxl.JxlEncoder;
using NativeJxl = JxlNet.Jxl;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jxl;

/// <summary>
/// Compares complete single-threaded encoding of the same unassociated sRGB eight-bit RGBA pixels.
/// </summary>
/// <remarks>
/// Both encoders use the selected reconstruction distance and preserve transparent colors.
/// The native reference uses effort two for progressive lossless output and effort one for the other settings.
/// Both timed methods include encoder creation, encoding and disposal. Both write into one shared output stream.
/// </remarks>
[MarkdownExporter]
[Config(typeof(Config.Short))]
public unsafe class EncodeJxl
{
    /// <summary>
    /// The managed settings, with explicit color, distance and precision instead of decoded-file metadata.
    /// </summary>
    private ManagedEncoder encoder;

    /// <summary>
    /// The contiguous source pixels shared by the managed image and the native frame input.
    /// </summary>
    private Rgba32[] pixels;

    /// <summary>
    /// The image view over the prepared source pixels, retained until global cleanup.
    /// </summary>
    private Image<Rgba32> image;

    /// <summary>
    /// The output stream for both encoders. Each call rewinds it, so after the first call it has the full capacity,
    /// and the results measure the encoders instead of the growth of a new stream.
    /// </summary>
    private MemoryStream outputStream;

    /// <summary>
    /// Gets or sets the stored input decoded before benchmark measurement.
    /// </summary>
    [Params(TestImages.Jxl.EmptyPalette, TestImages.Jxl.PermutedSections, TestImages.Jxl.ProgressiveAc, TestImages.Jxl.Dice)]
    public string TestImage { get; set; }

    /// <summary>
    /// Gets or sets the reconstruction distance. Zero selects lossless encoding; one selects lossy encoding.
    /// </summary>
    [Params(0F, 1F)]
    public float Distance { get; set; }

    /// <summary>
    /// Gets or sets whether both encoders write progressive output.
    /// </summary>
    [Params(false, true)]
    public bool Progressive { get; set; }

    /// <summary>
    /// Loads equivalent source pixels before measurement.
    /// </summary>
    [GlobalSetup]
    public void ReadImages()
    {
        this.encoder = new()
        {
            ColorType = JxlColorType.Rgb,
            BitDepth = new(8, 0),
            Distance = this.Distance,
            Progressive = this.Progressive,
            SkipMetadata = true,
            TransparentColorMode = TransparentColorMode.Preserve
        };

        byte[] bytes = File.ReadAllBytes(Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage));
        DecoderOptions options = new(Configuration.Default) { SkipMetadata = true, ColorProfileHandling = ColorProfileHandling.Convert };
        using Image<Rgba32> decoded = Image.Load<Rgba32>(options, bytes);
        this.pixels = new Rgba32[checked(decoded.Width * decoded.Height)];
        decoded.CopyPixelDataTo(this.pixels);

        // Both encoders read this same array. The image view has fresh metadata because conversion above produced sRGB pixels.
        // File decoding, color conversion, pixel packing and this one-time contiguous copy are outside measurement.
        this.image = Image.WrapMemory<Rgba32>(this.pixels.AsMemory(), decoded.Width, decoded.Height);
        this.outputStream = new MemoryStream();

        uint version = NativeJxl.JxlEncoderVersion();
        int effort = this.Distance == 0 && this.Progressive ? 2 : 1;
        Console.WriteLine($"libjxl {version / 1000000}.{(version / 1000) % 1000}.{version % 1000}; effort {effort}, distance {this.Distance}, one thread.");
        Console.WriteLine($"Progressive: {this.Progressive}.");
        Console.WriteLine($"{this.TestImage}: {this.image.Width} x {this.image.Height}, unassociated sRGB RGBA8.");
    }

    /// <summary>
    /// Releases the source image view and the output stream after measurement. Its managed pixel array remains owned by this benchmark instance.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        this.image.Dispose();
        this.outputStream.Dispose();
    }

    /// <summary>
    /// Encodes the prepared pixels into the shared output stream.
    /// </summary>
    /// <returns>The complete encoded length in bytes.</returns>
    [Benchmark]
    public long ImageSharp()
    {
        // The rewound stream keeps the bytes of the previous call, so the position after the write is the encoded length.
        this.outputStream.Position = 0;
        this.image.Save(this.outputStream, this.encoder);
        return this.outputStream.Position;
    }

    /// <summary>
    /// Creates the native encoder, encodes the prepared pixels into the shared output stream and releases the encoder.
    /// </summary>
    /// <returns>The complete encoded length in bytes.</returns>
    [Benchmark(Baseline = true)]
    public long Libjxl()
    {
        // The rewound stream keeps the bytes of the previous call, so the position after the write is the encoded length.
        this.outputStream.Position = 0;
        this.EncodeLibjxl(this.outputStream);
        return this.outputStream.Position;
    }

    /// <summary>
    /// Writes one raw codestream through the existing native bindings, with one thread and preserved hidden colors.
    /// </summary>
    /// <param name="output">The stream that receives complete encoded bytes.</param>
    private void EncodeLibjxl(MemoryStream output)
    {
        global::JxlNet.JxlEncoder* native = NativeJxl.JxlEncoderCreate(null);
        if (native is null)
        {
            throw new OutOfMemoryException("libjxl could not allocate an encoder.");
        }

        try
        {
            // A null parallel runner keeps native work on this calling thread. Native output uses a raw codestream.
            Check(NativeJxl.JxlEncoderSetParallelRunner(native, IntPtr.Zero, null));
            Check(NativeJxl.JxlEncoderUseContainer(native, 0));

            JxlBasicInfo info;
            NativeJxl.JxlEncoderInitBasicInfo(&info);
            info.xsize = (uint)this.image.Width;
            info.ysize = (uint)this.image.Height;
            info.bits_per_sample = 8;
            info.num_color_channels = 3;
            info.num_extra_channels = 1;
            info.alpha_bits = 8;
            info.alpha_premultiplied = 0;
            info.uses_original_profile = this.Distance == 0 ? 1 : 0;
            Check(NativeJxl.JxlEncoderSetBasicInfo(native, &info));

            JxlColorEncoding color;
            NativeJxl.JxlColorEncodingSetToSRGB(&color, 0);
            Check(NativeJxl.JxlEncoderSetColorEncoding(native, &color));

            JxlEncoderFrameSettings* settings = NativeJxl.JxlEncoderFrameSettingsCreate(native, null);
            if (settings is null)
            {
                throw new OutOfMemoryException("libjxl could not allocate frame settings.");
            }

            Check(NativeJxl.JxlEncoderSetFrameLossless(settings, this.Distance == 0 ? 1 : 0));
            Check(NativeJxl.JxlEncoderSetFrameDistance(settings, this.Distance));
            Check(NativeJxl.JxlEncoderSetExtraChannelDistance(settings, 0, 0F));

            // Effort one's fast lossless path always writes one pass and ignores RESPONSIVE. Effort two
            // selects the ordinary modular encoder so the requested progressive lossless setting takes effect.
            int effort = this.Distance == 0 && this.Progressive ? 2 : 1;
            Check(NativeJxl.JxlEncoderFrameSettingsSetOption(settings, JxlEncoderFrameSettingId.JXL_ENC_FRAME_SETTING_EFFORT, effort));

            // Native lossless output uses modular Squeeze progression. Lossy output uses VarDCT spectral AC passes.
            Check(NativeJxl.JxlEncoderFrameSettingsSetOption(
                settings,
                this.Distance == 0 ? JxlEncoderFrameSettingId.JXL_ENC_FRAME_SETTING_RESPONSIVE : JxlEncoderFrameSettingId.JXL_ENC_FRAME_SETTING_PROGRESSIVE_AC,
                this.Progressive ? 1 : 0));

            // Preserve RGB even where alpha is zero. The managed encoder uses TransparentColorMode.Preserve.
            Check(NativeJxl.JxlEncoderFrameSettingsSetOption(settings, JxlEncoderFrameSettingId.JXL_ENC_FRAME_SETTING_KEEP_INVISIBLE, 1));

            JxlPixelFormat format = new()
            {
                num_channels = 4,
                data_type = JxlDataType.JXL_TYPE_UINT8,
                endianness = JxlEndianness.JXL_NATIVE_ENDIAN
            };

            using IMemoryOwner<byte> bufferOwner = this.image.Configuration.MemoryAllocator.Allocate<byte>(this.image.Configuration.StreamProcessingBufferSize);
            Span<byte> buffer = bufferOwner.Memory.Span;
            fixed (Rgba32* input = this.pixels)
            {
                fixed (byte* storage = buffer)
                {
                    // Pin the shared source for the complete operation. Native input preparation and any internal copies remain measured.
                    nuint length = checked((nuint)this.pixels.Length * (nuint)Unsafe.SizeOf<Rgba32>());
                    Check(NativeJxl.JxlEncoderAddImageFrame(settings, &format, input, length));
                    NativeJxl.JxlEncoderCloseInput(native);
                    while (true)
                    {
                        byte* next = storage;
                        nuint available = (nuint)buffer.Length;
                        JxlEncoderStatus status = NativeJxl.JxlEncoderProcessOutput(native, &next, &available);
                        output.Write(buffer[..(buffer.Length - checked((int)available))]);
                        if (status == JxlEncoderStatus.JXL_ENC_SUCCESS)
                        {
                            break;
                        }

                        if (status != JxlEncoderStatus.JXL_ENC_NEED_MORE_OUTPUT)
                        {
                            throw new InvalidOperationException($"libjxl did not complete benchmark encoding: {NativeJxl.JxlEncoderGetError(native)}.");
                        }
                    }
                }
            }
        }
        finally
        {
            // Destroying the encoder also releases its frame settings and retained native input.
            NativeJxl.JxlEncoderDestroy(native);
        }
    }

    /// <summary>
    /// Stops setup or measurement when the native encoder rejects a required setting or input operation.
    /// </summary>
    /// <param name="status">The status returned by the native operation.</param>
    private static void Check(JxlEncoderStatus status)
    {
        if (status != JxlEncoderStatus.JXL_ENC_SUCCESS)
        {
            throw new InvalidOperationException($"libjxl rejected a benchmark encoder operation: {status}.");
        }
    }

    /* Results 12.11.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9818)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method     | TestImage                 | Distance | Progressive | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated | Alloc Ratio |
    |----------- |-------------------------- |--------- |------------ |-----------:|------------:|----------:|------:|--------:|---------:|---------:|---------:|----------:|------------:|
    | ImageSharp | Jxl/dice.jxl              | 0        | False       |   4.416 ms |   0.5669 ms | 0.0311 ms |  0.52 |    0.00 |   7.8125 |   7.8125 |   7.8125 |   10959 B |       40.29 |
    | Libjxl     | Jxl/dice.jxl              | 0        | False       |   8.532 ms |   1.2719 ms | 0.0697 ms |  1.00 |    0.01 |        - |        - |        - |     272 B |        1.00 |
    |            |                           |          |             |            |             |           |       |         |          |          |          |           |             |
    | ImageSharp | Jxl/dice.jxl              | 0        | True        |   5.396 ms |   0.4826 ms | 0.0265 ms |  0.15 |    0.00 |  15.6250 |  15.6250 |  15.6250 |   21131 B |       28.91 |
    | Libjxl     | Jxl/dice.jxl              | 0        | True        |  36.439 ms |   2.0891 ms | 0.1145 ms |  1.00 |    0.00 |        - |        - |        - |     731 B |        1.00 |
    |            |                           |          |             |            |             |           |       |         |          |          |          |           |             |
    | ImageSharp | Jxl/dice.jxl              | 1        | False       |   5.157 ms |   1.3154 ms | 0.0721 ms |  0.24 |    0.00 |  15.6250 |  15.6250 |  15.6250 |    8770 B |       21.87 |
    | Libjxl     | Jxl/dice.jxl              | 1        | False       |  21.226 ms |   4.2897 ms | 0.2351 ms |  1.00 |    0.01 |        - |        - |        - |     401 B |        1.00 |
    |            |                           |          |             |            |             |           |       |         |          |          |          |           |             |
    | ImageSharp | Jxl/dice.jxl              | 1        | True        |   5.184 ms |   1.0561 ms | 0.0579 ms |  0.17 |    0.00 |  15.6250 |  15.6250 |  15.6250 |    8770 B |       21.87 |
    | Libjxl     | Jxl/dice.jxl              | 1        | True        |  29.779 ms |   1.8818 ms | 0.1031 ms |  1.00 |    0.00 |        - |        - |        - |     401 B |        1.00 |
    |            |                           |          |             |            |             |           |       |         |          |          |          |           |             |
    | ImageSharp | Jxl/has_permutation.jxl   | 0        | False       |   5.908 ms |   0.4359 ms | 0.0239 ms |  0.27 |    0.00 |   7.8125 |   7.8125 |   7.8125 |   11138 B |       27.78 |
    | Libjxl     | Jxl/has_permutation.jxl   | 0        | False       |  21.489 ms |   0.5220 ms | 0.0286 ms |  1.00 |    0.00 |        - |        - |        - |     401 B |        1.00 |
    |            |                           |          |             |            |             |           |       |         |          |          |          |           |             |
    | ImageSharp | Jxl/has_permutation.jxl   | 0        | True        |  13.321 ms |   0.8920 ms | 0.0489 ms |  0.10 |    0.00 |  31.2500 |  31.2500 |  31.2500 |   10451 B |        4.75 |
    | Libjxl     | Jxl/has_permutation.jxl   | 0        | True        | 139.785 ms |  10.9720 ms | 0.6014 ms |  1.00 |    0.01 |        - |        - |        - |    2198 B |        1.00 |
    |            |                           |          |             |            |             |           |       |         |          |          |          |           |             |
    | ImageSharp | Jxl/has_permutation.jxl   | 1        | False       |  21.953 ms |   9.0182 ms | 0.4943 ms |  0.29 |    0.01 |  62.5000 |  62.5000 |  62.5000 |   11102 B |        8.42 |
    | Libjxl     | Jxl/has_permutation.jxl   | 1        | False       |  75.391 ms |   9.5058 ms | 0.5210 ms |  1.00 |    0.01 |        - |        - |        - |    1318 B |        1.00 |
    |            |                           |          |             |            |             |           |       |         |          |          |          |           |             |
    | ImageSharp | Jxl/has_permutation.jxl   | 1        | True        |  30.069 ms |  20.8179 ms | 1.1411 ms |  0.23 |    0.02 |  62.5000 |  62.5000 |  62.5000 |   11135 B |        5.07 |
    | Libjxl     | Jxl/has_permutation.jxl   | 1        | True        | 132.523 ms | 163.2294 ms | 8.9472 ms |  1.00 |    0.08 |        - |        - |        - |    2198 B |        1.00 |
    |            |                           |          |             |            |             |           |       |         |          |          |          |           |             |
    | ImageSharp | Jxl/issue648_palette0.jxl | 0        | False       |  15.571 ms |   2.1124 ms | 0.1158 ms |  0.41 |    0.01 |  31.2500 |  31.2500 |  31.2500 |   36222 B |       49.55 |
    | Libjxl     | Jxl/issue648_palette0.jxl | 0        | False       |  37.653 ms |  21.5425 ms | 1.1808 ms |  1.00 |    0.04 |        - |        - |        - |     731 B |        1.00 |
    |            |                           |          |             |            |             |           |       |         |          |          |          |           |             |
    | ImageSharp | Jxl/issue648_palette0.jxl | 0        | True        |  20.627 ms |   1.5929 ms | 0.0873 ms |  0.12 |    0.00 |  62.5000 |  62.5000 |  62.5000 |   12447 B |        5.66 |
    | Libjxl     | Jxl/issue648_palette0.jxl | 0        | True        | 165.912 ms |  10.4955 ms | 0.5753 ms |  1.00 |    0.00 |        - |        - |        - |    2198 B |        1.00 |
    |            |                           |          |             |            |             |           |       |         |          |          |          |           |             |
    | ImageSharp | Jxl/issue648_palette0.jxl | 1        | False       |  21.051 ms |   5.7083 ms | 0.3129 ms |  0.31 |    0.00 |  62.5000 |  62.5000 |  62.5000 |   10894 B |        9.30 |
    | Libjxl     | Jxl/issue648_palette0.jxl | 1        | False       |  67.920 ms |  10.6730 ms | 0.5850 ms |  1.00 |    0.01 |        - |        - |        - |    1171 B |        1.00 |
    |            |                           |          |             |            |             |           |       |         |          |          |          |           |             |
    | ImageSharp | Jxl/issue648_palette0.jxl | 1        | True        |  25.149 ms |   4.0404 ms | 0.2215 ms |  0.24 |    0.00 |  62.5000 |  62.5000 |  62.5000 |   10911 B |        6.11 |
    | Libjxl     | Jxl/issue648_palette0.jxl | 1        | True        | 102.883 ms |  11.9190 ms | 0.6533 ms |  1.00 |    0.01 |        - |        - |        - |    1787 B |        1.00 |
    |            |                           |          |             |            |             |           |       |         |          |          |          |           |             |
    | ImageSharp | Jxl/progressive_ac.jxl    | 0        | False       |  29.919 ms |   2.5288 ms | 0.1386 ms |  0.39 |    0.00 |  31.2500 |  31.2500 |  31.2500 |   36222 B |       27.48 |
    | Libjxl     | Jxl/progressive_ac.jxl    | 0        | False       |  77.084 ms |   3.6761 ms | 0.2015 ms |  1.00 |    0.00 |        - |        - |        - |    1318 B |        1.00 |
    |            |                           |          |             |            |             |           |       |         |          |          |          |           |             |
    | ImageSharp | Jxl/progressive_ac.jxl    | 0        | True        |  40.029 ms |   1.0292 ms | 0.0564 ms |  0.15 |    0.00 | 153.8462 | 153.8462 | 153.8462 |   26330 B |        6.19 |
    | Libjxl     | Jxl/progressive_ac.jxl    | 0        | True        | 275.095 ms |  28.2466 ms | 1.5483 ms |  1.00 |    0.01 |        - |        - |        - |    4252 B |        1.00 |
    |            |                           |          |             |            |             |           |       |         |          |          |          |           |             |
    | ImageSharp | Jxl/progressive_ac.jxl    | 1        | False       |  41.247 ms |  29.3119 ms | 1.6067 ms |  0.27 |    0.01 | 153.8462 | 153.8462 | 153.8462 |   14381 B |        6.54 |
    | Libjxl     | Jxl/progressive_ac.jxl    | 1        | False       | 150.854 ms |  30.4876 ms | 1.6711 ms |  1.00 |    0.01 |        - |        - |        - |    2198 B |        1.00 |
    |            |                           |          |             |            |             |           |       |         |          |          |          |           |             |
    | ImageSharp | Jxl/progressive_ac.jxl    | 1        | True        |  53.636 ms |   5.6686 ms | 0.3107 ms |  0.27 |    0.00 | 222.2222 | 222.2222 | 222.2222 |   16764 B |        5.81 |
    | Libjxl     | Jxl/progressive_ac.jxl    | 1        | True        | 198.356 ms |  39.6413 ms | 2.1729 ms |  1.00 |    0.01 |        - |        - |        - |    2883 B |        1.00 |

    Allocated reports managed memory only.
    */
}

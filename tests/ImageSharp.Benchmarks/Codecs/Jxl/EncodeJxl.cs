// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using JxlNet;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jxl;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
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
/// Both timed methods include encoder creation, pooled output allocation, encoding and disposal.
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

        uint version = NativeJxl.JxlEncoderVersion();
        int effort = this.Distance == 0 && this.Progressive ? 2 : 1;
        Console.WriteLine($"libjxl {version / 1000000}.{(version / 1000) % 1000}.{version % 1000}; effort {effort}, distance {this.Distance}, one thread.");
        Console.WriteLine($"Progressive: {this.Progressive}.");
        Console.WriteLine($"{this.TestImage}: {this.image.Width} x {this.image.Height}, unassociated sRGB RGBA8.");
    }

    /// <summary>
    /// Releases the source image view after measurement. Its managed pixel array remains owned by this benchmark instance.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup() => this.image.Dispose();

    /// <summary>
    /// Creates a pooled output stream, encodes the prepared pixels and releases the output storage.
    /// </summary>
    /// <returns>The complete encoded length in bytes.</returns>
    [Benchmark]
    public long ImageSharp()
    {
        MemoryAllocator allocator = this.image.Configuration.MemoryAllocator;
        int chunkSize = (int)Math.Min((long)this.pixels.Length * Unsafe.SizeOf<Rgba32>(), allocator.GetBufferCapacityInBytes());
        using ChunkedMemoryStream output = new(allocator, chunkSize);
        this.image.Save(output, this.encoder);
        return output.Length;
    }

    /// <summary>
    /// Creates the native encoder and the same pooled output stream, then releases both after encoding.
    /// </summary>
    /// <returns>The complete encoded length in bytes.</returns>
    [Benchmark(Baseline = true)]
    public long Libjxl()
    {
        MemoryAllocator allocator = this.image.Configuration.MemoryAllocator;
        int chunkSize = (int)Math.Min((long)this.pixels.Length * Unsafe.SizeOf<Rgba32>(), allocator.GetBufferCapacityInBytes());
        using ChunkedMemoryStream output = new(allocator, chunkSize);
        this.EncodeLibjxl(output);
        return output.Length;
    }

    /// <summary>
    /// Writes one raw codestream through the existing native bindings, with one thread and preserved hidden colors.
    /// </summary>
    /// <param name="output">The stream that receives complete encoded bytes.</param>
    private void EncodeLibjxl(ChunkedMemoryStream output)
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
}

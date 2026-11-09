// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using JxlNet;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jxl;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using ExtraChannelType = SixLabors.ImageSharp.Formats.Jxl.JxlExtraChannelType;
using NativeJxl = JxlNet.Jxl;
using SampleBitDepth = SixLabors.ImageSharp.Formats.Jxl.JxlBitDepth;

namespace SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

/// <summary>
/// Encodes reference pixels and an optional extra channel with libjxl.
/// </summary>
public sealed unsafe class LibJxlReferenceEncoder : ImageEncoder
{
    /// <summary>
    /// Gets the shared binary16 reference encoder.
    /// </summary>
    public static LibJxlReferenceEncoder Instance { get; } = new();

    /// <summary>
    /// Gets the declared sample precision. The default is binary16.
    /// </summary>
    public SampleBitDepth BitDepth { get; init; } = new(16, 5);

    /// <summary>
    /// Gets the native reconstruction distance. Zero selects lossless output.
    /// </summary>
    public float Distance { get; init; }

    /// <summary>
    /// Gets the optional caller-owned extra channel used by the reference encoding.
    /// </summary>
    public JxlExtraChannel ExtraChannel { get; init; }

    /// <inheritdoc/>
    protected override void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bool halfInput = this.Distance == 0 && this.BitDepth == new SampleBitDepth(16, 5);
        int bytesPerPixel = 3 * (halfInput ? sizeof(ushort) : sizeof(float));
        int inputLength = checked(image.Width * image.Height * bytesPerPixel);
        using IMemoryOwner<byte> inputOwner = image.Configuration.MemoryAllocator.Allocate<byte>(inputLength);
        Span<byte> input = inputOwner.Memory.Span[..inputLength];
        using IMemoryOwner<Vector4> rowOwner = image.Configuration.MemoryAllocator.Allocate<Vector4>(image.Width);
        bool halfExtra = this.ExtraChannel is null ? halfInput : this.ExtraChannel.Metadata.BitDepth == new SampleBitDepth(16, 5);
        int extraLength = checked(image.Width * image.Height * (halfExtra ? sizeof(ushort) : sizeof(float)));
        using IMemoryOwner<byte> extraOwner = image.Configuration.MemoryAllocator.Allocate<byte>(extraLength, AllocationOptions.Clean);
        Span<byte> extraInput = extraOwner.Memory.Span[..extraLength];

        JxlNet.JxlEncoder* encoder = NativeJxl.JxlEncoderCreate(null);
        if (encoder is null)
        {
            throw new InvalidOperationException("libjxl could not allocate a reference encoder.");
        }

        try
        {
            // A null runner keeps the native reference on one thread. The source and output buffers
            // remain pinned until processing finishes, and the native encoder never owns either buffer.
            Check(encoder, NativeJxl.JxlEncoderSetParallelRunner(encoder, IntPtr.Zero, null));
            Check(encoder, NativeJxl.JxlEncoderUseContainer(encoder, 0));

            JxlBasicInfo info;
            NativeJxl.JxlEncoderInitBasicInfo(&info);
            info.xsize = (uint)image.Width;
            info.ysize = (uint)image.Height;
            info.bits_per_sample = this.BitDepth.BitsPerSample;
            info.exponent_bits_per_sample = this.BitDepth.ExponentBits;
            info.num_color_channels = 3;
            info.num_extra_channels = 1;
            JxlExtraChannelMetadata extra = this.ExtraChannel?.Metadata;
            bool hasAlpha = extra is null || extra.Type == ExtraChannelType.Alpha;
            info.alpha_bits = hasAlpha ? (uint)(extra?.BitDepth.BitsPerSample ?? this.BitDepth.BitsPerSample) : 0U;
            info.alpha_exponent_bits = hasAlpha ? (uint)(extra?.BitDepth.ExponentBits ?? this.BitDepth.ExponentBits) : 0U;
            info.alpha_premultiplied = extra?.AlphaRepresentation == PixelAlphaRepresentation.Associated ? 1 : 0;
            info.have_animation = image.Frames.Count > 1 ? 1 : 0;
            info.uses_original_profile = this.Distance == 0 ? 1 : 0;
            Check(encoder, NativeJxl.JxlEncoderSetBasicInfo(encoder, &info));

            if (extra is not null)
            {
                JxlExtraChannelInfo channel;
                NativeJxl.JxlEncoderInitExtraChannelInfo((JxlNet.JxlExtraChannelType)extra.Type, &channel);
                channel.bits_per_sample = extra.BitDepth.BitsPerSample;
                channel.exponent_bits_per_sample = extra.BitDepth.ExponentBits;
                channel.dim_shift = extra.DimensionShift;
                channel.alpha_premultiplied = info.alpha_premultiplied;
                channel.cfa_channel = extra.ColorFilterArrayChannel;
                channel.spot_color[0] = extra.SpotColor.X;
                channel.spot_color[1] = extra.SpotColor.Y;
                channel.spot_color[2] = extra.SpotColor.Z;
                channel.spot_color[3] = extra.SpotColor.W;
                Check(encoder, NativeJxl.JxlEncoderSetExtraChannelInfo(encoder, 0, &channel));
                byte[] name = Encoding.UTF8.GetBytes(extra.Name);

                fixed (byte* nameBytes = name)
                {
                    Check(encoder, NativeJxl.JxlEncoderSetExtraChannelName(encoder, 0, (sbyte*)nameBytes, (nuint)name.Length));
                }
            }

            JxlColorEncoding color;
            NativeJxl.JxlColorEncodingSetToSRGB(&color, 0);
            Check(encoder, NativeJxl.JxlEncoderSetColorEncoding(encoder, &color));

            JxlEncoderFrameSettings* settings = NativeJxl.JxlEncoderFrameSettingsCreate(encoder, null);
            if (settings is null)
            {
                throw new InvalidOperationException("libjxl could not allocate reference frame settings.");
            }

            Check(encoder, NativeJxl.JxlEncoderSetFrameLossless(settings, this.Distance == 0 ? 1 : 0));
            Check(encoder, NativeJxl.JxlEncoderSetFrameDistance(settings, this.Distance));
            Check(encoder, NativeJxl.JxlEncoderSetExtraChannelDistance(settings, 0, 0F));
            bool binary32Samples = this.BitDepth == new SampleBitDepth(32, 8)
                || this.ExtraChannel?.Metadata.BitDepth == new SampleBitDepth(32, 8);

            // Binary32 samples use signed integer carriers. Differences between carriers can exceed the signed range.
            // Effort four is the first native tier that honors the selected predictor instead of forcing a fixed tree.
            Check(encoder, NativeJxl.JxlEncoderFrameSettingsSetOption(
                settings,
                JxlEncoderFrameSettingId.JXL_ENC_FRAME_SETTING_EFFORT,
                binary32Samples ? 4 : 1));

            if (binary32Samples)
            {
                Check(encoder, NativeJxl.JxlEncoderFrameSettingsSetOption(settings, JxlEncoderFrameSettingId.JXL_ENC_FRAME_SETTING_MODULAR_PREDICTOR, 0));
            }

            Check(encoder, NativeJxl.JxlEncoderFrameSettingsSetOption(settings, JxlEncoderFrameSettingId.JXL_ENC_FRAME_SETTING_KEEP_INVISIBLE, 1));

            // Native lossy modular defaults to responsive encoding, which selects a different quantizer.
            // Select the same single-pass modular path as the managed encoder before comparing decoded pixels.
            Check(encoder, NativeJxl.JxlEncoderFrameSettingsSetOption(settings, JxlEncoderFrameSettingId.JXL_ENC_FRAME_SETTING_MODULAR, 1));
            Check(encoder, NativeJxl.JxlEncoderFrameSettingsSetOption(settings, JxlEncoderFrameSettingId.JXL_ENC_FRAME_SETTING_RESPONSIVE, 0));

            if (extra is not null)
            {
                // The native input adapter requires a full-size buffer. ALREADY_DOWNSAMPLED prevents a second
                // filtering pass; modular coding consumes its reduced top-left rectangle at this declared scale.
                // Set color resampling explicitly: native frame-size calculation uses it before replacing auto defaults.
                Check(encoder, NativeJxl.JxlEncoderFrameSettingsSetOption(settings, JxlEncoderFrameSettingId.JXL_ENC_FRAME_SETTING_RESAMPLING, 1));
                Check(encoder, NativeJxl.JxlEncoderFrameSettingsSetOption(
                    settings, JxlEncoderFrameSettingId.JXL_ENC_FRAME_SETTING_EXTRA_CHANNEL_RESAMPLING, 1 << extra.DimensionShift));

                Check(encoder, NativeJxl.JxlEncoderFrameSettingsSetOption(settings, JxlEncoderFrameSettingId.JXL_ENC_FRAME_SETTING_ALREADY_DOWNSAMPLED, 1));
            }

            JxlPixelFormat format = new()
            {
                num_channels = 3,
                data_type = halfInput ? JxlDataType.JXL_TYPE_FLOAT16 : JxlDataType.JXL_TYPE_FLOAT,
                endianness = JxlEndianness.JXL_NATIVE_ENDIAN
            };

            JxlPixelFormat extraFormat = new()
            {
                num_channels = 1,
                data_type = halfExtra ? JxlDataType.JXL_TYPE_FLOAT16 : JxlDataType.JXL_TYPE_FLOAT,
                endianness = JxlEndianness.JXL_NATIVE_ENDIAN
            };

            using IMemoryOwner<byte> outputOwner = image.Configuration.MemoryAllocator.Allocate<byte>(image.Configuration.StreamProcessingBufferSize);
            Span<byte> output = outputOwner.Memory.Span;

            fixed (byte* pixels = input, extraPixels = extraInput, buffer = output)
            {
                for (int frameIndex = 0; frameIndex < image.Frames.Count; frameIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    CopyInput(image.Frames[frameIndex], input, this.ExtraChannel is null ? extraInput : [], rowOwner.Memory.Span, halfInput);

                    if (info.have_animation != 0)
                    {
                        JxlFrameHeader header;
                        NativeJxl.JxlEncoderInitFrameHeader(&header);
                        header.duration = 1;
                        Check(encoder, NativeJxl.JxlEncoderSetFrameHeader(settings, &header));
                    }

                    Check(encoder, NativeJxl.JxlEncoderAddImageFrame(settings, &format, pixels, (nuint)input.Length));

                    if (this.ExtraChannel is JxlExtraChannel supplied)
                    {
                        Buffer2D<float> plane = supplied.Frames[frameIndex];

                        for (int y = 0; y < plane.Height; y++)
                        {
                            ReadOnlySpan<float> samples = plane.DangerousGetRowSpan(y);
                            int start = y * image.Width;

                            if (halfExtra)
                            {
                                Span<ushort> destination = MemoryMarshal.Cast<byte, ushort>(extraInput).Slice(start, plane.Width);

                                for (int x = 0; x < samples.Length; x++)
                                {
                                    destination[x] = HalfTypeHelper.Pack(samples[x]);
                                }
                            }
                            else
                            {
                                samples.CopyTo(MemoryMarshal.Cast<byte, float>(extraInput).Slice(start, plane.Width));
                            }
                        }
                    }

                    // Supply alpha through the same native plane API as every other extra channel.
                    // RGB input contains no alpha, so it cannot replace this independently supplied plane.
                    Check(encoder, NativeJxl.JxlEncoderSetExtraChannelBuffer(settings, &extraFormat, extraPixels, (nuint)extraInput.Length, 0));
                }

                NativeJxl.JxlEncoderCloseInput(encoder);

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    byte* next = buffer;
                    nuint available = (nuint)output.Length;
                    JxlEncoderStatus status = NativeJxl.JxlEncoderProcessOutput(encoder, &next, &available);
                    stream.Write(output[..(output.Length - checked((int)available))]);

                    if (status == JxlEncoderStatus.JXL_ENC_SUCCESS)
                    {
                        break;
                    }

                    if (status != JxlEncoderStatus.JXL_ENC_NEED_MORE_OUTPUT)
                    {
                        throw new InvalidOperationException($"libjxl did not complete reference encoding: {NativeJxl.JxlEncoderGetError(encoder)}.");
                    }
                }
            }
        }
        finally
        {
            // Destroying the encoder also releases its frame settings and all native-owned working storage.
            NativeJxl.JxlEncoderDestroy(encoder);
        }
    }

    /// <summary>
    /// Packs one source frame as native RGB and, when needed, a separate alpha plane.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel type.</typeparam>
    /// <param name="frame">The source frame.</param>
    /// <param name="input">The reusable full-frame native input buffer.</param>
    /// <param name="alpha">The reusable alpha buffer, or an empty span when the caller supplies another plane.</param>
    /// <param name="row">The reusable color-vector row for RGB packing.</param>
    /// <param name="halfInput">Whether native input uses binary16 samples.</param>
    private static void CopyInput<TPixel>(ImageFrame<TPixel> frame, Span<byte> input, Span<byte> alpha, Span<Vector4> row, bool halfInput)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        for (int y = 0; y < frame.Height; y++)
        {
            ReadOnlySpan<TPixel> source = frame.PixelBuffer.DangerousGetRowSpan(y);

            // Convert each source row once. RGB and alpha use separate native buffers with unchanged sample values.
            // Binary16 packing uses the same conversion as RgbaHalf; float32 packing copies the vector components.
            PixelOperations<TPixel>.Instance.ToUnassociatedScaledVector4(frame.Configuration, source, row);
            int start = y * frame.Width * 3;
            int alphaStart = y * frame.Width;

            if (halfInput)
            {
                Span<ushort> samples = MemoryMarshal.Cast<byte, ushort>(input);
                Span<ushort> alphaSamples = MemoryMarshal.Cast<byte, ushort>(alpha);

                for (int x = 0; x < frame.Width; x++)
                {
                    Vector4 value = row[x];
                    int offset = start + (x * 3);
                    samples[offset] = HalfTypeHelper.Pack(value.X);
                    samples[offset + 1] = HalfTypeHelper.Pack(value.Y);
                    samples[offset + 2] = HalfTypeHelper.Pack(value.Z);

                    if (!alpha.IsEmpty)
                    {
                        alphaSamples[alphaStart + x] = HalfTypeHelper.Pack(value.W);
                    }
                }
            }
            else
            {
                Span<float> samples = MemoryMarshal.Cast<byte, float>(input);
                Span<float> alphaSamples = MemoryMarshal.Cast<byte, float>(alpha);

                for (int x = 0; x < frame.Width; x++)
                {
                    Vector4 value = row[x];
                    int offset = start + (x * 3);
                    samples[offset] = value.X;
                    samples[offset + 1] = value.Y;
                    samples[offset + 2] = value.Z;

                    if (!alpha.IsEmpty)
                    {
                        alphaSamples[alphaStart + x] = value.W;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Stops reference encoding at the native operation that failed.
    /// </summary>
    /// <param name="encoder">The native encoder that supplies the operation error.</param>
    /// <param name="status">The result of the native encoder operation.</param>
    private static void Check(JxlNet.JxlEncoder* encoder, JxlEncoderStatus status)
    {
        if (status != JxlEncoderStatus.JXL_ENC_SUCCESS)
        {
            throw new InvalidOperationException($"libjxl rejected a reference encoder operation: {status}, {NativeJxl.JxlEncoderGetError(encoder)}.");
        }
    }
}

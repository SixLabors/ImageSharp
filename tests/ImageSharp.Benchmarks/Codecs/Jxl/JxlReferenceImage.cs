// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using JxlNet;
using SixLabors.ImageSharp.PixelFormats;
using NativeJxl = JxlNet.Jxl;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jxl;

/// <summary>
/// Owns one reference decoder's contiguous float pixels until the benchmark or its validation releases them.
/// </summary>
internal sealed unsafe class JxlReferenceImage : SafeHandle
{
    private DecodeResult result;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlReferenceImage"/> class and applies the shared pixel range in place.
    /// </summary>
    /// <param name="result">The completed native allocation and dimensions.</param>
    private JxlReferenceImage(DecodeResult result)
        : base(IntPtr.Zero, true)
    {
        this.result = result;

        // Reference float output retains negative or greater-than-one alpha. RgbaVector has the library's
        // finite RGB and normalized alpha contract. Apply that same contract directly to the native allocation.
        // This pass is part of every timed reference decode; it creates no pixel copy or second image allocation.
        Span<Vector4> pixels = new(result.Pixels, checked((int)(result.Length / 4)));
        PixelOperations<RgbaVector>.Instance.FromUnassociatedScaledVector4Destructive(
            Configuration.Default, pixels, MemoryMarshal.Cast<Vector4, RgbaVector>(pixels));

        // Publish the handle only after conversion succeeds. Until this point, the factory owns failure cleanup.
        // A constructor failure therefore cannot free the same allocation through both its catch and a finalizer.
        this.SetHandle((nint)result.Pixels);
    }

    /// <inheritdoc/>
    public override bool IsInvalid => this.handle == IntPtr.Zero;

    /// <summary>
    /// Gets the output width in codestream pixels.
    /// </summary>
    public int Width => checked((int)this.result.Width);

    /// <summary>
    /// Gets the output height in codestream pixels.
    /// </summary>
    public int Height => checked((int)this.result.Height);

    /// <summary>
    /// Gets the unassociated sRGB pixel vectors. The span is valid only while this owner remains undisposed.
    /// </summary>
    public Span<Vector4> Pixels => new((void*)this.handle, checked((int)(this.result.Length / 4)));

    /// <summary>
    /// Decodes one still image through libjxl's direct C API with no parallel runner.
    /// </summary>
    /// <param name="source">The complete stored JPEG XL input.</param>
    /// <returns>The native output owner. Decoder creation, pixel allocation and range conversion occur within this call.</returns>
    public static JxlReferenceImage DecodeLibjxl(ReadOnlySpan<byte> source)
    {
        JxlDecoder* decoder = NativeJxl.JxlDecoderCreate(null);
        if (decoder is null)
        {
            throw new OutOfMemoryException("libjxl could not allocate a decoder.");
        }

        DecodeResult result = default;
        try
        {
            // A null parallel runner executes work on this calling thread. This setting does not affect .NET,
            // the JIT, process affinity or the native SIMD dispatch. Each invocation creates its own decoder.
            Check(NativeJxl.JxlDecoderSetParallelRunner(decoder, IntPtr.Zero, null));
            Check(NativeJxl.JxlDecoderSetKeepOrientation(decoder, 1));
            Check(NativeJxl.JxlDecoderSetUnpremultiplyAlpha(decoder, 1));
            Check(NativeJxl.JxlDecoderSubscribeEvents(
                decoder, (int)(JxlDecoderStatus.JXL_DEC_BASIC_INFO | JxlDecoderStatus.JXL_DEC_COLOR_ENCODING | JxlDecoderStatus.JXL_DEC_FULL_IMAGE)));

            JxlPixelFormat format = new()
            {
                num_channels = 4,
                data_type = JxlDataType.JXL_TYPE_FLOAT,
                endianness = JxlEndianness.JXL_NATIVE_ENDIAN
            };

            fixed (byte* input = source)
            {
                Check(NativeJxl.JxlDecoderSetInput(decoder, input, (nuint)source.Length));
                NativeJxl.JxlDecoderCloseInput(decoder);
                bool complete = false;
                while (true)
                {
                    JxlDecoderStatus status = NativeJxl.JxlDecoderProcessInput(decoder);
                    switch (status)
                    {
                        case JxlDecoderStatus.JXL_DEC_BASIC_INFO:
                            JxlBasicInfo info;
                            Check(NativeJxl.JxlDecoderGetBasicInfo(decoder, &info));
                            if (info.have_animation != 0)
                            {
                                throw new InvalidOperationException("The JPEG XL comparison requires a still image.");
                            }

                            result.Width = info.xsize;
                            result.Height = info.ysize;
                            break;

                        case JxlDecoderStatus.JXL_DEC_COLOR_ENCODING:
                            JxlColorEncoding color;
                            NativeJxl.JxlColorEncodingSetToSRGB(&color, 0);
                            Check(NativeJxl.JxlDecoderSetPreferredColorProfile(decoder, &color));
                            break;

                        case JxlDecoderStatus.JXL_DEC_NEED_IMAGE_OUT_BUFFER:
                            nuint bytes;
                            Check(NativeJxl.JxlDecoderImageOutBufferSize(decoder, &format, &bytes));
                            result.Pixels = (float*)NativeMemory.Alloc(bytes);
                            result.Length = bytes / sizeof(float);
                            Check(NativeJxl.JxlDecoderSetImageOutBuffer(decoder, &format, result.Pixels, bytes));
                            break;

                        case JxlDecoderStatus.JXL_DEC_FULL_IMAGE:
                            complete = true;
                            break;

                        case JxlDecoderStatus.JXL_DEC_SUCCESS when complete:
                            return new JxlReferenceImage(result);

                        default:
                            throw new InvalidOperationException($"libjxl did not complete the benchmark image: {status}.");
                    }
                }
            }
        }
        catch
        {
            NativeMemory.Free(result.Pixels);
            throw;
        }
        finally
        {
            NativeJxl.JxlDecoderDestroy(decoder);
        }
    }

    /// <inheritdoc/>
    protected override bool ReleaseHandle()
    {
        // The libjxl adapter allocates the output buffer with NativeMemory, so this owner releases it here.
        NativeMemory.Free(this.result.Pixels);
        return true;
    }

    /// <summary>
    /// Rejects a native API error at the call that reports it.
    /// </summary>
    /// <param name="status">The status returned by a libjxl setter or query.</param>
    private static void Check(JxlDecoderStatus status)
    {
        if (status != JxlDecoderStatus.JXL_DEC_SUCCESS)
        {
            throw new InvalidOperationException($"libjxl rejected a benchmark decoder setting: {status}.");
        }
    }

    /// <summary>
    /// Holds the native output buffer and the image dimensions. Length counts floats.
    /// </summary>
    private struct DecodeResult
    {
        public float* Pixels;
        public nuint Length;
        public nuint Width;
        public nuint Height;
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using JxlNet;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using NativeJxl = JxlNet.Jxl;

namespace SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

/// <summary>
/// Decodes JPEG XL inputs with libjxl for in-memory image comparisons.
/// </summary>
public sealed unsafe class LibJxlReferenceDecoder : ImageDecoder
{
    // Request native float pixels so the reference retains extended RGB and fractional precision.
    // Libjxl owns reconstruction and color conversion. ImageSharp only packs the resulting pixels.

    /// <summary>
    /// Gets the shared reference decoder.
    /// </summary>
    public static LibJxlReferenceDecoder Instance { get; } = new();

    /// <inheritdoc/>
    protected override Image<TPixel> Decode<TPixel>(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        // The decoder base supplies seekable input. Retain its bytes until native decoding finishes:
        // libjxl borrows the pinned input instead of taking ownership of it.
        int length = checked((int)(stream.Length - stream.Position));
        using IMemoryOwner<byte> input = options.Configuration.MemoryAllocator.Allocate<byte>(length);
        Span<byte> bytes = input.GetSpan();
        stream.ReadExactly(bytes);

        JxlDecoder* decoder = NativeJxl.JxlDecoderCreate(null);
        if (decoder is null)
        {
            throw new InvalidOperationException("libjxl could not allocate a decoder.");
        }

        void* pixels = null;
        int width = 0;
        int height = 0;
        bool grayscale = false;
        bool usesOriginalProfile = false;
        Image<TPixel> image = null;
        try
        {
            // A null runner keeps this reference decode on the calling thread. The native CMS
            // handles embedded profiles, including device encodings that need more than RGB.
            Check(NativeJxl.JxlDecoderSetParallelRunner(decoder, IntPtr.Zero, null));
            Check(NativeJxl.JxlDecoderSetCms(decoder, *JxlCms.JxlGetDefaultCms()));
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

            fixed (byte* source = bytes)
            {
                Check(NativeJxl.JxlDecoderSetInput(decoder, source, (nuint)length));
                NativeJxl.JxlDecoderCloseInput(decoder);
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    JxlDecoderStatus status = NativeJxl.JxlDecoderProcessInput(decoder);
                    switch (status)
                    {
                        case JxlDecoderStatus.JXL_DEC_BASIC_INFO:
                            JxlBasicInfo info;
                            Check(NativeJxl.JxlDecoderGetBasicInfo(decoder, &info));
                            width = checked((int)info.xsize);
                            height = checked((int)info.ysize);
                            grayscale = info.num_color_channels == 1;
                            usesOriginalProfile = info.uses_original_profile != 0;

                            break;

                        case JxlDecoderStatus.JXL_DEC_COLOR_ENCODING:
                        {
                            // decode.h requires the CMS before this event and the output profile before
                            // pixel decoding. Keep grayscale as grayscale; libjxl expands it to the RGBA buffer.
                            JxlColorEncoding srgb;
                            NativeJxl.JxlColorEncodingSetToSRGB(&srgb, grayscale ? 1 : 0);
                            JxlColorEncoding originalEncoding;
                            if (NativeJxl.JxlDecoderGetColorAsEncodedProfile(
                                decoder, JxlColorProfileTarget.JXL_COLOR_PROFILE_TARGET_ORIGINAL, &originalEncoding) == JxlDecoderStatus.JXL_DEC_SUCCESS)
                            {
                                srgb.rendering_intent = originalEncoding.rendering_intent;
                            }
                            else
                            {
                                // ICC-only inputs have no structured encoding. Read the declared intent
                                // through the existing ICC metadata API; libjxl performs the transform itself.
                                nuint profileSize;
                                Check(NativeJxl.JxlDecoderGetICCProfileSize(
                                    decoder, JxlColorProfileTarget.JXL_COLOR_PROFILE_TARGET_ORIGINAL, &profileSize));

                                byte[] profile = new byte[checked((int)profileSize)];
                                fixed (byte* profileData = profile)
                                {
                                    Check(NativeJxl.JxlDecoderGetColorAsICCProfile(
                                        decoder, JxlColorProfileTarget.JXL_COLOR_PROFILE_TARGET_ORIGINAL, profileData, profileSize));
                                }

                                IccProfile sourceProfile = new(profile);

                                // Match the RGB destination profile. Libjxl expands grayscale output to RGBA.
                                if (!grayscale && usesOriginalProfile && IccProfile.TryGetIccProfileForColorConversion(sourceProfile, out _))
                                {
                                    byte[] targetProfile = CompactSrgbV4Profile.Profile.ToByteArray();
                                    fixed (byte* targetProfileData = targetProfile)
                                    {
                                        Check(NativeJxl.JxlDecoderSetOutputColorProfile(decoder, null, targetProfileData, (nuint)targetProfile.Length));
                                    }

                                    break;
                                }

                                srgb.rendering_intent = (JxlRenderingIntent)sourceProfile.Header.RenderingIntent;
                            }

                            Check(NativeJxl.JxlDecoderSetOutputColorProfile(decoder, &srgb, null, 0));

                            break;
                        }

                        case JxlDecoderStatus.JXL_DEC_NEED_IMAGE_OUT_BUFFER:
                            nuint size;
                            Check(NativeJxl.JxlDecoderImageOutBufferSize(decoder, &format, &size));
                            pixels = NativeMemory.Realloc(pixels, size);
                            Check(NativeJxl.JxlDecoderSetImageOutBuffer(decoder, &format, pixels, size));

                            break;

                        case JxlDecoderStatus.JXL_DEC_FULL_IMAGE:
                        {
                            Span<Vector4> colors = new(pixels, checked(width * height));
                            ImageFrame<TPixel> frame;
                            if (image is null)
                            {
                                image = new Image<TPixel>(options.Configuration, width, height);
                                frame = image.Frames.RootFrame;
                            }
                            else
                            {
                                frame = image.Frames.CreateFrame(default(TPixel));
                            }

                            PixelOperations<TPixel> pixelOperations = PixelOperations<TPixel>.Instance;
                            for (int y = 0; y < height; y++)
                            {
                                pixelOperations.FromUnassociatedScaledVector4Destructive(
                                    options.Configuration, colors.Slice(y * width, width), frame.PixelBuffer.DangerousGetRowSpan(y));
                            }

                            if (image.Frames.Count >= options.MaxFrames)
                            {
                                return image;
                            }

                            break;
                        }

                        case JxlDecoderStatus.JXL_DEC_SUCCESS when image is not null:
                            return image;

                        default:
                            throw new InvalidImageContentException($"libjxl did not complete the reference image: {status}.");
                    }
                }
            }
        }
        catch
        {
            image?.Dispose();
            throw;
        }
        finally
        {
            // libjxl borrows the output allocation. Release the decoder before freeing that allocation.
            NativeJxl.JxlDecoderDestroy(decoder);
            NativeMemory.Free(pixels);
        }
    }

    /// <inheritdoc/>
    protected override Image Decode(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        => this.Decode<RgbaVector>(options, stream, cancellationToken);

    /// <inheritdoc/>
    protected override ImageInfo Identify(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        using Image<RgbaVector> image = this.Decode<RgbaVector>(options, stream, cancellationToken);
        return new ImageInfo(image.Size, image.Metadata);
    }

    /// <summary>
    /// Reports a native configuration or buffer error at the call that produced it.
    /// </summary>
    /// <param name="status">The status returned by libjxl.</param>
    private static void Check(JxlDecoderStatus status)
    {
        if (status != JxlDecoderStatus.JXL_DEC_SUCCESS)
        {
            throw new InvalidOperationException($"libjxl rejected a reference decoder operation: {status}.");
        }
    }
}

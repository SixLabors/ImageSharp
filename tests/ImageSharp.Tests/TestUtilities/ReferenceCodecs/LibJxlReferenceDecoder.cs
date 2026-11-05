// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Numerics.Tensors;
using System.Runtime.InteropServices;
using ImageMagick;
using JxlNet;
using SixLabors.ImageSharp.ColorProfiles;
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
    // Magick.NET 15's JXL comparisons lose the extended values required by these float tests.
    // Request float output directly from libjxl so integer export cannot clip or quantize RGB.
    // Original RGB samples can use the native decoder's sRGB output directly. Coded XYB frames
    // must compose in their original encoding before the native CMS converts their RGB samples.

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
        void* black = null;
        int width = 0;
        int height = 0;
        bool grayscale = false;
        bool usesOriginalProfile = false;
        bool directSrgb = false;
        float intensityTarget = 0;
        byte[] nativeSrgbProfile = null;
        uint blackIndex = uint.MaxValue;
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
                            intensityTarget = info.intensity_target;

                            for (uint i = 0; i < info.num_extra_channels; i++)
                            {
                                JxlExtraChannelInfo extra;
                                Check(NativeJxl.JxlDecoderGetExtraChannelInfo(decoder, i, &extra));
                                if (extra.type == JxlExtraChannelType.JXL_CHANNEL_BLACK)
                                {
                                    blackIndex = i;
                                }
                            }

                            break;

                        case JxlDecoderStatus.JXL_DEC_COLOR_ENCODING:
                        {
                            JxlColorEncoding originalEncoding;
                            bool hasOriginalEncoding = NativeJxl.JxlDecoderGetColorAsEncodedProfile(
                                decoder, JxlColorProfileTarget.JXL_COLOR_PROFILE_TARGET_ORIGINAL, &originalEncoding) == JxlDecoderStatus.JXL_DEC_SUCCESS;

                            if (usesOriginalProfile && hasOriginalEncoding && originalEncoding.color_space == JxlColorSpace.JXL_COLOR_SPACE_RGB
                                && !grayscale && blackIndex == uint.MaxValue)
                            {
                                // Native conversion follows composition for original structured RGB samples. In libjxl 0.12,
                                // DATA still reports the original encoding here, so retain the successful request.
                                // Retain the declared intent. ICC-only RGB, gray and CMYK keep their existing paths.
                                JxlColorEncoding srgb;
                                NativeJxl.JxlColorEncodingSetToSRGB(&srgb, 0);
                                srgb.rendering_intent = originalEncoding.rendering_intent;
                                Check(NativeJxl.JxlDecoderSetOutputColorProfile(decoder, &srgb, null, 0));
                                directSrgb = true;
                            }
                            else if (!usesOriginalProfile && hasOriginalEncoding && originalEncoding.color_space == JxlColorSpace.JXL_COLOR_SPACE_RGB
                                && !grayscale && blackIndex == uint.MaxValue)
                            {
                                // Coded XYB blends in its original encoding. Capture libjxl's own sRGB ICC before
                                // restoring that encoding, while profile requests are still allowed. The generated
                                // profile retains the native curve and primaries for extended float conversion.
                                JxlColorEncoding srgb;
                                NativeJxl.JxlColorEncodingSetToSRGB(&srgb, 0);
                                srgb.rendering_intent = originalEncoding.rendering_intent;
                                Check(NativeJxl.JxlDecoderSetOutputColorProfile(decoder, &srgb, null, 0));

                                nuint srgbProfileSize;
                                Check(NativeJxl.JxlDecoderGetICCProfileSize(
                                    decoder, JxlColorProfileTarget.JXL_COLOR_PROFILE_TARGET_DATA, &srgbProfileSize));

                                nativeSrgbProfile = new byte[checked((int)srgbProfileSize)];
                                fixed (byte* srgbProfileData = nativeSrgbProfile)
                                {
                                    Check(NativeJxl.JxlDecoderGetColorAsICCProfile(
                                        decoder, JxlColorProfileTarget.JXL_COLOR_PROFILE_TARGET_DATA, srgbProfileData, srgbProfileSize));
                                }

                                Check(NativeJxl.JxlDecoderSetOutputColorProfile(decoder, &originalEncoding, null, 0));
                            }

                            // An XYB input with an ICC declaration initially selects linear sRGB because the
                            // original profile has no structured fields yet. Ask the configured native CMS to
                            // recognize that same profile before decoding: cropped frames must blend in their
                            // original encoding, and libjxl rejects composition in the initial fallback space.
                            // Inputs that already carry original device samples do not need this selection.
                            if (!usesOriginalProfile && !hasOriginalEncoding)
                            {
                                nuint originalProfileSize;
                                Check(NativeJxl.JxlDecoderGetICCProfileSize(
                                    decoder, JxlColorProfileTarget.JXL_COLOR_PROFILE_TARGET_ORIGINAL, &originalProfileSize));

                                byte[] originalProfile = new byte[checked((int)originalProfileSize)];
                                fixed (byte* originalProfileData = originalProfile)
                                {
                                    Check(NativeJxl.JxlDecoderGetColorAsICCProfile(
                                        decoder, JxlColorProfileTarget.JXL_COLOR_PROFILE_TARGET_ORIGINAL, originalProfileData, originalProfileSize));
                                    Check(NativeJxl.JxlDecoderSetOutputColorProfile(decoder, null, originalProfileData, originalProfileSize));
                                }
                            }

                            break;
                        }

                        case JxlDecoderStatus.JXL_DEC_NEED_IMAGE_OUT_BUFFER:
                            nuint size;
                            Check(NativeJxl.JxlDecoderImageOutBufferSize(decoder, &format, &size));
                            pixels = NativeMemory.Alloc(size);
                            Check(NativeJxl.JxlDecoderSetImageOutBuffer(decoder, &format, pixels, size));
                            if (blackIndex != uint.MaxValue)
                            {
                                Check(NativeJxl.JxlDecoderExtraChannelBufferSize(decoder, &format, &size, blackIndex));
                                black = NativeMemory.Alloc(size);
                                Check(NativeJxl.JxlDecoderSetExtraChannelBuffer(decoder, &format, black, size, blackIndex));
                            }

                            break;

                        case JxlDecoderStatus.JXL_DEC_FULL_IMAGE:
                        {
                            Span<Vector4> colors = new(pixels, checked(width * height));

                            // decode.h distinguishes the exact structured encoding from its generated ICC
                            // approximation. Native sRGB output already has the requested primaries and transfer.
                            // Preserve those float samples instead of sending them through a second CMS transform.
                            JxlColorEncoding encoding;
                            if (directSrgb || (NativeJxl.JxlDecoderGetColorAsEncodedProfile(
                                decoder, JxlColorProfileTarget.JXL_COLOR_PROFILE_TARGET_DATA, &encoding) == JxlDecoderStatus.JXL_DEC_SUCCESS
                                && encoding.white_point == JxlWhitePoint.JXL_WHITE_POINT_D65
                                && encoding.transfer_function == JxlTransferFunction.JXL_TRANSFER_FUNCTION_SRGB
                                && (encoding.color_space == JxlColorSpace.JXL_COLOR_SPACE_GRAY
                                    || (encoding.color_space == JxlColorSpace.JXL_COLOR_SPACE_RGB && encoding.primaries == JxlPrimaries.JXL_PRIMARIES_SRGB))))
                            {
                                return CreateImage<TPixel>(options.Configuration, colors, width, height);
                            }

                            // DATA describes the returned samples. ORIGINAL can describe a different encoding
                            // when libjxl has already reconstructed XYB into its default RGB output space.
                            nuint profileSize;
                            Check(NativeJxl.JxlDecoderGetICCProfileSize(decoder, JxlColorProfileTarget.JXL_COLOR_PROFILE_TARGET_DATA, &profileSize));
                            byte[] profileBytes = new byte[checked((int)profileSize)];
                            fixed (byte* profileData = profileBytes)
                            {
                                Check(NativeJxl.JxlDecoderGetColorAsICCProfile(
                                    decoder, JxlColorProfileTarget.JXL_COLOR_PROFILE_TARGET_DATA, profileData, profileSize));
                            }

                            if (nativeSrgbProfile is not null)
                            {
                                ConvertRgbToSrgb(colors, width, profileBytes, nativeSrgbProfile, encoding, intensityTarget);
                                return CreateImage<TPixel>(options.Configuration, colors, width, height);
                            }

                            Span<float> opacity = new(black, black is null ? 0 : colors.Length);
                            string mapping = "RGBA";
                            Span<float> components = MemoryMarshal.Cast<Vector4, float>(colors);
                            if (black is not null)
                            {
                                // JXL stores CMYK as inverted device samples. Magick's float CMYK import
                                // expects ink amounts in [0,1]. Reuse the black plane to retain opacity,
                                // then replace the fourth interleaved component with the black ink sample.
                                for (int i = 0; i < colors.Length; i++)
                                {
                                    Vector4 value = colors[i];
                                    float k = opacity[i];
                                    opacity[i] = value.W;
                                    colors[i] = Vector4.One - new Vector4(value.X, value.Y, value.Z, k);
                                }

                                mapping = "CMYK";
                            }
                            else if (grayscale)
                            {
                                // A grayscale profile has one device component. Compact RGBA to intensity
                                // and alpha in place. Each write precedes the next unread RGBA pixel.
                                for (int i = 0; i < colors.Length; i++)
                                {
                                    Vector4 value = colors[i];
                                    components[2 * i] = value.X;
                                    components[(2 * i) + 1] = value.W;
                                }

                                components = components[..(2 * colors.Length)];
                                mapping = "IA";
                            }

                            PixelReadSettings settings = new((uint)width, (uint)height, StorageType.Float, mapping);
                            using MagickImage converted = new(MemoryMarshal.AsBytes(components), settings);
                            converted.SetProfile(new ImageMagick.ColorProfile(profileBytes));
                            IccProfile profile = new(profileBytes);
                            converted.RenderingIntent = profile.Header.RenderingIntent switch
                            {
                                IccRenderingIntent.Perceptual => RenderingIntent.Perceptual,
                                IccRenderingIntent.MediaRelativeColorimetric => RenderingIntent.Relative,
                                IccRenderingIntent.Saturation => RenderingIntent.Saturation,
                                IccRenderingIntent.AbsoluteColorimetric => RenderingIntent.Absolute,
                                _ => RenderingIntent.Undefined
                            };

                            // Match the existing reference decoder: use the source intent without BPC.
                            // Float import and export retain extended RGB; integer export would clip it.
                            converted.BlackPointCompensation = false;
                            if (!converted.TransformColorSpace(new ImageMagick.ColorProfile(CompactSrgbV4Profile.Profile.ToByteArray())))
                            {
                                throw new InvalidOperationException("The reference CMS could not convert the decoded JPEG XL samples.");
                            }

                            using IUnsafePixelCollection<float> collection = converted.GetPixelsUnsafe();
                            float[] values = collection.GetValues();
                            TensorPrimitives.Multiply(values, 1F / Quantum.Max, values);
                            ReadOnlySpan<float> samples = values;
                            int channels = (int)converted.ChannelCount;
                            bool hasAlpha = converted.HasAlpha;
                            for (int y = 0; y < height; y++)
                            {
                                Span<Vector4> row = colors.Slice(y * width, width);
                                ReadOnlySpan<float> sourceRow = samples.Slice(y * width * channels, width * channels);
                                if (hasAlpha)
                                {
                                    MemoryMarshal.Cast<float, Vector4>(sourceRow).CopyTo(row);
                                }
                                else
                                {
                                    Rgb.ToScaledVector4(MemoryMarshal.Cast<float, Rgb>(sourceRow), row);
                                }

                                if (!opacity.IsEmpty)
                                {
                                    Numerics.WithW(row, opacity.Slice(y * width, width));
                                }
                            }

                            return CreateImage<TPixel>(options.Configuration, colors, width, height);
                        }

                        default:
                            throw new InvalidImageContentException($"libjxl did not complete the reference image: {status}.");
                    }
                }
            }
        }
        finally
        {
            // libjxl borrows the output allocation. Release the decoder before freeing that allocation.
            NativeJxl.JxlDecoderDestroy(decoder);
            NativeMemory.Free(pixels);
            NativeMemory.Free(black);
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
    /// Converts composed native RGB samples through libjxl's CMS while retaining their alpha.
    /// </summary>
    /// <param name="colors">The straight RGBA samples updated in place.</param>
    /// <param name="width">The number of pixels in each row and native CMS buffer.</param>
    /// <param name="sourceProfile">The ICC profile of the native DATA samples.</param>
    /// <param name="targetProfile">The native sRGB ICC profile with the source rendering intent.</param>
    /// <param name="sourceEncoding">The structured encoding of the composed RGB samples.</param>
    /// <param name="intensityTarget">The image's peak intensity in nits, used by native HDR conversion.</param>
    private static void ConvertRgbToSrgb(
        Span<Vector4> colors,
        int width,
        byte[] sourceProfile,
        byte[] targetProfile,
        JxlColorEncoding sourceEncoding,
        float intensityTarget)
    {
        JxlCmsInterface cms = *JxlCms.JxlGetDefaultCms();

        // JxlNet stores the C callbacks as IntPtr. Match cms_interface.h's Cdecl signatures:
        // init creates one transform; buffer callbacks and run use thread zero of that transform.
        delegate* unmanaged[Cdecl]<void*, nuint, nuint, JxlColorProfile*, JxlColorProfile*, float, void*> initialize
            = (delegate* unmanaged[Cdecl]<void*, nuint, nuint, JxlColorProfile*, JxlColorProfile*, float, void*>)cms.init;
        delegate* unmanaged[Cdecl]<void*, nuint, float*> getSourceBuffer = (delegate* unmanaged[Cdecl]<void*, nuint, float*>)cms.get_src_buf;
        delegate* unmanaged[Cdecl]<void*, nuint, float*> getDestinationBuffer = (delegate* unmanaged[Cdecl]<void*, nuint, float*>)cms.get_dst_buf;
        delegate* unmanaged[Cdecl]<void*, nuint, float*, float*, nuint, int> run
            = (delegate* unmanaged[Cdecl]<void*, nuint, float*, float*, nuint, int>)cms.run;
        delegate* unmanaged[Cdecl]<void*, void> destroy = (delegate* unmanaged[Cdecl]<void*, void>)cms.destroy;

        // The CMS can borrow both profiles until destroy completes. Pin them across the whole
        // transform lifetime, including failure cleanup, rather than only during initialization.
        fixed (byte* sourceData = sourceProfile, targetData = targetProfile)
        {
            JxlColorProfile source = new()
            {
                icc = new() { data = sourceData, size = (nuint)sourceProfile.Length },
                color_encoding = sourceEncoding,
                num_channels = 3
            };

            JxlColorProfile target = new()
            {
                icc = new() { data = targetData, size = (nuint)targetProfile.Length },
                num_channels = 3
            };

            NativeJxl.JxlColorEncodingSetToSRGB(&target.color_encoding, 0);
            target.color_encoding.rendering_intent = sourceEncoding.rendering_intent;
            void* transform = initialize(cms.init_data, 1, (nuint)width, &source, &target, intensityTarget);

            if (transform is null)
            {
                throw new InvalidOperationException("libjxl could not initialize the reference color transform.");
            }

            try
            {
                // Native buffers contain r0,g0,b0,r1,g1,b1,... with no alpha or integer scaling.
                // Reuse the CMS-owned row buffers and existing RGB copy APIs. No color arithmetic
                // runs in ImageSharp; its RGBA row retains alpha while native code transforms RGB.
                float* sourceBuffer = getSourceBuffer(transform, 0);
                float* destinationBuffer = getDestinationBuffer(transform, 0);
                Span<Rgb> input = new(sourceBuffer, width);
                Span<Vector3> output = new(destinationBuffer, width);
                for (int offset = 0; offset < colors.Length; offset += width)
                {
                    Span<Vector4> row = colors.Slice(offset, width);
                    Rgb.FromScaledVector4(row, input);

                    if (run(transform, 0, sourceBuffer, destinationBuffer, (nuint)width) == 0)
                    {
                        throw new InvalidOperationException("libjxl could not convert the reference RGB samples.");
                    }

                    Numerics.WithXYZ(output, row);
                }
            }
            finally
            {
                // Only the native CMS owns its transform and row buffers.
                destroy(transform);
            }
        }
    }

    /// <summary>
    /// Copies native straight pixels into an image through the requested pixel format's bulk API.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    /// <param name="configuration">The allocator and pixel-conversion configuration.</param>
    /// <param name="colors">The complete native image in row order. Packing can modify these borrowed samples.</param>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    /// <returns>The owned image. The caller retains ownership of the native samples.</returns>
    private static Image<TPixel> CreateImage<TPixel>(Configuration configuration, Span<Vector4> colors, int width, int height)
        where TPixel : unmanaged, SixLabors.ImageSharp.PixelFormats.IPixel<TPixel>
    {
        Image<TPixel> image = new(configuration, width, height);
        try
        {
            PixelOperations<TPixel> pixelOperations = PixelOperations<TPixel>.Instance;
            for (int y = 0; y < height; y++)
            {
                pixelOperations.FromUnassociatedScaledVector4Destructive(
                    configuration, colors.Slice(y * width, width), image.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y));
            }

            return image;
        }
        catch
        {
            // The image owns allocator storage. A failed conversion must release it before native output is freed.
            image.Dispose();
            throw;
        }
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

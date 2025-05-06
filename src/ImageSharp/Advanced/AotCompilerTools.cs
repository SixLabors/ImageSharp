// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;
using SixLabors.ImageSharp.Formats.Pbm;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Qoi;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.ImageSharp.Processing.Processors.Binarization;
using SixLabors.ImageSharp.Processing.Processors.Convolution;
using SixLabors.ImageSharp.Processing.Processors.Dithering;
using SixLabors.ImageSharp.Processing.Processors.Drawing;
using SixLabors.ImageSharp.Processing.Processors.Effects;
using SixLabors.ImageSharp.Processing.Processors.Filters;
using SixLabors.ImageSharp.Processing.Processors.Normalization;
using SixLabors.ImageSharp.Processing.Processors.Overlays;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace SixLabors.ImageSharp.Advanced;

/// <summary>
/// Unlike traditional Mono/.NET, code on the iPhone is statically compiled ahead of time instead of being
/// compiled on demand by a JIT compiler. This means there are a few limitations with respect to generics,
/// these are caused because not every possible generic instantiation can be determined up front at compile time.
/// The Aot Compiler is designed to overcome the limitations of this compiler.
/// None of the methods in this class should ever be called, the code only has to exist at compile-time to be picked up by the AoT compiler.
/// (Very similar to the LinkerIncludes.cs technique used in Xamarin.Android projects.)
/// </summary>
[ExcludeFromCodeCoverage]
internal static class AotCompilerTools
{
    /// <summary>
    /// This is the method that seeds the AoT compiler.
    /// None of these seed methods needs to actually be called to seed the compiler.
    /// The calls just need to be present when the code is compiled, and each implementation will be built.
    /// </summary>
    /// <remarks>
    /// This method doesn't actually do anything but serves an important purpose...
    /// If you are running ImageSharp on iOS and try to call SaveAsGif, it will throw an exception:
    /// "Attempting to JIT compile method... OctreeFrameQuantizer.ConstructPalette... while running in aot-only mode."
    /// The reason this happens is the SaveAsGif method makes heavy use of generics, which are too confusing for the AoT
    /// compiler used on Xamarin.iOS. It spins up the JIT compiler to try and figure it out, but that is an illegal op on
    /// iOS so it bombs out.
    /// If you are getting the above error, you need to call this method, which will pre-seed the AoT compiler with the
    /// necessary methods to complete the SaveAsGif call. That's it, otherwise you should NEVER need this method!!!
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// This method is used for AOT code generation only. Do not call it at runtime.
    /// </exception>
    [Preserve]
    private static void SeedPixelFormats()
    {
        try
        {
            Unsafe.SizeOf<long>();
            Unsafe.SizeOf<short>();
            Unsafe.SizeOf<float>();
            Unsafe.SizeOf<double>();
            Unsafe.SizeOf<byte>();
            Unsafe.SizeOf<int>();
            Unsafe.SizeOf<bool>();
            Unsafe.SizeOf<Block8x8>();
            Unsafe.SizeOf<Vector4>();

            Seed<A8>();
            Seed<Argb32>();
            Seed<Abgr32>();
            Seed<Bgr24>();
            Seed<Bgr565>();
            Seed<Bgra32>();
            Seed<Bgra4444>();
            Seed<Bgra5551>();
            Seed<Byte4>();
            Seed<L16>();
            Seed<L8>();
            Seed<La16>();
            Seed<La32>();
            Seed<HalfSingle>();
            Seed<HalfVector2>();
            Seed<HalfVector4>();
            Seed<NormalizedByte2>();
            Seed<NormalizedByte4>();
            Seed<NormalizedShort2>();
            Seed<NormalizedShort4>();
            Seed<Rg32>();
            Seed<Rgb24>();
            Seed<Rgb48>();
            Seed<Rgba1010102>();
            Seed<Rgba32>();
            Seed<Rgba64>();
            Seed<RgbaVector>();
            Seed<Short2>();
            Seed<Short4>();
        }
        catch
        {
            // nop
        }

        throw new InvalidOperationException("This method is used for AOT code generation only. Do not call it at runtime.");
    }

    /// <summary>
    /// Seeds the compiler using the given pixel format.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    [Preserve]
    private static void Seed<TPixel>()
         where TPixel : unmanaged, IPixel<TPixel>
    {
        // This is we actually call all the individual methods you need to seed.
        AotCompileImage<TPixel>();
        AotCompileImageProcessingContextFactory<TPixel>();
        AotCompileImageEncoderInternals<TPixel>();
        AotCompileImageDecoderInternals<TPixel>();
        AotCompileImageEncoders<TPixel>();
        AotCompileImageDecoders<TPixel>();
        AotCompileSpectralConverter<TPixel>();
        AotCompileImageProcessors<TPixel>();
        AotCompileGenericImageProcessors<TPixel>();
        AotCompileResamplers<TPixel>();
        AotCompileQuantizers<TPixel>();
        AotCompilePixelSamplingStrategys<TPixel>();
        AotCompilePixelMaps<TPixel>();
        AotCompileDithers<TPixel>();
        AotCompileMemoryManagers<TPixel>();

        _ = Unsafe.SizeOf<TPixel>();

        // TODO: Do the discovery work to figure out what works and what doesn't.
    }

    /// <summary>
    /// This method pre-seeds the <see cref="Image{TPixel}"/> for a given pixel format in the AoT compiler.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    [Preserve]
    private static unsafe void AotCompileImage<TPixel>()
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Image<TPixel> img = default;
        img.CloneAs<A8>(default);
        img.CloneAs<Argb32>(default);
        img.CloneAs<Abgr32>(default);
        img.CloneAs<Bgr24>(default);
        img.CloneAs<Bgr565>(default);
        img.CloneAs<Bgra32>(default);
        img.CloneAs<Bgra4444>(default);
        img.CloneAs<Bgra5551>(default);
        img.CloneAs<Byte4>(default);
        img.CloneAs<L16>(default);
        img.CloneAs<L8>(default);
        img.CloneAs<La16>(default);
        img.CloneAs<La32>(default);
        img.CloneAs<HalfSingle>(default);
        img.CloneAs<HalfVector2>(default);
        img.CloneAs<HalfVector4>(default);
        img.CloneAs<NormalizedByte2>(default);
        img.CloneAs<NormalizedByte4>(default);
        img.CloneAs<NormalizedShort2>(default);
        img.CloneAs<NormalizedShort4>(default);
        img.CloneAs<Rg32>(default);
        img.CloneAs<Rgb24>(default);
        img.CloneAs<Rgb48>(default);
        img.CloneAs<Rgba1010102>(default);
        img.CloneAs<Rgba32>(default);
        img.CloneAs<Rgba64>(default);
        img.CloneAs<RgbaVector>(default);
        img.CloneAs<Short2>(default);
        img.CloneAs<Short4>(default);

        ImageFrame.LoadPixelData(default, default(ReadOnlySpan<TPixel>), default, default);
        ImageFrame.LoadPixelData<TPixel>(default, default(ReadOnlySpan<byte>), default, default);
    }

    /// <summary>
    /// This method pre-seeds the all <see cref="IImageProcessingContextFactory"/> in the AoT compiler.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    [Preserve]
    private static void AotCompileImageProcessingContextFactory<TPixel>()
        where TPixel : unmanaged, IPixel<TPixel>
            => default(DefaultImageOperationsProviderFactory).CreateImageProcessingContext<TPixel>(default, default, default);

    /// <summary>
    /// This method pre-seeds the all core encoders in the AoT compiler.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    [Preserve]
    private static void AotCompileImageEncoderInternals<TPixel>()
        where TPixel : unmanaged, IPixel<TPixel>
    {
        default(BmpEncoderCore).Encode<TPixel>(default, default, default);
        default(GifEncoderCore).Encode<TPixel>(default, default, default);
        default(JpegEncoderCore).Encode<TPixel>(default, default, default);
        default(PbmEncoderCore).Encode<TPixel>(default, default, default);
        default(PngEncoderCore).Encode<TPixel>(default, default, default);
        default(QoiEncoderCore).Encode<TPixel>(default, default, default);
        default(TgaEncoderCore).Encode<TPixel>(default, default, default);
        default(TiffEncoderCore).Encode<TPixel>(default, default, default);
        default(WebpEncoderCore).Encode<TPixel>(default, default, default);
    }

    /// <summary>
    /// This method pre-seeds the all <see cref="ImageDecoderCore"/> in the AoT compiler.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    [Preserve]
    private static void AotCompileImageDecoderInternals<TPixel>()
        where TPixel : unmanaged, IPixel<TPixel>
    {
        default(BmpDecoderCore).Decode<TPixel>(default, default, default);
        default(GifDecoderCore).Decode<TPixel>(default, default, default);
        default(JpegDecoderCore).Decode<TPixel>(default, default, default);
        default(PbmDecoderCore).Decode<TPixel>(default, default, default);
        default(PngDecoderCore).Decode<TPixel>(default, default, default);
        default(QoiDecoderCore).Decode<TPixel>(default, default, default);
        default(TgaDecoderCore).Decode<TPixel>(default, default, default);
        default(TiffDecoderCore).Decode<TPixel>(default, default, default);
        default(WebpDecoderCore).Decode<TPixel>(default, default, default);
    }

    /// <summary>
    /// This method pre-seeds the all <see cref="IImageEncoder"/> in the AoT compiler.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    [Preserve]
    private static void AotCompileImageEncoders<TPixel>()
       where TPixel : unmanaged, IPixel<TPixel>
    {
        AotCompileImageEncoder<TPixel, WebpEncoder>();
        AotCompileImageEncoder<TPixel, BmpEncoder>();
        AotCompileImageEncoder<TPixel, GifEncoder>();
        AotCompileImageEncoder<TPixel, JpegEncoder>();
        AotCompileImageEncoder<TPixel, PbmEncoder>();
        AotCompileImageEncoder<TPixel, PngEncoder>();
        AotCompileImageEncoder<TPixel, TgaEncoder>();
        AotCompileImageEncoder<TPixel, TiffEncoder>();
    }

    /// <summary>
    /// This method pre-seeds the all <see cref="IImageDecoder"/> in the AoT compiler.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    [Preserve]
    private static void AotCompileImageDecoders<TPixel>()
        where TPixel : unmanaged, IPixel<TPixel>
    {
        AotCompileImageDecoder<TPixel, WebpDecoder>();
        AotCompileImageDecoder<TPixel, BmpDecoder>();
        AotCompileImageDecoder<TPixel, GifDecoder>();
        AotCompileImageDecoder<TPixel, JpegDecoder>();
        AotCompileImageDecoder<TPixel, PbmDecoder>();
        AotCompileImageDecoder<TPixel, PngDecoder>();
        AotCompileImageDecoder<TPixel, TgaDecoder>();
        AotCompileImageDecoder<TPixel, TiffDecoder>();
    }

    [Preserve]
    private static void AotCompileSpectralConverter<TPixel>()
        where TPixel : unmanaged, IPixel<TPixel>
    {
        default(SpectralConverter<TPixel>).GetPixelBuffer(default);
        default(GrayJpegSpectralConverter<TPixel>).GetPixelBuffer(default);
        default(RgbJpegSpectralConverter<TPixel>).GetPixelBuffer(default);
        default(TiffJpegSpectralConverter<TPixel>).GetPixelBuffer(default);
        default(TiffOldJpegSpectralConverter<TPixel>).GetPixelBuffer(default);
    }

    /// <summary>
    /// This method pre-seeds the <see cref="IImageEncoder"/> in the AoT compiler.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <typeparam name="TEncoder">The encoder.</typeparam>
    [Preserve]
    private static void AotCompileImageEncoder<TPixel, TEncoder>()
        where TPixel : unmanaged, IPixel<TPixel>
        where TEncoder : class, IImageEncoder
    {
        default(TEncoder).Encode<TPixel>(default, default);
        default(TEncoder).EncodeAsync<TPixel>(default, default, default);
    }

    /// <summary>
    /// This method pre-seeds the <see cref="IImageDecoder"/> in the AoT compiler.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <typeparam name="TDecoder">The decoder.</typeparam>
    [Preserve]
    private static void AotCompileImageDecoder<TPixel, TDecoder>()
       where TPixel : unmanaged, IPixel<TPixel>
       where TDecoder : class, IImageDecoder
        => default(TDecoder).Decode<TPixel>(default, default);

    /// <summary>
    /// This method pre-seeds the all <see cref="IImageProcessor" /> in the AoT compiler.
    /// </summary>
    /// <remarks>
    /// There is no structure that implements ISwizzler.
    /// </remarks>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    [Preserve]
    private static void AotCompileImageProcessors<TPixel>()
        where TPixel : unmanaged, IPixel<TPixel>
    {
        AotCompileImageProcessor<TPixel, CloningImageProcessor>();
        AotCompileImageProcessor<TPixel, CropProcessor>();
        AotCompileImageProcessor<TPixel, AffineTransformProcessor>();
        AotCompileImageProcessor<TPixel, ProjectiveTransformProcessor>();
        AotCompileImageProcessor<TPixel, RotateProcessor>();
        AotCompileImageProcessor<TPixel, SkewProcessor>();
        AotCompileImageProcessor<TPixel, ResizeProcessor>();
        AotCompileImageProcessor<TPixel, EntropyCropProcessor>();
        AotCompileImageProcessor<TPixel, AutoOrientProcessor>();
        AotCompileImageProcessor<TPixel, FlipProcessor>();
        AotCompileImageProcessor<TPixel, QuantizeProcessor>();
        AotCompileImageProcessor<TPixel, BackgroundColorProcessor>();
        AotCompileImageProcessor<TPixel, GlowProcessor>();
        AotCompileImageProcessor<TPixel, VignetteProcessor>();
        AotCompileImageProcessor<TPixel, AdaptiveHistogramEqualizationProcessor>();
        AotCompileImageProcessor<TPixel, AdaptiveHistogramEqualizationSlidingWindowProcessor>();
        AotCompileImageProcessor<TPixel, GlobalHistogramEqualizationProcessor>();
        AotCompileImageProcessor<TPixel, AchromatomalyProcessor>();
        AotCompileImageProcessor<TPixel, AchromatopsiaProcessor>();
        AotCompileImageProcessor<TPixel, BlackWhiteProcessor>();
        AotCompileImageProcessor<TPixel, BrightnessProcessor>();
        AotCompileImageProcessor<TPixel, ContrastProcessor>();
        AotCompileImageProcessor<TPixel, DeuteranomalyProcessor>();
        AotCompileImageProcessor<TPixel, DeuteranopiaProcessor>();
        AotCompileImageProcessor<TPixel, FilterProcessor>();
        AotCompileImageProcessor<TPixel, GrayscaleBt601Processor>();
        AotCompileImageProcessor<TPixel, GrayscaleBt709Processor>();
        AotCompileImageProcessor<TPixel, HueProcessor>();
        AotCompileImageProcessor<TPixel, InvertProcessor>();
        AotCompileImageProcessor<TPixel, KodachromeProcessor>();
        AotCompileImageProcessor<TPixel, LightnessProcessor>();
        AotCompileImageProcessor<TPixel, LomographProcessor>();
        AotCompileImageProcessor<TPixel, OpacityProcessor>();
        AotCompileImageProcessor<TPixel, PolaroidProcessor>();
        AotCompileImageProcessor<TPixel, ProtanomalyProcessor>();
        AotCompileImageProcessor<TPixel, ProtanopiaProcessor>();
        AotCompileImageProcessor<TPixel, SaturateProcessor>();
        AotCompileImageProcessor<TPixel, SepiaProcessor>();
        AotCompileImageProcessor<TPixel, TritanomalyProcessor>();
        AotCompileImageProcessor<TPixel, TritanopiaProcessor>();
        AotCompileImageProcessor<TPixel, OilPaintingProcessor>();
        AotCompileImageProcessor<TPixel, PixelateProcessor>();
        AotCompileImageProcessor<TPixel, PixelRowDelegateProcessor>();
        AotCompileImageProcessor<TPixel, PositionAwarePixelRowDelegateProcessor>();
        AotCompileImageProcessor<TPixel, DrawImageProcessor>();
        AotCompileImageProcessor<TPixel, PaletteDitherProcessor>();
        AotCompileImageProcessor<TPixel, BokehBlurProcessor>();
        AotCompileImageProcessor<TPixel, BoxBlurProcessor>();
        AotCompileImageProcessor<TPixel, EdgeDetector2DProcessor>();
        AotCompileImageProcessor<TPixel, EdgeDetectorCompassProcessor>();
        AotCompileImageProcessor<TPixel, EdgeDetectorProcessor>();
        AotCompileImageProcessor<TPixel, GaussianBlurProcessor>();
        AotCompileImageProcessor<TPixel, GaussianSharpenProcessor>();
        AotCompileImageProcessor<TPixel, AdaptiveThresholdProcessor>();
        AotCompileImageProcessor<TPixel, BinaryThresholdProcessor>();

        AotCompilerCloningImageProcessor<TPixel, CloningImageProcessor>();
        AotCompilerCloningImageProcessor<TPixel, CropProcessor>();
        AotCompilerCloningImageProcessor<TPixel, AffineTransformProcessor>();
        AotCompilerCloningImageProcessor<TPixel, ProjectiveTransformProcessor>();
        AotCompilerCloningImageProcessor<TPixel, RotateProcessor>();
        AotCompilerCloningImageProcessor<TPixel, SkewProcessor>();
        AotCompilerCloningImageProcessor<TPixel, ResizeProcessor>();
    }

    /// <summary>
    /// This method pre-seeds the <see cref="IImageProcessor" /> in the AoT compiler.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <typeparam name="TProc">The processor type</typeparam>
    [Preserve]
    private static void AotCompileImageProcessor<TPixel, TProc>()
        where TPixel : unmanaged, IPixel<TPixel>
        where TProc : class, IImageProcessor
            => default(TProc).CreatePixelSpecificProcessor<TPixel>(default, default, default);

    /// <summary>
    /// This method pre-seeds the <see cref="ICloningImageProcessor" /> in the AoT compiler.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <typeparam name="TProc">The processor type</typeparam>
    [Preserve]
    private static void AotCompilerCloningImageProcessor<TPixel, TProc>()
        where TPixel : unmanaged, IPixel<TPixel>
        where TProc : class, ICloningImageProcessor
            => default(TProc).CreatePixelSpecificCloningProcessor<TPixel>(default, default, default);

    /// <summary>
    /// This method pre-seeds the all <see cref="IImageProcessor"/> in the AoT compiler.
    /// </summary>
    /// <remarks>
    /// There is no structure that implements ISwizzler.
    /// </remarks>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    [Preserve]
    private static void AotCompileGenericImageProcessors<TPixel>()
        where TPixel : unmanaged, IPixel<TPixel>
    {
        AotCompileGenericCloningImageProcessor<TPixel, CropProcessor<TPixel>>();
        AotCompileGenericCloningImageProcessor<TPixel, AffineTransformProcessor<TPixel>>();
        AotCompileGenericCloningImageProcessor<TPixel, ProjectiveTransformProcessor<TPixel>>();
        AotCompileGenericCloningImageProcessor<TPixel, ResizeProcessor<TPixel>>();
        AotCompileGenericCloningImageProcessor<TPixel, RotateProcessor<TPixel>>();
    }

    /// <summary>
    /// This method pre-seeds the <see cref="ICloningImageProcessor{TPixel}" /> in the AoT compiler.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <typeparam name="TProc">The processor type</typeparam>
    [Preserve]
    private static void AotCompileGenericCloningImageProcessor<TPixel, TProc>()
        where TPixel : unmanaged, IPixel<TPixel>
        where TProc : class, ICloningImageProcessor<TPixel>
            => default(TProc).CloneAndExecute();

    /// <summary>
    /// This method pre-seeds the all<see cref="IResampler" /> in the AoT compiler.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    [Preserve]
    private static void AotCompileResamplers<TPixel>()
              where TPixel : unmanaged, IPixel<TPixel>
    {
        AotCompileResampler<TPixel, BicubicResampler>();
        AotCompileResampler<TPixel, BoxResampler>();
        AotCompileResampler<TPixel, CubicResampler>();
        AotCompileResampler<TPixel, LanczosResampler>();
        AotCompileResampler<TPixel, NearestNeighborResampler>();
        AotCompileResampler<TPixel, TriangleResampler>();
        AotCompileResampler<TPixel, WelchResampler>();
    }

    /// <summary>
    /// This method pre-seeds the <see cref="IResampler" /> in the AoT compiler.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <typeparam name="TResampler">The processor type</typeparam>
    [Preserve]
    private static void AotCompileResampler<TPixel, TResampler>()
            where TPixel : unmanaged, IPixel<TPixel>
            where TResampler : struct, IResampler
    {
        default(TResampler).ApplyTransform<TPixel>(default);

        default(AffineTransformProcessor<TPixel>).ApplyTransform<TResampler>(default);
        default(ProjectiveTransformProcessor<TPixel>).ApplyTransform<TResampler>(default);
        default(ResizeProcessor<TPixel>).ApplyTransform<TResampler>(default);
        default(RotateProcessor<TPixel>).ApplyTransform<TResampler>(default);
    }

    /// <summary>
    /// This method pre-seeds the all <see cref="IQuantizer" /> in the AoT compiler.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    [Preserve]
    private static void AotCompileQuantizers<TPixel>()
        where TPixel : unmanaged, IPixel<TPixel>
    {
        AotCompileQuantizer<TPixel, OctreeQuantizer>();
        AotCompileQuantizer<TPixel, PaletteQuantizer>();
        AotCompileQuantizer<TPixel, WebSafePaletteQuantizer>();
        AotCompileQuantizer<TPixel, WernerPaletteQuantizer>();
        AotCompileQuantizer<TPixel, WuQuantizer>();
    }

    /// <summary>
    /// This method pre-seeds the <see cref="IQuantizer" /> in the AoT compiler.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <typeparam name="TQuantizer">The quantizer type</typeparam>
    [Preserve]
    private static void AotCompileQuantizer<TPixel, TQuantizer>()
        where TPixel : unmanaged, IPixel<TPixel>

        where TQuantizer : class, IQuantizer
    {
        default(TQuantizer).CreatePixelSpecificQuantizer<TPixel>(default);
        default(TQuantizer).CreatePixelSpecificQuantizer<TPixel>(default, default);
    }

    /// <summary>
    /// This method pre-seeds the <see cref="IPixelSamplingStrategy" /> in the AoT compiler.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    [Preserve]
    private static void AotCompilePixelSamplingStrategys<TPixel>()
        where TPixel : unmanaged, IPixel<TPixel>
    {
        default(DefaultPixelSamplingStrategy).EnumeratePixelRegions(default(Image<TPixel>));
        default(DefaultPixelSamplingStrategy).EnumeratePixelRegions(default(ImageFrame<TPixel>));
        default(ExtensivePixelSamplingStrategy).EnumeratePixelRegions(default(Image<TPixel>));
        default(ExtensivePixelSamplingStrategy).EnumeratePixelRegions(default(ImageFrame<TPixel>));
    }

    /// <summary>
    /// This method pre-seeds the all <see cref="IColorIndexCache{T}" /> in the AoT compiler.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    [Preserve]
    private static void AotCompilePixelMaps<TPixel>()
        where TPixel : unmanaged, IPixel<TPixel>
    {
        default(EuclideanPixelMap<TPixel, HybridCache>).GetClosestColor(default, out _);
        default(EuclideanPixelMap<TPixel, AccurateCache>).GetClosestColor(default, out _);
        default(EuclideanPixelMap<TPixel, CoarseCache>).GetClosestColor(default, out _);
        default(EuclideanPixelMap<TPixel, NullCache>).GetClosestColor(default, out _);
    }

    /// <summary>
    /// This method pre-seeds the all <see cref="IDither" /> in the AoT compiler.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    [Preserve]
    private static void AotCompileDithers<TPixel>()
       where TPixel : unmanaged, IPixel<TPixel>
    {
        AotCompileDither<TPixel, ErrorDither>();
        AotCompileDither<TPixel, OrderedDither>();
    }

    /// <summary>
    /// This method pre-seeds the <see cref="IDither" /> in the AoT compiler.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <typeparam name="TDither">The dither.</typeparam>
    [Preserve]
    private static void AotCompileDither<TPixel, TDither>()
        where TPixel : unmanaged, IPixel<TPixel>
        where TDither : struct, IDither
    {
        OctreeQuantizer<TPixel> octree = default;
        default(TDither).ApplyQuantizationDither<OctreeQuantizer<TPixel>, TPixel>(ref octree, default, default, default);

        PaletteQuantizer<TPixel> palette = default;
        default(TDither).ApplyQuantizationDither<PaletteQuantizer<TPixel>, TPixel>(ref palette, default, default, default);

        WuQuantizer<TPixel> wu = default;
        default(TDither).ApplyQuantizationDither<WuQuantizer<TPixel>, TPixel>(ref wu, default, default, default);
        default(TDither).ApplyPaletteDither<PaletteDitherProcessor<TPixel>.DitherProcessor, TPixel>(default, default, default);
    }

    /// <summary>
    /// This method pre-seeds the all <see cref="MemoryAllocator" /> in the AoT compiler.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    [Preserve]
    private static void AotCompileMemoryManagers<TPixel>()
        where TPixel : unmanaged, IPixel<TPixel>
    {
        AotCompileMemoryManager<TPixel, UniformUnmanagedMemoryPoolMemoryAllocator>();
        AotCompileMemoryManager<TPixel, SimpleGcMemoryAllocator>();
    }

    /// <summary>
    /// This method pre-seeds the <see cref="MemoryAllocator" /> in the AoT compiler.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <typeparam name="TBuffer">The buffer.</typeparam>
    [Preserve]
    private static void AotCompileMemoryManager<TPixel, TBuffer>()
        where TPixel : unmanaged, IPixel<TPixel>
        where TBuffer : MemoryAllocator
    {
        default(TBuffer).Allocate<long>(default, default);
        default(TBuffer).Allocate<short>(default, default);
        default(TBuffer).Allocate<float>(default, default);
        default(TBuffer).Allocate<double>(default, default);
        default(TBuffer).Allocate<byte>(default, default);
        default(TBuffer).Allocate<int>(default, default);
        default(TBuffer).Allocate<bool>(default, default);
        default(TBuffer).Allocate<decimal>(default, default);
        default(TBuffer).Allocate<Block8x8>(default, default);
        default(TBuffer).Allocate<Vector4>(default, default);
        default(TBuffer).Allocate<TPixel>(default, default);
    }
}

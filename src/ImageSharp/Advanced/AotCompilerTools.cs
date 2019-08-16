// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Dithering;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace SixLabors.ImageSharp.Advanced
{
    /// <summary>
    /// Unlike traditional Mono/.NET, code on the iPhone is statically compiled ahead of time instead of being
    /// compiled on demand by a JIT compiler. This means there are a few limitations with respect to generics,
    /// these are caused because not every possible generic instantiation can be determined up front at compile time.
    /// The Aot Compiler is designed to overcome the limitations of this compiler.
    /// None of the methods in this class should ever be called, the code only has to exist at compile-time to be picked up by the AoT compiler.
    /// (Very similar to the LinkerIncludes.cs technique used in Xamarin.Android projects.)
    /// </summary>
    internal static class AotCompilerTools
    {
        static AotCompilerTools()
        {
            System.Runtime.CompilerServices.Unsafe.SizeOf<long>();
            System.Runtime.CompilerServices.Unsafe.SizeOf<short>();
            System.Runtime.CompilerServices.Unsafe.SizeOf<float>();
            System.Runtime.CompilerServices.Unsafe.SizeOf<double>();
            System.Runtime.CompilerServices.Unsafe.SizeOf<byte>();
            System.Runtime.CompilerServices.Unsafe.SizeOf<int>();
            System.Runtime.CompilerServices.Unsafe.SizeOf<Block8x8>();
            System.Runtime.CompilerServices.Unsafe.SizeOf<Vector4>();
        }

        /// <summary>
        /// This is the method that seeds the AoT compiler.
        /// None of these seed methods needs to actually be called to seed the compiler.
        /// The calls just need to be present when the code is compiled, and each implementation will be built.
        /// </summary>
        private static void SeedEverything()
        {
            Seed<Alpha8>();
            Seed<Argb32>();
            Seed<Bgr24>();
            Seed<Bgr565>();
            Seed<Bgra32>();
            Seed<Bgra4444>();
            Seed<Bgra5551>();
            Seed<Byte4>();
            Seed<Gray16>();
            Seed<Gray8>();
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

        /// <summary>
        /// Seeds the compiler using the given pixel format.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        private static void Seed<TPixel>()
             where TPixel : struct, IPixel<TPixel>
        {
            // This is we actually call all the individual methods you need to seed.
            AotCompileOctreeQuantizer<TPixel>();
            AotCompileWuQuantizer<TPixel>();
            AotCompileDithering<TPixel>();
            AotCompilePixelOperations<TPixel>();
            AotCompileResizeOperations<TPixel>();

            System.Runtime.CompilerServices.Unsafe.SizeOf<TPixel>();

            AotCodec<TPixel>(new Formats.Png.PngDecoder(), new Formats.Png.PngEncoder());
            AotCodec<TPixel>(new Formats.Bmp.BmpDecoder(), new Formats.Bmp.BmpEncoder());
            AotCodec<TPixel>(new Formats.Gif.GifDecoder(), new Formats.Gif.GifEncoder());
            AotCodec<TPixel>(new Formats.Jpeg.JpegDecoder(), new Formats.Jpeg.JpegEncoder());

            // TODO: Do the discovery work to figure out what works and what doesn't.
        }

        /// <summary>
        /// This method doesn't actually do anything but serves an important purpose...
        /// If you are running ImageSharp on iOS and try to call SaveAsGif, it will throw an exception:
        /// "Attempting to JIT compile method... OctreeFrameQuantizer.ConstructPalette... while running in aot-only mode."
        /// The reason this happens is the SaveAsGif method makes heavy use of generics, which are too confusing for the AoT
        /// compiler used on Xamarin.iOS. It spins up the JIT compiler to try and figure it out, but that is an illegal op on
        /// iOS so it bombs out.
        /// If you are getting the above error, you need to call this method, which will pre-seed the AoT compiler with the
        /// necessary methods to complete the SaveAsGif call. That's it, otherwise you should NEVER need this method!!!
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        private static void AotCompileOctreeQuantizer<TPixel>()
            where TPixel : struct, IPixel<TPixel>
        {
            var test = new OctreeFrameQuantizer<TPixel>(new OctreeQuantizer(false));
            test.AotGetPalette();
        }

        /// <summary>
        /// This method pre-seeds the WuQuantizer in the AoT compiler for iOS.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        private static void AotCompileWuQuantizer<TPixel>()
            where TPixel : struct, IPixel<TPixel>
        {
            var test = new WuFrameQuantizer<TPixel>(Configuration.Default.MemoryAllocator, new WuQuantizer(false));
            test.QuantizeFrame(new ImageFrame<TPixel>(Configuration.Default, 1, 1));
            test.AotGetPalette();
        }

        /// <summary>
        /// This method pre-seeds the default dithering engine (FloydSteinbergDiffuser) in the AoT compiler for iOS.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        private static void AotCompileDithering<TPixel>()
            where TPixel : struct, IPixel<TPixel>
        {
            var test = new FloydSteinbergDiffuser();
            TPixel pixel = default;
            test.Dither(new ImageFrame<TPixel>(Configuration.Default, 1, 1), pixel, pixel, 0, 0, 0, 0, 0, 0);
        }

        /// <summary>
        /// This method pre-seeds the decoder and encoder for a given pixel format in the AoT compiler for iOS.
        /// </summary>
        /// <param name="decoder">The image decoder to seed.</param>
        /// <param name="encoder">The image encoder to seed.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        private static void AotCodec<TPixel>(IImageDecoder decoder, IImageEncoder encoder)
            where TPixel : struct, IPixel<TPixel>
        {
            try
            {
                decoder.Decode<TPixel>(Configuration.Default, null);
            }
            catch
            {
            }

            try
            {
                encoder.Encode<TPixel>(null, null);
            }
            catch
            {
            }
        }

        /// <summary>
        /// This method pre-seeds the PixelOperations engine for the AoT compiler on iOS.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        private static void AotCompilePixelOperations<TPixel>()
            where TPixel : struct, IPixel<TPixel>
        {
            var pixelOp = new PixelOperations<TPixel>();
            pixelOp.GetPixelBlender(PixelColorBlendingMode.Normal, PixelAlphaCompositionMode.Clear);
        }

        /// <summary>
        /// This method pre-seeds the ResizeProcessor for the AoT compiler on iOS.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        private static void AotCompileResizeOperations<TPixel>()
            where TPixel : struct, IPixel<TPixel>
        {
            var resizeProcessor = new ResizeProcessor(new ResizeOptions(), default);
            var genericResizeProcessor = new ResizeProcessor<TPixel>((ResizeProcessor)resizeProcessor.CreatePixelSpecificProcessor<TPixel>());
            genericResizeProcessor.AotCreateDestination(new Image<TPixel>(0, 0), default);
        }
    }
}

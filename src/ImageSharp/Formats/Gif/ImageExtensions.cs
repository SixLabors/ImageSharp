// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Extension methods for the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Saves the image to the given stream in the gif format.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        public static void SaveAsGif<TPixel>(this Image<TPixel> source, Stream stream)
            where TPixel : struct, IPixel<TPixel>
             => source.SaveAsGif(stream, null);

        /// <summary>
        /// Saves the image to the given stream in the gif format.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="encoder">The options for the encoder.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        public static void SaveAsGif<TPixel>(this Image<TPixel> source, Stream stream, GifEncoder encoder)
            where TPixel : struct, IPixel<TPixel>
            => source.Save(stream, encoder ?? source.GetConfiguration().ImageFormatsManager.FindEncoder(GifFormat.Instance));

        /// <summary>
        /// This method doesn't actually do anything but serves an important purpose...
        /// If you are running ImageSharp on iOS and try to call SaveAsGif, it will throw an excepion:
        /// "Attempting to JIT compile method... OctreeFrameQuantizer.ConstructPalette... while running in aot-only mode."
        /// The reason this happens is the SaveAsGif method makes haevy use of generics, which are too confusing for the AoT
        /// compiler used on Xamarin.iOS. It spins up the JIT compiler to try and figure it out, but that is an illegal op on 
        /// iOS so it bombs out. 
        /// If you are getting the above error, you need to call this method, which will pre-seed the AoT compiler with the 
        /// necessary methods to complete the SaveAsGif call. That's it, otherwise you should NEVER need this method!!!
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        public static void AotCompileOctreeQuantizer<TPixel>()
            where TPixel : struct, IPixel<TPixel>
        {
            var test = new OctreeFrameQuantizer<TPixel>(new OctreeQuantizer(false));
            test.AotGetPalette();
        }
    }
}

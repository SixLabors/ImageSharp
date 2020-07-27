// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Bmp;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Extension methods for the <see cref="Image"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Saves the image to the given stream with the bmp format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="path">The file path to save the image to.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the path is null.</exception>
        public static void SaveAsBmp(this Image source, string path) => SaveAsBmp(source, path, null);

        /// <summary>
        /// Saves the image to the given stream with the bmp format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="path">The file path to save the image to.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the path is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task SaveAsBmpAsync(this Image source, string path) => SaveAsBmpAsync(source, path, null);

        /// <summary>
        /// Saves the image to the given stream with the bmp format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="path">The file path to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the path is null.</exception>
        public static void SaveAsBmp(this Image source, string path, BmpEncoder encoder) =>
            source.Save(
                path,
                encoder ?? source.GetConfiguration().ImageFormatsManager.FindEncoder(BmpFormat.Instance));

        /// <summary>
        /// Saves the image to the given stream with the bmp format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="path">The file path to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the path is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task SaveAsBmpAsync(this Image source, string path, BmpEncoder encoder) =>
            source.SaveAsync(
                path,
                encoder ?? source.GetConfiguration().ImageFormatsManager.FindEncoder(BmpFormat.Instance));

        /// <summary>
        /// Saves the image to the given stream with the bmp format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        public static void SaveAsBmp(this Image source, Stream stream) => SaveAsBmp(source, stream, null);

        /// <summary>
        /// Saves the image to the given stream with the bmp format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task SaveAsBmpAsync(this Image source, Stream stream) => SaveAsBmpAsync(source, stream, null);

        /// <summary>
        /// Saves the image to the given stream with the bmp format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        public static void SaveAsBmp(this Image source, Stream stream, BmpEncoder encoder) =>
            source.Save(
                stream,
                encoder ?? source.GetConfiguration().ImageFormatsManager.FindEncoder(BmpFormat.Instance));

        /// <summary>
        /// Saves the image to the given stream with the bmp format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task SaveAsBmpAsync(this Image source, Stream stream, BmpEncoder encoder) =>
            source.SaveAsync(
                stream,
                encoder ?? source.GetConfiguration().ImageFormatsManager.FindEncoder(BmpFormat.Instance));
    }
}

// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Png;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Extension methods for the <see cref="Image"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Saves the image to the given stream with the png format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="path">The file path to save the image to.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the path is null.</exception>
        public static void SaveAsPng(this Image source, string path) => SaveAsPng(source, path, null);

        /// <summary>
        /// Saves the image to the given stream with the png format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="path">The file path to save the image to.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the path is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task SaveAsPngAsync(this Image source, string path) => SaveAsPngAsync(source, path, null);

        /// <summary>
        /// Saves the image to the given stream with the png format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="path">The file path to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the path is null.</exception>
        public static void SaveAsPng(this Image source, string path, PngEncoder encoder) =>
            source.Save(
                path,
                encoder ?? source.GetConfiguration().ImageFormatsManager.FindEncoder(PngFormat.Instance));

        /// <summary>
        /// Saves the image to the given stream with the png format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="path">The file path to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the path is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task SaveAsPngAsync(this Image source, string path, PngEncoder encoder) =>
            source.SaveAsync(
                path,
                encoder ?? source.GetConfiguration().ImageFormatsManager.FindEncoder(PngFormat.Instance));

        /// <summary>
        /// Saves the image to the given stream with the png format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        public static void SaveAsPng(this Image source, Stream stream) => SaveAsPng(source, stream, null);

        /// <summary>
        /// Saves the image to the given stream with the png format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task SaveAsPngAsync(this Image source, Stream stream) => SaveAsPngAsync(source, stream, null);

        /// <summary>
        /// Saves the image to the given stream with the png format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        public static void SaveAsPng(this Image source, Stream stream, PngEncoder encoder) =>
            source.Save(
                stream,
                encoder ?? source.GetConfiguration().ImageFormatsManager.FindEncoder(PngFormat.Instance));

        /// <summary>
        /// Saves the image to the given stream with the png format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task SaveAsPngAsync(this Image source, Stream stream, PngEncoder encoder) =>
            source.SaveAsync(
                stream,
                encoder ?? source.GetConfiguration().ImageFormatsManager.FindEncoder(PngFormat.Instance));
    }
}

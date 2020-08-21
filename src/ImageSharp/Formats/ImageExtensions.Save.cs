﻿// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

// <auto-generated />
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Advanced;

using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tga;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Extension methods for the <see cref="Image"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Saves the image to the given stream with the Bmp format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="path">The file path to save the image to.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the path is null.</exception>
        public static void SaveAsBmp(this Image source, string path) => SaveAsBmp(source, path, null);

        /// <summary>
        /// Saves the image to the given stream with the Bmp format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="path">The file path to save the image to.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the path is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task SaveAsBmpAsync(this Image source, string path) => SaveAsBmpAsync(source, path, null);

        /// <summary>
        /// Saves the image to the given stream with the Bmp format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="path">The file path to save the image to.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the path is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task SaveAsBmpAsync(this Image source, string path, CancellationToken cancellationToken)
            => SaveAsBmpAsync(source, path, null, cancellationToken);

        /// <summary>
        /// Saves the image to the given stream with the Bmp format.
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
        /// Saves the image to the given stream with the Bmp format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="path">The file path to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the path is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task SaveAsBmpAsync(this Image source, string path, BmpEncoder encoder, CancellationToken cancellationToken = default) =>
            source.SaveAsync(
                path,
                encoder ?? source.GetConfiguration().ImageFormatsManager.FindEncoder(BmpFormat.Instance),
                cancellationToken);

        /// <summary>
        /// Saves the image to the given stream with the Bmp format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        public static void SaveAsBmp(this Image source, Stream stream)
            => SaveAsBmp(source, stream, null);

        /// <summary>
        /// Saves the image to the given stream with the Bmp format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task SaveAsBmpAsync(this Image source, Stream stream, CancellationToken cancellationToken = default)
            => SaveAsBmpAsync(source, stream, null, cancellationToken);

        /// <summary>
        /// Saves the image to the given stream with the Bmp format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static void SaveAsBmp(this Image source, Stream stream, BmpEncoder encoder)
            => source.Save(
                stream,
                encoder ?? source.GetConfiguration().ImageFormatsManager.FindEncoder(BmpFormat.Instance));

        /// <summary>
        /// Saves the image to the given stream with the Bmp format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task SaveAsBmpAsync(this Image source, Stream stream, BmpEncoder encoder, CancellationToken cancellationToken = default) =>
            source.SaveAsync(
                stream,
                encoder ?? source.GetConfiguration().ImageFormatsManager.FindEncoder(BmpFormat.Instance),
                cancellationToken);

        /// <summary>
        /// Saves the image to the given stream with the Gif format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="path">The file path to save the image to.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the path is null.</exception>
        public static void SaveAsGif(this Image source, string path) => SaveAsGif(source, path, null);

        /// <summary>
        /// Saves the image to the given stream with the Gif format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="path">The file path to save the image to.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the path is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task SaveAsGifAsync(this Image source, string path) => SaveAsGifAsync(source, path, null);

        /// <summary>
        /// Saves the image to the given stream with the Gif format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="path">The file path to save the image to.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the path is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task SaveAsGifAsync(this Image source, string path, CancellationToken cancellationToken)
            => SaveAsGifAsync(source, path, null, cancellationToken);

        /// <summary>
        /// Saves the image to the given stream with the Gif format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="path">The file path to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the path is null.</exception>
        public static void SaveAsGif(this Image source, string path, GifEncoder encoder) =>
            source.Save(
                path,
                encoder ?? source.GetConfiguration().ImageFormatsManager.FindEncoder(GifFormat.Instance));

        /// <summary>
        /// Saves the image to the given stream with the Gif format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="path">The file path to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the path is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task SaveAsGifAsync(this Image source, string path, GifEncoder encoder, CancellationToken cancellationToken = default) =>
            source.SaveAsync(
                path,
                encoder ?? source.GetConfiguration().ImageFormatsManager.FindEncoder(GifFormat.Instance),
                cancellationToken);

        /// <summary>
        /// Saves the image to the given stream with the Gif format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        public static void SaveAsGif(this Image source, Stream stream)
            => SaveAsGif(source, stream, null);

        /// <summary>
        /// Saves the image to the given stream with the Gif format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task SaveAsGifAsync(this Image source, Stream stream, CancellationToken cancellationToken = default)
            => SaveAsGifAsync(source, stream, null, cancellationToken);

        /// <summary>
        /// Saves the image to the given stream with the Gif format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static void SaveAsGif(this Image source, Stream stream, GifEncoder encoder)
            => source.Save(
                stream,
                encoder ?? source.GetConfiguration().ImageFormatsManager.FindEncoder(GifFormat.Instance));

        /// <summary>
        /// Saves the image to the given stream with the Gif format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task SaveAsGifAsync(this Image source, Stream stream, GifEncoder encoder, CancellationToken cancellationToken = default) =>
            source.SaveAsync(
                stream,
                encoder ?? source.GetConfiguration().ImageFormatsManager.FindEncoder(GifFormat.Instance),
                cancellationToken);

        /// <summary>
        /// Saves the image to the given stream with the Jpeg format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="path">The file path to save the image to.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the path is null.</exception>
        public static void SaveAsJpeg(this Image source, string path) => SaveAsJpeg(source, path, null);

        /// <summary>
        /// Saves the image to the given stream with the Jpeg format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="path">The file path to save the image to.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the path is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task SaveAsJpegAsync(this Image source, string path) => SaveAsJpegAsync(source, path, null);

        /// <summary>
        /// Saves the image to the given stream with the Jpeg format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="path">The file path to save the image to.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the path is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task SaveAsJpegAsync(this Image source, string path, CancellationToken cancellationToken)
            => SaveAsJpegAsync(source, path, null, cancellationToken);

        /// <summary>
        /// Saves the image to the given stream with the Jpeg format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="path">The file path to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the path is null.</exception>
        public static void SaveAsJpeg(this Image source, string path, JpegEncoder encoder) =>
            source.Save(
                path,
                encoder ?? source.GetConfiguration().ImageFormatsManager.FindEncoder(JpegFormat.Instance));

        /// <summary>
        /// Saves the image to the given stream with the Jpeg format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="path">The file path to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the path is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task SaveAsJpegAsync(this Image source, string path, JpegEncoder encoder, CancellationToken cancellationToken = default) =>
            source.SaveAsync(
                path,
                encoder ?? source.GetConfiguration().ImageFormatsManager.FindEncoder(JpegFormat.Instance),
                cancellationToken);

        /// <summary>
        /// Saves the image to the given stream with the Jpeg format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        public static void SaveAsJpeg(this Image source, Stream stream)
            => SaveAsJpeg(source, stream, null);

        /// <summary>
        /// Saves the image to the given stream with the Jpeg format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task SaveAsJpegAsync(this Image source, Stream stream, CancellationToken cancellationToken = default)
            => SaveAsJpegAsync(source, stream, null, cancellationToken);

        /// <summary>
        /// Saves the image to the given stream with the Jpeg format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static void SaveAsJpeg(this Image source, Stream stream, JpegEncoder encoder)
            => source.Save(
                stream,
                encoder ?? source.GetConfiguration().ImageFormatsManager.FindEncoder(JpegFormat.Instance));

        /// <summary>
        /// Saves the image to the given stream with the Jpeg format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task SaveAsJpegAsync(this Image source, Stream stream, JpegEncoder encoder, CancellationToken cancellationToken = default) =>
            source.SaveAsync(
                stream,
                encoder ?? source.GetConfiguration().ImageFormatsManager.FindEncoder(JpegFormat.Instance),
                cancellationToken);

        /// <summary>
        /// Saves the image to the given stream with the Png format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="path">The file path to save the image to.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the path is null.</exception>
        public static void SaveAsPng(this Image source, string path) => SaveAsPng(source, path, null);

        /// <summary>
        /// Saves the image to the given stream with the Png format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="path">The file path to save the image to.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the path is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task SaveAsPngAsync(this Image source, string path) => SaveAsPngAsync(source, path, null);

        /// <summary>
        /// Saves the image to the given stream with the Png format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="path">The file path to save the image to.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the path is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task SaveAsPngAsync(this Image source, string path, CancellationToken cancellationToken)
            => SaveAsPngAsync(source, path, null, cancellationToken);

        /// <summary>
        /// Saves the image to the given stream with the Png format.
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
        /// Saves the image to the given stream with the Png format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="path">The file path to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the path is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task SaveAsPngAsync(this Image source, string path, PngEncoder encoder, CancellationToken cancellationToken = default) =>
            source.SaveAsync(
                path,
                encoder ?? source.GetConfiguration().ImageFormatsManager.FindEncoder(PngFormat.Instance),
                cancellationToken);

        /// <summary>
        /// Saves the image to the given stream with the Png format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        public static void SaveAsPng(this Image source, Stream stream)
            => SaveAsPng(source, stream, null);

        /// <summary>
        /// Saves the image to the given stream with the Png format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task SaveAsPngAsync(this Image source, Stream stream, CancellationToken cancellationToken = default)
            => SaveAsPngAsync(source, stream, null, cancellationToken);

        /// <summary>
        /// Saves the image to the given stream with the Png format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static void SaveAsPng(this Image source, Stream stream, PngEncoder encoder)
            => source.Save(
                stream,
                encoder ?? source.GetConfiguration().ImageFormatsManager.FindEncoder(PngFormat.Instance));

        /// <summary>
        /// Saves the image to the given stream with the Png format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task SaveAsPngAsync(this Image source, Stream stream, PngEncoder encoder, CancellationToken cancellationToken = default) =>
            source.SaveAsync(
                stream,
                encoder ?? source.GetConfiguration().ImageFormatsManager.FindEncoder(PngFormat.Instance),
                cancellationToken);

        /// <summary>
        /// Saves the image to the given stream with the Tga format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="path">The file path to save the image to.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the path is null.</exception>
        public static void SaveAsTga(this Image source, string path) => SaveAsTga(source, path, null);

        /// <summary>
        /// Saves the image to the given stream with the Tga format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="path">The file path to save the image to.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the path is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task SaveAsTgaAsync(this Image source, string path) => SaveAsTgaAsync(source, path, null);

        /// <summary>
        /// Saves the image to the given stream with the Tga format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="path">The file path to save the image to.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the path is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task SaveAsTgaAsync(this Image source, string path, CancellationToken cancellationToken)
            => SaveAsTgaAsync(source, path, null, cancellationToken);

        /// <summary>
        /// Saves the image to the given stream with the Tga format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="path">The file path to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the path is null.</exception>
        public static void SaveAsTga(this Image source, string path, TgaEncoder encoder) =>
            source.Save(
                path,
                encoder ?? source.GetConfiguration().ImageFormatsManager.FindEncoder(TgaFormat.Instance));

        /// <summary>
        /// Saves the image to the given stream with the Tga format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="path">The file path to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the path is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task SaveAsTgaAsync(this Image source, string path, TgaEncoder encoder, CancellationToken cancellationToken = default) =>
            source.SaveAsync(
                path,
                encoder ?? source.GetConfiguration().ImageFormatsManager.FindEncoder(TgaFormat.Instance),
                cancellationToken);

        /// <summary>
        /// Saves the image to the given stream with the Tga format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        public static void SaveAsTga(this Image source, Stream stream)
            => SaveAsTga(source, stream, null);

        /// <summary>
        /// Saves the image to the given stream with the Tga format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task SaveAsTgaAsync(this Image source, Stream stream, CancellationToken cancellationToken = default)
            => SaveAsTgaAsync(source, stream, null, cancellationToken);

        /// <summary>
        /// Saves the image to the given stream with the Tga format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static void SaveAsTga(this Image source, Stream stream, TgaEncoder encoder)
            => source.Save(
                stream,
                encoder ?? source.GetConfiguration().ImageFormatsManager.FindEncoder(TgaFormat.Instance));

        /// <summary>
        /// Saves the image to the given stream with the Tga format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task SaveAsTgaAsync(this Image source, Stream stream, TgaEncoder encoder, CancellationToken cancellationToken = default) =>
            source.SaveAsync(
                stream,
                encoder ?? source.GetConfiguration().ImageFormatsManager.FindEncoder(TgaFormat.Instance),
                cancellationToken);

    }
}

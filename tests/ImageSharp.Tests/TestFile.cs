// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests
{
    /// <summary>
    /// A test image file.
    /// </summary>
    public sealed class TestFile
    {
        /// <summary>
        /// The test file cache.
        /// </summary>
        private static readonly ConcurrentDictionary<string, TestFile> Cache = new();

        /// <summary>
        /// The "Formats" directory, as lazy value
        /// </summary>
        // ReSharper disable once InconsistentNaming
        private static readonly Lazy<string> InputImagesDirectoryValue = new(() => TestEnvironment.InputImagesDirectoryFullPath);

        /// <summary>
        /// The image (lazy initialized value)
        /// </summary>
        private volatile Image<Rgba32> image;

        /// <summary>
        /// The image bytes
        /// </summary>
        private byte[] bytes;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestFile"/> class.
        /// </summary>
        /// <param name="file">The file.</param>
        private TestFile(string file) => this.FullPath = file;

        /// <summary>
        /// Gets the image bytes.
        /// </summary>
        public byte[] Bytes => this.bytes ??= File.ReadAllBytes(this.FullPath);

        /// <summary>
        /// Gets the full path to file.
        /// </summary>
        public string FullPath { get; }

        /// <summary>
        /// Gets the file name.
        /// </summary>
        public string FileName => Path.GetFileName(this.FullPath);

        /// <summary>
        /// Gets the file name without extension.
        /// </summary>
        public string FileNameWithoutExtension => Path.GetFileNameWithoutExtension(this.FullPath);

        /// <summary>
        /// Gets the image with lazy initialization.
        /// </summary>
        private Image<Rgba32> Image
        {
            get
            {
                Image<Rgba32> img = this.image;
                if (img is null)
                {
                    Image<Rgba32> loadedImg = ImageSharp.Image.Load<Rgba32>(this.Bytes);
                    img = Interlocked.CompareExchange(location1: ref this.image, value: loadedImg, comparand: null);
                    if (img is not null)
                    {
                        loadedImg.Dispose();
                    }
                    else
                    {
                        img = loadedImg;
                    }
                }

                return img;
            }
        }

        /// <summary>
        /// Gets the input image directory.
        /// </summary>
        private static string InputImagesDirectory => InputImagesDirectoryValue.Value;

        /// <summary>
        /// Gets the full qualified path to the input test file.
        /// </summary>
        /// <param name="file">
        /// The file path.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        public static string GetInputFileFullPath(string file)
            => Path.Combine(InputImagesDirectory, file).Replace('\\', Path.DirectorySeparatorChar);

        /// <summary>
        /// Creates a new test file or returns one from the cache.
        /// </summary>
        /// <param name="file">The file path.</param>
        /// <returns>
        /// The <see cref="TestFile"/>.
        /// </returns>
        public static TestFile Create(string file)
            => Cache.GetOrAdd(file, (string fileName) => new TestFile(GetInputFileFullPath(fileName)));

        /// <summary>
        /// Gets the file name.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        public string GetFileName(object value)
            => $"{this.FileNameWithoutExtension}-{value}{Path.GetExtension(this.FullPath)}";

        /// <summary>
        /// Gets the file name without extension.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        public string GetFileNameWithoutExtension(object value)
            => this.FileNameWithoutExtension + "-" + value;

        /// <summary>
        /// Creates a new <see cref="Rgba32"/> image.
        /// </summary>
        /// <returns>
        /// The <see cref="Image{Rgba32}"/>.
        /// </returns>
        public Image<Rgba32> CreateRgba32Image()
            => this.Image.Clone();

        /// <summary>
        /// Creates a new <see cref="Rgba32"/> image.
        /// </summary>
        /// <returns>
        /// The <see cref="Image{Rgba32}"/>.
        /// </returns>
        public Image<Rgba32> CreateRgba32Image(IImageDecoder decoder)
            => this.CreateRgba32Image(decoder, new());

        /// <summary>
        /// Creates a new <see cref="Rgba32"/> image.
        /// </summary>
        /// <returns>
        /// The <see cref="Image{Rgba32}"/>.
        /// </returns>
        public Image<Rgba32> CreateRgba32Image(IImageDecoder decoder, DecoderOptions options)
        {
            options.Configuration = this.Image.GetConfiguration();
            using MemoryStream stream = new(this.Bytes);
            return decoder.Decode<Rgba32>(options, stream, default);
        }
    }
}

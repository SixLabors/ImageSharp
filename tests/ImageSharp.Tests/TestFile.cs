// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.IO;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests
{
    /// <summary>
    /// A test image file.
    /// </summary>
    public class TestFile
    {
        /// <summary>
        /// The test file cache.
        /// </summary>
        private static readonly ConcurrentDictionary<string, TestFile> Cache = new ConcurrentDictionary<string, TestFile>();

        /// <summary>
        /// The "Formats" directory, as lazy value
        /// </summary>
        // ReSharper disable once InconsistentNaming
        private static readonly Lazy<string> InputImagesDirectoryValue = new Lazy<string>(() => TestEnvironment.InputImagesDirectoryFullPath);

        /// <summary>
        /// The image (lazy initialized value)
        /// </summary>
        private Image<Rgba32> image;

        /// <summary>
        /// The image bytes
        /// </summary>
        private byte[] bytes;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestFile"/> class.
        /// </summary>
        /// <param name="file">The file.</param>
        private TestFile(string file)
        {
            this.FullPath = file;
        }

        /// <summary>
        /// Gets the image bytes.
        /// </summary>
        public byte[] Bytes => this.bytes ?? (this.bytes = File.ReadAllBytes(this.FullPath));

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
        private Image<Rgba32> Image => this.image ?? (this.image = ImageSharp.Image.Load<Rgba32>(this.Bytes));

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
        {
            return Path.Combine(InputImagesDirectory, file).Replace('\\', Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// Creates a new test file or returns one from the cache.
        /// </summary>
        /// <param name="file">The file path.</param>
        /// <returns>
        /// The <see cref="TestFile"/>.
        /// </returns>
        public static TestFile Create(string file)
        {
            return Cache.GetOrAdd(file, (string fileName) => new TestFile(GetInputFileFullPath(file)));
        }

        /// <summary>
        /// Gets the file name.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        public string GetFileName(object value)
        {
            return $"{this.FileNameWithoutExtension}-{value}{Path.GetExtension(this.FullPath)}";
        }

        /// <summary>
        /// Gets the file name without extension.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        public string GetFileNameWithoutExtension(object value)
        {
            return this.FileNameWithoutExtension + "-" + value;
        }

        /// <summary>
        /// Creates a new image.
        /// </summary>
        /// <returns>
        /// The <see cref="ImageSharp.Image"/>.
        /// </returns>
        public Image<Rgba32> CreateRgba32Image()
        {
            return this.Image.Clone();
        }

        /// <summary>
        /// Creates a new image.
        /// </summary>
        /// <returns>
        /// The <see cref="ImageSharp.Image"/>.
        /// </returns>
        public Image<Rgba32> CreateRgba32Image(IImageDecoder decoder)
        {
            return ImageSharp.Image.Load<Rgba32>(this.Image.GetConfiguration(), this.Bytes, decoder);
        }
    }
}

// <copyright file="TestImage.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.Collections.Concurrent;
    using System.IO;

    using ImageSharp.Formats;

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
        /// The formats directory.
        /// </summary>
        private static readonly string FormatsDirectory = GetFormatsDirectory();

        /// <summary>
        /// The image.
        /// </summary>
        private readonly Image image;

        /// <summary>
        /// The file.
        /// </summary>
        private readonly string file;

        /// <summary>
        /// Initializes static members of the <see cref="TestFile"/> class.
        /// </summary>
        static TestFile()
        {
            // Register the individual image formats.
            // TODO: Is this the best place to do this?
            Bootstrapper.Default.AddImageFormat(new PngFormat());
            Bootstrapper.Default.AddImageFormat(new JpegFormat());
            Bootstrapper.Default.AddImageFormat(new BmpFormat());
            Bootstrapper.Default.AddImageFormat(new GifFormat());
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TestFile"/> class.
        /// </summary>
        /// <param name="file">The file.</param>
        private TestFile(string file)
        {
            this.file = file;

            this.Bytes = File.ReadAllBytes(file);
            this.image = new Image(this.Bytes);
        }

        /// <summary>
        /// Gets the bytes.
        /// </summary>
        public byte[] Bytes { get; }

        /// <summary>
        /// The file name.
        /// </summary>
        public string FileName => Path.GetFileName(this.file);

        /// <summary>
        /// The file name without extension.
        /// </summary>
        public string FileNameWithoutExtension => Path.GetFileNameWithoutExtension(this.file);

        /// <summary>
        /// Gets the full qualified path to the file.
        /// </summary>
        /// <param name="file">
        /// The file path.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        public static string GetPath(string file)
        {
            return Path.Combine(FormatsDirectory, file);
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
            return Cache.GetOrAdd(file, (string fileName) => new TestFile(GetPath(file)));
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
            return $"{this.FileNameWithoutExtension}-{value}{Path.GetExtension(this.file)}";
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
        /// The <see cref="Image"/>.
        /// </returns>
        public Image CreateImage()
        {
            return new Image(this.image);
        }

        /// <summary>
        /// Gets the correct path to the formats directory.
        /// </summary>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        private static string GetFormatsDirectory()
        {
            // Here for code coverage tests.
            string directory = "TestImages/Formats/";
            if (Directory.Exists(directory))
            {
                return directory;
            }

            return "../../../../TestImages/Formats/";
        }
    }
}

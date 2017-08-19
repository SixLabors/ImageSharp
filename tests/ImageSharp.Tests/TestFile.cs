// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
        private static readonly Lazy<string> formatsDirectory = new Lazy<string>(GetFormatsDirectory);
        
        /// <summary>
        /// The image (lazy initialized value)
        /// </summary>
        private Image<Rgba32> image;

        /// <summary>
        /// The image bytes
        /// </summary>
        private byte[] bytes;

        /// <summary>
        /// The file.
        /// </summary>
        private readonly string file;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestFile"/> class.
        /// </summary>
        /// <param name="file">The file.</param>
        private TestFile(string file)
        {
            this.file = file;
        }

        /// <summary>
        /// Gets the image bytes.
        /// </summary>
        public byte[] Bytes => this.bytes ?? (this.bytes = File.ReadAllBytes(this.file));

        /// <summary>
        /// The file name.
        /// </summary>
        public string FilePath => this.file;

        /// <summary>
        /// The file name.
        /// </summary>
        public string FileName => Path.GetFileName(this.file);

        /// <summary>
        /// The file name without extension.
        /// </summary>
        public string FileNameWithoutExtension => Path.GetFileNameWithoutExtension(this.file);

        /// <summary>
        /// Gets the image with lazy initialization.
        /// </summary>
        private Image<Rgba32> Image => this.image ?? (this.image = ImageSharp.Image.Load<Rgba32>(this.Bytes));

        /// <summary>
        /// Gets the "Formats" test file directory.
        /// </summary>
        private static string FormatsDirectory => formatsDirectory.Value;
        
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
        /// The <see cref="ImageSharp.Image"/>.
        /// </returns>
        public Image<Rgba32> CreateImage()
        {
            return this.Image.Clone();
        }

        /// <summary>
        /// Creates a new image.
        /// </summary>
        /// <returns>
        /// The <see cref="ImageSharp.Image"/>.
        /// </returns>
        public Image<Rgba32> CreateImage(IImageDecoder decoder)
        {
            return ImageSharp.Image.Load(this.Image.Configuration, this.Bytes, decoder);
        }

        /// <summary>
        /// Gets the correct path to the formats directory.
        /// </summary>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        private static string GetFormatsDirectory()
        {
            List<string> directories = new List< string > {
                 "TestImages/Formats/", // Here for code coverage tests.
                  "tests/ImageSharp.Tests/TestImages/Formats/", // from travis/build script
                  "../../../../../ImageSharp.Tests/TestImages/Formats/", // from Sandbox46
                  "../../../../TestImages/Formats/",
                  "../../../TestImages/Formats/"
            };

            directories = directories.SelectMany(x => new[]
                                     {
                                         Path.GetFullPath(x)
                                     }).ToList();

            AddFormatsDirectoryFromTestAssebmlyPath(directories);

            string directory = directories.FirstOrDefault(x => Directory.Exists(x));

            if(directory  != null)
            {
                return directory;
            }

            throw new System.Exception($"Unable to find Formats directory at any of these locations [{string.Join(", ", directories)}]");
        }

        /// <summary>
        /// The path returned by Path.GetFullPath(x) can be relative to dotnet framework directory
        /// in certain scenarios like dotTrace test profiling.
        /// This method calculates and adds the format directory based on the ImageSharp.Tests assembly location.
        /// </summary>
        /// <param name="directories">The directories list</param>
        private static void AddFormatsDirectoryFromTestAssebmlyPath(List<string> directories)
        {
            string assemblyLocation = typeof(TestFile).GetTypeInfo().Assembly.Location;
            assemblyLocation = Path.GetDirectoryName(assemblyLocation);

            if (assemblyLocation != null)
            {
                string dirFromAssemblyLocation = Path.Combine(assemblyLocation, "../../../TestImages/Formats/");
                dirFromAssemblyLocation = Path.GetFullPath(dirFromAssemblyLocation);
                directories.Add(dirFromAssemblyLocation);
            }
        }
    }
}

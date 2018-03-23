// <copyright file="MultiImageBenchmarkBase.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.Codecs
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using System.Numerics;

    using BenchmarkDotNet.Attributes;

    using SixLabors.ImageSharp.Tests;

    using CoreImage = ImageSharp.Image;

    public abstract class MultiImageBenchmarkBase : BenchmarkBase
    {
        protected Dictionary<string, byte[]> FileNamesToBytes = new Dictionary<string, byte[]>();

        protected Dictionary<string, Image<Rgba32>> FileNamesToImageSharpImages = new Dictionary<string, Image<Rgba32>>();
        protected Dictionary<string, System.Drawing.Bitmap> FileNamesToSystemDrawingImages = new Dictionary<string, System.Drawing.Bitmap>();

        /// <summary>
        /// The values of this enum separate input files into categories
        /// </summary>
        public enum InputImageCategory
        {
            AllImages,

            SmallImagesOnly,

            LargeImagesOnly
        }

        [Params(InputImageCategory.AllImages, InputImageCategory.SmallImagesOnly, InputImageCategory.LargeImagesOnly)]
        public virtual InputImageCategory InputCategory { get; set; }

        protected virtual string BaseFolder => TestEnvironment.InputImagesDirectoryFullPath;

        protected virtual IEnumerable<string> SearchPatterns => new[] { "*.*" };

        /// <summary>
        /// Gets the file names containing these strings are substrings are not processed by the benchmark.
        /// </summary>
        protected IEnumerable<string> ExcludeSubstringsInFileNames => new[] { "badeof", "BadEof", "CriticalEOF" };

        /// <summary>
        /// Enumerates folders containing files OR files to be processed by the benchmark.
        /// </summary>
        protected IEnumerable<string> AllFoldersOrFiles => this.InputImageSubfoldersOrFiles.Select(f => Path.Combine(this.BaseFolder, f));

        /// <summary>
        /// The images sized above this threshold will be included in
        /// </summary>
        protected virtual int LargeImageThresholdInBytes => 100000;

        protected IEnumerable<KeyValuePair<string, T>> EnumeratePairsByBenchmarkSettings<T>(
            Dictionary<string, T> input,
            Predicate<T> checkIfSmall)
        {
            switch (this.InputCategory)
            {
                case InputImageCategory.AllImages:
                    return input;
                case InputImageCategory.SmallImagesOnly:
                    return input.Where(kv => checkIfSmall(kv.Value));
                case InputImageCategory.LargeImagesOnly:
                    return input.Where(kv => !checkIfSmall(kv.Value));
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        protected IEnumerable<KeyValuePair<string, byte[]>> FileNames2Bytes
            =>
            this.EnumeratePairsByBenchmarkSettings(
                this.FileNamesToBytes,
                arr => arr.Length < this.LargeImageThresholdInBytes);

        protected abstract IEnumerable<string> InputImageSubfoldersOrFiles { get; }

        [GlobalSetup]
        public void ReadImages()
        {
            if (!Vector.IsHardwareAccelerated)
            {
                throw new Exception("Vector.IsHardwareAccelerated == false! Check your build settings!");
            }
            // Console.WriteLine("Vector.IsHardwareAccelerated: " + Vector.IsHardwareAccelerated);
            this.ReadFilesImpl();
        }

        protected virtual void ReadFilesImpl()
        {
            foreach (string path in this.AllFoldersOrFiles)
            {
                if (File.Exists(path))
                {
                    this.FileNamesToBytes[path] = File.ReadAllBytes(path);
                    continue;
                }

                string[] allFiles =
                    this.SearchPatterns.SelectMany(
                        f =>
                            Directory.EnumerateFiles(path, f, SearchOption.AllDirectories)
                                .Where(fn => !this.ExcludeSubstringsInFileNames.Any(w => fn.ToLower().Contains(w)))).ToArray();

                foreach (string fn in allFiles)
                {
                    this.FileNamesToBytes[fn] = File.ReadAllBytes(fn);
                }
            }
        }

        /// <summary>
        /// Execute code for each image stream. If the returned object of the opearation <see cref="Func{T, TResult}"/> is <see cref="IDisposable"/> it will be disposed.
        /// </summary>
        /// <param name="operation">The operation to execute. If the returned object is &lt;see cref="IDisposable"/&gt; it will be disposed </param>
        protected void ForEachStream(Func<MemoryStream, object> operation)
        {
            foreach (KeyValuePair<string, byte[]> kv in this.FileNames2Bytes)
            {
                using (MemoryStream memoryStream = new MemoryStream(kv.Value))
                {
                    try
                    {
                        object obj = operation(memoryStream);
                        (obj as IDisposable)?.Dispose();

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Operation on {kv.Key} failed with {ex.Message}");
                    }
                }
            }
        }

        public abstract class WithImagesPreloaded : MultiImageBenchmarkBase
        {
            protected override void ReadFilesImpl()
            {
                base.ReadFilesImpl();

                foreach (KeyValuePair<string, byte[]> kv in this.FileNamesToBytes)
                {
                    byte[] bytes = kv.Value;
                    string fn = kv.Key;

                    using (MemoryStream ms1 = new MemoryStream(bytes))
                    {
                        this.FileNamesToImageSharpImages[fn] = CoreImage.Load<Rgba32>(ms1);

                    }

                    this.FileNamesToSystemDrawingImages[fn] = new Bitmap(new MemoryStream(bytes));
                }
            }

            protected IEnumerable<KeyValuePair<string, Image<Rgba32>>> FileNames2ImageSharpImages
                =>
                this.EnumeratePairsByBenchmarkSettings(
                    this.FileNamesToImageSharpImages,
                    img => img.Width * img.Height < this.LargeImageThresholdInPixels);

            protected IEnumerable<KeyValuePair<string, System.Drawing.Bitmap>> FileNames2SystemDrawingImages
                =>
                this.EnumeratePairsByBenchmarkSettings(
                    this.FileNamesToSystemDrawingImages,
                    img => img.Width * img.Height < this.LargeImageThresholdInPixels);

            protected virtual int LargeImageThresholdInPixels => 700000;

            protected void ForEachImageSharpImage(Func<Image<Rgba32>, object> operation)
            {
                foreach (KeyValuePair<string, Image<Rgba32>> kv in this.FileNames2ImageSharpImages)
                {
                    try
                    {
                        object obj = operation(kv.Value);
                        (obj as IDisposable)?.Dispose();

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Operation on {kv.Key} failed with {ex.Message}");
                    }

                }
            }

            protected void ForEachImageSharpImage(Func<Image<Rgba32>, MemoryStream, object> operation)
            {
                using (MemoryStream workStream = new MemoryStream())
                {

                    this.ForEachImageSharpImage(
                        img =>
                        {
                            // ReSharper disable AccessToDisposedClosure
                            object result = operation(img, workStream);
                            workStream.Seek(0, SeekOrigin.Begin);
                            // ReSharper restore AccessToDisposedClosure
                            return result;
                        });
                }
            }

            protected void ForEachSystemDrawingImage(Func<System.Drawing.Bitmap, object> operation)
            {
                foreach (KeyValuePair<string, Bitmap> kv in this.FileNames2SystemDrawingImages)
                {
                    try
                    {
                        object obj = operation(kv.Value);
                        (obj as IDisposable)?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Operation on {kv.Key} failed with {ex.Message}");
                    }
                }
            }

            protected void ForEachSystemDrawingImage(Func<System.Drawing.Bitmap, MemoryStream, object> operation)
            {
                using (MemoryStream workStream = new MemoryStream())
                {

                    this.ForEachSystemDrawingImage(
                        img =>
                        {
                            // ReSharper disable AccessToDisposedClosure
                            object result = operation(img, workStream);
                            workStream.Seek(0, SeekOrigin.Begin);
                            // ReSharper restore AccessToDisposedClosure
                            return result;
                        });
                }

            }
        }
    }
}
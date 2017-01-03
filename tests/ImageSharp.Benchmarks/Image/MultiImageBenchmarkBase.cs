namespace ImageSharp.Benchmarks.Image
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using System.Numerics;

    using BenchmarkDotNet.Attributes;

    using Image = ImageSharp.Image;

    public abstract class MultiImageBenchmarkBase : BenchmarkBase
    {
        protected Dictionary<string, byte[]> FileNamesToBytes = new Dictionary<string, byte[]>();

        protected Dictionary<string, Image> FileNamesToImageSharpImages = new Dictionary<string, Image>();
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
        public InputImageCategory InputCategory { get; set; }
        
        protected virtual string BaseFolder => "../ImageSharp.Tests/TestImages/Formats/";

        protected virtual IEnumerable<string> FileFilters => new[] { "*.*" };
        
        /// <summary>
        /// Gets the file names containing these strings are substrings are not processed by the benchmark.
        /// </summary>
        protected IEnumerable<string> ExcludeSubstringsInFileNames => new string[] { };

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

        [Setup]
        public void ReadImages()
        {
            // Console.WriteLine("Vector.IsHardwareAccelerated: " + Vector.IsHardwareAccelerated);
            this.ReadImagesImpl();
        }

        protected virtual void ReadImagesImpl()
        {
            foreach (string folder in this.AllFoldersOrFiles)
            {

                var allFiles =
                    this.FileFilters.SelectMany(
                        f =>
                            Directory.EnumerateFiles(folder, f, SearchOption.AllDirectories)
                                .Where(fn => !this.ExcludeSubstringsInFileNames.Any(w => fn.ToLower().Contains(w)))).ToArray();
                foreach (var fn in allFiles)
                {
                    this.FileNamesToBytes[fn] = File.ReadAllBytes(fn);
                }
            }
        }

        protected void ForEachStream(Func<MemoryStream, object> operation)
        {
            foreach (var kv in this.FileNames2Bytes)
            {
                using (MemoryStream memoryStream = new MemoryStream(kv.Value))
                {
                    try
                    {
                        var obj = operation(memoryStream);
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
            protected override void ReadImagesImpl()
            {
                base.ReadImagesImpl();

                foreach (var kv in this.FileNamesToBytes)
                {
                    byte[] bytes = kv.Value;
                    string fn = kv.Key;

                    using (var ms1 = new MemoryStream(bytes))
                    {
                        this.FileNamesToImageSharpImages[fn] = new Image(ms1);

                    }

                    this.FileNamesToSystemDrawingImages[fn] = new Bitmap(new MemoryStream(bytes));
                }
            }

            protected IEnumerable<KeyValuePair<string, ImageSharp.Image>> FileNames2ImageSharpImages
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

            protected void ForEachImageSharpImage(Func<Image, object> operation)
            {
                foreach (var kv in this.FileNames2ImageSharpImages)
                {
                    try
                    {
                        var obj = operation(kv.Value);
                        (obj as IDisposable)?.Dispose();

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Operation on {kv.Key} failed with {ex.Message}");
                    }

                }
            }

            protected void ForEachSystemDrawingImage(Func<System.Drawing.Bitmap, object> operation)
            {
                foreach (var kv in this.FileNames2SystemDrawingImages)
                {
                    try
                    {
                        var obj = operation(kv.Value);
                        (obj as IDisposable)?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Operation on {kv.Key} failed with {ex.Message}");
                    }
                }
            }
        }


    }


}
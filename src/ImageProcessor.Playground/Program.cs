// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The program.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.PlayGround
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing;
    using System.IO;
    using System.Linq;

    using ImageProcessor;
    using ImageProcessor.Configuration;
    using ImageProcessor.Imaging;
    using ImageProcessor.Imaging.Filters.EdgeDetection;
    using ImageProcessor.Imaging.Filters.Photo;
    using ImageProcessor.Imaging.Formats;

    /// <summary>
    /// The program.
    /// </summary>
    public class Program
    {
        protected static readonly IEnumerable<ISupportedImageFormat> formats = ImageProcessorBootstrapper.Instance.SupportedImageFormats;

        /// <summary>
        /// The main routine.
        /// </summary>
        /// <param name="args">
        /// The args.
        /// </param>
        public static void Main(string[] args)
        {
            string path = new Uri(System.Reflection.Assembly.GetExecutingAssembly().CodeBase).LocalPath;

            // ReSharper disable once AssignNullToNotNullAttribute
            string resolvedPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path), @"..\..\images\input"));
            DirectoryInfo di = new DirectoryInfo(resolvedPath);
            if (!di.Exists)
            {
                di.Create();
            }

            Image mask = Image.FromFile(Path.Combine(resolvedPath, "mask2.png"));
            IEnumerable<FileInfo> files = GetFilesByExtensions(di, ".jpg");
            //IEnumerable<FileInfo> files = GetFilesByExtensions(di, ".gif", ".webp", ".bmp", ".jpg", ".png", ".tif");

            foreach (FileInfo fileInfo in files)
            {
                byte[] photoBytes = File.ReadAllBytes(fileInfo.FullName);
                Console.WriteLine("Processing: " + fileInfo.Name);

                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                // ImageProcessor
                using (MemoryStream inStream = new MemoryStream(photoBytes))
                {
                    using (ImageFactory imageFactory = new ImageFactory(true))
                    {
                        Size size = new Size(800, 0);
                        ResizeLayer layer = new ResizeLayer(size, ResizeMode.Max, AnchorPosition.Center, false);

                        //ContentAwareResizeLayer layer = new ContentAwareResizeLayer(size)
                        //{
                        //    ConvolutionType = ConvolutionType.Sobel
                        //};
                        // Load, resize, set the format and quality and save an image.
                        imageFactory.Load(inStream)
                            //.BackgroundColor(Color.White)
                            //.Resize(new Size((int)(size.Width * 1.1), 0))
                            //.ContentAwareResize(layer)
                            //.Constrain(size)
                            .Rotate(66)
                            //.Mask(mask)
                            //.Format(new PngFormat())
                            //.BackgroundColor(Color.Cyan)
                            //.ReplaceColor(Color.FromArgb(255, 1, 107, 165), Color.FromArgb(255, 1, 165, 13), 80)
                            //.Resize(layer)
                            //.DetectEdges(new SobelEdgeFilter(), false)
                            //.DetectEdges(new LaplacianOfGaussianEdgeFilter())
                            //.EntropyCrop()
                            //.Filter(MatrixFilters.Invert)
                            //.Filter(MatrixFilters.Comic)
                            //.Filter(MatrixFilters.HiSatch)
                            //.Pixelate(8)
                            //.GaussianSharpen(10)
                            .Save(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path), @"..\..\images\output", fileInfo.Name)));

                        stopwatch.Stop();
                    }
                }

                Console.WriteLine("Processed: " + fileInfo.Name + " in " + stopwatch.ElapsedMilliseconds + "ms");
            }

            Console.ReadLine();
        }

        public static IEnumerable<FileInfo> GetFilesByExtensions(DirectoryInfo dir, params string[] extensions)
        {
            if (extensions == null)
            {
                throw new ArgumentNullException("extensions");
            }

            IEnumerable<FileInfo> files = dir.EnumerateFiles();
            return files.Where(f => extensions.Contains(f.Extension, StringComparer.OrdinalIgnoreCase));
        }
    }
}

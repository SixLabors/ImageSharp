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
    using ImageProcessor.Processors;
    using ImageProcessor.Web.Caching;

    /// <summary>
    /// The program.
    /// </summary>
    public class Program
    {
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

            // Image mask = Image.FromFile(Path.Combine(resolvedPath, "mask.png"));
            // Image overlay = Image.FromFile(Path.Combine(resolvedPath, "imageprocessor.128.png"));
            //FileInfo fileInfo = new FileInfo(Path.Combine(resolvedPath, "2008.jpg"));
            //FileInfo fileInfo = new FileInfo(Path.Combine(resolvedPath, "stretched.jpg"));
            //FileInfo fileInfo = new FileInfo(Path.Combine(resolvedPath, "mountain.jpg"));
            //FileInfo fileInfo = new FileInfo(Path.Combine(resolvedPath, "Arc-de-Triomphe-France.jpg"));
            //FileInfo fileInfo = new FileInfo(Path.Combine(resolvedPath, "Martin-Schoeller-Jack-Nicholson-Portrait.jpeg"));
            //FileInfo fileInfo = new FileInfo(Path.Combine(resolvedPath, "crop-base-300x200.jpg"));
            //FileInfo fileInfo = new FileInfo(Path.Combine(resolvedPath, "cmyk.png"));
            //IEnumerable<FileInfo> files = GetFilesByExtensions(di, ".gif");
            IEnumerable<FileInfo> files = GetFilesByExtensions(di, ".jpg", ".jpeg", ".jfif");
            //IEnumerable<FileInfo> files = GetFilesByExtensions(di, ".gif", ".webp", ".bmp", ".jpg", ".png", ".tif");

            foreach (FileInfo fileInfo in files)
            {
                if (fileInfo.Name == "test5.jpg")
                {
                    continue;
                }

                byte[] photoBytes = File.ReadAllBytes(fileInfo.FullName);
                Console.WriteLine("Processing: " + fileInfo.Name);

                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                // ImageProcessor
                using (MemoryStream inStream = new MemoryStream(photoBytes))
                {
                    using (ImageFactory imageFactory = new ImageFactory(true))
                    {
                        Size size = new Size(1024, 0);
                        //ResizeLayer layer = new ResizeLayer(size, ResizeMode.Max, AnchorPosition.Center, false);

                        //ContentAwareResizeLayer layer = new ContentAwareResizeLayer(size)
                        //{
                        //    ConvolutionType = ConvolutionType.Sobel
                        //};
                        // Load, resize, set the format and quality and save an image.
                        imageFactory.Load(inStream)
                            //.Overlay(new ImageLayer
                            //            {
                            //                Image = overlay,
                            //                Opacity = 50
                            //            })
                            //.Alpha(50)
                            //.BackgroundColor(Color.White)
                            //.Resize(new Size((int)(size.Width * 1.1), 0))
                            //.ContentAwareResize(layer)
                            //.Constrain(size)
                            //.Mask(mask)
                            //.Format(new PngFormat())
                            //.BackgroundColor(Color.Cyan)
                            //.ReplaceColor(Color.FromArgb(255, 223, 224), Color.FromArgb(121, 188, 255), 128)
                            //.Resize(size)
                            //.Resize(new ResizeLayer(size, ResizeMode.Max))
                            // .Resize(new ResizeLayer(size, ResizeMode.Stretch))
                            //.DetectEdges(new Laplacian3X3EdgeFilter(), true)
                            //.DetectEdges(new LaplacianOfGaussianEdgeFilter())
                            //.EntropyCrop()
                            //.Halftone(true)
                            .RotateBounded(150, false)
                            //.Rotate(140)
                            //.Filter(MatrixFilters.Invert)
                            //.Contrast(50)
                            //.Filter(MatrixFilters.Comic)
                            //.Flip()
                            //.Filter(MatrixFilters.HiSatch)
                            //.Pixelate(8)
                            //.GaussianSharpen(10)
                            //.Format(new PngFormat() { IsIndexed = true })
                            //.Format(new PngFormat() )
                            .Save(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path), @"..\..\images\output", fileInfo.Name)));
                            //.Save(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path), @"..\..\images\output", Path.GetFileNameWithoutExtension(fileInfo.Name) + ".png")));

                        stopwatch.Stop();
                    }
                }

                long peakWorkingSet64 = Process.GetCurrentProcess().PeakWorkingSet64;
                float mB = peakWorkingSet64 / (float)1024 / 1024;

                Console.WriteLine(@"Completed {0} in {1:s\.fff} secs {2}Peak memory usage was {3} bytes or {4} Mb.", fileInfo.Name, stopwatch.Elapsed, Environment.NewLine, peakWorkingSet64.ToString("#,#"), mB);

                //Console.WriteLine("Processed: " + fileInfo.Name + " in " + stopwatch.ElapsedMilliseconds + "ms");
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

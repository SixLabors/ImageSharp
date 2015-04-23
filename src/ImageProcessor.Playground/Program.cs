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
    //using ImageProcessor.Imaging.Filters.ObjectDetection;
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
            //FileInfo fileInfo = new FileInfo(Path.Combine(resolvedPath, "blur-test.png"));
            // FileInfo fileInfo = new FileInfo(Path.Combine(resolvedPath, "earth_lights_4800.tif"));
            //FileInfo fileInfo = new FileInfo(Path.Combine(resolvedPath, "gamma-1.0-or-2.2.png"));
            //FileInfo fileInfo = new FileInfo(Path.Combine(resolvedPath, "gamma_dalai_lama_gray.jpg"));
            //FileInfo fileInfo = new FileInfo(Path.Combine(resolvedPath, "Arc-de-Triomphe-France.jpg"));
            //FileInfo fileInfo = new FileInfo(Path.Combine(resolvedPath, "Martin-Schoeller-Jack-Nicholson-Portrait.jpeg"));
            //FileInfo fileInfo = new FileInfo(Path.Combine(resolvedPath, "night-bridge.png"));
            //FileInfo fileInfo = new FileInfo(Path.Combine(resolvedPath, "tree.jpg"));
            //FileInfo fileInfo = new FileInfo(Path.Combine(resolvedPath, "blue-balloon.jpg"));
            //FileInfo fileInfo = new FileInfo(Path.Combine(resolvedPath, "test2.png"));
            //FileInfo fileInfo = new FileInfo(Path.Combine(resolvedPath, "120430.gif"));
            //FileInfo fileInfo = new FileInfo(Path.Combine(resolvedPath, "rickroll.original.gif"));

            //////FileInfo fileInfo = new FileInfo(Path.Combine(resolvedPath, "crop-base-300x200.jpg"));
            //FileInfo fileInfo = new FileInfo(Path.Combine(resolvedPath, "cmyk.png"));
            //IEnumerable<FileInfo> files = GetFilesByExtensions(di, ".gif");
            //IEnumerable<FileInfo> files = GetFilesByExtensions(di, ".png", ".jpg", ".jpeg");
           // IEnumerable<FileInfo> files = GetFilesByExtensions(di, ".jpg", ".jpeg", ".jfif");
            IEnumerable<FileInfo> files = GetFilesByExtensions(di, ".png");
            //IEnumerable<FileInfo> files = GetFilesByExtensions(di, ".gif", ".webp", ".bmp", ".jpg", ".png");

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
                using (ImageFactory imageFactory = new ImageFactory(true, true))
                {
                    Size size = new Size(1920, 1920);
                    //CropLayer cropLayer = new CropLayer(20, 20, 20, 20, ImageProcessor.Imaging.CropMode.Percentage);
                    ResizeLayer layer = new ResizeLayer(size, ResizeMode.Max, AnchorPosition.Center, false);
                    // TextLayer textLayer = new TextLayer()
                    //{
                    //    Text = "هناك حقيقة مثبتة منذ زمن",
                    //    FontColor = Color.White,
                    //    DropShadow = true,
                    //    Vertical = true,
                    //    //RightToLeft = true,
                    //    //Position = new Point(5, 5)

                    //};

                    //ContentAwareResizeLayer layer = new ContentAwareResizeLayer(size)
                    //{
                    //    ConvolutionType = ConvolutionType.Sobel
                    //};
                    // Load, resize, set the format and quality and save an image.
                    imageFactory.Load(inStream)
                        //.DetectObjects(EmbeddedHaarCascades.FrontFaceDefault)
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
                        //.Watermark(textLayer)
                        //.ReplaceColor(Color.FromArgb(93, 136, 231), Color.FromArgb(94, 134, 78), 50)
                        //.GaussianSharpen(3)
                        //.Saturation(20)
                        //.Resize(size)
                        .Resize(layer)
                        // .Resize(new ResizeLayer(size, ResizeMode.Stretch))
                        //.DetectEdges(new SobelEdgeFilter(), true)
                        //.DetectEdges(new LaplacianOfGaussianEdgeFilter())
                        //.GaussianBlur(new GaussianLayer(10, 11))
                        //.EntropyCrop()
                        //.Gamma(2.2F)
                        //.Halftone()
                        //.RotateBounded(150, false)
                        //.Crop(cropLayer)
                        //.Rotate(140)
                        //.Filter(MatrixFilters.Invert)
                        //.Brightness(-5)
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

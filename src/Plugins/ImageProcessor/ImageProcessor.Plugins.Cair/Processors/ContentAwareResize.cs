// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ContentAwareResize.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Resizes an image to the given dimensions using content aware resizing.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Plugins.Cair.Processors
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.Globalization;
    using System.IO;

    using ImageProcessor.Common.Exceptions;
    using ImageProcessor.Plugins.Cair.Imaging;
    using ImageProcessor.Processors;

    /// <summary>
    /// Resizes an image to the given dimensions using content aware resizing.
    /// </summary>
    public class ContentAwareResize : IGraphicsProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ContentAwareResize"/> class.
        /// </summary>
        public ContentAwareResize()
        {
            this.Settings = new Dictionary<string, string>();
        }

        /// <summary>
        /// Gets or sets DynamicParameter.
        /// </summary>
        public dynamic DynamicParameter
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets any additional settings required by the processor.
        /// </summary>
        public Dictionary<string, string> Settings
        {
            get;
            set;
        }

        /// <summary>
        /// Processes the image.
        /// </summary>
        /// <param name="factory">
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class containing
        /// the image to process.
        /// </param>
        /// <returns>
        /// The processed image from the current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public Image ProcessImage(ImageFactory factory)
        {
            string fileName = Guid.NewGuid().ToString();

            // Use bmp's as the temporary files since they are lossless and support transparency.
            string sourcePath = Path.Combine(CairBootstrapper.CairPath, fileName + ".bmp");
            string resizedPath = Path.Combine(CairBootstrapper.CairPath, fileName + "-r.bmp");

            // Gather the parameters.
            ContentAwareResizeLayer layer = (ContentAwareResizeLayer)this.DynamicParameter;
            int width = layer.Size.Width;
            int height = layer.Size.Height;
            int timeout = layer.Timeout > 0 ? layer.Timeout : 60000;

            int defaultMaxWidth;
            int defaultMaxHeight;

            int.TryParse(this.Settings["MaxWidth"], NumberStyles.Any, CultureInfo.InvariantCulture, out defaultMaxWidth);
            int.TryParse(this.Settings["MaxHeight"], NumberStyles.Any, CultureInfo.InvariantCulture, out defaultMaxHeight);

            Bitmap newImage = null;
            Image image = factory.Image;

            try
            {
                int sourceWidth = image.Width;
                int sourceHeight = image.Height;

                // Fractional variants for preserving aspect ratio.
                double percentHeight = Math.Abs(height / (double)sourceHeight);
                double percentWidth = Math.Abs(width / (double)sourceWidth);

                int maxWidth = defaultMaxWidth > 0 ? defaultMaxWidth : int.MaxValue;
                int maxHeight = defaultMaxHeight > 0 ? defaultMaxHeight : int.MaxValue;

                // If height or width is not passed we assume that the standard ratio is to be kept.
                if (height == 0)
                {
                    height = (int)Math.Ceiling(sourceHeight * percentWidth);
                }

                if (width == 0)
                {
                    width = (int)Math.Ceiling(sourceWidth * percentHeight);
                }

                if (width > 0 && height > 0 && width <= maxWidth && height <= maxHeight)
                {
                    // Save the temporary bitmap.
                    image.Save(sourcePath, ImageFormat.Bmp);

                    // Process the image using the CAIR executable.
                    string arguments = string.Format(
                        " -I \"{0}\" -O \"{1}\" -C {2} -X {3} -Y {4} -E {5} -T {6} -R {7}",
                        sourcePath,
                        resizedPath,
                        (int)layer.ConvolutionType,
                        width,
                        height,
                        (int)layer.EnergyFunction,
                        layer.Parallelize ? Math.Min(4, Environment.ProcessorCount) : 1,
                        (int)layer.OutputType);

                    if (!string.IsNullOrWhiteSpace(layer.WeightPath))
                    {
                        arguments = string.Format("{0} -W {1}", arguments, layer.WeightPath);
                    }

                    this.ProcessCairImage(arguments, timeout);

                    // Assign the new image.
                    newImage = new Bitmap(resizedPath);
                    newImage.MakeTransparent();

                    // Reassign the image.
                    image.Dispose();
                    image = newImage;
                }
            }
            catch (Exception ex)
            {
                if (newImage != null)
                {
                    newImage.Dispose();
                }

                throw new ImageProcessingException("Error processing image with " + this.GetType().Name, ex);
            }
            finally
            {
                FileInfo sourceFileInfo = new FileInfo(sourcePath);
                if (sourceFileInfo.Exists)
                {
                    sourceFileInfo.Delete();
                }

                FileInfo resizedFileInfo = new FileInfo(resizedPath);
                if (resizedFileInfo.Exists)
                {
                    resizedFileInfo.Delete();
                }
            }

            return image;
        }

        /// <summary>
        /// The process cair image.
        /// </summary>
        /// <param name="arguments">
        /// The arguments.
        /// </param>
        /// <param name="timeout">
        /// The time in milliseconds to attempt to resize the image for.
        /// </param>
        private void ProcessCairImage(string arguments, int timeout)
        {
            // Set up and start a new process to resize the image.
            ProcessStartInfo start = new ProcessStartInfo(CairBootstrapper.CairExecutablePath, arguments)
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };

            using (Process process = Process.Start(start))
            {
                if (process != null)
                {
                    if (!process.WaitForExit(timeout))
                    {
                        process.Kill();

                        throw new ImageProcessingException("Error processing image with " + this.GetType().Name + " due to timeout.");
                    }

                    string output = string.Format(" {0} {1}", process.StandardError.ReadToEnd(), process.StandardOutput.ReadToEnd());

                    if (process.ExitCode != 0)
                    {
                        throw new ImageProcessingException("Error processing image with " + this.GetType().Name + output);
                    }
                }
            }
        }
    }
}

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
    using ImageProcessor.Imaging;
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
        public System.Drawing.Image ProcessImage(ImageFactory factory)
        {
            string fileName = Guid.NewGuid().ToString();

            // Use bmp's as the temporary files since they are lossless and support transparency.
            string sourcePath = Path.Combine(CairBootstrapper.CairImagePath, fileName + ".bmp");
            string resizedPath = Path.Combine(CairBootstrapper.CairImagePath, fileName + "-r.bmp");

            // Gather the parameters.
            int width = this.DynamicParameter.Size.Width ?? 0;
            int height = this.DynamicParameter.Size.Height ?? 0;
            ContentAwareResizeConvolutionType convolutionType = this.DynamicParameter.ConvolutionType;
            EnergyFunction energyFunction = this.DynamicParameter.EnergyFunction;
            bool prescale = this.DynamicParameter.PreScale;
            bool parallelize = this.DynamicParameter.Parallelize;
            int timeout = this.DynamicParameter.Timeout ?? 60000;

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
                    if (prescale)
                    {
                        if (width < image.Width || height < image.Height)
                        {
                            int preWidth = Math.Min(image.Width, width + 50); //(int)Math.Ceiling(width * 1.25));
                            ResizeLayer layer = new ResizeLayer(new Size(preWidth, 0));
                            Dictionary<string, string> resizeSettings = new Dictionary<string, string>
                            {
                                {
                                    "MaxWidth", image.Width.ToString("G")
                                },
                                {
                                    "MaxHeight", image.Height.ToString("G")
                                }
                            };
                            Resize resize = new Resize { DynamicParameter = layer, Settings = resizeSettings };
                            image = resize.ProcessImage(factory);
                        }
                    }

                    // Save the temporary bitmap.
                    image.Save(sourcePath, ImageFormat.Bmp);

                    // Process the image using the CAIR executable.
                    string arguments = string.Format(
                        " -I \"{0}\" -O \"{1}\" -C {2} -X {3} -Y {4} -E {5} -T {6}",
                        sourcePath,
                        resizedPath,
                        (int)convolutionType,
                        width,
                        height,
                        (int)energyFunction,
                        parallelize ? Math.Min(4, Environment.ProcessorCount) : 1);

                    bool success = this.ProcessCairImage(arguments, timeout);

                    if (!success)
                    {
                        throw new ImageProcessingException(
                            "Error processing image with " + this.GetType().Name + " due to timeout.");
                    }

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
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
        private bool ProcessCairImage(string arguments, int timeout)
        {
            // Set up and start a new process to resize the image.
            ProcessStartInfo start = new ProcessStartInfo(CairBootstrapper.CairPath, arguments)
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
                    bool result = process.WaitForExit(timeout);

                    if (!result)
                    {
                        process.Kill();
                    }

                    return result;
                }
            }

            return false;
        }
    }
}

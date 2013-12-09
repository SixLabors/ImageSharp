// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ImageFactory.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods for processing image files.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor
{
    #region Using
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Linq;
    using System.Threading;

    using ImageProcessor.Imaging;
    using ImageProcessor.Processors;
    #endregion

    /// <summary>
    /// Encapsulates methods for processing image files.
    /// </summary>
    public class ImageFactory : IDisposable
    {
        #region Fields
        /// <summary>
        /// The default quality for jpeg files.
        /// </summary>
        private const int DefaultJpegQuality = 90;

        /// <summary>
        /// The backup image format.
        /// </summary>
        private ImageFormat backupImageFormat;

        /// <summary>
        /// The original extension.
        /// </summary>
        private string originalExtension;

        /// <summary>
        /// Whether the image is indexed.
        /// </summary>
        private bool isIndexed;

        /// <summary>
        /// A value indicating whether this instance of the given entity has been disposed.
        /// </summary>
        /// <value><see langword="true"/> if this instance has been disposed; otherwise, <see langword="false"/>.</value>
        /// <remarks>
        /// If the entity is disposed, it must not be disposed a second
        /// time. The isDisposed field is set the first time the entity
        /// is disposed. If the isDisposed field is true, then the Dispose()
        /// method will not dispose again. This help not to prolong the entity's
        /// life in the Garbage Collector.
        /// </remarks>
        private bool isDisposed;
        #endregion

        #region Destructors
        /// <summary>
        /// Finalizes an instance of the <see cref="T:ImageProcessor.ImageFactory">ImageFactory</see> class. 
        /// </summary>
        /// <remarks>
        /// Use C# destructor syntax for finalization code.
        /// This destructor will run only if the Dispose method 
        /// does not get called.
        /// It gives your base class the opportunity to finalize.
        /// Do not provide destructors in types derived from this class.
        /// </remarks>
        ~ImageFactory()
        {
            // Do not re-create Dispose clean-up code here.
            // Calling Dispose(false) is optimal in terms of
            // readability and maintainability.
            this.Dispose(false);
        }

        #endregion

        #region Properties
        /// <summary>
        /// Gets the local image for manipulation.
        /// </summary>
        public Image Image { get; private set; }

        /// <summary>
        /// Gets the path to the local image for manipulation.
        /// </summary>
        public string ImagePath { get; private set; }

        /// <summary>
        /// Gets the query-string parameters for web image manipulation.
        /// </summary>
        public string QueryString { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the image factory should process the file.
        /// </summary>
        public bool ShouldProcess { get; private set; }

        /// <summary>
        /// Gets the file format of the image. 
        /// </summary>
        public ImageFormat ImageFormat { get; private set; }

        /// <summary>
        /// Gets or sets the quality of output for jpeg images as a percentile.
        /// </summary>
        internal int JpegQuality { get; set; }
        #endregion

        #region Methods
        /// <summary>
        /// Loads the image to process. Always call this method first.
        /// </summary>
        /// <param name="memoryStream">
        /// The <see cref="T:System.IO.MemoryStream"/> containing the image information.
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory Load(MemoryStream memoryStream)
        {
            // Set our image as the memory stream value.
            this.Image = Image.FromStream(memoryStream, true);

            // Store the stream in the image Tag property so we can dispose of it later.
            this.Image.Tag = memoryStream;

            // Set the other properties.
            this.JpegQuality = DefaultJpegQuality;
            this.backupImageFormat = ImageFormat.Jpeg;
            this.ImageFormat = ImageFormat.Jpeg;
            this.isIndexed = ImageUtils.IsIndexed(this.Image);
            this.ShouldProcess = true;

            return this;
        }

        /// <summary>
        /// Loads the image to process. Always call this method first.
        /// </summary>
        /// <param name="imagePath">The absolute path to the image to load.</param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory Load(string imagePath)
        {
            string[] paths = imagePath.Split('?');
            string path = paths[0];
            string query = string.Empty;

            if (paths.Length > 1)
            {
                query = paths[1];
            }

            string imageName = Path.GetFileName(path);

            if (File.Exists(path))
            {
                this.ImagePath = path;
                this.QueryString = query;

                // Open a file stream to prevent the need for lock.
                using (FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    MemoryStream memoryStream = new MemoryStream();

                    // Copy the stream.
                    fileStream.CopyTo(memoryStream);

                    // Set the position to 0 afterwards.
                    fileStream.Position = memoryStream.Position = 0;

                    // Set our image as the memory stream value.
                    this.Image = Image.FromStream(memoryStream, true);

                    // Store the stream in the image Tag property so we can dispose of it later.
                    this.Image.Tag = memoryStream;

                    // Set the other properties.
                    this.JpegQuality = DefaultJpegQuality;
                    ImageFormat imageFormat = ImageUtils.GetImageFormat(imageName);
                    this.backupImageFormat = imageFormat;
                    this.originalExtension = Path.GetExtension(this.ImagePath);
                    this.ImageFormat = imageFormat;
                    this.isIndexed = ImageUtils.IsIndexed(this.Image);
                    this.ShouldProcess = true;
                }
            }

            return this;
        }

        /// <summary>
        /// Updates the specified image. Used by the various IProcessors.
        /// </summary>
        /// <param name="image">The image.</param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory Update(Image image)
        {
            if (this.ShouldProcess)
            {
                this.Image = image;
            }

            return this;
        }

        /// <summary>
        /// Resets the current image to its original loaded state.
        /// </summary>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory Reset()
        {
            if (this.ShouldProcess)
            {
                MemoryStream memoryStream = (MemoryStream)this.Image.Tag;

                // Set our new image as the memory stream value.
                Image newImage = Image.FromStream(memoryStream, true);

                // Store the stream in the image Tag property so we can dispose of it later.
                newImage.Tag = memoryStream;

                // Dispose and reassign the image.
                this.Image.Dispose();
                this.Image = newImage;

                // Set the other properties.
                this.JpegQuality = DefaultJpegQuality;
                this.ImageFormat = this.backupImageFormat;
                this.isIndexed = ImageUtils.IsIndexed(this.Image);
            }

            return this;
        }

        #region Manipulation
        /// <summary>
        /// Adds a query-string to the image factory to allow auto-processing of remote files.
        /// </summary>
        /// <param name="query">The query-string parameter to process.</param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory AddQueryString(string query)
        {
            if (this.ShouldProcess)
            {
                this.QueryString = query;
            }

            return this;
        }

        /// <summary>
        /// Changes the opacity of the current image.
        /// </summary>
        /// <param name="percentage">
        /// The percentage by which to alter the images opacity.
        /// Any integer between 0 and 100.
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory Alpha(int percentage)
        {
            if (this.ShouldProcess)
            {
                // Sanitize the input.
                if (percentage > 100 || percentage < 0)
                {
                    percentage = 0;
                }

                Alpha alpha = new Alpha { DynamicParameter = percentage };

                this.Image = alpha.ProcessImage(this);
            }

            return this;
        }

        /// <summary>
        /// Changes the brightness of the current image.
        /// </summary>
        /// <param name="percentage">
        /// The percentage by which to alter the images brightness.
        /// Any integer between -100 and 100.
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory Brightness(int percentage)
        {
            if (this.ShouldProcess)
            {
                // Sanitize the input.
                if (percentage > 100 || percentage < -100)
                {
                    percentage = 0;
                }

                Brightness brightness = new Brightness { DynamicParameter = percentage };

                this.Image = brightness.ProcessImage(this);
            }

            return this;
        }

        /// <summary>
        /// Constrains the current image, resizing it to fit within the given dimensions whilst keeping its aspect ratio.
        /// </summary>
        /// <param name="size">
        /// The <see cref="T:System.Drawing.Size"/> containing the maximum width and height to set the image to.
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory Constrain(Size size)
        {
            if (this.ShouldProcess)
            {
                int width = size.Width;
                int height = size.Height;

                var constrainSettings = new Dictionary<string, string> { { "MaxWidth", width.ToString("G") }, { "MaxHeight", height.ToString("G") } };

                Constrain constrain = new Constrain { DynamicParameter = new Size(width, height), Settings = constrainSettings };

                this.Image = constrain.ProcessImage(this);
            }

            return this;
        }

        /// <summary>
        /// Changes the contrast of the current image.
        /// </summary>
        /// <param name="percentage">
        /// The percentage by which to alter the images contrast.
        /// Any integer between -100 and 100.
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory Contrast(int percentage)
        {
            if (this.ShouldProcess)
            {
                // Sanitize the input.
                if (percentage > 100 || percentage < -100)
                {
                    percentage = 0;
                }

                Contrast contrast = new Contrast { DynamicParameter = percentage };

                this.Image = contrast.ProcessImage(this);
            }

            return this;
        }

        /// <summary>
        /// Crops the current image to the given location and size.
        /// </summary>
        /// <param name="rectangle">
        /// The <see cref="T:System.Drawing.Rectangle"/> containing the coordinates to crop the image to.
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory Crop(Rectangle rectangle)
        {
            if (this.ShouldProcess)
            {
                Crop crop = new Crop { DynamicParameter = rectangle };

                this.Image = crop.ProcessImage(this);
            }

            return this;
        }

        /// <summary>
        /// Applies a filter to the current image.
        /// </summary>
        /// <param name="filterName">
        /// The name of the filter to add to the image.
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory Filter(string filterName)
        {
            if (this.ShouldProcess)
            {
                Filter filter = new Filter { DynamicParameter = filterName };

                this.Image = filter.ProcessImage(this);
            }

            return this;
        }

        /// <summary>
        /// Flips the current image either horizontally or vertically.
        /// </summary>
        /// <param name="flipVertically">
        /// Whether to flip the image vertically.
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory Flip(bool flipVertically)
        {
            if (this.ShouldProcess)
            {
                RotateFlipType rotateFlipType = flipVertically == false
                    ? RotateFlipType.RotateNoneFlipX
                    : RotateFlipType.RotateNoneFlipY;

                Flip flip = new Flip { DynamicParameter = rotateFlipType };

                this.Image = flip.ProcessImage(this);
            }

            return this;
        }

        /// <summary>
        /// Sets the output format of the current image to the matching <see cref="T:System.Drawing.Imaging.ImageFormat"/>.
        /// </summary>
        /// <param name="imageFormat">The <see cref="T:System.Drawing.Imaging.ImageFormat"/>. to set the image to.</param>
        /// <param name="indexedFormat">Whether the pixel format of the image should be indexed. Used for generating Png8 images.</param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "Reviewed. Suppression is OK here.")]
        public ImageFactory Format(ImageFormat imageFormat, bool indexedFormat = false)
        {
            if (this.ShouldProcess)
            {
                this.isIndexed = indexedFormat;
                this.ImageFormat = imageFormat;
            }

            return this;
        }

        /// <summary>
        /// Alters the output quality of the current image.
        /// <remarks>
        /// This method will only effect the output quality of jpeg images
        /// </remarks>
        /// </summary>
        /// <param name="percentage">A value between 1 and 100 to set the quality to.</param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory Quality(int percentage)
        {
            if (this.ShouldProcess)
            {
                this.JpegQuality = percentage;
            }

            return this;
        }

        /// <summary>
        /// Resizes the current image to the given dimensions.
        /// </summary>
        /// <param name="size">
        /// The <see cref="T:System.Drawing.Size"/> containing the width and height to set the image to.
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory Resize(Size size)
        {
            if (this.ShouldProcess)
            {
                int width = size.Width;
                int height = size.Height;

                var resizeSettings = new Dictionary<string, string> { { "MaxWidth", width.ToString("G") }, { "MaxHeight", height.ToString("G") } };

                ResizeLayer resizeLayer = new ResizeLayer(new Size(width, height));

                Resize resize = new Resize { DynamicParameter = resizeLayer, Settings = resizeSettings };

                this.Image = resize.ProcessImage(this);
            }

            return this;
        }

        /// <summary>
        /// Resizes the current image to the given dimensions.
        /// </summary>
        /// <param name="resizeLayer">
        /// The <see cref="ResizeLayer"/> containing the properties required to resize the image.
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory Resize(ResizeLayer resizeLayer)
        {
            if (this.ShouldProcess)
            {
                var resizeSettings = new Dictionary<string, string> { { "MaxWidth", resizeLayer.Size.Width.ToString("G") }, { "MaxHeight", resizeLayer.Size.Height.ToString("G") } };

                Resize resize = new Resize { DynamicParameter = resizeLayer, Settings = resizeSettings };

                this.Image = resize.ProcessImage(this);
            }

            return this;
        }

        /// <summary>
        /// Rotates the current image by the given angle.
        /// </summary>
        /// <param name="rotateLayer">
        /// The <see cref="T:ImageProcessor.Imaging.RotateLayer"/> containing the properties to rotate the image.
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory Rotate(RotateLayer rotateLayer)
        {
            if (this.ShouldProcess)
            {
                // Sanitize the input.
                if (rotateLayer.Angle > 360 || rotateLayer.Angle < 0)
                {
                    rotateLayer.Angle = 0;
                }

                Rotate rotate = new Rotate { DynamicParameter = rotateLayer };

                this.Image = rotate.ProcessImage(this);
            }

            return this;
        }

        /// <summary>
        /// Adds rounded corners to the current image.
        /// </summary>
        /// <param name="roundedCornerLayer">
        /// The <see cref="T:ImageProcessor.Imaging.RoundedCornerLayer"/> containing the properties to round corners on the image.
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory RoundedCorners(RoundedCornerLayer roundedCornerLayer)
        {
            if (this.ShouldProcess)
            {
                if (roundedCornerLayer.Radius < 0)
                {
                    roundedCornerLayer.Radius = 0;
                }

                RoundedCorners roundedCorners = new RoundedCorners { DynamicParameter = roundedCornerLayer };

                this.Image = roundedCorners.ProcessImage(this);
            }

            return this;
        }

        /// <summary>
        /// Changes the saturation of the current image.
        /// </summary>
        /// <param name="percentage">
        /// The percentage by which to alter the images saturation.
        /// Any integer between -100 and 100.
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory Saturation(int percentage)
        {
            if (this.ShouldProcess)
            {
                // Sanitize the input.
                if (percentage > 100 || percentage < -100)
                {
                    percentage = 0;
                }

                Saturation saturate = new Saturation { DynamicParameter = percentage };

                this.Image = saturate.ProcessImage(this);
            }

            return this;
        }

        /// <summary>
        /// Adds a vignette image effect to the current image.
        /// </summary>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory Vignette()
        {
            if (this.ShouldProcess)
            {
                Vignette vignette = new Vignette();

                this.Image = vignette.ProcessImage(this);
            }

            return this;
        }

        /// <summary>
        /// Adds a text based watermark to the current image.
        /// </summary>
        /// <param name="textLayer">
        /// The <see cref="T:ImageProcessor.Imaging.TextLayer"/> containing the properties necessary to add 
        /// the text based watermark to the image.
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory Watermark(TextLayer textLayer)
        {
            if (this.ShouldProcess)
            {
                Watermark watermark = new Watermark { DynamicParameter = textLayer };

                this.Image = watermark.ProcessImage(this);
            }

            return this;
        }
        #endregion

        /// <summary>
        /// Saves the current image to the specified file path.
        /// </summary>
        /// <param name="filePath">The path to save the image to.</param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory Save(string filePath)
        {
            if (this.ShouldProcess)
            {
                // We need to check here if the path has an extension and remove it if so.
                // This is so we can add the correct image format.
                int length = filePath.LastIndexOf(".", StringComparison.Ordinal);
                string extension = ImageUtils.GetExtensionFromImageFormat(this.ImageFormat, this.originalExtension);
                filePath = length == -1 ? filePath + extension : filePath.Substring(0, length) + extension;

                // Fix the colour palette of indexed images.
                this.FixIndexedPallete();

                // ReSharper disable once AssignNullToNotNullAttribute
                DirectoryInfo directoryInfo = new DirectoryInfo(Path.GetDirectoryName(filePath));

                if (this.ImageFormat.Equals(ImageFormat.Jpeg))
                {
                    // Jpegs can be saved with different settings to include a quality setting for the JPEG compression.
                    // This improves output compression and quality. 
                    using (EncoderParameters encoderParameters = ImageUtils.GetEncodingParameters(this.JpegQuality))
                    {
                        ImageCodecInfo imageCodecInfo =
                            ImageCodecInfo.GetImageEncoders()
                                .FirstOrDefault(ici => ici.MimeType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase));

                        if (imageCodecInfo != null)
                        {
                            for (int i = 0; i < 3; i++)
                            {
                                try
                                {
                                    if (!directoryInfo.Exists)
                                    {
                                        directoryInfo.Create();
                                    }

                                    this.Image.Save(filePath, imageCodecInfo, encoderParameters);
                                    break;
                                }
                                catch (IOException)
                                {
                                    Thread.Sleep(200);
                                }
                            }
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < 3; i++)
                    {
                        try
                        {
                            if (!directoryInfo.Exists)
                            {
                                directoryInfo.Create();
                            }

                            this.Image.Save(filePath, this.ImageFormat);
                            break;
                        }
                        catch (IOException)
                        {
                            Thread.Sleep(200);
                        }
                    }
                }
            }

            return this;
        }

        /// <summary>
        /// Saves the current image to the specified output stream.
        /// </summary>
        /// <param name="memoryStream">
        /// The <see cref="T:System.IO.MemoryStream"/> to save the image information to.
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory Save(MemoryStream memoryStream)
        {
            if (this.ShouldProcess)
            {
                // Fix the colour palette of gif images.
                this.FixIndexedPallete();

                if (this.ImageFormat.Equals(ImageFormat.Jpeg))
                {
                    // Jpegs can be saved with different settings to include a quality setting for the JPEG compression.
                    // This improves output compression and quality. 
                    using (EncoderParameters encoderParameters = ImageUtils.GetEncodingParameters(this.JpegQuality))
                    {
                        ImageCodecInfo imageCodecInfo =
                            ImageCodecInfo.GetImageEncoders().FirstOrDefault(
                                ici => ici.MimeType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase));

                        if (imageCodecInfo != null)
                        {
                            this.Image.Save(memoryStream, imageCodecInfo, encoderParameters);
                        }
                    }
                }
                else
                {
                    this.Image.Save(memoryStream, this.ImageFormat);
                }
            }

            return this;
        }

        #region IDisposable Members
        /// <summary>
        /// Disposes the object and frees resources for the Garbage Collector.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);

            // This object will be cleaned up by the Dispose method.
            // Therefore, you should call GC.SupressFinalize to
            // take this object off the finalization queue 
            // and prevent finalization code for this object
            // from executing a second time.
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the object and frees resources for the Garbage Collector.
        /// </summary>
        /// <param name="disposing">If true, the object gets disposed.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (this.isDisposed)
            {
                return;
            }

            if (disposing)
            {
                // Dispose of any managed resources here.
                if (this.Image != null)
                {
                    // Dispose of the memory stream from Load and the image.
                    if (this.Image.Tag != null)
                    {
                        ((IDisposable)this.Image.Tag).Dispose();
                        this.Image.Tag = null;
                    }

                    this.Image.Dispose();
                    this.Image = null;
                }
            }

            // Call the appropriate methods to clean up
            // unmanaged resources here.
            // Note disposing is done.
            this.isDisposed = true;
        }
        #endregion

        /// <summary>
        /// Uses the <see cref="T:ImageProcessor.Imaging.ColorQuantizer"/>
        /// to fix the color palette of gif images.
        /// </summary>
        private void FixIndexedPallete()
        {
            // Fix the colour palette of indexed images.
            if (this.isIndexed)
            {
                this.Image = ColorQuantizer.Quantize(this.Image, PixelFormat.Format8bppIndexed);
            }
        }
        #endregion
    }
}

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
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Linq;

    using ImageProcessor.Core.Common.Exceptions;
    using ImageProcessor.Imaging;
    using ImageProcessor.Imaging.Filters;
    using ImageProcessor.Imaging.Formats;
    using ImageProcessor.Processors;
    #endregion

    /// <summary>
    /// Encapsulates methods for processing image files.
    /// </summary>
    public class ImageFactory : IDisposable
    {
        #region Fields
        /// <summary>
        /// The default quality for image files.
        /// </summary>
        private const int DefaultQuality = 90;

        /// <summary>
        /// The backup supported image format.
        /// </summary>
        private ISupportedImageFormat backupFormat;

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

        #region Constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFactory"/> class.
        /// </summary>
        /// <param name="preserveExifData">
        /// Whether to preserve exif metadata. Defaults to false.
        /// </param>
        public ImageFactory(bool preserveExifData = false)
        {
            this.PreserveExifData = preserveExifData;
            this.ExifPropertyItems = new ConcurrentDictionary<int, PropertyItem>();
        }
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
        /// Gets the supported image format.
        /// </summary>
        public ISupportedImageFormat CurrentImageFormat { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether to preserve exif metadata.
        /// </summary>
        public bool PreserveExifData { get; set; }

        /// <summary>
        /// Gets or sets the exif property items.
        /// </summary>
        public ConcurrentDictionary<int, PropertyItem> ExifPropertyItems { get; set; }

        /// <summary>
        /// Gets or sets the local image for manipulation.
        /// </summary>
        internal Image Image { get; set; }

        /// <summary>
        /// Gets or sets the original extension.
        /// </summary>
        internal string OriginalExtension { get; set; }

        /// <summary>
        /// Gets or sets the memory stream for storing any input stream to prevent disposal.
        /// </summary>
        internal MemoryStream InputStream { get; set; }
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
            ISupportedImageFormat format = FormatUtilities.GetFormat(memoryStream);

            if (format == null)
            {
                throw new ImageFormatException("Input stream is not a supported format.");
            }

            this.backupFormat = format;
            this.CurrentImageFormat = format;

            // Set our image as the memory stream value.
            this.Image = format.Load(memoryStream);

            // Store the stream so we can dispose of it later.
            this.InputStream = memoryStream;

            // Set the other properties.
            format.Quality = DefaultQuality;
            format.IsIndexed = ImageUtils.IsIndexed(this.Image);

            if (this.PreserveExifData)
            {
                foreach (PropertyItem propertyItem in this.Image.PropertyItems)
                {
                    this.ExifPropertyItems[propertyItem.Id] = propertyItem;
                }
            }

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
            // Remove any querystring parameters passed by web requests.
            string[] paths = imagePath.Split('?');
            string path = paths[0];
            string query = string.Empty;

            if (paths.Length > 1)
            {
                query = paths[1];
            }

            if (File.Exists(path))
            {
                this.ImagePath = path;
                this.QueryString = query;

                // Open a file stream to prevent the need for lock.
                using (FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    ISupportedImageFormat format = FormatUtilities.GetFormat(fileStream);

                    if (format == null)
                    {
                        throw new ImageFormatException("Input stream is not a supported format.");
                    }

                    this.backupFormat = format;
                    this.CurrentImageFormat = format;

                    MemoryStream memoryStream = new MemoryStream();

                    // Copy the stream.
                    fileStream.CopyTo(memoryStream);

                    // Set the position to 0 afterwards.
                    fileStream.Position = memoryStream.Position = 0;

                    // Set our image as the memory stream value.
                    this.Image = format.Load(memoryStream);

                    // Store the stream so we can dispose of it later.
                    this.InputStream = memoryStream;

                    // Set the other properties.
                    format.Quality = DefaultQuality;
                    format.IsIndexed = ImageUtils.IsIndexed(this.Image);

                    this.OriginalExtension = Path.GetExtension(this.ImagePath);

                    if (this.PreserveExifData)
                    {
                        foreach (PropertyItem propertyItem in this.Image.PropertyItems)
                        {
                            this.ExifPropertyItems[propertyItem.Id] = propertyItem;
                        }
                    }

                    this.ShouldProcess = true;
                }
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
                // Set our new image as the memory stream value.
                Image newImage = Image.FromStream(this.InputStream, true);

                // Dispose and reassign the image.
                this.Image.Dispose();
                this.Image = newImage;

                // Set the other properties.
                this.CurrentImageFormat = this.backupFormat;
                this.CurrentImageFormat.Quality = DefaultQuality;
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
            // TODO: Remove this.
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
                this.CurrentImageFormat.ApplyProcessor(alpha.ProcessImage, this);
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
                this.CurrentImageFormat.ApplyProcessor(brightness.ProcessImage, this);
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
                ResizeLayer layer = new ResizeLayer(size, Color.Transparent, ResizeMode.Max);

                return this.Resize(layer);
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
                this.CurrentImageFormat.ApplyProcessor(contrast.ProcessImage, this);
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
                CropLayer cropLayer = new CropLayer(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom, CropMode.Pixels);
                return this.Crop(cropLayer);
            }

            return this;
        }

        /// <summary>
        /// Crops the current image to the given location and size.
        /// </summary>
        /// <param name="cropLayer">
        /// The <see cref="T:CropLayer"/> containing the coordinates and mode to crop the image with.
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory Crop(CropLayer cropLayer)
        {
            if (this.ShouldProcess)
            {
                Crop crop = new Crop { DynamicParameter = cropLayer };
                this.CurrentImageFormat.ApplyProcessor(crop.ProcessImage, this);
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
        [Obsolete("Will be removed in next major version. Filter(IMatrixFilter matrixFilter) instead.")]
        public ImageFactory Filter(string filterName)
        {
            if (this.ShouldProcess)
            {
                Filter filter = new Filter { DynamicParameter = filterName };
                this.CurrentImageFormat.ApplyProcessor(filter.ProcessImage, this);
            }

            return this;
        }

        /// <summary>
        /// Applies a filter to the current image. Use the <see cref="MatrixFilters"/> class to 
        /// assign the correct filter.
        /// </summary>
        /// <param name="matrixFilter">
        /// The <see cref="IMatrixFilter"/> of the filter to add to the image.
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory Filter(IMatrixFilter matrixFilter)
        {
            if (this.ShouldProcess)
            {
                Filter filter = new Filter { DynamicParameter = matrixFilter };
                this.CurrentImageFormat.ApplyProcessor(filter.ProcessImage, this);
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
                this.CurrentImageFormat.ApplyProcessor(flip.ProcessImage, this);
            }

            return this;
        }

        /// <summary>
        /// Sets the output format of the current image to the matching <see cref="T:System.Drawing.Imaging.ImageFormat"/>.
        /// </summary>
        /// <param name="format">The <see cref="ISupportedImageFormat"/>. to set the image to.</param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory Format(ISupportedImageFormat format)
        {
            if (this.ShouldProcess)
            {
                this.CurrentImageFormat = format;
            }

            return this;
        }

        /// <summary>
        /// Uses a Gaussian kernel to blur the current image.
        /// <remarks>
        /// <para>
        /// The sigma and threshold values applied to the kernel are 
        /// 1.4 and 0 respectively.
        /// </para>
        /// </remarks>
        /// </summary>
        /// <param name="size">
        /// The size to set the Gaussian kernel to.
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory GaussianBlur(int size)
        {
            if (this.ShouldProcess && size > 0)
            {
                GaussianLayer layer = new GaussianLayer(size);
                return this.GaussianBlur(layer);
            }

            return this;
        }

        /// <summary>
        /// Uses a Gaussian kernel to blur the current image.
        /// </summary>
        /// <param name="gaussianLayer">
        /// The <see cref="T:ImageProcessor.Imaging.GaussianLayer"/> for applying sharpening and 
        /// blurring methods to an image.
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory GaussianBlur(GaussianLayer gaussianLayer)
        {
            if (this.ShouldProcess)
            {
                GaussianBlur gaussianBlur = new GaussianBlur { DynamicParameter = gaussianLayer };
                this.CurrentImageFormat.ApplyProcessor(gaussianBlur.ProcessImage, this);
            }

            return this;
        }

        /// <summary>
        /// Uses a Gaussian kernel to sharpen the current image.
        /// <remarks>
        /// <para>
        /// The sigma and threshold values applied to the kernel are 
        /// 1.4 and 0 respectively.
        /// </para>
        /// </remarks>
        /// </summary>
        /// <param name="size">
        /// The size to set the Gaussian kernel to.
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory GaussianSharpen(int size)
        {
            if (this.ShouldProcess && size > 0)
            {
                GaussianLayer layer = new GaussianLayer(size);
                return this.GaussianSharpen(layer);
            }

            return this;
        }

        /// <summary>
        /// Uses a Gaussian kernel to sharpen the current image.
        /// </summary>
        /// <param name="gaussianLayer">
        /// The <see cref="T:ImageProcessor.Imaging.GaussianLayer"/> for applying sharpening and 
        /// blurring methods to an image.
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory GaussianSharpen(GaussianLayer gaussianLayer)
        {
            if (this.ShouldProcess)
            {
                GaussianSharpen gaussianSharpen = new GaussianSharpen { DynamicParameter = gaussianLayer };
                this.CurrentImageFormat.ApplyProcessor(gaussianSharpen.ProcessImage, this);
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
                this.CurrentImageFormat.Quality = percentage;
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

                ResizeLayer resizeLayer = new ResizeLayer(new Size(width, height));
                return this.Resize(resizeLayer);
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
                this.CurrentImageFormat.ApplyProcessor(resize.ProcessImage, this);
            }

            return this;
        }

        /// <summary>
        /// Rotates the current image by the given angle.
        /// </summary>
        /// <param name="degrees">
        /// The angle at which to rotate the image in degrees.
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory Rotate(int degrees)
        {
            if (this.ShouldProcess)
            {
                // Sanitize the input.
                if (degrees > 360 || degrees < 0)
                {
                    degrees = 0;
                }

                Rotate rotate = new Rotate { DynamicParameter = degrees };
                this.CurrentImageFormat.ApplyProcessor(rotate.ProcessImage, this);
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
                this.CurrentImageFormat.ApplyProcessor(roundedCorners.ProcessImage, this);
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
                this.CurrentImageFormat.ApplyProcessor(saturate.ProcessImage, this);
            }

            return this;
        }

        /// <summary>
        /// Tints the current image with the given color.
        /// </summary>
        /// <param name="color">
        /// The <see cref="T:System.Drawing.Color"/> to tint the image with.
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory Tint(Color color)
        {
            if (this.ShouldProcess)
            {
                Tint tint = new Tint { DynamicParameter = color };
                this.CurrentImageFormat.ApplyProcessor(tint.ProcessImage, this);
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
                this.CurrentImageFormat.ApplyProcessor(vignette.ProcessImage, this);
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
                this.CurrentImageFormat.ApplyProcessor(watermark.ProcessImage, this);
            }

            return this;
        }
        #endregion

        /// <summary>
        /// Saves the current image to the specified file path. If the extension does not 
        /// match the correct extension for the current format it will be replaced by the 
        /// correct default value.
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
                string extension = Path.GetExtension(filePath).TrimStart('.');
                string fallback = this.CurrentImageFormat.DefaultExtension;

                if (extension != fallback && !this.CurrentImageFormat.FileExtensions.Contains(extension))
                {
                    filePath = length == -1
                        ? string.Format("{0}.{1}", filePath, fallback)
                        : filePath.Substring(0, length + 1) + fallback;
                }

                // ReSharper disable once AssignNullToNotNullAttribute
                DirectoryInfo directoryInfo = new DirectoryInfo(Path.GetDirectoryName(filePath));
                if (!directoryInfo.Exists)
                {
                    directoryInfo.Create();
                }

                this.Image = this.CurrentImageFormat.Save(filePath, this.Image);
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
                this.Image = this.CurrentImageFormat.Save(memoryStream, this.Image);
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
                    if (this.InputStream != null)
                    {
                        this.InputStream.Dispose();
                        this.InputStream = null;
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
        #endregion
    }
}

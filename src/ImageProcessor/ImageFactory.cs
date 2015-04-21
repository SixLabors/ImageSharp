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

    using ImageProcessor.Common.Exceptions;
    using ImageProcessor.Imaging;
    using ImageProcessor.Imaging.Filters.EdgeDetection;
    //using ImageProcessor.Imaging.Filters.ObjectDetection;
    using ImageProcessor.Imaging.Filters.Photo;
    using ImageProcessor.Imaging.Formats;
    using ImageProcessor.Imaging.Helpers;
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
            : this(preserveExifData, true)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFactory"/> class.
        /// </summary>
        /// <param name="preserveExifData">
        /// Whether to preserve exif metadata. Defaults to false.
        /// </param>
        /// <param name="fixGamma">
        /// Whether to fix the gamma component of the image. Defaults to true.
        /// </param>
        public ImageFactory(bool preserveExifData, bool fixGamma)
        {
            this.PreserveExifData = preserveExifData;
            this.ExifPropertyItems = new ConcurrentDictionary<int, PropertyItem>();
            this.FixGamma = fixGamma;
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
        /// Gets or sets a value indicating whether to fix the gamma component of the current image.
        /// </summary>
        public bool FixGamma { get; set; }

        /// <summary>
        /// Gets or the current gamma value.
        /// </summary>
        public float CurrentGamma { get; private set; }

        /// <summary>
        /// Gets or sets the exif property items.
        /// </summary>
        public ConcurrentDictionary<int, PropertyItem> ExifPropertyItems { get; set; }

        /// <summary>
        /// Gets or the local image for manipulation.
        /// </summary>
        public Image Image { get; internal set; }

        /// <summary>
        /// Gets or sets the stream for storing any input stream to prevent disposal.
        /// </summary>
        internal Stream InputStream { get; set; }
        #endregion

        #region Methods
        /// <summary>
        /// Loads the image to process. Always call this method first.
        /// </summary>
        /// <param name="stream">
        /// The <see cref="T:System.IO.Stream"/> containing the image information.
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory Load(Stream stream)
        {
            // Reset the position of the stream to ensure we're reading the correct part.
            stream.Position = 0;

            ISupportedImageFormat format = FormatUtilities.GetFormat(stream);

            if (format == null)
            {
                throw new ImageFormatException("Input stream is not a supported format.");
            }

            MemoryStream memoryStream = new MemoryStream();

            // Copy the stream. Disposal of the input stream is the responsibility  
            // of the user.
            stream.CopyTo(memoryStream);

            // Set the position to 0 afterwards.
            stream.Position = memoryStream.Position = 0;

            // Set our image as the memory stream value.
            this.Image = format.Load(memoryStream);

            // Store the stream so we can dispose of it later.
            this.InputStream = memoryStream;

            // Set the other properties.
            format.Quality = DefaultQuality;
            format.IsIndexed = FormatUtilities.IsIndexed(this.Image);

            IQuantizableImageFormat imageFormat = format as IQuantizableImageFormat;
            if (imageFormat != null)
            {
                imageFormat.ColorCount = FormatUtilities.GetColorCount(this.Image);
            }

            this.backupFormat = format;
            this.CurrentImageFormat = format;

            // Always load the data.
            // TODO. Some custom data doesn't seem to get copied by default methods.
            foreach (int id in this.Image.PropertyIdList)
            {
                this.ExifPropertyItems[id] = this.Image.GetPropertyItem(id);
            }

            this.ShouldProcess = true;

            // Normalize the gamma component of the image.
            if (this.FixGamma)
            {
                this.Gamma(2.2F);
            }

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
            FileInfo fileInfo = new FileInfo(imagePath);
            if (fileInfo.Exists)
            {
                this.ImagePath = imagePath;

                // Open a file stream to prevent the need for lock.
                using (FileStream fileStream = new FileStream(imagePath, FileMode.Open, FileAccess.Read))
                {
                    ISupportedImageFormat format = FormatUtilities.GetFormat(fileStream);

                    if (format == null)
                    {
                        throw new ImageFormatException("Input stream is not a supported format.");
                    }

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
                    format.IsIndexed = FormatUtilities.IsIndexed(this.Image);

                    IQuantizableImageFormat imageFormat = format as IQuantizableImageFormat;
                    if (imageFormat != null)
                    {
                        imageFormat.ColorCount = FormatUtilities.GetColorCount(this.Image);
                    }

                    this.backupFormat = format;
                    this.CurrentImageFormat = format;

                    // Always load the data.
                    foreach (PropertyItem propertyItem in this.Image.PropertyItems)
                    {
                        this.ExifPropertyItems[propertyItem.Id] = propertyItem;
                    }

                    this.ShouldProcess = true;

                    // Normalize the gamma component of the image.
                    if (this.FixGamma)
                    {
                        this.Gamma(2.2F);
                    }
                }
            }
            else
            {
                throw new FileNotFoundException(imagePath);
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
                this.InputStream.Position = 0;

#if !__MonoCS__
                Image newImage = Image.FromStream(this.InputStream, true);
#else
                Image newImage = Image.FromStream(this.InputStream);
#endif

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
                percentage = ImageMaths.Clamp(percentage, 0, 100);

                Alpha alpha = new Alpha { DynamicParameter = percentage };
                this.CurrentImageFormat.ApplyProcessor(alpha.ProcessImage, this);
            }

            return this;
        }

        /// <summary>
        /// Performs auto-rotation to ensure that EXIF defined rotation is reflected in 
        /// the final image.
        /// </summary>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory AutoRotate()
        {
            if (this.ShouldProcess)
            {
                AutoRotate autoRotate = new AutoRotate();
                this.CurrentImageFormat.ApplyProcessor(autoRotate.ProcessImage, this);
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
        /// Changes the background color of the current image.
        /// </summary>
        /// <param name="color">
        /// The <see cref="T:System.Drawing.Color"/> to paint the image with.
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory BackgroundColor(Color color)
        {
            if (this.ShouldProcess)
            {
                BackgroundColor backgroundColor = new BackgroundColor { DynamicParameter = color };
                this.CurrentImageFormat.ApplyProcessor(backgroundColor.ProcessImage, this);
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
                ResizeLayer layer = new ResizeLayer(size, ResizeMode.Max);

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
                CropLayer cropLayer = new CropLayer(rectangle.Left, rectangle.Top, rectangle.Width, rectangle.Height, CropMode.Pixels);
                return this.Crop(cropLayer);
            }

            return this;
        }

        /// <summary>
        /// Crops the current image to the given location and size.
        /// </summary>
        /// <param name="cropLayer">
        /// The <see cref="Imaging.CropLayer"/> containing the coordinates and mode to crop the image with.
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
        /// Detects the edges in the current image.
        /// </summary>
        /// <param name="filter">
        /// The <see cref="IEdgeFilter"/> to detect edges with.
        /// </param>
        /// <param name="greyscale">
        /// Whether to convert the image to greyscale first - Defaults to true.
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory DetectEdges(IEdgeFilter filter, bool greyscale = true)
        {
            if (this.ShouldProcess)
            {
                DetectEdges detectEdges = new DetectEdges { DynamicParameter = new Tuple<IEdgeFilter, bool>(filter, greyscale) };
                this.CurrentImageFormat.ApplyProcessor(detectEdges.ProcessImage, this);
            }

            return this;
        }

        //public ImageFactory DetectObjects(HaarCascade cascade, bool drawRectangles = true, Color color = default(Color))
        //{
        //    if (this.ShouldProcess)
        //    {
        //        DetectObjects detectObjects = new DetectObjects { DynamicParameter = cascade };
        //        this.CurrentImageFormat.ApplyProcessor(detectObjects.ProcessImage, this);
        //    }

        //    return this;
        //}

        /// <summary>
        /// Crops an image to the area of greatest entropy.
        /// </summary>
        /// <param name="threshold">
        /// The threshold in bytes to control the entropy.
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory EntropyCrop(byte threshold = 128)
        {
            if (this.ShouldProcess)
            {
                EntropyCrop autoCrop = new EntropyCrop { DynamicParameter = threshold };
                this.CurrentImageFormat.ApplyProcessor(autoCrop.ProcessImage, this);
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
        /// <param name="flipBoth">
        /// Whether to flip the image both vertically and horizontally.
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory Flip(bool flipVertically = false, bool flipBoth = false)
        {
            if (this.ShouldProcess)
            {
                RotateFlipType rotateFlipType;
                if (flipBoth)
                {
                    rotateFlipType = RotateFlipType.RotateNoneFlipXY;
                }
                else
                {
                    rotateFlipType = flipVertically
                        ? RotateFlipType.RotateNoneFlipY
                        : RotateFlipType.RotateNoneFlipX;
                }

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
        /// Adjust the gamma (intensity of the light) component of the given image.
        /// </summary>
        /// <param name="value">
        /// The value to adjust the gamma by (typically between .2 and 5).
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory Gamma(float value)
        {
            if (this.ShouldProcess)
            {
                // Sanitize the input.
                if (value > 5 || value < .1)
                {
                    value = 2.2F;
                }

                this.CurrentGamma = value;
                Gamma gamma = new Gamma { DynamicParameter = value };
                this.CurrentImageFormat.ApplyProcessor(gamma.ProcessImage, this);
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
        /// Alters the hue of the current image changing the overall color.
        /// </summary>
        /// <param name="degrees">
        /// The angle by which to alter the images hue.
        /// Any integer between 0 and 360.
        /// </param>
        /// <param name="rotate">
        /// Whether to rotate the hue of the current image altering each color
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory Hue(int degrees, bool rotate = false)
        {
            // Sanitize the input.
            if (degrees > 360 || degrees < 0)
            {
                degrees = 0;
            }

            if (this.ShouldProcess && degrees > 0)
            {
                Hue hue = new Hue { DynamicParameter = new Tuple<int, bool>(degrees, rotate) };
                this.CurrentImageFormat.ApplyProcessor(hue.ProcessImage, this);
            }

            return this;
        }

        /// <summary>
        /// Converts the current image to a CMYK halftone representation of that image.
        /// </summary>
        /// <param name="comicMode">
        /// Whether to trace over the current image and add borders to add a comic book effect.
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory Halftone(bool comicMode = false)
        {
            if (this.ShouldProcess)
            {
                Halftone halftone = new Halftone { DynamicParameter = comicMode };
                this.CurrentImageFormat.ApplyProcessor(halftone.ProcessImage, this);
            }

            return this;
        }

        /// <summary>
        /// Applies the given image mask to the current image.
        /// </summary>
        /// <param name="imageMask">
        /// The image containing the mask to apply.
        /// </param>
        /// <param name="point">
        /// The <see cref="Point"/> to place the mask if it not the same dimensions as the original image.
        /// If no position is set, the mask will be centered within the image.
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory Mask(Image imageMask, Point? point = null)
        {
            if (this.ShouldProcess)
            {
                Mask mask = new Mask { DynamicParameter = new Tuple<Image, Point?>(imageMask, point) };
                this.CurrentImageFormat.ApplyProcessor(mask.ProcessImage, this);
            }

            return this;
        }

        /// <summary>
        /// Adds a image overlay to the current image.
        /// </summary>
        /// <param name="imageLayer">
        /// The <see cref="T:ImageProcessor.Imaging.ImageLayer"/> containing the properties necessary to add 
        /// the image overlay to the image.
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory Overlay(ImageLayer imageLayer)
        {
            if (this.ShouldProcess)
            {
                Overlay watermark = new Overlay { DynamicParameter = imageLayer };
                this.CurrentImageFormat.ApplyProcessor(watermark.ProcessImage, this);
            }

            return this;
        }

        /// <summary>
        /// Pixelates an image with the given size.
        /// </summary>
        /// <param name="pixelSize">
        /// The size of the pixels to create.</param>
        /// <param name="rectangle">
        /// The area in which to pixelate the image. If not set, the whole image is pixelated.
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory Pixelate(int pixelSize, Rectangle? rectangle = null)
        {
            if (this.ShouldProcess && pixelSize > 0)
            {
                Pixelate pixelate = new Pixelate { DynamicParameter = new Tuple<int, Rectangle?>(pixelSize, rectangle) };
                this.CurrentImageFormat.ApplyProcessor(pixelate.ProcessImage, this);
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
            if (percentage <= 100 && percentage >= 0 && this.ShouldProcess)
            {
                this.CurrentImageFormat.Quality = percentage;
            }

            return this;
        }

        /// <summary>
        /// Replaces a color within the current image.
        /// </summary>
        /// <param name="target">
        /// The target <see cref="System.Drawing.Color"/>.
        /// </param>
        /// <param name="replacement">
        /// The replacement <see cref="System.Drawing.Color"/>.
        /// </param>
        /// <param name="fuzziness">
        /// A value between 0 and 128 with which to alter the target detection accuracy.
        /// </param>
        /// <returns>
        /// The <see cref="ImageFactory"/>.
        /// </returns>
        public ImageFactory ReplaceColor(Color target, Color replacement, int fuzziness = 0)
        {
            // Sanitize the input.
            if (fuzziness < 0 || fuzziness > 128)
            {
                fuzziness = 0;
            }

            if (this.ShouldProcess && target != Color.Empty && replacement != Color.Empty)
            {
                ReplaceColor replaceColor = new ReplaceColor
                {
                    DynamicParameter = new Tuple<Color, Color, int>(target, replacement, fuzziness)
                };
                this.CurrentImageFormat.ApplyProcessor(replaceColor.ProcessImage, this);
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
                Dictionary<string, string> resizeSettings = new Dictionary<string, string>
                {
                    { "MaxWidth", resizeLayer.Size.Width.ToString("G") },
                    { "MaxHeight", resizeLayer.Size.Height.ToString("G") }
                };

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
        public ImageFactory Rotate(float degrees)
        {
            if (this.ShouldProcess)
            {
                Rotate rotate = new Rotate { DynamicParameter = degrees };
                this.CurrentImageFormat.ApplyProcessor(rotate.ProcessImage, this);
            }

            return this;
        }

        /// <summary>
        /// Rotates the image without expanding the canvas to fit the image.
        /// </summary>
        /// <param name="degrees">
        /// The angle at which to rotate the image in degrees.
        /// </param>
        /// <param name="keepSize">
        /// Whether to keep the original image dimensions.
        /// <para>
        /// If set to true, the image is zoomed to fit the bounding area.
        /// </para>
        /// <para>
        /// If set to false, the area is cropped to fit the rotated image.
        /// </para>
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory" /> class.
        /// </returns>
        public ImageFactory RotateBounded(float degrees, bool keepSize = false)
        {
            if (this.ShouldProcess)
            {
                RotateBounded rotate = new RotateBounded { DynamicParameter = new Tuple<float, bool>(degrees, keepSize) };
                this.CurrentImageFormat.ApplyProcessor(rotate.ProcessImage, this);
            }

            return this;
        }

        /// <summary>
        /// Adds rounded corners to the current image.
        /// </summary>
        /// <param name="radius">
        /// The radius at which the corner will be rounded.
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory RoundedCorners(int radius)
        {
            if (this.ShouldProcess)
            {
                if (radius < 0)
                {
                    radius = 0;
                }

                RoundedCornerLayer roundedCornerLayer = new RoundedCornerLayer(radius);

                RoundedCorners roundedCorners = new RoundedCorners { DynamicParameter = roundedCornerLayer };
                this.CurrentImageFormat.ApplyProcessor(roundedCorners.ProcessImage, this);
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
        /// <param name="color">
        /// The <see cref="T:System.Drawing.Color"/> to tint the image with. Defaults to black.
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory Vignette(Color? color = null)
        {
            if (this.ShouldProcess)
            {
                Vignette vignette = new Vignette
                {
                    DynamicParameter = color.HasValue && !color.Equals(Color.Transparent)
                                        ? color.Value
                                        : Color.Black
                };

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
                // ReSharper disable once AssignNullToNotNullAttribute
                DirectoryInfo directoryInfo = new DirectoryInfo(Path.GetDirectoryName(filePath));
                if (!directoryInfo.Exists)
                {
                    directoryInfo.Create();
                }

                // Normalize the gamma component of the image.
                if (this.FixGamma)
                {
                    this.Gamma(1 / 2.2F);
                }

                this.Image = this.CurrentImageFormat.Save(filePath, this.Image);
            }

            return this;
        }

        /// <summary>
        /// Saves the current image to the specified output stream.
        /// </summary>
        /// <param name="stream">
        /// The <see cref="T:System.IO.MemoryStream"/> to save the image information to.
        /// </param>
        /// <returns>
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public ImageFactory Save(Stream stream)
        {
            if (this.ShouldProcess)
            {
                // Allow the same stream to be used as for input.
                stream.SetLength(0);

                // Normalize the gamma component of the image.
                if (this.FixGamma)
                {
                    this.Gamma(1 / 2.2F);
                }

                this.Image = this.CurrentImageFormat.Save(stream, this.Image);
                stream.Position = 0;
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
            // Therefore, you should call GC.SuppressFinalize to
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

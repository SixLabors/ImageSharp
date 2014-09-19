namespace ImageProcessor.Processors
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using ImageProcessor.Common.Exceptions;
    using ImageProcessor.Imaging;

    /// <summary>
    /// Encapsulates methods allowing the replacement of a color within an image.
    /// <see href="http://softwarebydefault.com/2013/03/16/bitmap-color-substitution/"/>
    /// </summary>
    public class ReplaceColor : IGraphicsProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ReplaceColor"/> class.
        /// </summary>
        public ReplaceColor()
        {
            this.Settings = new Dictionary<string, string>();
        }

        /// <summary>
        /// Gets or sets the dynamic parameter.
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
            Bitmap newImage = null;
            Image image = factory.Image;

            try
            {
                Tuple<Color, Color, int> parameters = this.DynamicParameter;
                Color original = parameters.Item1;
                Color replacement = parameters.Item2;
                int threshold = parameters.Item3;

                newImage = new Bitmap(image);

                using (FastBitmap fastBitmap = new FastBitmap(newImage))
                {
                    

                }

                image.Dispose();
                image = newImage;

            }
            catch (Exception ex)
            {
                if (newImage != null)
                {
                    newImage.Dispose();
                }

                throw new ImageProcessingException("Error processing image with " + this.GetType().Name, ex);
            }

            return image;
        }
    }
}

// -----------------------------------------------------------------------
// <copyright file="Class1.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using ImageProcessor.Imaging;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public class Png8 : IGraphicsProcessor
    {
        #region Implementation of IGraphicsProcessor

        /// <summary>
        /// Gets the name.
        /// </summary>
        public string Name
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Gets the description.
        /// </summary>
        public string Description
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Gets the regular expression to search strings for.
        /// </summary>
        public Regex RegexPattern
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Gets DynamicParameter.
        /// </summary>
        public dynamic DynamicParameter
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Gets the order in which this processor is to be used in a chain.
        /// </summary>
        public int SortOrder
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Gets or sets any additional settings required by the processor.
        /// </summary>
        public Dictionary<string, string> Settings
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// The position in the original string where the first character of the captured substring was found.
        /// </summary>
        /// <param name="queryString">
        /// The query string to search.
        /// </param>
        /// <returns>
        /// The zero-based starting position in the original string where the captured substring was found.
        /// </returns>
        public int MatchRegexIndex(string queryString)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Processes the image.
        /// </summary>
        /// <param name="factory">
        /// The the current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class containing
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
                newImage = new Bitmap(image) { Tag = image.Tag };
                using (Graphics graphics = Graphics.FromImage(newImage))
                {
                    ArrayList pallete = new ArrayList();
                    PaletteQuantizer paletteQuantizer = new PaletteQuantizer(new ArrayList(newImage.Palette.Entries));
                    newImage = paletteQuantizer.Quantize(newImage);
                }

            }
            catch
            {
                if (newImage != null)
                {
                    newImage.Dispose();
                }
            }

            return image;
        }

        #endregion
    }
}

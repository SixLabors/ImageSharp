// -----------------------------------------------------------------------
// <copyright file="Filter.cs" company="James South">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    #region Using
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.Text.RegularExpressions;
    #endregion

    /// <summary>
    /// Encapsulates methods with which to add filters to an image.
    /// </summary>
    public class Filter : IGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"filter=(lomograph|polaroid|blackwhite|sepia|greyscale)", RegexOptions.Compiled);

        #region IGraphicsProcessor Members
        /// <summary>
        /// Gets the name.
        /// </summary>
        public string Name
        {
            get
            {
                return "Filter";
            }
        }

        /// <summary>
        /// Gets the description.
        /// </summary>
        public string Description
        {
            get
            {
                return "Encapsulates methods with which to add filters to an image. e.g polaroid, lomograph";
            }
        }

        /// <summary>
        /// Gets the regular expression to search strings for.
        /// </summary>
        public Regex RegexPattern
        {
            get
            {
                return QueryRegex;
            }
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
        /// Gets the order in which this processor is to be used in a chain.
        /// </summary>
        public int SortOrder
        {
            get;
            private set;
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
            int index = 0;

            // Set the sort order to max to allow filtering.
            this.SortOrder = int.MaxValue;

            foreach (Match match in this.RegexPattern.Matches(queryString))
            {
                if (match.Success)
                {
                    if (index == 0)
                    {
                        // Set the index on the first instance only.
                        this.SortOrder = match.Index;
                        this.DynamicParameter = match.Value.Split('=')[1];
                    }

                    index += 1;
                }
            }

            return this.SortOrder;
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
                newImage = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppPArgb) { Tag = image.Tag };

                ColorMatrix colorMatrix = null;

                switch ((string)this.DynamicParameter)
                {
                    case "polaroid":
                        colorMatrix = ColorMatrixes.Poloroid;
                        break;
                    case "lomograph":
                        colorMatrix = ColorMatrixes.Lomograph;
                        break;
                    case "sepia":
                        colorMatrix = ColorMatrixes.Sepia;
                        break;
                    case "blackwhite":
                        colorMatrix = ColorMatrixes.BlackWhite;
                        break;
                    case "greyscale":
                        colorMatrix = ColorMatrixes.GreyScale;
                        break;
                }

                using (Graphics graphics = Graphics.FromImage(newImage))
                {
                    using (ImageAttributes attributes = new ImageAttributes())
                    {
                        if (colorMatrix != null)
                        {
                            attributes.SetColorMatrix(colorMatrix);
                        }

                        Rectangle rectangle = new Rectangle(0, 0, image.Width, image.Height);

                        graphics.DrawImage(image, rectangle, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
                    }
                }

                // Reassign the image.
                image.Dispose();
                image = newImage;
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

        /// <summary>
        /// A list of available color matrices to apply to an image.
        /// </summary>
        private static class ColorMatrixes
        {
            /// <summary>
            /// Gets Sepia.
            /// </summary>
            internal static ColorMatrix Sepia
            {
                get
                {
                    return new ColorMatrix(
                        new float[][]
                            {
                                new float[] { .393f, .349f, .272f, 0, 0 }, 
                                new float[] { .769f, .686f, .534f, 0, 0 },
                                new float[] { .189f, .168f, .131f, 0, 0 },
                                new float[] { 0, 0, 0, 1, 0 },
                                new float[] { 0, 0, 0, 0, 1 }
                          });
                }
            }

            /// <summary>
            /// Gets BlackWhite.
            /// </summary>
            internal static ColorMatrix BlackWhite
            {
                get
                {
                    return new ColorMatrix(
                        new float[][]
                            {
                                new float[] { 1.5f, 1.5f, 1.5f, 0, 0 }, 
                                new float[] { 1.5f, 1.5f, 1.5f, 0, 0 },
                                new float[] { 1.5f, 1.5f, 1.5f, 0, 0 },
                                new float[] { 0, 0, 0, 1, 0 },
                                new float[] { -1, -1, -1, 0, 1 }
                          });
                }
            }

            /// <summary>
            /// Gets Poloroid.
            /// </summary>
            internal static ColorMatrix Poloroid
            {
                get
                {
                    return new ColorMatrix(
                        new float[][]
                            {
                                new float[] { 1.438f, -0.062f, -0.062f, 0, 0 },
                                new float[] { -0.122f, 1.378f, -0.122f, 0, 0 },
                                new float[] { -0.016f, -0.016f, 1.483f, 0, 0 },
                                new float[] { 0, 0, 0, 1, 0 },
                                new float[] { -0.03f, 0.05f, -0.02f, 0, 1 }
                          });
                }
            }

            /// <summary>
            /// Gets Lomograph.
            /// </summary>
            internal static ColorMatrix Lomograph
            {
                get
                {
                    return new ColorMatrix(
                        new float[][]
                            {
                                new float[] { 1.25f, 0, 0, 0, 0 }, 
                                new float[] { 0, 1.25f, 0, 0, 0 },
                                new float[] { 0, 0, 0.94f, 0, 0 }, 
                                new float[] { 0, 0, 0, 1, 0 },
                                new float[] { 0, 0, 0, 0, 1 }
                            });
                }
            }

            /// <summary>
            /// Gets GreyScale.
            /// </summary>
            internal static ColorMatrix GreyScale
            {
                get
                {
                    return new ColorMatrix(
                        new float[][]
                            {
                                new float[] { .33f, .33f, .33f, 0, 0 }, 
                                new float[] { .59f, .59f, .59f, 0, 0 },
                                new float[] { .11f, .11f, .11f, 0, 0 }, 
                                new float[] { 0, 0, 0, 1, 0 },
                                new float[] { 0, 0, 0, 0, 1 }
                            });
                }
            }
        }
    }
}

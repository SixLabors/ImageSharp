// -----------------------------------------------------------------------
// <copyright file="Watermark.cs" company="James South">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    #region Using
    using System.Collections.Generic;
    using System.Drawing;
    using System.Text.RegularExpressions;
    using ImageProcessor.Helpers.Extensions;
    using ImageProcessor.Imaging;
    #endregion

    /// <summary>
    /// Encapsulates methods to change the alpha component of the image to effect its transparency.
    /// </summary>
    public class Watermark : IGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// http://stackoverflow.com/questions/11263949/optimize-a-variable-regex
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"watermark=(text-\w+\|position-\d+-\d+(\|color-([0-9a-fA-F]{3}){1,2}(\|size-\d+(\|style-(bold|italic|regular|strikeout|underline))?)?)?|\w+)", RegexOptions.Compiled);

        #region IGraphicsProcessor Members
        /// <summary>
        /// Gets the name.
        /// </summary>
        public string Name
        {
            get
            {
                return "Watermark";
            }
        }

        /// <summary>
        /// Gets the description.
        /// </summary>
        public string Description
        {
            get
            {
                return "Adds a watermark containing text to the image.";
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

                        TextLayer textLayer = new TextLayer();

                        // Split the string up.
                        string[] firstPass = match.Value.Split('=')[1].Split('|');

                        switch (firstPass.Length)
                        {
                            case 1:
                                textLayer.Text = firstPass[0];
                                break;
                            case 2:
                                textLayer.Text = firstPass[0];
                                textLayer.Position = this.GetPosition(firstPass[1]);
                                break;

                            case 3:
                                textLayer.Text = firstPass[0];
                                textLayer.Position = this.GetPosition(firstPass[1]);
                                textLayer.TextColor = this.GetColor(firstPass[2]);
                                break;

                            case 4:
                                textLayer.Text = firstPass[0];
                                textLayer.Position = this.GetPosition(firstPass[1]);
                                textLayer.TextColor = this.GetColor(firstPass[2]);
                                textLayer.FontSize = this.GetFontSize(firstPass[3]);
                                break;

                            case 5:
                                textLayer.Text = firstPass[0];
                                textLayer.Position = this.GetPosition(firstPass[1]);
                                textLayer.TextColor = this.GetColor(firstPass[2]);
                                textLayer.FontSize = this.GetFontSize(firstPass[3]);
                                textLayer.Style = this.GetFontStyle(firstPass[4]);
                                break;
                        }

                        this.DynamicParameter = textLayer;
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
                newImage = new Bitmap(image) { Tag = image.Tag };

                TextLayer textLayer = this.DynamicParameter;
                string text = textLayer.Text;
                int opacity = textLayer.Opacity;
                int fontSize = textLayer.FontSize;
                FontStyle fontStyle = textLayer.Style;

                using (Graphics graphics = Graphics.FromImage(newImage))
                {
                    using (Font font = this.GetFont(textLayer.Font, fontSize, fontStyle))
                    {
                        using (StringFormat drawFormat = new StringFormat())
                        {
                            using (Brush brush = new SolidBrush(Color.FromArgb(opacity, textLayer.TextColor)))
                            {
                                Point origin = textLayer.Position;

                                // We need to ensure that there is a position set for the watermark
                                if (origin == Point.Empty)
                                {
                                    // Work out the size of the text.
                                    SizeF textSize = graphics.MeasureString(text, font, new SizeF(image.Width, image.Height), drawFormat);

                                    int x = (int)(image.Width - textSize.Width) / 2;
                                    int y = (int)(image.Height - textSize.Height) / 2;
                                    origin = new Point(x, y);
                                }

                                graphics.DrawString(text, font, brush, origin, drawFormat);
                            }
                        }
                    }

                    image.Dispose();
                    image = newImage;
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

        #region Private Methods
        /// <summary>
        /// Returns the correct <see cref="T:System.Drawing.Font"/> for the given parameters
        /// </summary>
        /// <param name="font">
        /// The font.
        /// </param>
        /// <param name="fontSize">
        /// The font size.
        /// </param>
        /// <param name="fontStyle">
        /// The font style.
        /// </param>
        /// <returns>
        /// The correct <see cref="T:System.Drawing.Font"/>
        /// </returns>
        private Font GetFont(string font, int fontSize, FontStyle fontStyle)
        {
            FontFamily fontFamily = string.IsNullOrEmpty(font) ? FontFamily.GenericSansSerif : new FontFamily(font);

            return new Font(fontFamily, fontSize, fontStyle, GraphicsUnit.Pixel);
        }

        /// <summary>
        /// Returns the correct <see cref="T:System.Drawing.Point"/> for the given string.
        /// </summary>
        /// <param name="input">
        /// The input string containing the value to parse.
        /// </param>
        /// <returns>
        /// The correct <see cref="T:System.Drawing.Point"/>
        /// </returns>
        private Point GetPosition(string input)
        {
            int[] position = input.ToIntegerArray();
            int x = position[0];
            int y = position[1];

            return new Point(x, y);
        }

        /// <summary>
        /// Returns the correct <see cref="T:System.Drawing.Color"/> for the given string.
        /// </summary>
        /// <param name="input">
        /// The input string containing the value to parse.
        /// </param>
        /// <returns>
        /// The correct <see cref="T:System.Drawing.Color"/>
        /// </returns>
        private Color GetColor(string input)
        {
            // split on color-hex
            return ColorTranslator.FromHtml("#" + input.Split('-')[1]);
        }

        /// <summary>
        /// Returns the correct <see cref="T:System.Int32"/> for the given string.
        /// </summary>
        /// <param name="input">
        /// The input string containing the value to parse.
        /// </param>
        /// <returns>
        /// The correct <see cref="T:System.Int32"/>
        /// </returns>
        private int GetFontSize(string input)
        {
            // split on size-value
            return int.Parse(input.Split('-')[1]);
        }

        /// <summary>
        /// Returns the correct <see cref="T:System.Drawing.FontStyle"/> for the given string.
        /// </summary>
        /// <param name="input">
        /// The string containing the respective fontstyle.
        /// </param>
        /// <returns>
        /// The correct <see cref="T:System.Drawing.FontStyle"/>
        /// </returns>
        private FontStyle GetFontStyle(string input)
        {
            FontStyle fontStyle = FontStyle.Bold;

            switch (input.ToLowerInvariant())
            {
                case "italic":
                    fontStyle = FontStyle.Italic;
                    break;
                case "regular":
                    fontStyle = FontStyle.Regular;
                    break;
                case "strikeout":
                    fontStyle = FontStyle.Strikeout;
                    break;
                case "underline":
                    fontStyle = FontStyle.Underline;
                    break;
            }

            return fontStyle;
        }
        #endregion
    }
}

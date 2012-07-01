// -----------------------------------------------------------------------
// <copyright file="Filter.cs" company="James South">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    #region Using

    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Text.RegularExpressions;
    using System.Web;
    using System.Web.Hosting;

    #endregion

    /// <summary>
    /// Encapsulates methods with which to add filters to an image.
    /// </summary>
    public class Filter : IGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"filter=(lomograph|polaroid|blackwhite|sepia|greyscale|gotham|invert|hisatch|losatch|comic)", RegexOptions.Compiled);

        /// <summary>
        /// Enumurates Argb colour channels.
        /// </summary>
        private enum ChannelArgb
        {
            /// <summary>
            /// The blue channel
            /// </summary>
            Blue = 0,

            /// <summary>
            /// The green channel
            /// </summary>
            Green = 1,

            /// <summary>
            /// The red channel
            /// </summary>
            Red = 2,

            /// <summary>
            /// The alpha channel
            /// </summary>
            Alpha = 3
        }

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
            // Bitmaps for comic pattern
            Bitmap hisatchBitmap = null;
            Bitmap patternBitmap = null;

            try
            {
                // Dont use an object initializer here.
                newImage = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppPArgb);
                newImage.Tag = image.Tag;

                ColorMatrix colorMatrix = null;

                switch ((string)this.DynamicParameter)
                {
                    case "polaroid":
                        colorMatrix = ColorMatrixes.Polaroid;
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
                    case "gotham":
                        colorMatrix = ColorMatrixes.Gotham;
                        break;
                    case "invert":
                        colorMatrix = ColorMatrixes.Invert;
                        break;
                    case "hisatch":
                        colorMatrix = ColorMatrixes.HiSatch;
                        break;
                    case "losatch":
                        colorMatrix = ColorMatrixes.LoSatch;
                        break;
                    case "comic":
                        colorMatrix = ColorMatrixes.LoSatch;
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

                        if (this.DynamicParameter == "comic")
                        {
                            // Set the attributes to LoSatch and draw the image.
                            graphics.DrawImage(image, rectangle, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);

                            // Create a bitmap for overlaying.
                            hisatchBitmap = new Bitmap(rectangle.Width, rectangle.Height, PixelFormat.Format32bppPArgb);

                            // Set the color matrix
                            attributes.SetColorMatrix(ColorMatrixes.HiSatch);

                            // Draw the image with the hisatch colormatrix.
                            using (var g = Graphics.FromImage(hisatchBitmap))
                            {
                                g.DrawImage(image, rectangle, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
                            }

                            // We need to create a new image now with the hi saturation colormatrix and a pattern mask to paint it
                            // onto the other image with.
                            patternBitmap = new Bitmap(rectangle.Width, rectangle.Height, PixelFormat.Format32bppPArgb);

                            // Create the pattern mask.
                            using (var g = Graphics.FromImage(patternBitmap))
                            {
                                g.Clear(Color.Black);
                                g.SmoothingMode = SmoothingMode.HighQuality;
                                for (var y = 0; y < image.Height; y += 10)
                                {
                                    for (var x = 0; x < image.Width; x += 6)
                                    {
                                        g.FillEllipse(Brushes.White, x, y, 4, 4);
                                        g.FillEllipse(Brushes.White, x + 3, y + 5, 4, 4);
                                    }
                                }
                            }

                            // Transfer the alpha channel from the mask to the hi sturation image.
                            TransferOneArgbChannelFromOneBitmapToAnother(patternBitmap, hisatchBitmap, ChannelArgb.Blue, ChannelArgb.Alpha);

                            // Overlay the image.
                            graphics.DrawImage(hisatchBitmap, 0, 0);

                            // Dispose of the other images
                            hisatchBitmap.Dispose();
                            patternBitmap.Dispose();
                        }
                        else
                        {
                            graphics.DrawImage(image, rectangle, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);

                            // Polaroid requires an extra tweak.
                            if (this.DynamicParameter == "polaroid")
                            {
                                using (GraphicsPath path = new GraphicsPath())
                                {
                                    path.AddEllipse(rectangle);
                                    using (PathGradientBrush brush = new PathGradientBrush(path))
                                    {
                                        // Fill a rectangle with an elliptical gradient brush that goes from orange to transparent.
                                        // This has the effect of painting the far corners transparent and fading in to orange on the 
                                        // way in to the centre.
                                        brush.WrapMode = WrapMode.Tile;
                                        brush.CenterColor = Color.FromArgb(70, 255, 153, 102);
                                        brush.SurroundColors = new Color[] { Color.FromArgb(0, 0, 0, 0) };

                                        Blend blend = new Blend
                                            {
                                                Positions = new float[] { 0.0f, 0.2f, 0.4f, 0.6f, 0.8f, 1.0F },
                                                Factors = new float[] { 0.0f, 0.5f, 1f, 1f, 1.0f, 1.0f }
                                            };

                                        brush.Blend = blend;

                                        Region oldClip = graphics.Clip;
                                        graphics.Clip = new Region(rectangle);
                                        graphics.FillRectangle(brush, rectangle);
                                        graphics.Clip = oldClip;
                                    }
                                }
                            }

                            // Gotham requires an extra tweak.
                            if (this.DynamicParameter == "gotham")
                            {
                                using (GraphicsPath path = new GraphicsPath())
                                {
                                    path.AddRectangle(rectangle);

                                    // Paint a burgundy rectangle with a transparency of ~30% over the image.
                                    // Paint a blue rectangle with a transparency of 20% over the image.
                                    using (SolidBrush brush = new SolidBrush(Color.FromArgb(77, 43, 4, 18)))
                                    {
                                        Region oldClip = graphics.Clip;
                                        graphics.Clip = new Region(rectangle);
                                        graphics.FillRectangle(brush, rectangle);

                                        // Fill the blue.
                                        brush.Color = Color.FromArgb(51, 12, 22, 88);
                                        graphics.FillRectangle(brush, rectangle);
                                        graphics.Clip = oldClip;
                                    }
                                }
                            }
                        }
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

                if (hisatchBitmap != null)
                {
                    hisatchBitmap.Dispose();
                }

                if (patternBitmap != null)
                {
                    patternBitmap.Dispose();
                }
            }

            return image;
        }
        #endregion

        /// <summary>
        /// Transfers a single ARGB channel from one image to another.
        /// </summary>
        /// <param name="source">
        /// The source.
        /// </param>
        /// <param name="destination">
        /// The destination.
        /// </param>
        /// <param name="sourceChannel">
        /// The source channel.
        /// </param>
        /// <param name="destinationChannel">
        /// The destination channel.
        /// </param>
        private static void TransferOneArgbChannelFromOneBitmapToAnother(Bitmap source, Bitmap destination, ChannelArgb sourceChannel, ChannelArgb destinationChannel)
        {
            if (source.Size != destination.Size)
            {
                throw new ArgumentException();
            }

            Rectangle rectangle = new Rectangle(Point.Empty, source.Size);

            // Lockbits the source.
            BitmapData bitmapDataSource = source.LockBits(rectangle, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);

            // Declare an array to hold the bytes of the bitmap.
            int bytes = bitmapDataSource.Stride * bitmapDataSource.Height;

            // Allocate a buffer for the source image
            byte[] sourceRgbValues = new byte[bytes];

            // Copy the RGB values into the array.
            System.Runtime.InteropServices.Marshal.Copy(bitmapDataSource.Scan0, sourceRgbValues, 0, bytes);

            // Unlockbits the source.
            source.UnlockBits(bitmapDataSource);

            // Lockbits the destination.
            BitmapData bitmapDataDestination = destination.LockBits(rectangle, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);

            // Allocate a buffer for image
            byte[] destinationRgbValues = new byte[bytes];

            // Copy the RGB values into the array.
            System.Runtime.InteropServices.Marshal.Copy(bitmapDataDestination.Scan0, destinationRgbValues, 0, bytes);

            int s = (int)sourceChannel;
            int d = (int)destinationChannel;

            for (int i = rectangle.Height * rectangle.Width; i > 0; i--)
            {
                destinationRgbValues[d] = sourceRgbValues[s];
                d += 4;
                s += 4;
            }

            // Copy the RGB values back to the bitmap
            System.Runtime.InteropServices.Marshal.Copy(destinationRgbValues, 0, bitmapDataDestination.Scan0, bytes);

            // Unlock bits the destination.
            destination.UnlockBits(bitmapDataDestination);
        }

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
            /// Gets Polaroid.
            /// </summary>
            internal static ColorMatrix Polaroid
            {
                get
                {
                    return new ColorMatrix(
                        new float[][]
                            {
                                new float[] { 1.638f, -0.062f, -0.262f, 0, 0 },
                                new float[] { -0.122f, 1.378f, -0.122f, 0, 0 },
                                new float[] { 1.016f, -0.016f, 1.383f, 0, 0 },
                                new float[] { 0, 0, 0, 1, 0 },
                                new float[] { 0.06f, -0.05f, -0.05f, 0, 1 }
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
                                new float[] { 1.50f, 0, 0, 0, 0 }, 
                                new float[] { 0, 1.45f, 0, 0, 0 },
                                new float[] { 0, 0, 1.09f, 0, 0 }, 
                                new float[] { 0, 0, 0, 1, 0 },
                                new float[] { -0.10f, 0.05f, -0.08f, 0, 1 }
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

            /// <summary>
            /// Gets Gotham.
            /// </summary>
            internal static ColorMatrix Gotham
            {
                get
                {
                    return new ColorMatrix(
                        new float[][]
                            {
                                new float[] { .9f, .9f, .9f, 0, 0 }, 
                                new float[] { .9f, .9f, .9f, 0, 0 }, 
                                new float[] { .9f, .9f, .9f, 0, 0 }, 
                                new float[] { 0, 0, 0, 1, 0 },
                                new float[] { -.5f, -.5f, -.45f, 0, 1 }
                            });
                }
            }

            /// <summary>
            /// Gets Invert.
            /// </summary>
            internal static ColorMatrix Invert
            {
                get
                {
                    return new ColorMatrix(
                        new float[][]
                            {
                                new float[] { -1, 0, 0, 0, 0 }, 
                                new float[] { 0, -1, 0, 0, 0 },  
                                new float[] { 0, 0, -1, 0, 0 }, 
                                new float[] { 0, 0, 0, 1, 0 },
                                new float[] { 1, 1, 1, 0, 1 }
                            });
                }
            }

            /// <summary>
            /// Gets HiSatch.
            /// </summary>
            internal static ColorMatrix HiSatch
            {
                get
                {
                    return new ColorMatrix(
                        new float[][]
                            {
                                new float[] { 3, -1, -1, 0, 0 }, 
                                new float[] { -1, 3, -1, 0, 0 },  
                                new float[] { -1, -1, 3, 0, 0 }, 
                                new float[] { 0, 0, 0, 1, 0 },
                                new float[] { 0, 0, 0, 0, 1 }
                            });
                }
            }

            /// <summary>
            /// Gets LoSatch.
            /// </summary>
            internal static ColorMatrix LoSatch
            {
                get
                {
                    return new ColorMatrix(
                           new float[][]
                            {
                                new float[] { 1, 0, 0, 0, 0 }, 
                                new float[] { 0, 1, 0, 0, 0 },  
                                new float[] { 0, 0, 1, 0, 0 }, 
                                new float[] { 0, 0, 0, 1, 0 },
                                new float[] { .25f, .25f, .25f, 0, 1 }
                            });
                }
            }
        }
    }
}

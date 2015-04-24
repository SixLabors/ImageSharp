// --------------------------------------------------------------------------------------------------------------------
// <copyright file="WuQuantizerBase.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods to calculate the color palette of an image using
//   a Wu color quantizer <see href="http://www.ece.mcmaster.ca/~xwu/cq.c" />.
//   Adapted from <see href="https://github.com/drewnoakes" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Quantizers.WuQuantizer
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.Linq;

    using ImageProcessor.Common.Exceptions;
    using ImageProcessor.Imaging.Colors;

    /// <summary>
    /// Encapsulates methods to calculate the color palette of an image using 
    /// a Wu color quantizer <see href="http://www.ece.mcmaster.ca/~xwu/cq.c"/>.
    /// Adapted from <see href="https://github.com/drewnoakes"/>
    /// </summary>
    public abstract class WuQuantizerBase : IWuQuantizer
    {
        /// <summary>
        /// The maximum value for an alpha color component.
        /// </summary>
        protected const byte AlphaMax = 255;

        /// <summary>
        /// The minimum value for an alpha color component.
        /// </summary>
        protected const byte AlphaMin = 0;

        /// <summary>
        /// The position of the alpha component within a byte array.
        /// </summary>
        protected const int Alpha = 3;

        /// <summary>
        /// The position of the red component within a byte array.
        /// </summary>
        protected const int Red = 2;

        /// <summary>
        /// The position of the green component within a byte array.
        /// </summary>
        protected const int Green = 1;

        /// <summary>
        /// The position of the blue component within a byte array.
        /// </summary>
        protected const int Blue = 0;

        /// <summary>
        /// The size of a color cube side.
        /// </summary>
        private const int SideSize = 33;

        /// <summary>
        /// The maximum index within a color cube side
        /// </summary>
        private const int MaxSideIndex = 32;

        /// <summary>
        /// Quantize an image and return the resulting output bitmap
        /// </summary>
        /// <param name="source">
        /// The 32 bit per pixel image to quantize.
        /// </param>
        /// <returns>
        /// A quantized version of the image.
        /// </returns>
        public Bitmap Quantize(Image source)
        {
            return this.Quantize(source, 0, 1);
        }

        /// <summary>
        /// Quantize an image and return the resulting output bitmap
        /// </summary>
        /// <param name="source">
        /// The 32 bit per pixel image to quantize.
        /// </param>
        /// <param name="alphaThreshold">
        /// All colors with an alpha value less than this will be considered fully transparent.
        /// </param>
        /// <param name="alphaFader">
        /// Alpha values will be normalized to the nearest multiple of this value.
        /// </param>
        /// <returns>
        /// A quantized version of the image.
        /// </returns>
        public Bitmap Quantize(Image source, int alphaThreshold, int alphaFader)
        {
            return this.Quantize(source, alphaThreshold, alphaFader, null, 256);
        }

        /// <summary>
        /// Quantize an image and return the resulting output bitmap
        /// </summary>
        /// <param name="source">
        /// The 32 bit per pixel image to quantize.
        /// </param>
        /// <param name="alphaThreshold">
        /// All colors with an alpha value less than this will be considered fully transparent.
        /// </param>
        /// <param name="alphaFader">
        /// Alpha values will be normalized to the nearest multiple of this value.
        /// </param>
        /// <param name="histogram">
        /// The <see cref="Histogram"/> representing the distribution of color data.
        /// </param>
        /// <param name="maxColors">
        /// The maximum number of colors apply to the image.
        /// </param>
        /// <returns>
        /// A quantized version of the image.
        /// </returns>
        public Bitmap Quantize(Image source, int alphaThreshold, int alphaFader, Histogram histogram, int maxColors)
        {
            try
            {
                ImageBuffer buffer;

                // The image has to be a 32 bit per pixel Argb image.
                if (Image.GetPixelFormatSize(source.PixelFormat) != 32)
                {
                    Bitmap clone = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppPArgb);
                    clone.SetResolution(source.HorizontalResolution, source.VerticalResolution);

                    using (Graphics graphics = Graphics.FromImage(clone))
                    {
                        graphics.Clear(Color.Transparent);
                        graphics.DrawImage(source, new Rectangle(0, 0, clone.Width, clone.Height));
                    }

                    source.Dispose();
                    buffer = new ImageBuffer(clone);
                }
                else
                {
                    buffer = new ImageBuffer((Bitmap)source);
                }

                if (histogram == null)
                {
                    histogram = new Histogram();
                }
                else
                {
                    histogram.Clear();
                }

                BuildHistogram(histogram, buffer, alphaThreshold, alphaFader);
                CalculateMoments(histogram.Moments);
                Box[] cubes = SplitData(ref maxColors, histogram.Moments);
                Color32[] lookups = BuildLookups(cubes, histogram.Moments);
                return this.GetQuantizedImage(buffer, maxColors, lookups, alphaThreshold);
            }
            catch (Exception ex)
            {
                throw new QuantizationException(ex.Message, ex);
            }
        }

        /// <summary>
        /// Quantizes the image contained within the <see cref="ImageBuffer"/> returning the result.
        /// </summary>
        /// <param name="imageBuffer">
        /// The <see cref="ImageBuffer"/> for storing and manipulating pixel information..
        /// </param>
        /// <param name="colorCount">
        /// The maximum number of colors apply to the image.
        /// </param>
        /// <param name="lookups">
        /// The array of <see cref="Color32"/> containing indexed versions of the images colors.
        /// </param>
        /// <param name="alphaThreshold">
        /// All colors with an alpha value less than this will be considered fully transparent.
        /// </param>
        /// <returns>
        /// The quantized <see cref="Bitmap"/>.
        /// </returns>
        internal abstract Bitmap GetQuantizedImage(ImageBuffer imageBuffer, int colorCount, Color32[] lookups, int alphaThreshold);

        /// <summary>
        /// Builds a histogram from the current image.
        /// </summary>
        /// <param name="histogram">
        /// The <see cref="Histogram"/> representing the distribution of color data.
        /// </param>
        /// <param name="imageBuffer">
        /// The <see cref="ImageBuffer"/> for storing pixel information.
        /// </param>
        /// <param name="alphaThreshold">
        /// All colors with an alpha value less than this will be considered fully transparent.
        /// </param>
        /// <param name="alphaFader">
        /// Alpha values will be normalized to the nearest multiple of this value.
        /// </param>
        [SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1001:CommasMustBeSpacedCorrectly", Justification = "Reviewed. Suppression is OK here.")]
        private static void BuildHistogram(Histogram histogram, ImageBuffer imageBuffer, int alphaThreshold, int alphaFader)
        {
            ColorMoment[, , ,] moments = histogram.Moments;

            foreach (Color32[] pixelLine in imageBuffer.PixelLines)
            {
                foreach (Color32 pixel in pixelLine)
                {
                    byte pixelAlpha = pixel.A;
                    if (pixelAlpha > alphaThreshold)
                    {
                        if (pixelAlpha < 255)
                        {
                            int alpha = pixel.A + (pixel.A % alphaFader);
                            pixelAlpha = (byte)(alpha > 255 ? 255 : alpha);
                        }

                        byte pixelRed = pixel.R;
                        byte pixelGreen = pixel.G;
                        byte pixelBlue = pixel.B;

                        pixelAlpha = (byte)((pixelAlpha >> 3) + 1);
                        pixelRed = (byte)((pixelRed >> 3) + 1);
                        pixelGreen = (byte)((pixelGreen >> 3) + 1);
                        pixelBlue = (byte)((pixelBlue >> 3) + 1);
                        moments[pixelAlpha, pixelRed, pixelGreen, pixelBlue].Add(pixel);
                    }
                }
            }

            // Set a default pixel for images with less than 256 colors.
            moments[0, 0, 0, 0].Add(new Color32(0, 0, 0, 0));
        }

        /// <summary>
        /// Calculates the color moments from the histogram of moments.
        /// </summary>
        /// <param name="moments">
        /// The three dimensional array of <see cref="ColorMoment"/> to process.
        /// </param>
        [SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1001:CommasMustBeSpacedCorrectly", Justification = "Reviewed. Suppression is OK here.")]
        private static void CalculateMoments(ColorMoment[, , ,] moments)
        {
            ColorMoment[,] areaSquared = new ColorMoment[SideSize, SideSize];
            ColorMoment[] area = new ColorMoment[SideSize];
            for (int alphaIndex = 1; alphaIndex < SideSize; alphaIndex++)
            {
                for (int redIndex = 1; redIndex < SideSize; redIndex++)
                {
                    Array.Clear(area, 0, area.Length);
                    for (int greenIndex = 1; greenIndex < SideSize; greenIndex++)
                    {
                        ColorMoment line = new ColorMoment();
                        for (int blueIndex = 1; blueIndex < SideSize; blueIndex++)
                        {
                            line.AddFast(ref moments[alphaIndex, redIndex, greenIndex, blueIndex]);
                            area[blueIndex].AddFast(ref line);
                            areaSquared[greenIndex, blueIndex].AddFast(ref area[blueIndex]);

                            ColorMoment moment = moments[alphaIndex - 1, redIndex, greenIndex, blueIndex];
                            moment.AddFast(ref areaSquared[greenIndex, blueIndex]);
                            moments[alphaIndex, redIndex, greenIndex, blueIndex] = moment;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Calculates the volume of the top of the cube.
        /// </summary>
        /// <param name="cube">
        /// The cube to calculate the volume from.
        /// </param>
        /// <param name="direction">
        /// The direction to calculate.
        /// </param>
        /// <param name="position">
        /// The position at which to begin.
        /// </param>
        /// <param name="moment">
        /// The three dimensional moment.
        /// </param>
        /// <returns>
        /// The <see cref="ColorMoment"/> representing the top of the cube.
        /// </returns>
        [SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1001:CommasMustBeSpacedCorrectly", Justification = "Reviewed. Suppression is OK here.")]
        private static ColorMoment Top(Box cube, int direction, int position, ColorMoment[, , ,] moment)
        {
            switch (direction)
            {
                case Alpha:
                    return (moment[position, cube.RedMaximum, cube.GreenMaximum, cube.BlueMaximum] -
                            moment[position, cube.RedMaximum, cube.GreenMinimum, cube.BlueMaximum] -
                            moment[position, cube.RedMinimum, cube.GreenMaximum, cube.BlueMaximum] +
                            moment[position, cube.RedMinimum, cube.GreenMinimum, cube.BlueMaximum]) -
                           (moment[position, cube.RedMaximum, cube.GreenMaximum, cube.BlueMinimum] -
                            moment[position, cube.RedMaximum, cube.GreenMinimum, cube.BlueMinimum] -
                            moment[position, cube.RedMinimum, cube.GreenMaximum, cube.BlueMinimum] +
                            moment[position, cube.RedMinimum, cube.GreenMinimum, cube.BlueMinimum]);

                case Red:
                    return (moment[cube.AlphaMaximum, position, cube.GreenMaximum, cube.BlueMaximum] -
                            moment[cube.AlphaMaximum, position, cube.GreenMinimum, cube.BlueMaximum] -
                            moment[cube.AlphaMinimum, position, cube.GreenMaximum, cube.BlueMaximum] +
                            moment[cube.AlphaMinimum, position, cube.GreenMinimum, cube.BlueMaximum]) -
                           (moment[cube.AlphaMaximum, position, cube.GreenMaximum, cube.BlueMinimum] -
                            moment[cube.AlphaMaximum, position, cube.GreenMinimum, cube.BlueMinimum] -
                            moment[cube.AlphaMinimum, position, cube.GreenMaximum, cube.BlueMinimum] +
                            moment[cube.AlphaMinimum, position, cube.GreenMinimum, cube.BlueMinimum]);

                case Green:
                    return (moment[cube.AlphaMaximum, cube.RedMaximum, position, cube.BlueMaximum] -
                            moment[cube.AlphaMaximum, cube.RedMinimum, position, cube.BlueMaximum] -
                            moment[cube.AlphaMinimum, cube.RedMaximum, position, cube.BlueMaximum] +
                            moment[cube.AlphaMinimum, cube.RedMinimum, position, cube.BlueMaximum]) -
                           (moment[cube.AlphaMaximum, cube.RedMaximum, position, cube.BlueMinimum] -
                            moment[cube.AlphaMaximum, cube.RedMinimum, position, cube.BlueMinimum] -
                            moment[cube.AlphaMinimum, cube.RedMaximum, position, cube.BlueMinimum] +
                            moment[cube.AlphaMinimum, cube.RedMinimum, position, cube.BlueMinimum]);

                case Blue:
                    return (moment[cube.AlphaMaximum, cube.RedMaximum, cube.GreenMaximum, position] -
                            moment[cube.AlphaMaximum, cube.RedMaximum, cube.GreenMinimum, position] -
                            moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMaximum, position] +
                            moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMinimum, position]) -
                           (moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMaximum, position] -
                            moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMinimum, position] -
                            moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMaximum, position] +
                            moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMinimum, position]);

                default:
                    return new ColorMoment();
            }
        }

        /// <summary>
        /// Calculates the volume of the bottom of the cube.
        /// </summary>
        /// <param name="cube">
        /// The cube to calculate the volume from.
        /// </param>
        /// <param name="direction">
        /// The direction to calculate.
        /// </param>
        /// <param name="moment">
        /// The three dimensional moment.
        /// </param>
        /// <returns>
        /// The <see cref="ColorMoment"/> representing the bottom of the cube.
        /// </returns>
        [SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1001:CommasMustBeSpacedCorrectly", Justification = "Reviewed. Suppression is OK here.")]
        private static ColorMoment Bottom(Box cube, int direction, ColorMoment[, , ,] moment)
        {
            switch (direction)
            {
                case Alpha:
                    return (-moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMaximum, cube.BlueMaximum] +
                            moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMaximum] +
                            moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMaximum] -
                            moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMaximum]) -
                           (-moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMaximum, cube.BlueMinimum] +
                            moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMinimum] +
                            moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMinimum] -
                            moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMinimum]);

                case Red:
                    return (-moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMaximum] +
                            moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMaximum] +
                            moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMaximum] -
                            moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMaximum]) -
                           (-moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMinimum] +
                            moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMinimum] +
                            moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMinimum] -
                            moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMinimum]);

                case Green:
                    return (-moment[cube.AlphaMaximum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMaximum] +
                            moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMaximum] +
                            moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMaximum] -
                            moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMaximum]) -
                           (-moment[cube.AlphaMaximum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMinimum] +
                            moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMinimum] +
                            moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMinimum] -
                            moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMinimum]);

                case Blue:
                    return (-moment[cube.AlphaMaximum, cube.RedMaximum, cube.GreenMaximum, cube.BlueMinimum] +
                            moment[cube.AlphaMaximum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMinimum] +
                            moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMinimum] -
                            moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMinimum]) -
                           (-moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMaximum, cube.BlueMinimum] +
                            moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMinimum] +
                            moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMinimum] -
                            moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMinimum]);

                default:
                    return new ColorMoment();
            }
        }

        /// <summary>
        /// Maximizes the sum of the two boxes.
        /// </summary>
        /// <param name="moments">
        /// The <see cref="ColorMoment"/>.
        /// </param>
        /// <param name="cube">
        /// The <see cref="Box"/> cube.
        /// </param>
        /// <param name="direction">
        /// The direction.
        /// </param>
        /// <param name="first">
        /// The first byte.
        /// </param>
        /// <param name="last">
        /// The last byte.
        /// </param>
        /// <param name="whole">
        /// The whole <see cref="ColorMoment"/>.
        /// </param>
        /// <returns>
        /// The <see cref="CubeCut"/> representing the sum.
        /// </returns>
        [SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1001:CommasMustBeSpacedCorrectly", Justification = "Reviewed. Suppression is OK here.")]
        private static CubeCut Maximize(ColorMoment[, , ,] moments, Box cube, int direction, byte first, byte last, ColorMoment whole)
        {
            ColorMoment bottom = Bottom(cube, direction, moments);
            float result = 0.0f;
            byte? cutPoint = null;

            for (byte position = first; position < last; ++position)
            {
                ColorMoment half = bottom + Top(cube, direction, position, moments);
                if (half.Weight == 0)
                {
                    continue;
                }

                long temp = half.WeightedDistance();

                half = whole - half;
                if (half.Weight != 0)
                {
                    temp += half.WeightedDistance();

                    if (temp > result)
                    {
                        result = temp;
                        cutPoint = position;
                    }
                }
            }

            return new CubeCut(cutPoint, result);
        }

        /// <summary>
        /// Returns a value indicating whether a cube can be cut.
        /// </summary>
        /// <param name="moments">
        /// The three dimensional array of <see cref="ColorMoment"/>.
        /// </param>
        /// <param name="first">
        /// The first <see cref="Box"/>.
        /// </param>
        /// <param name="second">
        /// The second <see cref="Box"/>.
        /// </param>
        /// <returns>
        /// The <see cref="bool"/> indicating the result.
        /// </returns>
        [SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1001:CommasMustBeSpacedCorrectly", Justification = "Reviewed. Suppression is OK here.")]
        private static bool Cut(ColorMoment[, , ,] moments, ref Box first, ref Box second)
        {
            int direction;
            ColorMoment whole = Volume(moments, first);
            CubeCut maxAlpha = Maximize(moments, first, Alpha, (byte)(first.AlphaMinimum + 1), first.AlphaMaximum, whole);
            CubeCut maxRed = Maximize(moments, first, Red, (byte)(first.RedMinimum + 1), first.RedMaximum, whole);
            CubeCut maxGreen = Maximize(moments, first, Green, (byte)(first.GreenMinimum + 1), first.GreenMaximum, whole);
            CubeCut maxBlue = Maximize(moments, first, Blue, (byte)(first.BlueMinimum + 1), first.BlueMaximum, whole);

            if ((maxAlpha.Value >= maxRed.Value) && (maxAlpha.Value >= maxGreen.Value) && (maxAlpha.Value >= maxBlue.Value))
            {
                direction = Alpha;
                if (maxAlpha.Position == null)
                {
                    return false;
                }
            }
            else if ((maxRed.Value >= maxAlpha.Value) && (maxRed.Value >= maxGreen.Value)
                     && (maxRed.Value >= maxBlue.Value))
            {
                direction = Red;
            }
            else
            {
                if ((maxGreen.Value >= maxAlpha.Value) && (maxGreen.Value >= maxRed.Value)
                    && (maxGreen.Value >= maxBlue.Value))
                {
                    direction = Green;
                }
                else
                {
                    direction = Blue;
                }
            }

            second.AlphaMaximum = first.AlphaMaximum;
            second.RedMaximum = first.RedMaximum;
            second.GreenMaximum = first.GreenMaximum;
            second.BlueMaximum = first.BlueMaximum;

            switch (direction)
            {
                case Alpha:
                    if (maxAlpha.Position == null)
                    {
                        return false;
                    }

                    second.AlphaMinimum = first.AlphaMaximum = (byte)maxAlpha.Position;
                    second.RedMinimum = first.RedMinimum;
                    second.GreenMinimum = first.GreenMinimum;
                    second.BlueMinimum = first.BlueMinimum;
                    break;

                case Red:
                    if (maxRed.Position == null)
                    {
                        return false;
                    }

                    second.RedMinimum = first.RedMaximum = (byte)maxRed.Position;
                    second.AlphaMinimum = first.AlphaMinimum;
                    second.GreenMinimum = first.GreenMinimum;
                    second.BlueMinimum = first.BlueMinimum;
                    break;

                case Green:
                    if (maxGreen.Position == null)
                    {
                        return false;
                    }

                    second.GreenMinimum = first.GreenMaximum = (byte)maxGreen.Position;
                    second.AlphaMinimum = first.AlphaMinimum;
                    second.RedMinimum = first.RedMinimum;
                    second.BlueMinimum = first.BlueMinimum;
                    break;

                case Blue:
                    if (maxBlue.Position == null)
                    {
                        return false;
                    }

                    second.BlueMinimum = first.BlueMaximum = (byte)maxBlue.Position;
                    second.AlphaMinimum = first.AlphaMinimum;
                    second.RedMinimum = first.RedMinimum;
                    second.GreenMinimum = first.GreenMinimum;
                    break;
            }

            first.Size = (first.AlphaMaximum - first.AlphaMinimum) * (first.RedMaximum - first.RedMinimum) * (first.GreenMaximum - first.GreenMinimum) * (first.BlueMaximum - first.BlueMinimum);
            second.Size = (second.AlphaMaximum - second.AlphaMinimum) * (second.RedMaximum - second.RedMinimum) * (second.GreenMaximum - second.GreenMinimum) * (second.BlueMaximum - second.BlueMinimum);

            return true;
        }

        /// <summary>
        /// Calculates the variance of the volume of the cube.
        /// </summary>
        /// <param name="moments">
        /// The three dimensional array of <see cref="ColorMoment"/>.
        /// </param>
        /// <param name="cube">
        /// The <see cref="Box"/> cube.
        /// </param>
        /// <returns>
        /// The <see cref="float"/> representing the variance.
        /// </returns>
        [SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1001:CommasMustBeSpacedCorrectly", Justification = "Reviewed. Suppression is OK here.")]
        private static float CalculateVariance(ColorMoment[, , ,] moments, Box cube)
        {
            ColorMoment volume = Volume(moments, cube);
            return volume.Variance();
        }

        /// <summary>
        /// Calculates the volume of the colors.
        /// </summary>
        /// <param name="moments">
        /// The three dimensional array of <see cref="ColorMoment"/>.
        /// </param>
        /// <param name="cube">
        /// The <see cref="Box"/> cube.
        /// </param>
        /// <returns>
        /// The <see cref="float"/> representing the volume.
        /// </returns>
        [SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1001:CommasMustBeSpacedCorrectly", Justification = "Reviewed. Suppression is OK here.")]
        private static ColorMoment Volume(ColorMoment[, , ,] moments, Box cube)
        {
            return (moments[cube.AlphaMaximum, cube.RedMaximum, cube.GreenMaximum, cube.BlueMaximum] -
                    moments[cube.AlphaMaximum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMaximum] -
                    moments[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMaximum] +
                    moments[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMaximum] -
                    moments[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMaximum, cube.BlueMaximum] +
                    moments[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMaximum] +
                    moments[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMaximum] -
                    moments[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMaximum]) -

                   (moments[cube.AlphaMaximum, cube.RedMaximum, cube.GreenMaximum, cube.BlueMinimum] -
                    moments[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMaximum, cube.BlueMinimum] -
                    moments[cube.AlphaMaximum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMinimum] +
                    moments[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMinimum] -
                    moments[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMinimum] +
                    moments[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMinimum] +
                    moments[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMinimum] -
                    moments[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMinimum]);
        }

        /// <summary>
        /// Splits the data.
        /// </summary>
        /// <param name="colorCount">
        /// The color count.
        /// </param>
        /// <param name="moments">
        /// The three dimensional array of <see cref="ColorMoment"/>.
        /// </param>
        /// <returns>
        /// The array <see cref="Box"/>.
        /// </returns>
        [SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1001:CommasMustBeSpacedCorrectly", Justification = "Reviewed. Suppression is OK here.")]
        private static Box[] SplitData(ref int colorCount, ColorMoment[, , ,] moments)
        {
            --colorCount;
            int next = 0;
            float[] volumeVariance = new float[colorCount];
            Box[] cubes = new Box[colorCount];
            cubes[0].AlphaMaximum = MaxSideIndex;
            cubes[0].RedMaximum = MaxSideIndex;
            cubes[0].GreenMaximum = MaxSideIndex;
            cubes[0].BlueMaximum = MaxSideIndex;
            for (int cubeIndex = 1; cubeIndex < colorCount; ++cubeIndex)
            {
                if (Cut(moments, ref cubes[next], ref cubes[cubeIndex]))
                {
                    volumeVariance[next] = cubes[next].Size > 1 ? CalculateVariance(moments, cubes[next]) : 0.0f;
                    volumeVariance[cubeIndex] = cubes[cubeIndex].Size > 1 ? CalculateVariance(moments, cubes[cubeIndex]) : 0.0f;
                }
                else
                {
                    volumeVariance[next] = 0.0f;
                    cubeIndex--;
                }

                next = 0;
                float temp = volumeVariance[0];

                for (int index = 1; index <= cubeIndex; ++index)
                {
                    if (volumeVariance[index] <= temp)
                    {
                        continue;
                    }

                    temp = volumeVariance[index];
                    next = index;
                }

                if (temp > 0.0)
                {
                    continue;
                }

                colorCount = cubeIndex + 1;
                break;
            }

            return cubes.Take(colorCount).ToArray();
        }

        /// <summary>
        /// Builds an array of pixel data to look within.
        /// </summary>
        /// <param name="cubes">
        /// The array of <see cref="Box"/> cubes.
        /// </param>
        /// <param name="moments">
        /// The three dimensional array of <see cref="ColorMoment"/>.
        /// </param>
        /// <returns>
        /// The array of <see cref="Color32"/>.
        /// </returns>
        [SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1001:CommasMustBeSpacedCorrectly", Justification = "Reviewed. Suppression is OK here.")]
        private static Color32[] BuildLookups(Box[] cubes, ColorMoment[, , ,] moments)
        {
            Color32[] lookups = new Color32[cubes.Length];

            for (int cubeIndex = 0; cubeIndex < cubes.Length; cubeIndex++)
            {
                ColorMoment volume = Volume(moments, cubes[cubeIndex]);

                if (volume.Weight <= 0)
                {
                    continue;
                }

                Color32 lookup = new Color32
                {
                    A = (byte)(volume.Alpha / volume.Weight),
                    R = (byte)(volume.Red / volume.Weight),
                    G = (byte)(volume.Green / volume.Weight),
                    B = (byte)(volume.Blue / volume.Weight)
                };

                lookups[cubeIndex] = lookup;
            }

            return lookups;
        }
    }
}
// <copyright file="ResizeHelper.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Samplers
{
    using System;
    using System.Linq;

    /// <summary>
    /// Provides methods to help calculate the target rectangle when resizing using the 
    /// <see cref="ResizeMode"/> enumeration.
    /// </summary>
    internal static class ResizeHelper
    {
        /// <summary>
        /// Calculates the target location and bounds to perform the resize operation against.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="options">The resize options.</param>
        /// <returns>
        /// The <see cref="Rectangle"/>.
        /// </returns>
        public static Rectangle CalculateTargetLocationAndBounds(ImageBase source, ResizeOptions options)
        {
            switch (options.Mode)
            {
                case ResizeMode.Pad:
                    return CalculatePadRectangle(source, options);

                // TODO: Additional modes
                // Default case ResizeMode.Crop
                default:
                    return CalculateCropRectangle(source, options);
            }
        }

        /// <summary>
        /// Calculates the target rectangle for crop mode.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="options">The resize options.</param>
        /// <returns>
        /// The <see cref="Rectangle"/>.
        /// </returns>
        private static Rectangle CalculateCropRectangle(ImageBase source, ResizeOptions options)
        {
            int width = options.Size.Width;
            int height = options.Size.Height;

            if (width <= 0 || height <= 0)
            {
                return new Rectangle(0, 0, source.Width, source.Height);
            }

            double ratio;
            int sourceWidth = source.Width;
            int sourceHeight = source.Height;

            int destinationX = 0;
            int destinationY = 0;
            int destinationWidth = width;
            int destinationHeight = height;

            // Fractional variants for preserving aspect ratio.
            double percentHeight = Math.Abs(height / (double)sourceHeight);
            double percentWidth = Math.Abs(width / (double)sourceWidth);

            if (percentHeight < percentWidth)
            {
                ratio = percentWidth;

                if (options.CenterCoordinates.Any())
                {
                    double center = -(ratio * sourceHeight) * options.CenterCoordinates.First();
                    destinationY = (int)center + (height / 2);

                    if (destinationY > 0)
                    {
                        destinationY = 0;
                    }

                    if (destinationY < (int)(height - (sourceHeight * ratio)))
                    {
                        destinationY = (int)(height - (sourceHeight * ratio));
                    }
                }
                else
                {
                    switch (options.Position)
                    {
                        case AnchorPosition.Top:
                        case AnchorPosition.TopLeft:
                        case AnchorPosition.TopRight:
                            destinationY = 0;
                            break;
                        case AnchorPosition.Bottom:
                        case AnchorPosition.BottomLeft:
                        case AnchorPosition.BottomRight:
                            destinationY = (int)(height - (sourceHeight * ratio));
                            break;
                        default:
                            destinationY = (int)((height - (sourceHeight * ratio)) / 2);
                            break;
                    }
                }

                destinationHeight = (int)Math.Ceiling(sourceHeight * percentWidth);
            }
            else
            {
                ratio = percentHeight;

                if (options.CenterCoordinates.Any())
                {
                    double center = -(ratio * sourceWidth) * options.CenterCoordinates.ToArray()[1];
                    destinationX = (int)center + (width / 2);

                    if (destinationX > 0)
                    {
                        destinationX = 0;
                    }

                    if (destinationX < (int)(width - (sourceWidth * ratio)))
                    {
                        destinationX = (int)(width - (sourceWidth * ratio));
                    }
                }
                else
                {
                    switch (options.Position)
                    {
                        case AnchorPosition.Left:
                        case AnchorPosition.TopLeft:
                        case AnchorPosition.BottomLeft:
                            destinationX = 0;
                            break;
                        case AnchorPosition.Right:
                        case AnchorPosition.TopRight:
                        case AnchorPosition.BottomRight:
                            destinationX = (int)(width - (sourceWidth * ratio));
                            break;
                        default:
                            destinationX = (int)((width - (sourceWidth * ratio)) / 2);
                            break;
                    }
                }

                destinationWidth = (int)Math.Ceiling(sourceWidth * percentHeight);
            }

            return new Rectangle(destinationX, destinationY, destinationWidth, destinationHeight);
        }

        /// <summary>
        /// Calculates the target rectangle for pad mode.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="options">The resize options.</param>
        /// <returns>
        /// The <see cref="Rectangle"/>.
        /// </returns>
        private static Rectangle CalculatePadRectangle(ImageBase source, ResizeOptions options)
        {
            int width = options.Size.Width;
            int height = options.Size.Height;

            if (width <= 0 || height <= 0)
            {
                return new Rectangle(0, 0, source.Width, source.Height);
            }

            double ratio;
            int sourceWidth = source.Width;
            int sourceHeight = source.Height;

            int destinationX = 0;
            int destinationY = 0;
            int destinationWidth = width;
            int destinationHeight = height;

            // Fractional variants for preserving aspect ratio.
            double percentHeight = Math.Abs(height / (double)sourceHeight);
            double percentWidth = Math.Abs(width / (double)sourceWidth);

            if (percentHeight < percentWidth)
            {
                ratio = percentHeight;
                destinationWidth = Convert.ToInt32(sourceWidth * percentHeight);

                switch (options.Position)
                {
                    case AnchorPosition.Left:
                    case AnchorPosition.TopLeft:
                    case AnchorPosition.BottomLeft:
                        destinationX = 0;
                        break;
                    case AnchorPosition.Right:
                    case AnchorPosition.TopRight:
                    case AnchorPosition.BottomRight:
                        destinationX = (int)(width - (sourceWidth * ratio));
                        break;
                    default:
                        destinationX = Convert.ToInt32((width - (sourceWidth * ratio)) / 2);
                        break;
                }
            }
            else
            {
                ratio = percentWidth;
                destinationHeight = Convert.ToInt32(sourceHeight * percentWidth);

                switch (options.Position)
                {
                    case AnchorPosition.Top:
                    case AnchorPosition.TopLeft:
                    case AnchorPosition.TopRight:
                        destinationY = 0;
                        break;
                    case AnchorPosition.Bottom:
                    case AnchorPosition.BottomLeft:
                    case AnchorPosition.BottomRight:
                        destinationY = (int)(height - (sourceHeight * ratio));
                        break;
                    default:
                        destinationY = (int)((height - (sourceHeight * ratio)) / 2);
                        break;
                }
            }

            return new Rectangle(destinationX, destinationY, destinationWidth, destinationHeight);
        }
    }
}

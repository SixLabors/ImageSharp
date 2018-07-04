// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Provides methods to help calculate the target rectangle when resizing using the
    /// <see cref="ResizeMode"/> enumeration.
    /// </summary>
    internal static class ResizeHelper
    {
        /// <summary>
        /// Calculates the target location and bounds to perform the resize operation against.
        /// </summary>
        /// <param name="sourceSize">The source image size.</param>
        /// <param name="options">The resize options.</param>
        /// <param name="width">The target width</param>
        /// <param name="height">The target height</param>
        /// <returns>
        /// The <see cref="ValueTuple{Size,Rectangle}"/>.
        /// </returns>
        public static (Size, Rectangle) CalculateTargetLocationAndBounds(Size sourceSize, ResizeOptions options, int width, int height)
        {
            switch (options.Mode)
            {
                case ResizeMode.Crop:
                    return CalculateCropRectangle(sourceSize, options, width, height);
                case ResizeMode.Pad:
                    return CalculatePadRectangle(sourceSize, options, width, height);
                case ResizeMode.BoxPad:
                    return CalculateBoxPadRectangle(sourceSize, options, width, height);
                case ResizeMode.Max:
                    return CalculateMaxRectangle(sourceSize, options, width, height);
                case ResizeMode.Min:
                    return CalculateMinRectangle(sourceSize, options, width, height);

                // Last case ResizeMode.Stretch:
                default:
                    return (new Size(width, height), new Rectangle(0, 0, width, height));
            }
        }

        private static (Size, Rectangle) CalculateCropRectangle(Size source, ResizeOptions options, int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return (new Size(source.Width, source.Height), new Rectangle(0, 0, source.Width, source.Height));
            }

            float ratio;
            int sourceWidth = source.Width;
            int sourceHeight = source.Height;

            int destinationX = 0;
            int destinationY = 0;
            int destinationWidth = width;
            int destinationHeight = height;

            // Fractional variants for preserving aspect ratio.
            float percentHeight = MathF.Abs(height / (float)sourceHeight);
            float percentWidth = MathF.Abs(width / (float)sourceWidth);

            if (percentHeight < percentWidth)
            {
                ratio = percentWidth;

                if (options.CenterCoordinates.Any())
                {
                    float center = -(ratio * sourceHeight) * options.CenterCoordinates.ToArray()[1];
                    destinationY = (int)MathF.Round(center + (height / 2F));

                    if (destinationY > 0)
                    {
                        destinationY = 0;
                    }

                    if (destinationY < (int)MathF.Round(height - (sourceHeight * ratio)))
                    {
                        destinationY = (int)MathF.Round(height - (sourceHeight * ratio));
                    }
                }
                else
                {
                    switch (options.Position)
                    {
                        case AnchorPositionMode.Top:
                        case AnchorPositionMode.TopLeft:
                        case AnchorPositionMode.TopRight:
                            destinationY = 0;
                            break;
                        case AnchorPositionMode.Bottom:
                        case AnchorPositionMode.BottomLeft:
                        case AnchorPositionMode.BottomRight:
                            destinationY = (int)MathF.Round(height - (sourceHeight * ratio));
                            break;
                        default:
                            destinationY = (int)MathF.Round((height - (sourceHeight * ratio)) / 2F);
                            break;
                    }
                }

                destinationHeight = (int)MathF.Ceiling(sourceHeight * percentWidth);
            }
            else
            {
                ratio = percentHeight;

                if (options.CenterCoordinates.Any())
                {
                    float center = -(ratio * sourceWidth) * options.CenterCoordinates.First();
                    destinationX = (int)MathF.Round(center + (width / 2F));

                    if (destinationX > 0)
                    {
                        destinationX = 0;
                    }

                    if (destinationX < (int)MathF.Round(width - (sourceWidth * ratio)))
                    {
                        destinationX = (int)MathF.Round(width - (sourceWidth * ratio));
                    }
                }
                else
                {
                    switch (options.Position)
                    {
                        case AnchorPositionMode.Left:
                        case AnchorPositionMode.TopLeft:
                        case AnchorPositionMode.BottomLeft:
                            destinationX = 0;
                            break;
                        case AnchorPositionMode.Right:
                        case AnchorPositionMode.TopRight:
                        case AnchorPositionMode.BottomRight:
                            destinationX = (int)MathF.Round(width - (sourceWidth * ratio));
                            break;
                        default:
                            destinationX = (int)MathF.Round((width - (sourceWidth * ratio)) / 2F);
                            break;
                    }
                }

                destinationWidth = (int)MathF.Ceiling(sourceWidth * percentHeight);
            }

            return (new Size(width, height), new Rectangle(destinationX, destinationY, destinationWidth, destinationHeight));
        }

        private static (Size, Rectangle) CalculatePadRectangle(Size source, ResizeOptions options, int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return (new Size(source.Width, source.Height), new Rectangle(0, 0, source.Width, source.Height));
            }

            float ratio;
            int sourceWidth = source.Width;
            int sourceHeight = source.Height;

            int destinationX = 0;
            int destinationY = 0;
            int destinationWidth = width;
            int destinationHeight = height;

            // Fractional variants for preserving aspect ratio.
            float percentHeight = MathF.Abs(height / (float)sourceHeight);
            float percentWidth = MathF.Abs(width / (float)sourceWidth);

            if (percentHeight < percentWidth)
            {
                ratio = percentHeight;
                destinationWidth = (int)MathF.Round(sourceWidth * percentHeight);

                switch (options.Position)
                {
                    case AnchorPositionMode.Left:
                    case AnchorPositionMode.TopLeft:
                    case AnchorPositionMode.BottomLeft:
                        destinationX = 0;
                        break;
                    case AnchorPositionMode.Right:
                    case AnchorPositionMode.TopRight:
                    case AnchorPositionMode.BottomRight:
                        destinationX = (int)MathF.Round(width - (sourceWidth * ratio));
                        break;
                    default:
                        destinationX = (int)MathF.Round((width - (sourceWidth * ratio)) / 2F);
                        break;
                }
            }
            else
            {
                ratio = percentWidth;
                destinationHeight = (int)MathF.Round(sourceHeight * percentWidth);

                switch (options.Position)
                {
                    case AnchorPositionMode.Top:
                    case AnchorPositionMode.TopLeft:
                    case AnchorPositionMode.TopRight:
                        destinationY = 0;
                        break;
                    case AnchorPositionMode.Bottom:
                    case AnchorPositionMode.BottomLeft:
                    case AnchorPositionMode.BottomRight:
                        destinationY = (int)MathF.Round(height - (sourceHeight * ratio));
                        break;
                    default:
                        destinationY = (int)MathF.Round((height - (sourceHeight * ratio)) / 2F);
                        break;
                }
            }

            return (new Size(width, height), new Rectangle(destinationX, destinationY, destinationWidth, destinationHeight));
        }

        private static (Size, Rectangle) CalculateBoxPadRectangle(Size source, ResizeOptions options, int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return (new Size(source.Width, source.Height), new Rectangle(0, 0, source.Width, source.Height));
            }

            int sourceWidth = source.Width;
            int sourceHeight = source.Height;

            // Fractional variants for preserving aspect ratio.
            float percentHeight = MathF.Abs(height / (float)sourceHeight);
            float percentWidth = MathF.Abs(width / (float)sourceWidth);

            int boxPadHeight = height > 0 ? height : (int)MathF.Round(sourceHeight * percentWidth);
            int boxPadWidth = width > 0 ? width : (int)MathF.Round(sourceWidth * percentHeight);

            // Only calculate if upscaling.
            if (sourceWidth < boxPadWidth && sourceHeight < boxPadHeight)
            {
                int destinationX;
                int destinationY;
                int destinationWidth = sourceWidth;
                int destinationHeight = sourceHeight;
                width = boxPadWidth;
                height = boxPadHeight;

                switch (options.Position)
                {
                    case AnchorPositionMode.Left:
                        destinationY = (height - sourceHeight) / 2;
                        destinationX = 0;
                        break;
                    case AnchorPositionMode.Right:
                        destinationY = (height - sourceHeight) / 2;
                        destinationX = width - sourceWidth;
                        break;
                    case AnchorPositionMode.TopRight:
                        destinationY = 0;
                        destinationX = width - sourceWidth;
                        break;
                    case AnchorPositionMode.Top:
                        destinationY = 0;
                        destinationX = (width - sourceWidth) / 2;
                        break;
                    case AnchorPositionMode.TopLeft:
                        destinationY = 0;
                        destinationX = 0;
                        break;
                    case AnchorPositionMode.BottomRight:
                        destinationY = height - sourceHeight;
                        destinationX = width - sourceWidth;
                        break;
                    case AnchorPositionMode.Bottom:
                        destinationY = height - sourceHeight;
                        destinationX = (width - sourceWidth) / 2;
                        break;
                    case AnchorPositionMode.BottomLeft:
                        destinationY = height - sourceHeight;
                        destinationX = 0;
                        break;
                    default:
                        destinationY = (height - sourceHeight) / 2;
                        destinationX = (width - sourceWidth) / 2;
                        break;
                }

                return (new Size(width, height), new Rectangle(destinationX, destinationY, destinationWidth, destinationHeight));
            }

            // Switch to pad mode to downscale and calculate from there.
            return CalculatePadRectangle(source, options, width, height);
        }

        private static (Size, Rectangle) CalculateMaxRectangle(Size source, ResizeOptions options, int width, int height)
        {
            int destinationWidth = width;
            int destinationHeight = height;

            // Fractional variants for preserving aspect ratio.
            float percentHeight = MathF.Abs(height / (float)source.Height);
            float percentWidth = MathF.Abs(width / (float)source.Width);

            // Integers must be cast to floats to get needed precision
            float ratio = options.Size.Height / (float)options.Size.Width;
            float sourceRatio = source.Height / (float)source.Width;

            if (sourceRatio < ratio)
            {
                destinationHeight = (int)MathF.Round(source.Height * percentWidth);
                height = destinationHeight;
            }
            else
            {
                destinationWidth = (int)MathF.Round(source.Width * percentHeight);
                width = destinationWidth;
            }

            // Replace the size to match the rectangle.
            return (new Size(width, height), new Rectangle(0, 0, destinationWidth, destinationHeight));
        }

        private static (Size, Rectangle) CalculateMinRectangle(Size source, ResizeOptions options, int width, int height)
        {
            int sourceWidth = source.Width;
            int sourceHeight = source.Height;
            int destinationWidth;
            int destinationHeight;

            // Don't upscale
            if (width > sourceWidth || height > sourceHeight)
            {
                return (new Size(sourceWidth, sourceWidth), new Rectangle(0, 0, sourceWidth, sourceHeight));
            }

            // Fractional variants for preserving aspect ratio.
            float percentHeight = MathF.Abs(height / (float)sourceHeight);
            float percentWidth = MathF.Abs(width / (float)sourceWidth);

            float sourceRatio = (float)sourceHeight / sourceWidth;

            // Find the shortest distance to go.
            int widthDiff = sourceWidth - width;
            int heightDiff = sourceHeight - height;

            if (widthDiff < heightDiff)
            {
                destinationHeight = (int)MathF.Round(width * sourceRatio);
                height = destinationHeight;
                destinationWidth = width;
            }
            else if (widthDiff > heightDiff)
            {
                destinationWidth = (int)MathF.Round(height / sourceRatio);
                destinationHeight = height;
                width = destinationWidth;
            }
            else
            {
                if (height > width)
                {
                    destinationWidth = width;
                    destinationHeight = (int)MathF.Round(sourceHeight * percentWidth);
                    height = destinationHeight;
                }
                else
                {
                    destinationHeight = height;
                    destinationWidth = (int)MathF.Round(sourceWidth * percentHeight);
                    width = destinationWidth;
                }
            }

            // Replace the size to match the rectangle.
            return (new Size(width, height), new Rectangle(0, 0, destinationWidth, destinationHeight));
        }
    }
}
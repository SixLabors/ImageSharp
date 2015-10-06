// <copyright file="ImageFilterExtensions.cs" company="James South">
// Copyright © James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Samplers
{
    /// <summary>
    /// Exstensions methods for <see cref="Image"/> to apply samplers to the image.
    /// </summary>
    public static class ImageSampleExtensions
    {
        public static Image Resize(this Image source, int width, int height)
        {
            return source.Process(width, height, default(Rectangle), default(Rectangle), new Resize(new BicubicResampler(), width, height));
        }

        public static Image Resize(this Image source, int width, int height, IResampler sampler)
        {
            return source.Process(width, height, default(Rectangle), default(Rectangle), new Resize(sampler, width, height));
        }

        public static Image Resize(this Image source, int width, int height, IResampler sampler, Rectangle sourceRectangle, Rectangle targetRectangle)
        {
            return source.Process(width, height, sourceRectangle, targetRectangle, new Resize(sampler, width, height));
        }
    }
}

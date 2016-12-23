// <copyright file="GenericFactory.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.TestUtilities
{
    using System;

    /// <summary>
    /// Utility class to create specialized subclasses generic classes (eg. <see cref="Image"/>)
    /// </summary>
    public class GenericFactory<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        public virtual Image<TColor> CreateImage(int width, int height)
        {
            return new Image<TColor>(width, height);
        }

        public virtual Image<TColor> CreateImage(byte[] bytes)
        {
            return new Image<TColor>(bytes);
        }

        public virtual PixelArea<TColor> CreatePixelArea(int width, int height, ComponentOrder componentOrder)
        {
            return new PixelArea<TColor>(width, height, componentOrder);
        }
    }

    public class DefaultImageClassSpecificFactory : GenericFactory<Color>
    {
        public override Image<Color> CreateImage(byte[] bytes) => new Image(bytes);

        public override Image<Color> CreateImage(int width, int height) => new Image(width, height);

        public override PixelArea<Color> CreatePixelArea(int width, int height, ComponentOrder componentOrder)
            => new PixelArea<Color>(width, height, componentOrder);
    }
}
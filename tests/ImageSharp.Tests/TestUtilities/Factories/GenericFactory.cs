// <copyright file="GenericFactory.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;

    /// <summary>
    /// Utility class to create specialized subclasses of generic classes (eg. <see cref="Image"/>)
    /// Used as parameter for <see cref="WithMemberFactoryAttribute"/> -based factory methods
    /// </summary>
    public class GenericFactory<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        public virtual Image<TPixel> CreateImage(int width, int height)
        {
            return new Image<TPixel>(width, height);
        }

        public virtual Image<TPixel> CreateImage(byte[] bytes)
        {
            return Image.Load<TPixel>(bytes);
        }

        public virtual Image<TPixel> CreateImage(Image<TPixel> other)
        {
            return new Image<TPixel>(other);
        }
    }
}
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
    public class GenericFactory<TColor>
        where TColor : struct, IPixel<TColor>
    {
        public virtual Image<TColor> CreateImage(int width, int height)
        {
            return new Image<TColor>(width, height);
        }

        public virtual Image<TColor> CreateImage(byte[] bytes)
        {
            return new Image<TColor>(bytes);
        }

        public virtual Image<TColor> CreateImage(Image<TColor> other)
        {
            return new Image<TColor>(other);
        }
    }
}
// <copyright file="LambdaProvider.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;

    /// <summary>
    /// Provides <see cref="Image{TPixel}" /> instances for parametric unit tests.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format of the image</typeparam>
    public abstract partial class TestImageProvider<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private class LambdaProvider : TestImageProvider<TPixel>
        {
            private readonly Func<GenericFactory<TPixel>, Image<TPixel>> creator;

            public LambdaProvider(Func<GenericFactory<TPixel>, Image<TPixel>> creator)
            {
                this.creator = creator;
            }

            public override Image<TPixel> GetImage() => this.creator(this.Factory);
        }
    }
}
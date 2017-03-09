// <copyright file="LambdaProvider.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;

    /// <summary>
    /// Provides <see cref="Image{TColor}" /> instances for parametric unit tests.
    /// </summary>
    /// <typeparam name="TColor">The pixel format of the image</typeparam>
    public abstract partial class TestImageProvider<TColor>
        where TColor : struct, IPixel<TColor>
    {
        private class LambdaProvider : TestImageProvider<TColor>
        {
            private readonly Func<GenericFactory<TColor>, Image<TColor>> creator;

            public LambdaProvider(Func<GenericFactory<TColor>, Image<TColor>> creator)
            {
                this.creator = creator;
            }

            public override Image<TColor> GetImage() => this.creator(this.Factory);
        }
    }
}
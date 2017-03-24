// <copyright file="IColorConversion.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Colors.Conversion
{
    /// <summary>
    /// Converts color between two color spaces.
    /// </summary>
    /// <typeparam name="T">The input color type.</typeparam>
    /// <typeparam name="TResult">The result color type.</typeparam>
    public interface IColorConversion<in T, out TResult>
    {
        /// <summary>
        /// Performs the conversion from the <see cref="T"/> input to an instance of the output <see cref="TResult"/> type.
        /// </summary>
        /// <param name="input">The input color instance.</param>
        /// <returns>The <see cref="TResult"/></returns>
        TResult Convert(T input);
    }
}
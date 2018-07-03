// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <summary>
    /// Converts color between two color spaces.
    /// </summary>
    /// <typeparam name="T">The input color type.</typeparam>
    /// <typeparam name="TResult">The result color type.</typeparam>
    internal interface IColorConversion<T, TResult>
        where T : struct
        where TResult : struct
    {
        /// <summary>
        /// Performs the conversion from the input to an instance of the output type.
        /// </summary>
        /// <param name="input">The input color instance.</param>
        /// <returns>The converted result</returns>
        TResult Convert(in T input);
    }
}
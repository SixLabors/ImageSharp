// <copyright file="IccConverter.Profile.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.ColorSpaces.Conversion.Implementation.Icc
{
    /// <summary>
    /// Color converter for ICC profiles
    /// </summary>
    internal abstract partial class IccConverterBase
    {
        private enum ConversionMethod
        {
            A0,
            A1,
            A2,
            D0,
            D1,
            D2,
            D3,
            ColorTrc,
            GrayTrc,
            Invalid,
        }
    }
}

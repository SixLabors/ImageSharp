// <copyright file="IccStandardObserver.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    /// <summary>
    /// Standard Observer
    /// </summary>
    internal enum IccStandardObserver : uint
    {
        /// <summary>
        /// Unknown observer
        /// </summary>
        Unkown = 0,

        /// <summary>
        /// CIE 1931 observer
        /// </summary>
        Cie1931Observer = 1,

        /// <summary>
        /// CIE 1964 observer
        /// </summary>
        Cie1964Observer = 2,
    }
}

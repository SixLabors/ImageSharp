// <copyright file="ThrowHelper.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Helps removing exception throwing code from hot path by providing non-inlined exception thrower methods.
    /// </summary>
    internal static class ThrowHelper
    {
        /// <summary>
        /// Throws an <see cref="ArgumentNullException"/>
        /// </summary>
        /// <param name="paramName">The parameter name</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgumentNullException(string paramName)
        {
            throw new ArgumentNullException(nameof(paramName));
        }
    }
}
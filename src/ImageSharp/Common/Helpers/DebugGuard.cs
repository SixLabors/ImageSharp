// <copyright file="DebugGuard.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// Provides methods to protect against invalid parameters for a DEBUG build.
    /// </summary>
    [DebuggerStepThrough]
    internal static class DebugGuard
    {
        /// <summary>
        /// Verifies, that the method parameter with specified object value is not null
        /// and throws an exception if it is found to be so.
        /// </summary>
        /// <param name="target">The target object, which cannot be null.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null</exception>
        [Conditional("DEBUG")]
        public static void NotNull(object target, string parameterName)
        {
            if (target == null)
            {
                throw new ArgumentNullException(parameterName);
            }
        }
    }
}
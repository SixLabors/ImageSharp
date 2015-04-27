// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Utils.cs" company="James South">
//   Copyright © James South and contributors.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   General utility methods.
//   TODO: I don't like having classes like this as they turn into a dumping ground. Investigate method usage.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor
{
    /// <summary>
    /// General utility methods.
    /// TODO: I don't like having classes like this as they turn into a dumping ground. Investigate method usage.
    /// </summary>
    internal static class Utils
    {
        /// <summary>
        /// Swaps two references.
        /// </summary>
        /// <typeparam name="TRef">The type of the references to swap.</typeparam>
        /// <param name="lhs">The first reference.</param>
        /// <param name="rhs">The second reference.</param>
        public static void Swap<TRef>(ref TRef lhs, ref TRef rhs) where TRef : class
        {
            TRef tmp = lhs;

            lhs = rhs;
            rhs = tmp;
        }
    }
}

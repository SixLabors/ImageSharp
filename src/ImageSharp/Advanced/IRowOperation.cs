// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Advanced
{
    /// <summary>
    /// Defines the contract for an action that operates on a row.
    /// </summary>
    public interface IRowOperation
    {
        /// <summary>
        /// Invokes the method passing the row y coordinate.
        /// </summary>
        /// <param name="y">The row y coordinate.</param>
        void Invoke(int y);
    }
}

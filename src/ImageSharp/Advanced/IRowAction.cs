// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Advanced
{
    /// <summary>
    /// Defines the contract for an action that operates on a row.
    /// </summary>
    public interface IRowAction
    {
        /// <summary>
        /// Invokes the method passing the row y coordinate.
        /// </summary>
        /// <param name="y">The row y coordinate.</param>
        void Invoke(int y);
    }

    /// <summary>
    /// A <see langword="struct"/> that wraps a value delegate of a specified type, and info on the memory areas to process
    /// </summary>
    /// <typeparam name="T">The type of value delegate to invoke</typeparam>
    internal readonly struct WrappingRowAction<T>
        where T : struct, IRowAction
    {
        public readonly int MinY;
        public readonly int MaxY;
        public readonly int StepY;
        public readonly int MaxX;

        private readonly T action;

        [MethodImpl(InliningOptions.ShortMethod)]
        public WrappingRowAction(int minY, int maxY, int stepY, in T action)
            : this(minY, maxY, stepY, 0, action)
        {
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public WrappingRowAction(int minY, int maxY, int stepY, int maxX, in T action)
        {
            this.MinY = minY;
            this.MaxY = maxY;
            this.StepY = stepY;
            this.MaxX = maxX;
            this.action = action;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public void Invoke(int i)
        {
            int yMin = this.MinY + (i * this.StepY);

            if (yMin >= this.MaxY)
            {
                return;
            }

            int yMax = Math.Min(yMin + this.StepY, this.MaxY);

            for (int y = yMin; y < yMax; y++)
            {
                // Skip the safety copy when invoking a potentially impure method on a readonly field
                Unsafe.AsRef(this.action).Invoke(y);
            }
        }
    }
}

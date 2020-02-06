// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Advanced
{
    /// <summary>
    /// Defines the contract for an action that operates on a row interval.
    /// </summary>
    public interface IRowIntervalAction
    {
        /// <summary>
        /// Invokes the method passing the row interval.
        /// </summary>
        /// <param name="rows">The row interval.</param>
        void Invoke(in RowInterval rows);
    }

    internal readonly struct WrappingRowIntervalInfo
    {
        public readonly int MinY;
        public readonly int MaxY;
        public readonly int StepY;
        public readonly int MaxX;

        public WrappingRowIntervalInfo(int minY, int maxY, int stepY)
            : this(minY, maxY, stepY, 0)
        {
        }

        public WrappingRowIntervalInfo(int minY, int maxY, int stepY, int maxX)
        {
            this.MinY = minY;
            this.MaxY = maxY;
            this.StepY = stepY;
            this.MaxX = maxX;
        }
    }

    internal readonly struct WrappingRowIntervalAction<T>
        where T : struct, IRowIntervalAction
    {
        private readonly WrappingRowIntervalInfo info;
        private readonly T action;

        [MethodImpl(InliningOptions.ShortMethod)]
        public WrappingRowIntervalAction(in WrappingRowIntervalInfo info, in T action)
        {
            this.info = info;
            this.action = action;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public void Invoke(int i)
        {
            int yMin = this.info.MinY + (i * this.info.StepY);

            if (yMin >= this.info.MaxY)
            {
                return;
            }

            int yMax = Math.Min(yMin + this.info.StepY, this.info.MaxY);
            var rows = new RowInterval(yMin, yMax);

            this.action.Invoke(in rows);
        }
    }
}

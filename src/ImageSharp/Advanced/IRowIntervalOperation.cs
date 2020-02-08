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
    public interface IRowIntervalOperation
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

    internal readonly struct WrappingRowIntervalOperation
    {
        private readonly WrappingRowIntervalInfo info;
        private readonly Action<RowInterval> action;

        [MethodImpl(InliningOptions.ShortMethod)]
        public WrappingRowIntervalOperation(in WrappingRowIntervalInfo info, Action<RowInterval> action)
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

            this.action(rows);
        }
    }

    internal readonly struct WrappingRowIntervalOperation<T>
        where T : struct, IRowIntervalOperation
    {
        private readonly WrappingRowIntervalInfo info;
        private readonly T operation;

        [MethodImpl(InliningOptions.ShortMethod)]
        public WrappingRowIntervalOperation(in WrappingRowIntervalInfo info, in T operation)
        {
            this.info = info;
            this.operation = operation;
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

            // Skip the safety copy when invoking a potentially impure method on a readonly field
            Unsafe.AsRef(this.operation).Invoke(in rows);
        }
    }
}

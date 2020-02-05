// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Advanced.ParallelUtils
{
    /// <summary>
    /// Defines the contract for.
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
        public readonly int Min;
        public readonly int Max;
        public readonly int Step;

        public WrappingRowIntervalInfo(int min, int max, int step)
        {
            this.Min = min;
            this.Max = max;
            this.Step = step;
        }
    }

    internal readonly struct WrappingRowIntervalAction<T> : IRowIntervalAction
        where T : struct, IRowIntervalAction
    {
        private readonly WrappingRowIntervalInfo info;
        private readonly T action;

        public WrappingRowIntervalAction(in WrappingRowIntervalInfo info, ref T action)
        {
            this.info = info;
            this.action = action;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public void Invoke(int i)
        {
            int yMin = this.info.Min + (i * this.info.Step);

            if (yMin >= this.info.Max)
            {
                return;
            }

            int yMax = Math.Min(yMin + this.info.Step, this.info.Max);

            var rows = new RowInterval(yMin, yMax);

            this.Invoke(in rows);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public void Invoke(in RowInterval rows) => this.action.Invoke(in rows);
    }
}

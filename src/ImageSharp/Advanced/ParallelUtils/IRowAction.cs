// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Advanced.ParallelUtils
{
    /// <summary>
    /// Defines the contract for.
    /// </summary>
    public interface IRowAction
    {
        /// <summary>
        /// Invokes the method passing the row interval.
        /// </summary>
        /// <param name="rows">The row interval.</param>
        void Invoke(in RowInterval rows);
    }

    internal readonly struct WrappingRowAction<T> : IRowAction
        where T : struct, IRowAction
    {
        private readonly T action;

        public WrappingRowAction(ref T action)
        {
            this.action = action;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public void Invoke(in RowInterval rows)
        {
            this.action.Invoke(in rows);
        }
    }
}

// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    /// <summary>
    /// The collection of lookup tables used for fast AC entropy scan decoding.
    /// </summary>
    internal sealed class FastACTables
    {
        private readonly FastACTable[] tables = new FastACTable[4];

        /// <summary>
        /// Gets the table at the given index.
        /// </summary>
        /// <param name="index">The index</param>
        /// <returns>The <see cref="FastACTable"/></returns>
        public ref FastACTable this[int index]
        {
            [MethodImpl(InliningOptions.ShortMethod)]
            get => ref this.tables[index];
        }
    }
}
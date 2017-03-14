// <copyright file="ITiffGenDataSource.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.Collections.Generic;

    /// <summary>
    /// An interface for any class within the Tiff generator that produces data to be included in the file.
    /// </summary>
    internal interface ITiffGenDataSource
    {
        IEnumerable<TiffGenDataBlock> GetData(bool isLittleEndian);
    }
}
// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Tests
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
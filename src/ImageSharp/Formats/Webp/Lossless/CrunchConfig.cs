// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.Webp.Lossless
{
    internal class CrunchConfig
    {
        public EntropyIx EntropyIdx { get; set; }

        public List<CrunchSubConfig> SubConfigs { get; } = new List<CrunchSubConfig>();
    }
}

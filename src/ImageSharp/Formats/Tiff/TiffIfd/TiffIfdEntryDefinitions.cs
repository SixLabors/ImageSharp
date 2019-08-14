// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    internal static class TiffIfdEntryDefinitions
    {
        public const TiffResolutionUnit DefaultResolutionUnit = TiffResolutionUnit.Inch;
        public const TiffPlanarConfiguration DefaultPlanarConfiguration = TiffPlanarConfiguration.Chunky;

        public static readonly Dictionary<TiffTagId, string> MetadataTags = new Dictionary<TiffTagId, string>
        {
            { TiffTagId.Artist, "Artist" },
            { TiffTagId.Copyright, "Copyright" },
            { TiffTagId.DateTime, "DateTime" },
            { TiffTagId.HostComputer, "HostComputer" },
            { TiffTagId.ImageDescription, "ImageDescription" },
            { TiffTagId.Make, "Make" },
            { TiffTagId.Model, "Model" },
            { TiffTagId.Software, "Software" },
        };
    }
}

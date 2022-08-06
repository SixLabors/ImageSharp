// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Common.Helpers
{
    internal readonly struct ExifResolutionValues
    {
        public ExifResolutionValues(ushort resolutionUnit, double? horizontalResolution, double? verticalResolution)
        {
            this.ResolutionUnit = resolutionUnit;
            this.HorizontalResolution = horizontalResolution;
            this.VerticalResolution = verticalResolution;
        }

        public ushort ResolutionUnit { get; }

        public double? HorizontalResolution { get; }

        public double? VerticalResolution { get; }
    }
}

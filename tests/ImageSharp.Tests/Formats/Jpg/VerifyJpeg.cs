namespace SixLabors.ImageSharp.Tests
{
    using System.Collections.Generic;
    using System.Linq;

    using SixLabors.ImageSharp.Formats.Jpeg.Common;

    using Xunit;

    internal static class VerifyJpeg
    {
        internal static void ComponentSize(IJpegComponent component, int expectedBlocksX, int expectedBlocksY)
        {
            Assert.Equal(component.WidthInBlocks, expectedBlocksX);
            Assert.Equal(component.HeightInBlocks, expectedBlocksY);
        }

        internal static void Components3(
            IEnumerable<IJpegComponent> components,
            int xBc0, int yBc0,
            int xBc1, int yBc1,
            int xBc2, int yBc2)
        {
            IJpegComponent[] c = components.ToArray();
            Assert.Equal(3, components.Count());

            ComponentSize(c[0], xBc0, yBc0);
            ComponentSize(c[1], xBc1, yBc1);
            ComponentSize(c[2], xBc2, yBc2);
        }
    }
}
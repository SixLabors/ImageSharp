// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using SixLabors.ImageSharp.Formats.Jpeg;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    public class JpegMetadataTests
    {
        [Fact]
        public void CloneIsDeep()
        {
            var meta = new JpegMetadata { Quality = 50 };
            var clone = (JpegMetadata)meta.DeepClone();

            clone.Quality = 99;

            Assert.False(meta.Quality.Equals(clone.Quality));
        }
    }
}

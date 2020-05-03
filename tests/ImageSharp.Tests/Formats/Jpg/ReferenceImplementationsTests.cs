// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using Xunit.Abstractions;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    using SixLabors.ImageSharp.Tests.Formats.Jpg.Utils;

    public partial class ReferenceImplementationsTests : JpegFixture
    {
        public ReferenceImplementationsTests(ITestOutputHelper output)
            : base(output)
        {
        }
    }
}
// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using SixLabors.ImageSharp.Formats;

namespace SixLabors.ImageSharp.Tests
{
    /// <summary>
    ///  You can't mock the "DetectFormat" method due to the  ReadOnlySpan{byte} parameter.
    /// </summary>
    public class MockImageFormatDetector : IImageFormatDetector
    {
        private IImageFormat localImageFormatMock;

        public MockImageFormatDetector(IImageFormat imageFormat)
        {
            this.localImageFormatMock = imageFormat;
        }

        public int HeaderSize => 1;

        public IImageFormat DetectFormat(ReadOnlySpan<byte> header)
        {
            return this.localImageFormatMock;
        }
    }
}

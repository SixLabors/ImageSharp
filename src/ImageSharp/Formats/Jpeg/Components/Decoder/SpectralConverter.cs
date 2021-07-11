// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    internal abstract class SpectralConverter : IDisposable
    {
        public abstract void InjectFrameData(JpegFrame frame, IRawJpegData jpegData);

        public abstract void ConvertStrideBaseline();

        public abstract void Dispose();
    }
}

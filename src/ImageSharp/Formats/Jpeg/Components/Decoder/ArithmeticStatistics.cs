// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    internal class ArithmeticStatistics
    {
        private readonly byte[] statistics;

        public ArithmeticStatistics(bool dc, int identifier)
        {
            this.IsDcStatistics = dc;
            this.Identifier = identifier;
            this.statistics = dc ? new byte[64] : new byte[256];
        }

        public bool IsDcStatistics { get; private set; }

        public int Identifier { get; private set; }

        public ref byte GetReference() => ref this.statistics[0];

        public ref byte GetReference(int offset) => ref this.statistics[offset];

        public void Reset() => this.statistics.AsSpan().Clear();
    }
}

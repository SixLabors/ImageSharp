// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal readonly struct Av1ScanOrder
{
    public Av1ScanOrder(short[] scan)
    {
        this.Scan = scan;
        this.InverseScan = [];
        this.Neighbors = [];
    }

    public Av1ScanOrder(short[] scan, short[] inverseScan, short[] neighbors)
    {
        this.Scan = scan;
        this.InverseScan = inverseScan;
        this.Neighbors = neighbors;
    }

    public short[] Scan { get; }

    public short[] InverseScan { get; }

    public short[] Neighbors { get; }
}

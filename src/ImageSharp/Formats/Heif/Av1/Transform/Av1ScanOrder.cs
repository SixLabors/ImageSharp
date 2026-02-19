// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal readonly struct Av1ScanOrder
{
    private readonly short[] scan;
    private readonly short[] inverseScan;
    private readonly short[] neighbors;

    public Av1ScanOrder(short[] scan)
    {
        this.scan = scan;
        this.inverseScan = [];
        this.neighbors = [];
    }

    public Av1ScanOrder(short[] scan, short[] inverseScan, short[] neighbors)
    {
        this.scan = scan;
        this.inverseScan = inverseScan;
        this.neighbors = neighbors;
    }

    public ReadOnlySpan<short> Scan => this.scan;

    public ReadOnlySpan<short> InverseScan => this.inverseScan;

    public ReadOnlySpan<short> Neighbors => this.neighbors;
}

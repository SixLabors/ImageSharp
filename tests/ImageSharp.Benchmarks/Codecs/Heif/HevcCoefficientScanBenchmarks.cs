// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Heif.Hevc;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Heif;

/// <summary>
/// Measures full-frame HEVC grouped coefficient-scan generation into reusable transform scratch.
/// </summary>
[MemoryDiagnoser(displayGenColumns: false)]
public class HevcCoefficientScanBenchmarks
{
    /// <summary>
    /// The coded frame width, which is an exact multiple of the maximum transform-block side.
    /// </summary>
    private const int Width = 1920;

    /// <summary>
    /// The coded frame height including the final padded coding-tree row for a 1080-line presentation.
    /// </summary>
    private const int Height = 1088;

    /// <summary>
    /// The maximum transform-block side.
    /// </summary>
    private const int BlockSize = 32;

    /// <summary>
    /// The reusable maximum-size grouped scan destination.
    /// </summary>
    private readonly int[] scan = new int[BlockSize * BlockSize];

    /// <summary>
    /// Measures diagonal scan generation for every maximum-size transform block in one coded full-HD frame.
    /// </summary>
    /// <returns>The final last-significant scan position, keeping the generated scan observable.</returns>
    [Benchmark]
    public int WriteDiagonalFrame()
    {
        int lastScanPosition = 0;
        for (int y = 0; y < Height; y += BlockSize)
        {
            for (int x = 0; x < Width; x += BlockSize)
            {
                lastScanPosition = HevcCoefficientScanOrder.Write(
                    this.scan,
                    BlockSize,
                    BlockSize,
                    HevcCoefficientScanType.Diagonal,
                    (BlockSize * BlockSize) - 1);
            }
        }

        return lastScanPosition;
    }
}

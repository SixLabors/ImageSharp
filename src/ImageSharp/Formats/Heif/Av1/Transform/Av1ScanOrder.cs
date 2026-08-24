// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Describes the forward scan, inverse scan, and entropy-neighbor mapping for an AV1 transform size.
/// </summary>
internal readonly struct Av1ScanOrder
{
    /// <summary>
    /// The coefficient positions in coded traversal order.
    /// </summary>
    private readonly short[] scan;

    /// <summary>
    /// The coded position of each raster-order coefficient.
    /// </summary>
    private readonly short[] inverseScan;

    /// <summary>
    /// The coefficient-neighbor mapping used to derive entropy contexts.
    /// </summary>
    private readonly short[] neighbors;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1ScanOrder"/> struct when only coefficient traversal is required.
    /// </summary>
    /// <param name="scan">The coefficient positions in coded traversal order.</param>
    public Av1ScanOrder(short[] scan)
    {
        this.scan = scan;
        this.inverseScan = [];
        this.neighbors = [];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1ScanOrder"/> struct with complete entropy-context mappings.
    /// </summary>
    /// <param name="scan">The coefficient positions in coded traversal order.</param>
    /// <param name="inverseScan">The coded position of each raster-order coefficient.</param>
    /// <param name="neighbors">The coefficient neighbors used to derive entropy contexts.</param>
    public Av1ScanOrder(short[] scan, short[] inverseScan, short[] neighbors)
    {
        this.scan = scan;
        this.inverseScan = inverseScan;
        this.neighbors = neighbors;
    }

    /// <summary>
    /// Gets the coefficient positions in coded traversal order.
    /// </summary>
    public ReadOnlySpan<short> Scan => this.scan;

    /// <summary>
    /// Gets the coded position of each raster-order coefficient.
    /// </summary>
    public ReadOnlySpan<short> InverseScan => this.inverseScan;

    /// <summary>
    /// Gets the coefficient-neighbor mapping used for entropy contexts.
    /// </summary>
    public ReadOnlySpan<short> Neighbors => this.neighbors;
}

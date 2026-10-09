// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using static SixLabors.ImageSharp.Formats.Heif.Av1.Motion.Av1MotionSearchSettings;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <summary>
/// Borrows one retained search-site configuration, including stride-relative sample offsets.
/// </summary>
internal readonly ref struct Av1MotionSearchSites
{
    /// <summary>
    /// The number of integer storage elements for sites, stage metadata, and the configured stride.
    /// </summary>
    public const int StorageLength = (22 * 17 * 2) + (22 * 2) + 2;

    /// <summary>
    /// The greatest number of stages in one configuration.
    /// </summary>
    private const int StageCapacity = 22;

    /// <summary>
    /// The number of site slots in each stage, including the center site.
    /// </summary>
    private const int SitesPerStage = 17;

    /// <summary>
    /// The number of integers that hold the sites. Each site takes two integers.
    /// </summary>
    private const int SiteStorageLength = StageCapacity * SitesPerStage * 2;

    /// <summary>
    /// The index of the stage count. The configured stride follows it.
    /// </summary>
    private const int StageCountOffset = SiteStorageLength + (StageCapacity * 2);

    /// <summary>
    /// The borrowed storage. It holds the sites, then the candidate count and radius of each stage, then the stage count and the stride.
    /// </summary>
    private readonly Span<int> storage;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1MotionSearchSites"/> struct.
    /// </summary>
    /// <param name="storage">The retained configuration storage, whose stride slot is initialized by its owner.</param>
    public Av1MotionSearchSites(Span<int> storage) => this.storage = storage;

    /// <summary>
    /// Gets the number of populated search stages.
    /// </summary>
    public int StageCount => this.storage[StageCountOffset];

    /// <summary>
    /// Gets the number of non-center candidates at the given stage.
    /// </summary>
    /// <param name="stage">The stage, ordered from the smallest search radius.</param>
    /// <returns>The candidate count.</returns>
    public int GetCandidateCount(int stage) => this.storage[SiteStorageLength + stage];

    /// <summary>
    /// Gets the radius of the given stage in full samples.
    /// </summary>
    /// <param name="stage">The stage, ordered from the smallest search radius.</param>
    /// <returns>The search radius.</returns>
    public int GetRadius(int stage) => this.storage[SiteStorageLength + StageCapacity + stage];

    /// <summary>
    /// Gets the ordered candidate sites for a stage.
    /// </summary>
    /// <param name="stage">The stage, ordered from the smallest search radius.</param>
    /// <returns>The fixed stage slot. Only the configured candidate entries hold sites.</returns>
    public ReadOnlySpan<Site> GetSites(int stage)
        => MemoryMarshal.Cast<int, Site>(this.storage[..SiteStorageLength]).Slice(stage * SitesPerStage, SitesPerStage);

    /// <summary>
    /// Initializes or refreshes offsets when the retained reference plane's stride changes.
    /// </summary>
    /// <param name="method">The distinct search shape owned by this configuration.</param>
    /// <param name="stride">The reference plane stride in samples.</param>
    public void Configure(FullPixelSearchMethod method, int stride)
    {
        if (this.storage[StageCountOffset + 1] == stride)
        {
            return;
        }

        Span<Site> sites = MemoryMarshal.Cast<int, Site>(this.storage[..SiteStorageLength]);
        Span<int> counts = this.storage.Slice(SiteStorageLength, StageCapacity);
        Span<int> radii = this.storage.Slice(SiteStorageLength + StageCapacity, StageCapacity);
        bool nStep = method is FullPixelSearchMethod.NStep or FullPixelSearchMethod.EightPointNStep;
        bool diamond = method is FullPixelSearchMethod.Diamond or FullPixelSearchMethod.ClampedDiamond;
        int stageCount = nStep ? (method == FullPixelSearchMethod.NStep ? 15 : 16) : 11;
        int radius = 1;
        for (int stage = 0; stage < stageCount; stage++)
        {
            Span<Site> stageSites = sites.Slice(stage * SitesPerStage, SitesPerStage);
            if (diamond)
            {
                // The clamped shape repeats its three outer stages at radius 256.
                // These stages stay, because a move at one stage permits another move at the same radius.
                radius = 1 << Math.Min(stage, method == FullPixelSearchMethod.ClampedDiamond ? 8 : 10);
            }
            else if (!nStep)
            {
                radius = 1 << stage;
            }

            radii[stage] = radius;
            if (nStep || diamond)
            {
                bool twelveSites = nStep && radius > 5 && method != FullPixelSearchMethod.EightPointNStep;
                int tangent = twelveSites ? Math.Max((int)(0.41 * radius), 1) : radius;
                counts[stage] = twelveSites ? 12 : 8;
                stageSites[0] = new Site(0, 0, stride);
                stageSites[1] = new Site(-radius, 0, stride);
                stageSites[2] = new Site(radius, 0, stride);
                stageSites[3] = new Site(0, -radius, stride);
                stageSites[4] = new Site(0, radius, stride);
                stageSites[5] = new Site(-radius, -tangent, stride);
                stageSites[6] = new Site(radius, tangent, stride);
                stageSites[7] = new Site(-tangent, radius, stride);
                stageSites[8] = new Site(tangent, -radius, stride);
                if (twelveSites)
                {
                    stageSites[9] = new Site(-radius, tangent, stride);
                    stageSites[10] = new Site(radius, -tangent, stride);
                    stageSites[11] = new Site(tangent, radius, stride);
                    stageSites[12] = new Site(-tangent, -radius, stride);
                }

                // Through stage twelve, each N-step radius grows by half, rounded to the nearest sample, and by at least one sample.
                // Later stages keep the outer radius.
                if (nStep && stage < 12)
                {
                    radius = Math.Max(((3 * radius) + 1) / 2, radius + 1);
                }
            }
            else
            {
                // Pattern sites omit the center. Pairs are row then column, in traversal order.
                // After stage zero, the code multiplies these integer coordinates by half the radius.
                ReadOnlySpan<sbyte> coordinates;
                int scale;
                if (method == FullPixelSearchMethod.Hexagon)
                {
                    coordinates = stage == 0
                        ? [-1, -1, 0, -1, 1, -1, 1, 0, 1, 1, 0, 1, -1, 1, -1, 0]
                        : [-1, -2, 1, -2, 2, 0, 1, 2, -1, 2, -2, 0];

                    scale = stage == 0 ? 1 : radius / 2;
                }
                else
                {
                    coordinates = stage == 0
                        ? [0, -1, 1, 0, 0, 1, -1, 0]
                        : [-1, -1, 0, -2, 1, -1, 2, 0, 1, 1, 0, 2, -1, 1, -2, 0];

                    scale = stage == 0 ? 1 : radius / 2;
                }

                counts[stage] = coordinates.Length / 2;
                for (int index = 0; index < counts[stage]; index++)
                {
                    stageSites[index] = new Site(coordinates[index * 2] * scale, coordinates[(index * 2) + 1] * scale, stride);
                }
            }
        }

        this.storage[StageCountOffset] = stageCount;
        this.storage[StageCountOffset + 1] = stride;
    }

    /// <summary>
    /// Writes the first-pass site geometry. It has eleven stages whose radius doubles from one to 1024 samples.
    /// The innermost stage has eight sites, and every other stage has twelve.
    /// </summary>
    /// <remarks>
    /// The method always writes the configuration, because its stride slot cannot tell it apart from a <see cref="Configure"/> result for the same stride.
    /// Thus the owner must use this storage only for this geometry.
    /// </remarks>
    /// <param name="stride">The reference plane stride in samples.</param>
    public void ConfigureFirstPass(int stride)
    {
        const int stageCount = 11;
        Span<Site> sites = MemoryMarshal.Cast<int, Site>(this.storage[..SiteStorageLength]);
        Span<int> counts = this.storage.Slice(SiteStorageLength, StageCapacity);
        Span<int> radii = this.storage.Slice(SiteStorageLength + StageCapacity, StageCapacity);

        // Stage zero holds the unit radius. Each later stage doubles the radius, up to 1024 samples at stage ten.
        // No two stages share a radius, so no stage is a duplicate.
        for (int stage = 0; stage < stageCount; stage++)
        {
            int radius = 1 << stage;

            // The tangent offset places the extra sites near 22.5 degrees.
            // At the unit radius it rounds to the diagonal, and only the first eight sites after the center hold values.
            int tangent = Math.Max((int)(0.41 * radius), 1);
            int count = radius == 1 ? 8 : 12;
            Span<Site> stageSites = sites.Slice(stage * SitesPerStage, SitesPerStage);
            stageSites[0] = new Site(0, 0, stride);
            stageSites[1] = new Site(-radius, 0, stride);
            stageSites[2] = new Site(radius, 0, stride);
            stageSites[3] = new Site(0, -radius, stride);
            stageSites[4] = new Site(0, radius, stride);
            stageSites[5] = new Site(-radius, -tangent, stride);
            stageSites[6] = new Site(radius, tangent, stride);
            stageSites[7] = new Site(-tangent, radius, stride);
            stageSites[8] = new Site(tangent, -radius, stride);
            if (count == 12)
            {
                stageSites[9] = new Site(-radius, tangent, stride);
                stageSites[10] = new Site(radius, -tangent, stride);
                stageSites[11] = new Site(tangent, radius, stride);
                stageSites[12] = new Site(-tangent, -radius, stride);
            }

            counts[stage] = count;
            radii[stage] = radius;
        }

        this.storage[StageCountOffset] = stageCount;
        this.storage[StageCountOffset + 1] = stride;
    }

    /// <summary>
    /// Stores one full-sample displacement and its reference-plane offset in eight bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct Site
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Site"/> struct.
        /// </summary>
        /// <param name="row">The vertical full-sample displacement.</param>
        /// <param name="column">The horizontal full-sample displacement.</param>
        /// <param name="stride">The reference row stride in samples.</param>
        public Site(int row, int column, int stride)
        {
            this.Row = (short)row;
            this.Column = (short)column;
            this.Offset = (row * stride) + column;
        }

        /// <summary>
        /// Gets the vertical full-sample displacement.
        /// </summary>
        public short Row { get; }

        /// <summary>
        /// Gets the horizontal full-sample displacement.
        /// </summary>
        public short Column { get; }

        /// <summary>
        /// Gets the signed displacement as a sample index offset in the reference plane.
        /// </summary>
        public int Offset { get; }
    }
}

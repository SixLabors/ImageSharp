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

    private const int StageCapacity = 22;
    private const int SitesPerStage = 17;
    private const int SiteStorageLength = StageCapacity * SitesPerStage * 2;
    private const int StageCountOffset = SiteStorageLength + (StageCapacity * 2);
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
    /// <returns>The fixed stage slot; only the configured candidate entries are populated.</returns>
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
                // The clamped shape repeats its three outer stages at radius 256. Retaining those stages
                // matters because a move at one stage permits another move at the same radius.
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

                // N-step radii grow by rounded halves through stage twelve, then retain the outer radius.
                if (nStep && stage < 12)
                {
                    radius = Math.Max(((3 * radius) + 1) / 2, radius + 1);
                }
            }
            else
            {
                // Pattern sites omit the center. Pairs are row then column, in traversal order.
                // Beyond scale zero, multiply the half-radius by these integer coordinates.
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
        /// Gets the signed displacement in reference-plane samples.
        /// </summary>
        public int Offset { get; }
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopRestoration;

/// <content>
/// Specializes projection traversal for the parameter set's active self-guided radii.
/// </content>
internal static partial class Av1LoopRestorationEncoder
{
    /// <summary>
    /// Identifies which fixed-point filtered planes contribute to a projection.
    /// </summary>
    private interface IProjectionOperator
    {
        /// <summary>
        /// Gets a value indicating whether radius-two output contributes.
        /// </summary>
        static abstract bool UsesRadiusTwo { get; }

        /// <summary>
        /// Gets a value indicating whether radius-one output contributes.
        /// </summary>
        static abstract bool UsesRadiusOne { get; }
    }

    /// <summary>
    /// Combines the outputs of both self-guided radii.
    /// </summary>
    private readonly struct DualRadiusProjection : IProjectionOperator
    {
        /// <inheritdoc/>
        public static bool UsesRadiusTwo => true;

        /// <inheritdoc/>
        public static bool UsesRadiusOne => true;
    }

    /// <summary>
    /// Uses only the radius-one self-guided output.
    /// </summary>
    private readonly struct RadiusOneProjection : IProjectionOperator
    {
        /// <inheritdoc/>
        public static bool UsesRadiusTwo => false;

        /// <inheritdoc/>
        public static bool UsesRadiusOne => true;
    }

    /// <summary>
    /// Uses only the radius-two self-guided output.
    /// </summary>
    private readonly struct RadiusTwoProjection : IProjectionOperator
    {
        /// <inheritdoc/>
        public static bool UsesRadiusTwo => true;

        /// <inheritdoc/>
        public static bool UsesRadiusOne => false;
    }
}

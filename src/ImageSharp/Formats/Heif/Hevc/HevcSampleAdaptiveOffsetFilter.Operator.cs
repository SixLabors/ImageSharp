// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

internal static partial class HevcSampleAdaptiveOffsetFilter
{
    /// <summary>
    /// Defines the sample classifier shared by the SIMD row traversal and scalar tail.
    /// </summary>
    private interface ISampleClassifier
    {
        /// <summary>
        /// Gets a value indicating whether classification reads the two neighboring sample rows.
        /// </summary>
        public static abstract bool UsesNeighbors { get; }

        /// <summary>
        /// Classifies thirty-two current samples against their two classifier inputs.
        /// </summary>
        /// <param name="current">The current sample lanes.</param>
        /// <param name="neighbor0">The first neighboring sample lanes.</param>
        /// <param name="neighbor1">The second neighboring sample lanes.</param>
        /// <param name="kernel">The scaled offset and band-class state.</param>
        /// <returns>The zero-based offset-table indices.</returns>
        public static abstract Vector512<short> Classify(
            Vector512<short> current,
            Vector512<short> neighbor0,
            Vector512<short> neighbor1,
            in KernelParameters kernel);

        /// <summary>
        /// Classifies sixteen current samples against their two classifier inputs.
        /// </summary>
        /// <param name="current">The current sample lanes.</param>
        /// <param name="neighbor0">The first neighboring sample lanes.</param>
        /// <param name="neighbor1">The second neighboring sample lanes.</param>
        /// <param name="kernel">The scaled offset and band-class state.</param>
        /// <returns>The zero-based offset-table indices.</returns>
        public static abstract Vector256<short> Classify(
            Vector256<short> current,
            Vector256<short> neighbor0,
            Vector256<short> neighbor1,
            in KernelParameters kernel);

        /// <summary>
        /// Classifies eight current samples against their two classifier inputs.
        /// </summary>
        /// <param name="current">The current sample lanes.</param>
        /// <param name="neighbor0">The first neighboring sample lanes.</param>
        /// <param name="neighbor1">The second neighboring sample lanes.</param>
        /// <param name="kernel">The scaled offset and band-class state.</param>
        /// <returns>The zero-based offset-table indices.</returns>
        public static abstract Vector128<short> Classify(
            Vector128<short> current,
            Vector128<short> neighbor0,
            Vector128<short> neighbor1,
            in KernelParameters kernel);

        /// <summary>
        /// Classifies one current sample against its two classifier inputs.
        /// </summary>
        /// <param name="current">The current sample.</param>
        /// <param name="neighbor0">The first neighboring sample.</param>
        /// <param name="neighbor1">The second neighboring sample.</param>
        /// <param name="kernel">The scaled offset and band-class state.</param>
        /// <returns>The zero-based offset-table index.</returns>
        public static abstract int Classify(short current, short neighbor0, short neighbor1, in KernelParameters kernel);
    }
}

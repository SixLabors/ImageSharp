// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Symbol;

internal static class Av1DefaultDistributions
{
    public static uint[] YMode => [Av1SymbolReader.CdfProbabilityTop, 0];

    public static uint[] UvModeCflNotAllowed => [Av1SymbolReader.CdfProbabilityTop, 0];

    public static uint[] UvModeCflAllowed => [Av1SymbolReader.CdfProbabilityTop, 0];

    public static uint[] AngleDelta => [Av1SymbolReader.CdfProbabilityTop, 0];

    public static uint[] IntraBlockCopy => [30531, 0, 0];

    public static uint[] PartitionWidth8 => [Av1SymbolReader.CdfProbabilityTop, 0];

    public static uint[] PartitionWidth16 => [Av1SymbolReader.CdfProbabilityTop, 0];

    public static uint[] PartitionWidth32 => [Av1SymbolReader.CdfProbabilityTop, 0];

    public static uint[] PartitionWidth64 => [Av1SymbolReader.CdfProbabilityTop, 0];

    public static uint[] SegmentId => [Av1SymbolReader.CdfProbabilityTop, 0];
}

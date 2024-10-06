// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

internal enum ObuMatrixCoefficients
{
    Identity = 0,
    Bt407 = 1,
    Unspecified = 2,
    Fcc = 4,
    Bt470BG = 5,
    Bt601 = 6,
    Smpte240 = 7,
    SmpteYCgCo = 8,
    Bt2020NonConstantLuminance = 9,
    Bt2020ConstantLuminance = 10,
    Smpte2085 = 11,
    ChromaticityDerivedNonConstantLuminance = 12,
    ChromaticityDerivedConstandLuminance = 13,
    Bt2100ICtCp = 14,
}

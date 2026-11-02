// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Entropy;

internal static class JxlAnsConstants
{
    public const int AnsLogTableSize = 12;
    public const int AnsTableSize = 1 << AnsLogTableSize;
    public const int AnsTabMask = AnsTableSize - 1;
    public const int PrefixMaxAlphabetSize = 4096;
    public const int AnsMaxAlphabetSize = 256;
    public const int PrefixMaxBits = 15;
    public const int AnsSignature = 0x13;
}

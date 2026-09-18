// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;

internal static class JxlAcStrategyEncoder
{
    private static readonly byte[][] TypeColors =
    [
        [0xFF, 0xFF, 0x00],  // DCT8       | yellow
        [0xFF, 0x80, 0x80],  // HORNUSS    | vivid tangerine
        [0xFF, 0x80, 0x80],  // DCT2x2     | vivid tangerine
        [0xFF, 0x80, 0x80],  // DCT4x4     | vivid tangerine
        [0x80, 0xFF, 0x00],  // DCT16x16   | chartreuse
        [0x00, 0xC0, 0x00],  // DCT32x32   | waystone green
        [0xC0, 0xFF, 0x00],  // DCT16x8    | lime
        [0xC0, 0xFF, 0x00],  // DCT8x16    | lime
        [0x00, 0xFF, 0x00],  // DCT32x8    | green
        [0x00, 0xFF, 0x00],  // DCT8x32    | green
        [0x00, 0xFF, 0x00],  // DCT32x16   | green
        [0x00, 0xFF, 0x00],  // DCT16x32   | green
        [0xFF, 0x80, 0x00],  // DCT4x8     | orange juice
        [0xFF, 0x80, 0x00],  // DCT8x4     | orange juice
        [0xFF, 0xFF, 0x80],  // AFV0       | butter
        [0xFF, 0xFF, 0x80],  // AFV1       | butter
        [0xFF, 0xFF, 0x80],  // AFV2       | butter
        [0xFF, 0xFF, 0x80],  // AFV3       | butter
        [0x00, 0xC0, 0xFF],  // DCT64x64   | capri
        [0x00, 0xFF, 0xFF],  // DCT64x32   | aqua
        [0x00, 0xFF, 0xFF],  // DCT32x64   | aqua
        [0x00, 0x40, 0xFF],  // DCT128x128 | rare blue
        [0x00, 0x80, 0xFF],  // DCT128x64  | magic ink
        [0x00, 0x80, 0xFF],  // DCT64x128  | magic ink
        [0x00, 0x00, 0xC0],  // DCT256x256 | keese blue
        [0x00, 0x00, 0xFF],  // DCT256x128 | blue
        [0x00, 0x00, 0xFF],  // DCT128x256 | blue
        [0x00, 0x00, 0x00] // invalid    | black
    ];

#pragma warning disable // Indentation warnings only

    private static readonly byte[][] Mask =
    [
        [
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
      ],                           // DCT8
      [
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 1, 0, 0, 1, 0, 0,  //
          0, 0, 1, 0, 0, 1, 0, 0,  //
          0, 0, 1, 1, 1, 1, 0, 0,  //
          0, 0, 1, 0, 0, 1, 0, 0,  //
          0, 0, 1, 0, 0, 1, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
      ],                           // HORNUSS
      [
        1, 1, 1, 1, 1, 1, 1, 1,  //
          1, 0, 1, 0, 1, 0, 1, 0,  //
          1, 1, 1, 1, 1, 1, 1, 1,  //
          1, 0, 1, 0, 1, 0, 1, 0,  //
          1, 1, 1, 1, 1, 1, 1, 1,  //
          1, 0, 1, 0, 1, 0, 1, 0,  //
          1, 1, 1, 1, 1, 1, 1, 1,  //
          1, 0, 1, 0, 1, 0, 1, 0,  //
      ],                           // 2x2
      [
        0, 0, 0, 0, 1, 0, 0, 0,  //
          0, 0, 0, 0, 1, 0, 0, 0,  //
          0, 0, 0, 0, 1, 0, 0, 0,  //
          0, 0, 0, 0, 1, 0, 0, 0,  //
          1, 1, 1, 1, 1, 1, 1, 1,  //
          0, 0, 0, 0, 1, 0, 0, 0,  //
          0, 0, 0, 0, 1, 0, 0, 0,  //
          0, 0, 0, 0, 1, 0, 0, 0,  //
      ],                           // 4x4
      [ ],                          // DCT16x16 (unused)
      [ ],                          // DCT32x32 (unused)
      [ ],                          // DCT16x8 (unused)
      [ ],                          // DCT8x16 (unused)
      [ ],                          // DCT32x8 (unused)
      [ ],                          // DCT8x32 (unused)
      [ ],                          // DCT32x16 (unused)
      [ ],                          // DCT16x32 (unused)
      [
        0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          1, 1, 1, 1, 1, 1, 1, 1,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
      ],                           // DCT4x8
      [
        0, 0, 0, 0, 1, 0, 0, 0,  //
          0, 0, 0, 0, 1, 0, 0, 0,  //
          0, 0, 0, 0, 1, 0, 0, 0,  //
          0, 0, 0, 0, 1, 0, 0, 0,  //
          0, 0, 0, 0, 1, 0, 0, 0,  //
          0, 0, 0, 0, 1, 0, 0, 0,  //
          0, 0, 0, 0, 1, 0, 0, 0,  //
          0, 0, 0, 0, 1, 0, 0, 0,  //
      ],                           // DCT8x4
      [
        1, 1, 1, 1, 1, 0, 0, 0,  //
          1, 1, 1, 1, 0, 0, 0, 0,  //
          1, 1, 1, 0, 0, 0, 0, 0,  //
          1, 1, 0, 0, 0, 0, 0, 0,  //
          1, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
      ],                           // AFV0
      [
    0, 0, 0, 0, 1, 1, 1, 1,  //
          0, 0, 0, 0, 0, 1, 1, 1,  //
          0, 0, 0, 0, 0, 0, 1, 1,  //
          0, 0, 0, 0, 0, 0, 0, 1,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
      ],                           // AFV1
      [
    0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          1, 0, 0, 0, 0, 0, 0, 0,  //
          1, 1, 0, 0, 0, 0, 0, 0,  //
          1, 1, 1, 0, 0, 0, 0, 0,  //
          1, 1, 1, 1, 0, 0, 0, 0,  //
      ],                           // AFV2
      [
    0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 1,  //
          0, 0, 0, 0, 0, 0, 1, 1,  //
          0, 0, 0, 0, 0, 1, 1, 1,  //
      ],                           // AFV3
      [ ]                           // invalid
    ];

#pragma warning restore

    public static ReadOnlySpan<byte> TypeColor(int rawStrategy)
    {
        DebugGuard.IsTrue(JxlAcStrategy.IsRawStrategyValid(rawStrategy), $"Raw strategy must be a valid strategy: {rawStrategy}");
        rawStrategy = Math.Clamp(rawStrategy, 0, JxlAcStrategy.NumberOfValidStrategies);
        return TypeColors[rawStrategy];
    }

    public static ReadOnlySpan<byte> TypeMask(int rawStrategy)
    {
        DebugGuard.IsTrue(JxlAcStrategy.IsRawStrategyValid(rawStrategy), $"Raw strategy must be a valid strategy: {rawStrategy}");
        rawStrategy = Math.Clamp(rawStrategy, 0, JxlAcStrategy.NumberOfValidStrategies);
        return Mask[rawStrategy];
    }

    public static bool MultiBlockTransformCrossesHorizontalBoundary(JxlAcStrategyImage acStrategyImage, int startX, int y, int endX)
    {
        if (startX >= acStrategyImage.XSize || y >= acStrategyImage.YSize)
        {
            return false;
        }

        if (y % 8 == 0)
        {
            // Nothing crosses 64x64 boundaries, and the memory on the other side
            // of the 64x64 block may still uninitialized.
            return false;
        }

        endX = Math.Min(endX, acStrategyImage.XSize);

        // The first multiblock might be before the start_x, let's adjust it
        // to point to the first IsFirstBlock() == true block we find by backward
        // tracing.
        JxlAcStrategyRow row = acStrategyImage.GetRow(y);
        int startXLimit = startX & ~7;

        while (startX != startXLimit && !row[startX].IsFirstBlock)
        {
            startX--;
        }

        for (int x = startX; x < endX;)
        {
            if (row[x].IsFirstBlock)
            {
                x += row[x].CoveredBlocksX;
            }
            else
            {
                return true;
            }
        }

        return false;
    }

    public static bool MultiBlockTransformCrossesVerticalBoundary(JxlAcStrategyImage acStrategyImage, int x, int startY, int endY)
    {
        if (x >= acStrategyImage.XSize || startY >= acStrategyImage.YSize)
        {
            return false;
        }

        if (x % 8 == 0)
        {
            // Nothing crosses 64x64 boundaries, and the memory on the other side
            // of the 64x64 block may still uninitialized.
            return false;
        }

        endY = Math.Min(endY, acStrategyImage.YSize);

        // The first multiblock might be before the start_y, let's adjust it
        // to point to the first IsFirstBlock() == true block we find by backward
        // tracing.
        int startYLimit = startY & ~7;
        while (startY != startYLimit && !acStrategyImage.GetRow(startY)[x].IsFirstBlock)
        {
            startY--;
        }

        for (int y = startY; y < endY;)
        {
            JxlAcStrategyRow row = acStrategyImage.GetRow(y);

            if (row[x].IsFirstBlock)
            {
                y += row[x].CoveredBlocksY;
            }
            else
            {
                return true;
            }
        }

        return false;
    }
}

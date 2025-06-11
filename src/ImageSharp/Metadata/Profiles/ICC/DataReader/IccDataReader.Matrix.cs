// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc;

/// <summary>
/// Provides methods to read ICC data types
/// </summary>
internal sealed partial class IccDataReader
{
    /// <summary>
    /// Reads a two dimensional matrix
    /// </summary>
    /// <param name="xCount">Number of values in X</param>
    /// <param name="yCount">Number of values in Y</param>
    /// <param name="isSingle">True if the values are encoded as Single; false if encoded as Fix16</param>
    /// <returns>The read matrix</returns>
    public float[,] ReadMatrix(int xCount, int yCount, bool isSingle)
    {
        float[,] matrix = new float[xCount, yCount];

        if (isSingle)
        {
            for (int y = 0; y < yCount; y++)
            {
                for (int x = 0; x < xCount; x++)
                {
                    matrix[x, y] = this.ReadSingle();
                }
            }
        }
        else
        {
            for (int y = 0; y < yCount; y++)
            {
                for (int x = 0; x < xCount; x++)
                {
                    matrix[x, y] = this.ReadFix16();
                }
            }
        }

        return matrix;
    }

    /// <summary>
    /// Reads a one dimensional matrix
    /// </summary>
    /// <param name="yCount">Number of values</param>
    /// <param name="isSingle">True if the values are encoded as Single; false if encoded as Fix16</param>
    /// <returns>The read matrix</returns>
    public float[] ReadMatrix(int yCount, bool isSingle)
    {
        float[] matrix = new float[yCount];
        if (isSingle)
        {
            for (int i = 0; i < yCount; i++)
            {
                matrix[i] = this.ReadSingle();
            }
        }
        else
        {
            for (int i = 0; i < yCount; i++)
            {
                matrix[i] = this.ReadFix16();
            }
        }

        return matrix;
    }
}

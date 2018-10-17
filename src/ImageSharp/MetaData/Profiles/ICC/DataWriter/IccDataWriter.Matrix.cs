// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.Primitives;

namespace SixLabors.ImageSharp.MetaData.Profiles.Icc
{
    /// <summary>
    /// Provides methods to write ICC data types
    /// </summary>
    internal sealed partial class IccDataWriter
    {
        /// <summary>
        /// Writes a two dimensional matrix
        /// </summary>
        /// <param name="value">The matrix to write</param>
        /// <param name="isSingle">True if the values are encoded as Single; false if encoded as Fix16</param>
        /// <returns>The number of bytes written</returns>
        public int WriteMatrix(Matrix4x4 value, bool isSingle)
        {
            int count = 0;

            if (isSingle)
            {
                count += this.WriteSingle(value.M11);
                count += this.WriteSingle(value.M21);
                count += this.WriteSingle(value.M31);

                count += this.WriteSingle(value.M12);
                count += this.WriteSingle(value.M22);
                count += this.WriteSingle(value.M32);

                count += this.WriteSingle(value.M13);
                count += this.WriteSingle(value.M23);
                count += this.WriteSingle(value.M33);
            }
            else
            {
                count += this.WriteFix16(value.M11);
                count += this.WriteFix16(value.M21);
                count += this.WriteFix16(value.M31);

                count += this.WriteFix16(value.M12);
                count += this.WriteFix16(value.M22);
                count += this.WriteFix16(value.M32);

                count += this.WriteFix16(value.M13);
                count += this.WriteFix16(value.M23);
                count += this.WriteFix16(value.M33);
            }

            return count;
        }

        /// <summary>
        /// Writes a two dimensional matrix
        /// </summary>
        /// <param name="value">The matrix to write</param>
        /// <param name="isSingle">True if the values are encoded as Single; false if encoded as Fix16</param>
        /// <returns>The number of bytes written</returns>
        public int WriteMatrix(in DenseMatrix<float> value, bool isSingle)
        {
            int count = 0;
            for (int y = 0; y < value.Rows; y++)
            {
                for (int x = 0; x < value.Columns; x++)
                {
                    if (isSingle)
                    {
                        count += this.WriteSingle(value[x, y]);
                    }
                    else
                    {
                        count += this.WriteFix16(value[x, y]);
                    }
                }
            }

            return count;
        }

        /// <summary>
        /// Writes a two dimensional matrix
        /// </summary>
        /// <param name="value">The matrix to write</param>
        /// <param name="isSingle">True if the values are encoded as Single; false if encoded as Fix16</param>
        /// <returns>The number of bytes written</returns>
        public int WriteMatrix(float[,] value, bool isSingle)
        {
            int count = 0;
            for (int y = 0; y < value.GetLength(1); y++)
            {
                for (int x = 0; x < value.GetLength(0); x++)
                {
                    if (isSingle)
                    {
                        count += this.WriteSingle(value[x, y]);
                    }
                    else
                    {
                        count += this.WriteFix16(value[x, y]);
                    }
                }
            }

            return count;
        }

        /// <summary>
        /// Writes a one dimensional matrix
        /// </summary>
        /// <param name="value">The matrix to write</param>
        /// <param name="isSingle">True if the values are encoded as Single; false if encoded as Fix16</param>
        /// <returns>The number of bytes written</returns>
        public int WriteMatrix(Vector3 value, bool isSingle)
        {
            int count = 0;
            if (isSingle)
            {
                count += this.WriteSingle(value.X);
                count += this.WriteSingle(value.Y);
                count += this.WriteSingle(value.Z);
            }
            else
            {
                count += this.WriteFix16(value.X);
                count += this.WriteFix16(value.Y);
                count += this.WriteFix16(value.Z);
            }

            return count;
        }

        /// <summary>
        /// Writes a one dimensional matrix
        /// </summary>
        /// <param name="value">The matrix to write</param>
        /// <param name="isSingle">True if the values are encoded as Single; false if encoded as Fix16</param>
        /// <returns>The number of bytes written</returns>
        public int WriteMatrix(float[] value, bool isSingle)
        {
            int count = 0;
            for (int i = 0; i < value.Length; i++)
            {
                if (isSingle)
                {
                    count += this.WriteSingle(value[i]);
                }
                else
                {
                    count += this.WriteFix16(value[i]);
                }
            }

            return count;
        }
    }
}

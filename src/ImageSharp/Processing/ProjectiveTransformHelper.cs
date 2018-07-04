// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Enumerates the various options which determine which side to taper
    /// </summary>
    public enum TaperSide
    {
        /// <summary>
        /// Taper the left side
        /// </summary>
        Left,

        /// <summary>
        /// Taper the top side
        /// </summary>
        Top,

        /// <summary>
        /// Taper the right side
        /// </summary>
        Right,

        /// <summary>
        /// Taper the bottom side
        /// </summary>
        Bottom
    }

    /// <summary>
    /// Enumerates the various options which determine how to taper corners
    /// </summary>
    public enum TaperCorner
    {
        /// <summary>
        /// Taper the left or top corner
        /// </summary>
        LeftOrTop,

        /// <summary>
        /// Taper the right or bottom corner
        /// </summary>
        RightOrBottom,

        /// <summary>
        /// Taper the both sets of corners
        /// </summary>
        Both
    }

    /// <summary>
    /// Provides helper methods for working with generalized projective transforms.
    /// </summary>
    public static class ProjectiveTransformHelper
    {
        /// <summary>
        /// Creates a matrix that performs a tapering projective transform.
        /// <see href="https://docs.microsoft.com/en-us/xamarin/xamarin-forms/user-interface/graphics/skiasharp/transforms/non-affine"/>
        /// </summary>
        /// <param name="size">The rectangular size of the image being transformed.</param>
        /// <param name="taperSide">An enumeration that indicates the side of the rectangle that tapers.</param>
        /// <param name="taperCorner">An enumeration that indicates on which corners to taper the rectangle.</param>
        /// <param name="taperFraction">The amount to taper.</param>
        /// <returns>The <see cref="Matrix4x4"/></returns>
        public static Matrix4x4 CreateTaperMatrix(Size size, TaperSide taperSide, TaperCorner taperCorner, float taperFraction)
        {
            Matrix4x4 matrix = Matrix4x4.Identity;

            switch (taperSide)
            {
                case TaperSide.Left:
                    matrix.M11 = taperFraction;
                    matrix.M22 = taperFraction;
                    matrix.M13 = (taperFraction - 1) / size.Width;

                    switch (taperCorner)
                    {
                        case TaperCorner.RightOrBottom:
                            break;

                        case TaperCorner.LeftOrTop:
                            matrix.M12 = size.Height * matrix.M13;
                            matrix.M32 = size.Height * (1 - taperFraction);
                            break;

                        case TaperCorner.Both:
                            matrix.M12 = (size.Height * 0.5f) * matrix.M13;
                            matrix.M32 = size.Height * (1 - taperFraction) / 2;
                            break;
                    }

                    break;

                case TaperSide.Top:
                    matrix.M11 = taperFraction;
                    matrix.M22 = taperFraction;
                    matrix.M23 = (taperFraction - 1) / size.Height;

                    switch (taperCorner)
                    {
                        case TaperCorner.RightOrBottom:
                            break;

                        case TaperCorner.LeftOrTop:
                            matrix.M21 = size.Width * matrix.M23;
                            matrix.M31 = size.Width * (1 - taperFraction);
                            break;

                        case TaperCorner.Both:
                            matrix.M21 = (size.Width * 0.5f) * matrix.M23;
                            matrix.M31 = size.Width * (1 - taperFraction) / 2;
                            break;
                    }

                    break;

                case TaperSide.Right:
                    matrix.M11 = 1 / taperFraction;
                    matrix.M13 = (1 - taperFraction) / (size.Width * taperFraction);

                    switch (taperCorner)
                    {
                        case TaperCorner.RightOrBottom:
                            break;

                        case TaperCorner.LeftOrTop:
                            matrix.M12 = size.Height * matrix.M13;
                            break;

                        case TaperCorner.Both:
                            matrix.M12 = (size.Height * 0.5f) * matrix.M13;
                            break;
                    }

                    break;

                case TaperSide.Bottom:
                    matrix.M22 = 1 / taperFraction;
                    matrix.M23 = (1 - taperFraction) / (size.Height * taperFraction);

                    switch (taperCorner)
                    {
                        case TaperCorner.RightOrBottom:
                            break;

                        case TaperCorner.LeftOrTop:
                            matrix.M21 = size.Width * matrix.M23;
                            break;

                        case TaperCorner.Both:
                            matrix.M21 = (size.Width * 0.5f) * matrix.M23;
                            break;
                    }

                    break;
            }

            return matrix;
        }
    }
}
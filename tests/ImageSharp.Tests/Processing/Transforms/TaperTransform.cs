using System.Numerics;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
    public enum TaperSide { Left, Top, Right, Bottom }

    public enum TaperCorner { LeftOrTop, RightOrBottom, Both }

    public static class TaperTransform
    {
        public static Matrix4x4 Make(Size size, TaperSide taperSide, TaperCorner taperCorner, float taperFraction)
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
                            matrix.M12 = (size.Height / 2) * matrix.M13;
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
                            matrix.M21 = (size.Width / 2) * matrix.M23;
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
                            matrix.M12 = (size.Height / 2) * matrix.M13;
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
                            matrix.M21 = (size.Width / 2) * matrix.M23;
                            break;
                    }
                    break;
            }
            return matrix;
        }
    }
}

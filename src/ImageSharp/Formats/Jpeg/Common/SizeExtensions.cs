using System.Numerics;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Formats.Jpeg.Common
{
    /// <summary>
    /// Extension methods for <see cref="Size"/>
    /// </summary>
    internal static class SizeExtensions
    {
        public static Size MultiplyBy(this Size a, Size b) => new Size(a.Width * b.Width, a.Height * b.Height);

        public static Size DivideBy(this Size a, Size b) => new Size(a.Width / b.Width, a.Height / b.Height);

        public static Size DivideRoundUp(this Size originalSize, int divX, int divY)
        {
            var sizeVect = (Vector2)(SizeF)originalSize;
            sizeVect /= new Vector2(divX, divY);
            sizeVect.X = MathF.Ceiling(sizeVect.X);
            sizeVect.Y = MathF.Ceiling(sizeVect.Y);

            return new Size((int)sizeVect.X, (int)sizeVect.Y);
        }

        public static Size DivideRoundUp(this Size originalSize, int divisor) =>
            DivideRoundUp(originalSize, divisor, divisor);

        public static Size DivideRoundUp(this Size originalSize, Size divisor) =>
            DivideRoundUp(originalSize, divisor.Width, divisor.Height);
    }
}
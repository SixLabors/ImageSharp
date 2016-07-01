using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace ImageProcessorCore
{

    public static class ProcessMatrixHelper
    {
        public static void CreateNewTarget(ImageBase target, Rectangle sourceRectangle, Matrix3x2 processMatrix)
        {
            Matrix3x2 sizeMatrix;
            if (Matrix3x2.Invert(processMatrix, out sizeMatrix))
            {
                Rectangle rectangle = ImageMaths.GetBoundingRectangle(sourceRectangle, sizeMatrix);
                target.SetPixels(rectangle.Width, rectangle.Height, new float[rectangle.Width*rectangle.Height*4]);
            }
        }
        public static Matrix3x2 Matrix3X2(ImageBase target, ImageBase source, Matrix3x2 processMatrix)
        {
            Matrix3x2 translationToTargetCenter = Matrix3x2.CreateTranslation(-target.Width / 2f, -target.Height / 2f);
            Matrix3x2 translateToSourceCenter = Matrix3x2.CreateTranslation(source.Width / 2f, source.Height / 2f);
            Matrix3x2 apply = (translationToTargetCenter * processMatrix) * translateToSourceCenter;
            return apply;
        }

        public static void DrawHorizontalData(ImageBase target, ImageBase source, int y, Matrix3x2 apply)
        {
            for (int x = 0; x < target.Width; x++)
            {
                Point rotated = Point.Rotate(new Point(x, y), apply);
                if (source.Bounds.Contains(rotated.X, rotated.Y))
                {
                    target[x, y] = source[rotated.X, rotated.Y];
                }
            }
        }
    }
}

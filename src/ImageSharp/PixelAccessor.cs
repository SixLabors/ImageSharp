// <copyright file="PixelAccessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System.Runtime.CompilerServices;

    /// <summary>
    /// An optimized pixel accessor for the <see cref="Image"/> class.
    /// </summary>
    public sealed unsafe class PixelAccessor : PixelAccessor<Color, uint>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PixelAccessor"/> class.
        /// </summary>
        /// <param name="image">The image to provide pixel access for.</param>
        public PixelAccessor(ImageBase<Color, uint> image)
          : base(image)
        {
        }

        /// <inheritdoc />
        protected override void CopyFromXYZW(PixelRow<Color, uint> row, int targetY, int targetX, int width)
        {
            byte* source = row.PixelBase;
            byte* destination = this.GetRowPointer(targetY) + targetX;

            Unsafe.CopyBlock(destination, source, (uint) width * 4);
        }

        /// <inheritdoc />
        protected override void CopyFromXYZ(PixelRow<Color, uint> row, int targetY, int targetX, int width)
        {
            byte* source = row.PixelBase;
            byte* destination = this.GetRowPointer(targetY) + targetX;

            for (int x = 0; x < width; x++)
            {
                Unsafe.Write(destination, (uint)(*(source) << 0 | *(source + 1) << 8 | *(source + 2) << 16 | 255 << 24));

                source += 3;
                destination += 4;
            }
        }

        /// <inheritdoc />
        protected override void CopyFromZYX(PixelRow<Color, uint> row, int targetY, int targetX, int width)
        {
            byte* source = row.PixelBase;
            byte* destination = this.GetRowPointer(targetY) + targetX;

            for (int x = 0; x < width; x++)
            {
                Unsafe.Write(destination, (uint)(*(source + 2) << 0 | *(source + 1) << 8 | *source << 16 | 255 << 24));

                source += 3;
                destination += 4;
            }
        }

        /// <inheritdoc />
        protected override void CopyFromZYXW(PixelRow<Color, uint> row, int targetY, int targetX, int width)
        {
            byte* source = row.PixelBase;
            byte* destination = this.GetRowPointer(targetY) + targetX;

            for (int x = 0; x < width; x++)
            {
                Unsafe.Write(destination, (uint)(*(source + 2) << 0 | *(source + 1) << 8 | *source << 16 | *(source + 3) << 24));

                source += 4;
                destination += 4;
            }
        }

        /// <inheritdoc />
        protected override void CopyToZYX(PixelRow<Color, uint> row, int sourceY, int width)
        {
            byte* source = this.GetRowPointer(sourceY);
            byte* destination = row.PixelBase;

            for (int x = 0; x < width; x++)
            {
                *destination = *(source + 2);
                *(destination + 1) = *(source + 1);
                *(destination + 2) = *(source + 0);

                source += 4;
                destination += 3;
            }
        }

        /// <inheritdoc />
        protected override void CopyToZYXW(PixelRow<Color, uint> row, int sourceY, int width)
        {
            byte* source = this.GetRowPointer(sourceY);
            byte* destination = row.PixelBase;

            for (int x = 0; x < width; x++)
            {
                *destination = *(source + 2);
                *(destination + 1) = *(source + 1);
                *(destination + 2) = *(source + 0);
                *(destination + 3) = *(source + 3);

                source += 4;
                destination += 4;
            }
        }
    }
}
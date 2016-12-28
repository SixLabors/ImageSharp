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
    public sealed unsafe class PixelAccessor : PixelAccessor<Color>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PixelAccessor"/> class.
        /// </summary>
        /// <param name="image">The image to provide pixel access for.</param>
        public PixelAccessor(ImageBase<Color> image)
          : base(image)
        {
        }

        /// <inheritdoc />
        protected override void CopyFromXyzw(PixelArea<Color> area, int targetX, int targetY, int width, int height)
        {
            uint byteCount = (uint)width * 4;

            for (int y = 0; y < height; y++)
            {
                byte* source = area.PixelBase + (y * area.RowStride);
                byte* destination = this.GetRowPointer(targetX, targetY + y);

                Unsafe.CopyBlock(destination, source, byteCount);
            }
        }

        /// <inheritdoc />
        protected override void CopyFromXyz(PixelArea<Color> area, int targetX, int targetY, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                byte* source = area.PixelBase + (y * area.RowStride);
                byte* destination = this.GetRowPointer(targetX, targetY + y);

                for (int x = 0; x < width; x++)
                {
                    Unsafe.Write(destination, (uint)(*source << 0 | *(source + 1) << 8 | *(source + 2) << 16 | 255 << 24));

                    source += 3;
                    destination += 4;
                }
            }
        }

        /// <inheritdoc />
        protected override void CopyFromZyx(PixelArea<Color> area, int targetX, int targetY, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                byte* source = area.PixelBase + (y * area.RowStride);
                byte* destination = this.GetRowPointer(targetX, targetY + y);

                for (int x = 0; x < width; x++)
                {
                    Unsafe.Write(destination, (uint)(*(source + 2) << 0 | *(source + 1) << 8 | *source << 16 | 255 << 24));

                    source += 3;
                    destination += 4;
                }
            }
        }

        /// <inheritdoc />
        protected override void CopyFromZyxw(PixelArea<Color> area, int targetX, int targetY, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                byte* source = area.PixelBase + (y * area.RowStride);
                byte* destination = this.GetRowPointer(targetX, targetY + y);

                for (int x = 0; x < width; x++)
                {
                    Unsafe.Write(destination, (uint)(*(source + 2) << 0 | *(source + 1) << 8 | *source << 16 | *(source + 3) << 24));

                    source += 4;
                    destination += 4;
                }
            }
        }

        /// <inheritdoc />
        protected override void CopyToZyx(PixelArea<Color> area, int sourceX, int sourceY, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                byte* source = this.GetRowPointer(sourceX, sourceY + y);
                byte* destination = area.PixelBase + (y * area.RowStride);

                for (int x = 0; x < width; x++)
                {
                    *destination = *(source + 2);
                    *(destination + 1) = *(source + 1);
                    *(destination + 2) = *(source + 0);

                    source += 4;
                    destination += 3;
                }
            }
        }

        /// <inheritdoc />
        protected override void CopyToXyz(PixelArea<Color> area, int sourceX, int sourceY, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                byte* source = this.GetRowPointer(sourceX, sourceY + y);
                byte* destination = area.PixelBase + (y * area.RowStride);

                for (int x = 0; x < width; x++)
                {
                    *destination = *(source + 0);
                    *(destination + 1) = *(source + 1);
                    *(destination + 2) = *(source + 2);

                    source += 4;
                    destination += 3;
                }
            }
        }

        /// <inheritdoc />
        protected override void CopyToZyxw(PixelArea<Color> area, int sourceX, int sourceY, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                byte* source = this.GetRowPointer(sourceX, sourceY + y);
                byte* destination = area.PixelBase + (y * area.RowStride);

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
}
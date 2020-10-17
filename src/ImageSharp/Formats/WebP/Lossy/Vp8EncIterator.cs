// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.WebP.Lossy
{
    /// <summary>
    /// Iterator structure to iterate through macroblocks, pointing to the
    /// right neighbouring data (samples, predictions, contexts, ...)
    /// </summary>
    internal class Vp8EncIterator : IDisposable
    {
        private const int YOffEnc = 0;

        private const int UOffEnc = 16;

        private const int VOffEnc = 16 + 8;

        private readonly int mbw;

        private readonly int mbh;

        public Vp8EncIterator(MemoryAllocator memoryAllocator, IMemoryOwner<byte> yTop, IMemoryOwner<byte> uvTop, int mbw, int mbh)
        {
            this.mbw = mbw;
            this.mbh = mbh;
            this.YTop = yTop;
            this.UvTop = uvTop;
            this.YuvIn = memoryAllocator.Allocate<byte>(WebPConstants.Bps * 16);
            this.YuvOut = memoryAllocator.Allocate<byte>(WebPConstants.Bps * 16);
            this.YuvOut2 = memoryAllocator.Allocate<byte>(WebPConstants.Bps * 16);
            this.YLeft = memoryAllocator.Allocate<byte>(WebPConstants.Bps + 1);
            this.ULeft = memoryAllocator.Allocate<byte>(16);
            this.VLeft = memoryAllocator.Allocate<byte>(16);
            this.TopNz = memoryAllocator.Allocate<int>(9);
            this.LeftNz = memoryAllocator.Allocate<int>(9);

            this.Reset();
        }

        /// <summary>
        /// Gets or sets the current macroblock X value.
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// Gets or sets the current macroblock Y.
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// Gets or sets the input samples.
        /// </summary>
        public IMemoryOwner<byte> YuvIn { get; set; }

        /// <summary>
        /// Gets or sets the output samples.
        /// </summary>
        public IMemoryOwner<byte> YuvOut { get; set; }

        public IMemoryOwner<byte> YuvOut2 { get; set; }

        /// <summary>
        /// Gets or sets the left luma samples.
        /// </summary>
        public IMemoryOwner<byte> YLeft { get; set; }

        /// <summary>
        /// Gets or sets the left u samples.
        /// </summary>
        public IMemoryOwner<byte> ULeft { get; set; }

        /// <summary>
        /// Gets or sets the left v samples.
        /// </summary>
        public IMemoryOwner<byte> VLeft { get; set; }

        /// <summary>
        /// Gets or sets the top luma samples at position 'X'.
        /// </summary>
        public IMemoryOwner<byte> YTop { get; set; }

        /// <summary>
        /// Gets or sets the top u/v samples at position 'X', packed as 16 bytes.
        /// </summary>
        public IMemoryOwner<byte> UvTop { get; set; }

        /// <summary>
        /// Gets or sets the non-zero pattern.
        /// </summary>
        public IMemoryOwner<uint> Nz { get; set; }

        /// <summary>
        /// Gets or sets the top-non-zero context.
        /// </summary>
        public IMemoryOwner<int> TopNz { get; set; }

        /// <summary>
        /// Gets or sets the left-non-zero. leftNz[8] is independent.
        /// </summary>
        public IMemoryOwner<int> LeftNz { get; set; }

        /// <summary>
        /// Gets or sets the number of mb still to be processed.
        /// </summary>
        public int CountDown { get; set; }

        public void Import(Span<byte> y, Span<byte> u, Span<byte> v, int yStride, int uvStride, int width, int height)
        {
            int yStartIdx = ((this.Y * yStride) + this.X) * 16;
            int uvStartIdx = ((this.Y * uvStride) + this.X) * 8;
            Span<byte> ySrc = y.Slice(yStartIdx);
            Span<byte> uSrc = u.Slice(uvStartIdx);
            Span<byte> vSrc = v.Slice(uvStartIdx);
            int w = Math.Min(width - (this.X * 16), 16);
            int h = Math.Min(height - (this.Y * 16), 16);
            int uvw = (w + 1) >> 1;
            int uvh = (h + 1) >> 1;

            Span<byte> yuvIn = this.YuvIn.Slice(YOffEnc);
            Span<byte> uIn = this.YuvIn.Slice(UOffEnc);
            Span<byte> vIn = this.YuvIn.Slice(VOffEnc);
            this.ImportBlock(ySrc, yStride, yuvIn, w, h, 16);
            this.ImportBlock(uSrc, uvStride, uIn, uvw, uvh, 8);
            this.ImportBlock(vSrc, uvStride, vIn, uvw, uvh, 8);

            // Import source (uncompressed) samples into boundary.
            if (this.X == 0)
            {
                this.InitLeft();
            }
            else
            {
                Span<byte> yLeft = this.YLeft.GetSpan();
                Span<byte> uLeft = this.ULeft.GetSpan();
                Span<byte> vLeft = this.VLeft.GetSpan();
                if (this.Y == 0)
                {
                    yLeft[0] = 127;
                    uLeft[0] = 127;
                    vLeft[0] = 127;
                }
                else
                {
                    yLeft[0] = ySrc[-1 - yStride];
                    uLeft[0] = uSrc[-1 - uvStride];
                    vLeft[0] = vSrc[-1 - uvStride];
                }

                this.ImportLine(y.Slice(yStartIdx - 1), yStride, yLeft.Slice(1), h, 16);
                this.ImportLine(u.Slice(uvStartIdx - 1), uvStride, uLeft.Slice(1), uvh, 8);
                this.ImportLine(v.Slice(uvStartIdx - 1), uvStride, vLeft.Slice(1), uvh, 8);
            }

            if (this.Y == 0)
            {
                this.YTop.GetSpan().Fill(127);
                this.UvTop.GetSpan().Fill(127);
            }
            else
            {
                this.ImportLine(y.Slice(yStartIdx - yStride), 1, this.YTop.GetSpan(), w, 16);
                this.ImportLine(u.Slice(uvStartIdx - uvStride), 1, this.UvTop.GetSpan(), uvw, 8);
                this.ImportLine(v.Slice(uvStartIdx - uvStride), 1, this.UvTop.GetSpan().Slice(8), uvw, 8);
            }
        }

        public bool IsDone()
        {
            return this.CountDown <= 0;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.YuvIn.Dispose();
            this.YuvOut.Dispose();
            this.YuvOut2.Dispose();
            this.YLeft.Dispose();
            this.ULeft.Dispose();
            this.VLeft.Dispose();
            this.Nz.Dispose();
            this.LeftNz.Dispose();
            this.TopNz.Dispose();
        }

        public bool Next(int mbw)
        {
            if (++this.X == mbw)
            {
                this.SetRow(++this.Y);
            }
            else
            {
                // TODO:
                /* it->preds_ += 4;
                it->mb_ += 1;
                it->nz_ += 1;
                it->y_top_ += 16;
                it->uv_top_ += 16;*/
            }

            return --this.CountDown > 0;
        }

        private void ImportBlock(Span<byte> src, int srcStride, Span<byte> dst, int w, int h, int size)
        {
            int dstIdx = 0;
            for (int i = 0; i < h; ++i)
            {
                src.Slice(0, w).CopyTo(dst.Slice(dstIdx));
                if (w < size)
                {
                    dst.Slice(dstIdx, size - w).Fill(dst[dstIdx + w - 1]);
                }

                dstIdx += WebPConstants.Bps;
                src = src.Slice(srcStride);
            }

            for (int i = h; i < size; ++i)
            {
                dst.Slice(dstIdx - WebPConstants.Bps, size).CopyTo(dst);
                dstIdx += WebPConstants.Bps;
            }
        }

        private void ImportLine(Span<byte> src, int srcStride, Span<byte> dst, int len, int totalLen)
        {
            int i;
            for (i = 0; i < len; ++i)
            {
                dst[i] = src[i];
                src = src.Slice(srcStride);
            }

            for (; i < totalLen; ++i)
            {
                dst[i] = dst[len - 1];
            }
        }

        private void Reset()
        {
            this.SetRow(0);
            this.SetCountDown(this.mbw * this.mbh);
            this.InitTop();
            // TODO: memset(it->bit_count_, 0, sizeof(it->bit_count_));
        }

        private void SetRow(int y)
        {
            this.X = 0;
            this.Y = y;

            // TODO:
            // it->preds_ = enc->preds_ + y * 4 * enc->preds_w_;
            // it->nz_ = enc->nz_;
            // it->mb_ = enc->mb_info_ + y * enc->mb_w_;
            // it->y_top_ = enc->y_top_;
            // it->uv_top_ = enc->uv_top_;
        }

        private void InitLeft()
        {
            Span<byte> yLeft = this.YLeft.GetSpan();
            Span<byte> uLeft = this.ULeft.GetSpan();
            Span<byte> vLeft = this.VLeft.GetSpan();
            byte val = (byte)((this.Y > 0) ? 129 : 127);
            yLeft[0] = val;
            uLeft[0] = val;
            vLeft[0] = val;
            this.YLeft.Slice(1).Fill(129);
            this.ULeft.Slice(1).Fill(129);
            this.VLeft.Slice(1).Fill(129);
            this.LeftNz.GetSpan()[8] = 0;
        }

        private void InitTop()
        {
            int topSize = this.mbw * 16;
            this.YTop.Slice(0, topSize).Fill(127);
            // TODO: memset(enc->nz_, 0, enc->mb_w_ * sizeof(*enc->nz_));
        }

        private void SetCountDown(int countDown)
        {
            this.CountDown = countDown;
        }
    }
}

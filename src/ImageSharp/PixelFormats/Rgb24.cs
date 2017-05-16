namespace ImageSharp.PixelFormats
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    public struct Rgb24 : IPixel<Rgb24>
    {
        /// <summary>
        /// Gets or sets the red component.
        /// </summary>
        public byte R;

        /// <summary>
        /// Gets or sets the green component.
        /// </summary>
        public byte G;

        /// <summary>
        /// Gets or sets the blue component.
        /// </summary>
        public byte B;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Rgb24(byte r, byte g, byte b)
        {
            this.R = r;
            this.G = g;
            this.B = b;
        }

        public PixelOperations<Rgb24> CreatePixelOperations() => new PixelOperations<Rgb24>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Rgb24 other)
        {
            return this.R == other.R && this.G == other.G && this.B == other.B;
        }

        public override bool Equals(object obj)
        {
            return obj?.GetType() == typeof(Rgb24) && this.Equals((Rgb24)obj);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.R;
                hashCode = (hashCode * 397) ^ this.G;
                hashCode = (hashCode * 397) ^ this.B;
                return hashCode;
            }
        }

        public void PackFromBytes(byte x, byte y, byte z, byte w)
        {
            throw new NotImplementedException();
        }

        public void PackFromVector4(Vector4 vector)
        {
            throw new NotImplementedException();
        }

        public Vector4 ToVector4()
        {
            throw new NotImplementedException();
        }

        public void ToXyzBytes(Span<byte> bytes, int startIndex)
        {
            throw new NotImplementedException();
        }

        public void ToXyzwBytes(Span<byte> bytes, int startIndex)
        {
            throw new NotImplementedException();
        }

        public void ToZyxBytes(Span<byte> bytes, int startIndex)
        {
            throw new NotImplementedException();
        }

        public void ToZyxwBytes(Span<byte> bytes, int startIndex)
        {
            throw new NotImplementedException();
        }
    }
}
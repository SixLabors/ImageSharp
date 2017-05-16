namespace ImageSharp.PixelFormats
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    public struct Bgr24 : IPixel<Bgr24>
    {
        /// <summary>
        /// Gets or sets the blue component.
        /// </summary>
        public byte B;

        /// <summary>
        /// Gets or sets the green component.
        /// </summary>
        public byte G;

        /// <summary>
        /// Gets or sets the red component.
        /// </summary>
        public byte R;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bgr24(byte r, byte g, byte b)
        {
            this.R = r;
            this.G = g;
            this.B = b;
        }

        public PixelOperations<Bgr24> CreatePixelOperations() => new PixelOperations<Bgr24>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Bgr24 other)
        {
            return this.R == other.R && this.G == other.G && this.B == other.B;
        }

        public override bool Equals(object obj)
        {
            return obj?.GetType() == typeof(Bgr24) && this.Equals((Bgr24)obj);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.B;
                hashCode = (hashCode * 397) ^ this.G;
                hashCode = (hashCode * 397) ^ this.R;
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
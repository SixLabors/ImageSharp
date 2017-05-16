namespace ImageSharp
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    using ImageSharp.PixelFormats;

    [StructLayout(LayoutKind.Sequential)]
    public struct Bgra32 : IPixel<Bgra32>, IPackedVector<uint>
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

        /// <summary>
        /// Gets or sets the alpha component.
        /// </summary>
        public byte A;

        /// <summary>
        /// Initializes a new instance of the <see cref="Bgra32"/> struct.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bgra32(byte r, byte g, byte b)
        {
            this.R = r;
            this.G = g;
            this.B = b;
            this.A = 255;
        }


        /// <summary>
        /// Initializes a new instance of the <see cref="Bgra32"/> struct.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        /// <param name="a">The alpha component.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bgra32(byte r, byte g, byte b, byte a)
        {
            this.R = r;
            this.G = g;
            this.B = b;
            this.A = a;
        }

        public PixelOperations<Bgra32> CreatePixelOperations() => new PixelOperations<Bgra32>();

        public bool Equals(Bgra32 other)
        {
            return this.R == other.R && this.G == other.G && this.B == other.B && this.A == other.A;
        }

        public override bool Equals(object obj) => obj?.GetType() == typeof(Bgra32) && this.Equals((Bgra32)obj);

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.B;
                hashCode = (hashCode * 397) ^ this.G;
                hashCode = (hashCode * 397) ^ this.R;
                hashCode = (hashCode * 397) ^ this.A;
                return hashCode;
            }
        }

        public void PackFromVector4(Vector4 vector)
        {
            throw new NotImplementedException();
        }

        public Vector4 ToVector4()
        {
            throw new NotImplementedException();
        }

        public void PackFromBytes(byte x, byte y, byte z, byte w)
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

        /// <summary>
        /// Gets or sets the packed representation of the Bgra32 struct.
        /// </summary>
        public uint Bgra
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return Unsafe.As<Bgra32, uint>(ref this);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                Unsafe.As<Bgra32, uint>(ref this) = value;
            }
        }

        /// <inheritdoc/>
        public uint PackedValue
        {
            get => this.Bgra;
            set => this.Bgra = value;
        }
    }
}
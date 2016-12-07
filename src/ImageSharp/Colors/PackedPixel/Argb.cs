using System;
using System.Numerics;

namespace ImageSharp
{
    /// <summary>
    /// Packed pixel type containing four 8-bit unsigned normalized values ranging from 0 to 255.
    /// The color components are stored in alpha, red, green, and blue order.
    /// </summary>
    /// <remarks>
    /// This struct is fully mutable. This is done (against the guidelines) for the sake of performance,
    /// as it avoids the need to create new values for modification operations.
    /// </remarks>
    public class Argb : IPackedPixel<uint>, IEquatable<Argb>
    {
        const int  BlueShift  = 0;
        const uint BlueMask   = 0xFFFFFF00;
        const int  GreenShift = 8;
        const uint GreenMask  = 0xFFFF00FF;
        const int  RedShift   = 16;
        const uint RedMask    = 0xFF00FFFF;
        const int  AlphaShift = 24;
        const uint AlphaMask  = 0x00FFFFFF;

        /// <summary>
        /// The maximum byte value.
        /// </summary>
        readonly static Vector4 MaxBytes = new Vector4(255);

        /// <summary>
        /// The half vector value.
        /// </summary>
        readonly static Vector4 Half = new Vector4(0.5F);

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        /// <param name="a">The alpha component.</param>
        public Argb(byte r, byte g, byte b, byte a = 255)
        {
            PackedValue = Pack(r, g, b, a);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        /// <param name="a">The alpha component.</param>
        public Argb(float r, float g, float b, float a = 1)
        {
            PackedValue = Pack(r, g, b, a);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="vector">
        /// The vector containing the components for the packed vector.
        /// </param>
        public Argb(Vector3 vector)
        {
            PackedValue = Pack(ref vector);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="vector">
        /// The vector containing the components for the packed vector.
        /// </param>
        public Argb(Vector4 vector)
        {
            PackedValue = Pack(ref vector);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="packed">
        /// The packed value.
        /// </param>
        public Argb(uint packed = 0)
        {
            PackedValue = packed;
        }
        /// <summary>
        /// Gets or sets the red component.
        /// </summary>
        public byte R
        {
            get
            {
                return (byte)(PackedValue >> RedShift);
            }

            set
            {
                PackedValue = PackedValue & RedMask | (uint)value << RedShift;
            }
        }

        /// <summary>
        /// Gets or sets the green component.
        /// </summary>
        public byte G
        {
            get
            {
                return (byte)(PackedValue >> GreenShift);
            }

            set
            {
                PackedValue = PackedValue & GreenMask | (uint)value << GreenShift;
            }
        }

        /// <summary>
        /// Gets or sets the blue component.
        /// </summary>
        public byte B
        {
            get
            {
                return (byte)(PackedValue >> BlueShift);
            }

            set
            {
                PackedValue = PackedValue & BlueMask | (uint)value << BlueShift;
            }
        }

        /// <summary>
        /// Gets or sets the alpha component.
        /// </summary>
        public byte A
        {
            get
            {
                return (byte)(PackedValue >> AlphaShift);
            }

            set
            {
                PackedValue = PackedValue & AlphaMask | (uint)value << AlphaShift;
            }
        }

        /// <inheritdoc/>
        public void PackFromVector4(Vector4 vector)
        {
            PackedValue = Pack(ref vector);
        }

        /// <inheritdoc/>
        public Vector4 ToVector4()
        {
            return new Vector4(R, G, B, A) / MaxBytes;
        }

        /// <inheritdoc/>
        public uint PackedValue { get; set; }

        /// <inheritdoc/>
        public void PackFromBytes(byte x, byte y, byte z, byte w)
        {
            PackedValue = Pack(x, y, z, w);
        }

        /// <summary>
        /// Packs the four floats into a <see cref="uint"/>.
        /// </summary>
        /// <param name="x">The x-component</param>
        /// <param name="y">The y-component</param>
        /// <param name="z">The z-component</param>
        /// <param name="w">The w-component</param>
        /// <returns>The <see cref="uint"/></returns>
        static uint Pack(float x, float y, float z, float w)
        {
            var value = new Vector4(x, y, z, w);
            return Pack(ref value);
        }
        /// <summary>
        /// Packs the four floats into a <see cref="uint"/>.
        /// </summary>
        /// <param name="x">The x-component</param>
        /// <param name="y">The y-component</param>
        /// <param name="z">The z-component</param>
        /// <param name="w">The w-component</param>
        /// <returns>The <see cref="uint"/></returns>
        static uint Pack(byte x, byte y, byte z, byte w)
        {
            return (uint)(x << RedShift | y << GreenShift | z << BlueShift | w << AlphaShift);
        }

        /// <summary>
        /// Packs a <see cref="Vector3"/> into a uint.
        /// </summary>
        /// <param name="vector">The vector containing the values to pack.</param>
        /// <returns>The <see cref="uint"/> containing the packed values.</returns>
        static uint Pack(ref Vector3 vector)
        {
            var value = new Vector4(vector, 1);
            return Pack(ref value);
        }

        /// <summary>
        /// Packs a <see cref="Vector4"/> into a uint.
        /// </summary>
        /// <param name="vector">The vector containing the values to pack.</param>
        /// <returns>The <see cref="uint"/> containing the packed values.</returns>
        static uint Pack(ref Vector4 vector)
        {
            vector = Vector4.Clamp(vector, Vector4.Zero, Vector4.One);
            vector *= MaxBytes;
            vector += Half;
            return (uint)(((byte)vector.X << RedShift)
                        | ((byte)vector.Y << GreenShift)
                        | ((byte)vector.Z << BlueShift)
                        | (byte)vector.W << AlphaShift);
        }

        /// <inheritdoc/>
        public void ToBytes(byte[] bytes, int startIndex, ComponentOrder componentOrder)
        {
            switch(componentOrder) {
            case ComponentOrder.ZYX:
                bytes[startIndex] = B;
                bytes[startIndex + 1] = G;
                bytes[startIndex + 2] = R;
                break;
            case ComponentOrder.ZYXW:
                bytes[startIndex] = B;
                bytes[startIndex + 1] = G;
                bytes[startIndex + 2] = R;
                bytes[startIndex + 3] = A;
                break;
            case ComponentOrder.XYZ:
                bytes[startIndex] = R;
                bytes[startIndex + 1] = G;
                bytes[startIndex + 2] = B;
                break;
            case ComponentOrder.XYZW:
                bytes[startIndex] = R;
                bytes[startIndex + 1] = G;
                bytes[startIndex + 2] = B;
                bytes[startIndex + 3] = A;
                break;
            default:
                throw new NotSupportedException();
            }
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            var a = obj as Argb;
            return (a != null) && Equals(a);
        }

        /// <inheritdoc/>
        public bool Equals(Argb other)
        {
            return PackedValue == other.PackedValue;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            // ReSharper disable once NonReadonlyMemberInGetHashCode
            return PackedValue.GetHashCode();
        }
    }
}
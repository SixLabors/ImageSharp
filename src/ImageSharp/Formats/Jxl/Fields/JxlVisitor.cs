// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Fields;

/// <summary>
/// Base JPEG XL visitor that can visit all fields of a class.
/// This is highly similar to the following Reflection code, but with
/// lower overhead:
/// <code>
/// // Pseudocode
/// void Visit(Type type)
/// {
///     foreach (PropertyInfo property in type.GetProperties())
///     {
///         /* visitor implementation */(property);
///     }
/// }
/// </code>
/// </summary>
internal class JxlVisitor
{
    public virtual bool IsReading => false;

    public virtual bool Visit(IJxlFields fields) => false;

    public virtual bool Boolean(bool defaultValue, ref bool value) => false;

    public virtual bool U32(JxlU32Enc enc, uint defaultValue, ref uint value) => false;

    public virtual bool U32(
        JxlU32Distribution d0,
        JxlU32Distribution d1,
        JxlU32Distribution d2,
        JxlU32Distribution d3,
        uint defaultValue,
        ref uint value)
        => this.U32(new JxlU32Enc(d0, d1, d2, d3), value, ref defaultValue);

    public virtual unsafe bool Enum<T>(T defaultValue, ref T value)
        where T : unmanaged
    {
        DebugGuard.IsTrue(sizeof(T) == 4, "We use unsafe bit casting so anything beside 4 bytes will break memory layout");

        ref uint u32 = ref Unsafe.As<T, uint>(ref value);
        if (!this.U32(
            JxlFieldExpressions.Value(0),
            JxlFieldExpressions.Value(1),
            JxlFieldExpressions.BitsOffset(4, 2),
            JxlFieldExpressions.BitsOffset(6, 18),
            Unsafe.BitCast<T, uint>(defaultValue),
            ref u32))
        {
            return false;
        }

        return System.Enum.IsDefined(typeof(T), value);
    }

    public virtual bool Bits(int bits, uint defaultValue, ref uint value) => false;

    public virtual bool U64(ulong defaultValue, ref ulong value) => false;

    public virtual bool F16(float defaultValue, ref float value) => false;

    public virtual bool Conditional(bool condition) => condition;

    public virtual bool AllDefault(IJxlFields fields, ref bool allDefault)
    {
        // Do not remove the fields parameter, derived classes
        // use it.
        if (!this.Boolean(true, ref allDefault))
        {
            return false;
        }

        return allDefault;
    }

    public virtual void SetDefault(IJxlFields fields)
    {
        // Used by derived methods.
    }

    public virtual bool VisitNested(IJxlFields fields) => this.Visit(fields);

    public virtual bool BeginExtensions(ref ulong extensions) => false;

    public virtual bool EndExtensions() => false;
}

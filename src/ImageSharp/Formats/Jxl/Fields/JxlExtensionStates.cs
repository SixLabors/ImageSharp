// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Fields;

internal sealed class JxlExtensionStates
{
    private ulong begun;
    private ulong ended;

    public bool IsBegun => (this.begun & 1) != 0;

    public bool IsEnded => (this.ended & 1) != 0;

    public void Push()
    {
        this.begun <<= 1;
        this.ended <<= 1;
    }

    public void Pop()
    {
        this.begun >>= 1;
        this.ended >>= 1;
    }

    public void Begin()
    {
        DebugGuard.IsFalse(this.IsBegun, nameof(this.IsBegun), "This must be false.");
        DebugGuard.IsFalse(this.IsEnded, nameof(this.IsEnded), "This must be false.");

        this.begun++;
    }

    public void End()
    {
        DebugGuard.IsTrue(this.IsBegun, nameof(this.IsBegun), "This must be true.");
        DebugGuard.IsFalse(this.IsEnded, nameof(this.IsEnded), "This must be false.");

        this.ended++;
    }
}

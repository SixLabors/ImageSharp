// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

/// <summary>
/// Conditionally invokes the specified method when
/// appropriate and when it goes out of scope.
/// </summary>
internal struct JxlScopeGuard(Action action) : IDisposable
{
    /// <summary>
    /// When true the action will be invoked when Dispose() is called.
    /// </summary>
    private bool isArmed = true;

    public void Disarm() => this.isArmed = false;

    public readonly void Dispose()
    {
        if (this.isArmed)
        {
            action();
        }
    }
}

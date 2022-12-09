// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Tests.TestUtilities;

public interface IPausedStream : IDisposable
{
    public void OnWaiting(Action<Stream> onWaitingCallback);

    public void OnWaiting(Action onWaitingCallback);

    public void Next();

    public void Release();

    public long Length { get; }
}

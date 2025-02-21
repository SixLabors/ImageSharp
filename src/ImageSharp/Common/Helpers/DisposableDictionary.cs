// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Common.Helpers;

/// <summary>
/// Dictionary of <see cref="IDisposable"/> objects, which is itself <see cref="IDisposable"/>.
/// </summary>
/// <typeparam name="TKey">The type of the key.</typeparam>
/// <typeparam name="TValue">Tye type of value, needs to implement <see cref="IDisposable"/>.</typeparam>
public sealed class DisposableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IDisposable
    where TKey : notnull
    where TValue : IDisposable
{
    private bool disposedValue;

    /// <inheritdoc />
    public DisposableDictionary()
        : base()
    {
    }

    /// <inheritdoc />
    public DisposableDictionary(int capacity)
        : base(capacity)
    {
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!this.disposedValue)
        {
            if (disposing)
            {
                foreach (KeyValuePair<TKey, TValue> pair in this)
                {
                    pair.Value?.Dispose();
                }
            }

            this.Clear();
            this.disposedValue = true;
        }
    }
}

// License: CPOL at http://www.codeproject.com/info/cpol10.aspx
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace System.IO
{
    public static class AsyncIoExtensions
    {
        public static Task<Stream> GetRequestStreamAsync(this WebRequest webRequest)
        {
            return Task.Factory.FromAsync(
                webRequest.BeginGetRequestStream,
                ar => webRequest.EndGetRequestStream(ar),
                null);
        }

        public static Task<WebResponse> GetResponseAsync(this WebRequest webRequest)
        {
            return Task.Factory.FromAsync(
                webRequest.BeginGetResponse,
                ar => webRequest.EndGetResponse(ar),
                null);
        }

        public static Task<int> ReadAsync(this Stream input, Byte[] buffer, int offset, int count)
        {
            return Task.Factory.FromAsync(
                input.BeginRead,
                (Func<IAsyncResult, int>)input.EndRead,
                buffer, offset, count,
                null);
        }

        public static Task WriteAsync(this Stream input, Byte[] buffer, int offset, int count)
        {
            return Task.Factory.FromAsync(
                input.BeginWrite,
                input.EndWrite,
                buffer, offset, count,
                null);
        }

        public static /*async*/ Task CopyToAsync(this Stream input, Stream output, CancellationToken cancellationToken = default(CancellationToken))
        {
            return CopyToAsyncTasks(input, output, cancellationToken).ToTask();
        }
        private static IEnumerable<Task> CopyToAsyncTasks(Stream input, Stream output, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[0x1000];   // 4 KiB
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var readTask = input.ReadAsync(buffer, 0, buffer.Length);
                yield return readTask;
                if (readTask.Result == 0) break;

                cancellationToken.ThrowIfCancellationRequested();
                yield return output.WriteAsync(buffer, 0, readTask.Result);
            }
        }
    }
}

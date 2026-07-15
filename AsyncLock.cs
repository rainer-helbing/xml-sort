namespace XmlSort {

    internal sealed class AsyncLock : IDisposable {
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public async Task<IDisposable> LockAsync(CancellationToken ct = default) {
            await _semaphore.WaitAsync(ct);
            return new Releaser(_semaphore);
        }

        public void Dispose() => _semaphore.Dispose();

        private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable {
            public void Dispose() => semaphore.Release();
        }
    }
}
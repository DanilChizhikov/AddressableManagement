using System;
using System.Threading;
using System.Threading.Tasks;

namespace DTech.AddressableManagement
{
    /// <summary>
    /// Hold on loaded content. Every load returns its own handler; the content stays in memory while at least one
    /// handler of it is not released.
    /// </summary>
    public interface IAssetHandler
    {
        /// <summary>
        /// The key the content was requested with.
        /// </summary>
        AssetKey Key { get; }

        /// <summary>
        /// Progress of the load from 0 to 1, including the download of its bundles.
        /// </summary>
        float Progress { get; }

        /// <summary>
        /// Whether the load finished, successfully or not.
        /// </summary>
        bool IsDone { get; }

        /// <summary>
        /// Whether the load finished with a failure.
        /// </summary>
        bool IsFailed { get; }

        /// <summary>
        /// Whether the handler was released; its result is no longer available.
        /// </summary>
        bool IsReleased { get; }

        /// <summary>
        /// The reason of a failed load, or null.
        /// </summary>
        Exception Exception { get; }

        /// <summary>
        /// Releases the handler when the token is cancelled, for example when the owning scene or object goes away.
        /// </summary>
        /// <param name="token">The token that ends the handler's lifetime.</param>
        void AttachCancellationToken(CancellationToken token);

        /// <summary>
        /// Waits until the load finishes. A failure does not throw; check IsFailed. Throws OperationCanceledException
        /// when the token is cancelled or the handler is released before the load finishes.
        /// </summary>
        /// <param name="token">Cancels the wait only, not the load.</param>
        /// <returns>A task that completes when the load finishes.</returns>
        Task WaitForCompletionAsync(CancellationToken token = default);

        /// <summary>
        /// Releases the hold on the content. Safe to call more than once.
        /// </summary>
        void Release();
    }
    
    /// <summary>
    /// Hold on loaded content with a typed result.
    /// </summary>
    public interface IAssetHandler<TResult> : IAssetHandler
    {
        /// <summary>
        /// The loaded content; default until the load succeeds. Throws ObjectDisposedException after Release.
        /// </summary>
        TResult Result { get; }

        /// <summary>
        /// Waits until the load finishes. A failure does not throw and returns default; check IsFailed.
        /// Throws OperationCanceledException when the token is cancelled or the handler is released before the load
        /// finishes.
        /// </summary>
        /// <param name="token">Cancels the wait only, not the load.</param>
        /// <returns>The loaded content, or default when the load failed.</returns>
        new Task<TResult> WaitForCompletionAsync(CancellationToken token = default);
    }
}
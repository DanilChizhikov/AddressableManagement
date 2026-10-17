using System.Threading;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace DTech.AddressableManagement
{
	/// <summary>
	/// Hold on a loaded addressable scene. Result is the scene once it is activated. Release unloads the scene.
	/// </summary>
	public interface ISceneHandler : IAssetHandler<Scene>
	{
		/// <summary>
		/// Whether the scene is active, either at once or after ActivateAsync.
		/// </summary>
		bool IsActivated { get; }

		/// <summary>
		/// Activates a scene loaded with deferred activation. Completes at once when the scene is already active.
		/// </summary>
		/// <param name="token">Cancels the wait only, not the activation.</param>
		/// <returns>A task that completes when the scene is active.</returns>
		Task ActivateAsync(CancellationToken token = default);

		/// <summary>
		/// Unloads the scene and releases the handler.
		/// </summary>
		/// <param name="token">Cancels the wait only, not the unload.</param>
		/// <returns>A task that completes when the scene is unloaded.</returns>
		Task UnloadAsync(CancellationToken token = default);
	}
}
using UnityEngine.SceneManagement;

namespace DTech.AddressableManagement
{
	/// <summary>
	/// How a scene loads. The default value replaces the open scenes and activates the new one at once.
	/// </summary>
	public readonly struct SceneLoadOptions
	{
		/// <summary>
		/// Whether the scene replaces the open scenes or is added to them.
		/// </summary>
		public LoadSceneMode Mode { get; }

		/// <summary>
		/// Whether the scene stays inactive after loading until ISceneHandler.ActivateAsync is called.
		/// </summary>
		public bool DeferActivation { get; }

		/// <summary>
		/// Priority of the scene load among other asynchronous loads.
		/// </summary>
		public int Priority { get; }

		/// <summary>
		/// Creates scene load options.
		/// </summary>
		/// <param name="mode">Whether the scene replaces the open scenes or is added to them.</param>
		/// <param name="deferActivation">Whether activation waits for ISceneHandler.ActivateAsync.</param>
		/// <param name="priority">Priority among other asynchronous loads.</param>
		public SceneLoadOptions(LoadSceneMode mode, bool deferActivation, int priority)
		{
			Mode = mode;
			DeferActivation = deferActivation;
			Priority = priority;
		}
	}
}
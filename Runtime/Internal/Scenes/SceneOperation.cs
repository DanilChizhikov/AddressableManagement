using System.Threading;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace DTech.AddressableManagement.Internal.Scenes
{
	internal abstract class SceneOperation
	{
		public abstract Task Completion { get; }
		public abstract float Progress { get; }
		public abstract Scene Scene { get; }
		public abstract bool IsActivated { get; }

		public abstract Task ActivateAsync(CancellationToken token);
		public abstract Task UnloadAsync();
	}
}
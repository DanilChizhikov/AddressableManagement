using System;

namespace DTech.AddressableManagement.Internal.Diagnostics
{
	internal static class HandlerTrace
	{
		public static string Capture()
		{
			#if UNITY_EDITOR || DEVELOPMENT_BUILD
			return Environment.StackTrace;
			#else
            return null;
			#endif
		}

		public static string Describe(string createdAt, string releasedAt)
		{
			if (createdAt == null)
			{
				return " Build with DEVELOPMENT_BUILD to see where the handler was created and released.";
			}

			return $"\nCreated at:\n{createdAt}\nReleased at:\n{releasedAt ?? "<not released>"}";
		}
	}
}
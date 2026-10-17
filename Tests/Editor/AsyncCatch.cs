using System;
using System.Threading.Tasks;

namespace DTech.AddressableManagement.Tests.Editor
{
	internal static class AsyncCatch
	{
		public static async Task<Exception> ExceptionAsync(Func<Task> action)
		{
			try
			{
				await action();
				return null;
			}
			catch (Exception exception)
			{
				return exception;
			}
		}
	}
}
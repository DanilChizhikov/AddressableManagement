using System;

namespace DTech.AddressableManagement
{
	/// <summary>
	/// A content delivery failure that retries and CDN fallbacks could not recover from.
	/// </summary>
	public readonly struct ContentFailure
	{
		/// <summary>
		/// The step that failed.
		/// </summary>
		public ContentStage Stage { get; }

		/// <summary>
		/// Why it failed.
		/// </summary>
		public FailureKind Kind { get; }

		/// <summary>
		/// The runtime key involved, or a short description when several keys or none were involved.
		/// </summary>
		public string Key { get; }

		/// <summary>
		/// The underlying exception, if any.
		/// </summary>
		public Exception Exception { get; }

		internal ContentFailure(ContentStage stage, FailureKind kind, string key, Exception exception)
		{
			Stage = stage;
			Kind = kind;
			Key = key ?? string.Empty;
			Exception = exception;
		}

		/// <summary>
		/// A one-line description of the failure.
		/// </summary>
		/// <returns>The description.</returns>
		public override string ToString()
		{
			string reason = Exception == null ? string.Empty : $": {Exception.Message}";
			return $"{Stage} failed ({Kind}) for '{Key}'{reason}";
		}
	}
}
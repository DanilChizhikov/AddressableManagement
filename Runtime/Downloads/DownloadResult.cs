using System;

namespace DTech.AddressableManagement
{
	/// <summary>
	/// How a download ended and, for a failure, why.
	/// </summary>
	public readonly struct DownloadResult
	{
		/// <summary>
		/// How the download ended.
		/// </summary>
		public DownloadOutcome Outcome { get; }

		/// <summary>
		/// Why the last attempt failed; None unless the outcome is Failed.
		/// </summary>
		public FailureKind Failure { get; }

		/// <summary>
		/// The exception of the last failed attempt, if any.
		/// </summary>
		public Exception Exception { get; }

		/// <summary>
		/// Whether the content is on the device: downloaded now or already cached.
		/// </summary>
		public bool IsSuccess => Outcome == DownloadOutcome.Downloaded || Outcome == DownloadOutcome.AlreadyCached;

		internal DownloadResult(DownloadOutcome outcome, FailureKind failure, Exception exception)
		{
			Outcome = outcome;
			Failure = failure;
			Exception = exception;
		}

		/// <summary>
		/// A one-line description of the result.
		/// </summary>
		/// <returns>The description.</returns>
		public override string ToString()
		{
			return Outcome == DownloadOutcome.Failed ? $"{Outcome} ({Failure}): {Exception?.Message}" : Outcome.ToString();
		}
	}
}
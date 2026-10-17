using System;
using UnityEngine;

namespace DTech.AddressableManagement
{
	/// <summary>
	/// How many times a failed download is attempted and how long to wait between attempts. Switching to the next
	/// CDN mirror or content root does not consume an attempt. The default value allows a single attempt.
	/// </summary>
	[Serializable]
	public struct RetryPolicy
	{
		/// <summary>
		/// A single attempt without retries.
		/// </summary>
		public static RetryPolicy None => new (1, TimeSpan.Zero, false);

		/// <summary>
		/// Three attempts, three seconds apart.
		/// </summary>
		public static RetryPolicy Default => new (3, TimeSpan.FromSeconds(3d), false);

		/// <summary>
		/// The number of attempts, at least one. Ignored when IsUnlimited is set.
		/// </summary>
		public int MaxAttempts => Math.Max(1, _maxAttempts);

		/// <summary>
		/// The real-time pause before the next attempt.
		/// </summary>
		public TimeSpan Delay => TimeSpan.FromSeconds(Math.Max(0f, _delaySeconds));

		/// <summary>
		/// Whether attempts continue until success or cancellation.
		/// </summary>
		public bool IsUnlimited => _isUnlimited;

		[Tooltip("Number of attempts, at least one. Ignored when unlimited.")]
		[SerializeField, Min(1)] private int _maxAttempts;

		[Tooltip("Real-time seconds between attempts.")]
		[SerializeField, Min(0f)] private float _delaySeconds;

		[Tooltip("Keep attempting until success or cancellation.")]
		[SerializeField] private bool _isUnlimited;

		/// <summary>
		/// Creates a policy with a fixed number of attempts.
		/// </summary>
		/// <param name="maxAttempts">The number of attempts; values below one mean one.</param>
		/// <param name="delay">The real-time pause between attempts.</param>
		public RetryPolicy(int maxAttempts, TimeSpan delay) : this(maxAttempts, delay, false)
		{
		}

		private RetryPolicy(int maxAttempts, TimeSpan delay, bool isUnlimited)
		{
			_maxAttempts = Math.Max(1, maxAttempts);
			_delaySeconds = (float)Math.Max(0d, delay.TotalSeconds);
			_isUnlimited = isUnlimited;
		}

		/// <summary>
		/// A policy that keeps attempting until success or cancellation, for example to wait out a lost connection.
		/// </summary>
		/// <param name="delay">The real-time pause between attempts.</param>
		/// <returns>The policy.</returns>
		public static RetryPolicy Unlimited(TimeSpan delay) => new(1, delay, true);

		internal bool AllowsAttempt(int attemptNumber) => _isUnlimited || attemptNumber <= MaxAttempts;
	}
}
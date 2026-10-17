using System;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.Exceptions;

namespace DTech.AddressableManagement.Internal.Delivery
{
	internal static class FailureClassifier
	{
		private const int MaxDepth = 16;

		public static FailureKind Classify(Exception exception) => Find(exception, 0) ?? FailureKind.Unknown;

		public static bool MovesRoot(FailureKind kind)
		{
			return kind == FailureKind.NotFound;
		}

		public static bool MovesMirror(FailureKind kind)
		{
			return kind == FailureKind.Network || kind == FailureKind.ServerError || kind == FailureKind.Corrupted;
		}
		
		private static FailureKind Classify(long responseCode, UnityWebRequest.Result result)
		{
			if (responseCode == 404 ||
				responseCode == 410)
			{
				return FailureKind.NotFound;
			}

			if (responseCode == 408 ||
				responseCode == 429)
			{
				return FailureKind.Network;
			}

			if (responseCode >= 500 &&
				responseCode < 600)
			{
				return FailureKind.ServerError;
			}

			if (responseCode >= 400 &&
				responseCode < 500)
			{
				return FailureKind.ClientError;
			}

			switch (result)
			{
				case UnityWebRequest.Result.ConnectionError:
					return FailureKind.Network;

				case UnityWebRequest.Result.DataProcessingError:
					return FailureKind.Corrupted;

				case UnityWebRequest.Result.ProtocolError:
					return FailureKind.ServerError;

				default:
					return FailureKind.Unknown;
			}
		}

		private static FailureKind? Find(Exception exception, int depth)
		{
			if (exception == null ||
				depth > MaxDepth)
			{
				return null;
			}

			switch (exception)
			{
				case ContentDeliveryException delivery:
					return delivery.Kind;

				case RemoteProviderException remote when remote.WebRequestResult != null:
					return Classify(remote.WebRequestResult.ResponseCode, remote.WebRequestResult.Result);

				case InvalidKeyException _:
					return FailureKind.InvalidKey;

				case AggregateException aggregate:
					foreach (Exception inner in aggregate.InnerExceptions)
					{
						FailureKind? kind = Find(inner, depth + 1);
						if (kind.HasValue)
						{
							return kind;
						}
					}

					return null;

				default:
					return Find(exception.InnerException, depth + 1);
			}
		}
	}
}
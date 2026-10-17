using System;
using DTech.AddressableManagement.Internal.Delivery;
using NUnit.Framework;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.Exceptions;

namespace DTech.AddressableManagement.Tests.Editor
{
    [TestFixture]
    internal sealed class FailureClassifierTests
    {
        [TestCase(404L, FailureKind.NotFound)]
        [TestCase(410L, FailureKind.NotFound)]
        [TestCase(408L, FailureKind.Network)]
        [TestCase(429L, FailureKind.Network)]
        [TestCase(403L, FailureKind.ClientError)]
        [TestCase(500L, FailureKind.ServerError)]
        [TestCase(503L, FailureKind.ServerError)]
        public void Classify_MapsHttpResponseCodes(long responseCode, FailureKind expected)
        {
            Assert.That(FailureClassifier.Classify(responseCode, UnityWebRequest.Result.ProtocolError), Is.EqualTo(expected));
        }

        [Test]
        public void Classify_MapsRequestResultsWithoutAResponseCode()
        {
            Assert.That(FailureClassifier.Classify(0L, UnityWebRequest.Result.ConnectionError), Is.EqualTo(FailureKind.Network));
            Assert.That(FailureClassifier.Classify(0L, UnityWebRequest.Result.DataProcessingError), Is.EqualTo(FailureKind.Corrupted));
            Assert.That(FailureClassifier.Classify(0L, UnityWebRequest.Result.Success), Is.EqualTo(FailureKind.Unknown));
        }

        [Test]
        public void Classify_FindsTheKindInsideWrappedExceptions()
        {
            var inner = new ContentDeliveryException(FailureKind.NotFound, "missing");
            var wrapped = new OperationException("group failed", new InvalidOperationException("dependency failed", inner));

            Assert.That(FailureClassifier.Classify(wrapped), Is.EqualTo(FailureKind.NotFound));
        }

        [Test]
        public void Classify_LooksIntoAggregateExceptions()
        {
            var aggregate = new AggregateException(new InvalidOperationException("unrelated"),
                new ContentDeliveryException(FailureKind.Network, "timeout"));

            Assert.That(FailureClassifier.Classify(aggregate), Is.EqualTo(FailureKind.Network));
        }

        [Test]
        public void Classify_RecognizesInvalidKeys()
        {
            Assert.That(FailureClassifier.Classify(new InvalidKeyException("missing-key")), Is.EqualTo(FailureKind.InvalidKey));
        }

        [Test]
        public void Classify_FallsBackToUnknown()
        {
            Assert.That(FailureClassifier.Classify(new InvalidOperationException("boom")), Is.EqualTo(FailureKind.Unknown));
            Assert.That(FailureClassifier.Classify(null), Is.EqualTo(FailureKind.Unknown));
        }

        [Test]
        public void RouteMoves_SeparateMissingContentFromTransportFailures()
        {
            Assert.That(FailureClassifier.MovesRoot(FailureKind.NotFound), Is.True);
            Assert.That(FailureClassifier.MovesMirror(FailureKind.NotFound), Is.False);
            Assert.That(FailureClassifier.MovesMirror(FailureKind.Network), Is.True);
            Assert.That(FailureClassifier.MovesMirror(FailureKind.ClientError), Is.False);
        }
    }
}
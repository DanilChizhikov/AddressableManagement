using System;
using System.Threading;
using System.Threading.Tasks;
using DTech.AddressableManagement.Internal.Threading;
using NUnit.Framework;

namespace DTech.AddressableManagement.Tests.Editor
{
    [TestFixture]
    internal sealed class SingleFlightTests
    {
        [Test]
        public async Task RunAsync_SharesTheRunningOperation()
        {
            var flight = new SingleFlight<int>();
            var gate = new TaskCompletionSource<int>();
            int started = 0;

            Task<int> first = flight.RunAsync(() =>
            {
                started++;
                return gate.Task;
            }, CancellationToken.None);

            Task<int> second = flight.RunAsync(() =>
            {
                started++;
                return gate.Task;
            }, CancellationToken.None);

            gate.SetResult(7);

            Assert.That(await first, Is.EqualTo(7));
            Assert.That(await second, Is.EqualTo(7));
            Assert.That(started, Is.EqualTo(1));
        }

        [Test]
        public async Task RunAsync_StartsAgainAfterCompletion()
        {
            var flight = new SingleFlight<int>();
            int started = 0;

            await flight.RunAsync(() => Task.FromResult(++started), CancellationToken.None);
            await flight.RunAsync(() => Task.FromResult(++started), CancellationToken.None);

            Assert.That(started, Is.EqualTo(2));
        }

        [Test]
        public async Task RunAsync_CallerCancellationStopsOnlyItsWait()
        {
            var flight = new SingleFlight<int>();
            var gate = new TaskCompletionSource<int>();
            using (var cancellation = new CancellationTokenSource())
            {
                Task<int> cancelled = flight.RunAsync(() => gate.Task, cancellation.Token);
                Task<int> other = flight.RunAsync(() => gate.Task, CancellationToken.None);

                cancellation.Cancel();
                Exception error = await AsyncCatch.ExceptionAsync(() => cancelled);
                Assert.That(error, Is.InstanceOf<OperationCanceledException>());

                gate.SetResult(3);
                Assert.That(await other, Is.EqualTo(3));
            }
        }

        [Test]
        public async Task WaitIdleAsync_IgnoresTheFailureOfTheRunningOperation()
        {
            var flight = new SingleFlight<int>();
            var gate = new TaskCompletionSource<int>();
            Task<int> running = flight.RunAsync(() => gate.Task, CancellationToken.None);

            Task idle = flight.WaitIdleAsync(CancellationToken.None);
            gate.SetException(new InvalidOperationException("boom"));

            await idle;
            Assert.That(running.Exception, Is.Not.Null);
            Assert.That(flight.IsRunning, Is.False);
        }
    }
}
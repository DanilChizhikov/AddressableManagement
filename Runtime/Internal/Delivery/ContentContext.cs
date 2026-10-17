using System;
using System.Threading;
using DTech.AddressableManagement.Internal.Diagnostics;
using DTech.AddressableManagement.Internal.Gateway;

namespace DTech.AddressableManagement.Internal.Delivery
{
	internal sealed class ContentContext
	{
		private readonly Action<ContentFailure> _report;

		public AddressablesGateway Gateway { get; }
		public ContentRoute Route { get; }
		public AssetLog Log { get; }
		public CancellationToken Lifetime { get; }

		public ContentContext(AddressablesGateway gateway, ContentRoute route, AssetLog log,
			Action<ContentFailure> report, CancellationToken lifetime)
		{
			Gateway = gateway;
			Route = route;
			Log = log;
			_report = report;
			Lifetime = lifetime;
		}

		public void Report(ContentFailure failure)
		{
			_report(failure);
		}
	}
}
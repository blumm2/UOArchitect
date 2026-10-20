using System;
using System.Threading;

namespace OrbServerSDK
{
	public abstract class OrbCommand
	{
		public abstract void OnCommand(OrbClientInfo clientInfo, OrbCommandArgs args);
	}
}

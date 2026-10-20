using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace OrbServerSDK
{
	/// <summary>
	/// Server side of the Orb connection. Replaces the .NET Remoting TcpChannel and
	/// RemotingConfiguration.RegisterWellKnownServiceType(typeof(OrbConnection), ...).
	///
	/// Every client gets its own thread; calls are forwarded to the OrbConnection
	/// events (OnLogin, OnExecuteCommand, OnExecuteRequest, OnDisconnect) exactly
	/// as Remoting did. The handlers run on that network thread – the server script
	/// is responsible for moving world access onto the game thread.
	///
	/// The client identity is bound to the TCP connection at login: commands and
	/// requests always carry the OrbClientInfo created by the server, never data
	/// sent by the client, and nothing is accepted before a successful login.
	/// </summary>
	public static class OrbServerHost
	{
		public const int MaxClients = 16;
		public const int MaxLoginAttempts = 3;

		private static readonly TimeSpan LoginTimeout = TimeSpan.FromSeconds(30);

		private static readonly object m_Lock = new object();
		private static TcpListener m_Listener;
		private static int m_ClientCount;

		public static bool IsRunning
		{
			get{ lock(m_Lock) return m_Listener != null; }
		}

		public static void Start(int port)
		{
			Start(IPAddress.Any, port);
		}

		public static void Start(IPAddress address, int port)
		{
			lock(m_Lock)
			{
				if(m_Listener != null)
					throw new InvalidOperationException("The Orb server is already running.");

				TcpListener listener = new TcpListener(address, port);
				listener.Start();
				m_Listener = listener;

				Thread thread = new Thread(AcceptLoop);
				thread.Name = "Orb Server";
				thread.IsBackground = true;
				thread.Start(listener);
			}
		}

		public static void Stop()
		{
			lock(m_Lock)
			{
				if(m_Listener != null)
				{
					m_Listener.Stop();
					m_Listener = null;
				}
			}
		}

		private static void AcceptLoop(object state)
		{
			TcpListener listener = (TcpListener)state;

			while(true)
			{
				TcpClient client;

				try
				{
					client = listener.AcceptTcpClient();
				}
				catch(ObjectDisposedException)
				{
					return;
				}
				catch(SocketException)
				{
					if(!IsRunning)
						return;

					continue;
				}

				if(Interlocked.Increment(ref m_ClientCount) > MaxClients)
				{
					Interlocked.Decrement(ref m_ClientCount);
					client.Close();
					continue;
				}

				Thread thread = new Thread(ClientLoop);
				thread.Name = "Orb Client";
				thread.IsBackground = true;
				thread.Start(client);
			}
		}

		private static void ClientLoop(object state)
		{
			TcpClient client = (TcpClient)state;
			OrbConnection connection = new OrbConnection();
			OrbClientInfo session = null;
			string endPoint = null;

			try
			{
				endPoint = client.Client.RemoteEndPoint != null ? client.Client.RemoteEndPoint.ToString() : "?";
				client.NoDelay = true;
				client.ReceiveTimeout = (int)LoginTimeout.TotalMilliseconds;

				NetworkStream stream = client.GetStream();
				int loginAttempts = 0;

				while(true)
				{
					OrbProtocol.Message message;
					BinaryReader reader = OrbProtocol.Receive(stream, out message);

					if(reader == null || message == OrbProtocol.Message.Disconnect)
						break;

					if(session == null)
					{
						if(message != OrbProtocol.Message.Login || ++loginAttempts > MaxLoginAttempts)
							break;

						session = HandleLogin(stream, reader, connection);

						if(session != null)
							client.ReceiveTimeout = 0; // no timeout, the tool may stay idle for a long time (-1 fails on Linux)

						continue;
					}

					switch(message)
					{
						case OrbProtocol.Message.Command:
							HandleCommand(stream, reader, connection, session);
							break;

						case OrbProtocol.Message.Request:
							HandleRequest(stream, reader, connection, session);
							break;

						default:
							throw new InvalidDataException(string.Format("Unexpected message 0x{0:X2}.", (byte)message));
					}
				}
			}
			catch(IOException)
			{
				// connection lost or timed out
			}
			catch(InvalidDataException e)
			{
				Console.WriteLine("Orb Server: dropping {0}: {1}", endPoint, e.Message);
			}
			catch(Exception e)
			{
				Console.WriteLine("Orb Server: error on {0}: {1}", endPoint, e);
			}
			finally
			{
				if(session != null)
				{
					try
					{
						connection.Disconnect(session.ClientID);
					}
					catch(Exception e)
					{
						Console.WriteLine("Orb Server: disconnect handler failed: {0}", e.Message);
					}
				}

				client.Close();
				Interlocked.Decrement(ref m_ClientCount);
			}
		}

		private static OrbClientInfo HandleLogin(Stream stream, BinaryReader reader, OrbConnection connection)
		{
			int magic = reader.ReadInt32();
			ushort version = reader.ReadUInt16();

			if(magic != OrbProtocol.Magic)
				throw new InvalidDataException("Not an Orb client.");

			string userName = reader.ReadString();
			string password = reader.ReadString();
			OrbProtocol.EnsureConsumed(reader);

			LoginResult result;
			OrbClientInfo clientInfo = null;

			if(version != OrbProtocol.Version)
			{
				result = new LoginResult(LoginCodes.ConnectionFailure,
					string.Format("Protocol version mismatch (client {0}, server {1}). Update UO Architect or the server.", version, OrbProtocol.Version));
			}
			else
			{
				// the session id is generated here, a client cannot pick or reuse someone else's
				clientInfo = new OrbClientInfo(Guid.NewGuid().ToString(), userName);
				result = connection.LoginToServer(clientInfo, password);
			}

			BinaryWriter frame = OrbProtocol.BeginFrame(OrbProtocol.Message.LoginResult);
			frame.Write((byte)result.Code);
			OrbProtocol.WriteNullable(frame, result.ErrorMessage);
			OrbProtocol.Send(stream, frame);

			return result.Code == LoginCodes.Success ? clientInfo : null;
		}

		private static void HandleCommand(Stream stream, BinaryReader reader, OrbConnection connection, OrbClientInfo session)
		{
			string alias = reader.ReadString();
			OrbCommandArgs args = (OrbCommandArgs)ReadArgs(reader, typeof(OrbCommandArgs));
			string error = null;

			try
			{
				connection.SendCommand(alias, session, args);
			}
			catch(Exception e)
			{
				Console.WriteLine("Orb Server: command {0} failed: {1}", alias, e.Message);
				error = "The server failed to execute the command.";
			}

			BinaryWriter frame = OrbProtocol.BeginFrame(OrbProtocol.Message.CommandResult);
			OrbProtocol.WriteNullable(frame, error);
			OrbProtocol.Send(stream, frame);
		}

		private static void HandleRequest(Stream stream, BinaryReader reader, OrbConnection connection, OrbClientInfo session)
		{
			string alias = reader.ReadString();
			OrbRequestArgs args = (OrbRequestArgs)ReadArgs(reader, typeof(OrbRequestArgs));

			// OrbConnection.SendRequest swallows handler exceptions and returns null, as before
			OrbResponse response = connection.SendRequest(alias, session, args);

			BinaryWriter frame = OrbProtocol.BeginFrame(OrbProtocol.Message.RequestResult);

			try
			{
				OrbProtocol.WriteNullable(frame, null);
				OrbSerializer.Write(frame, response);
			}
			catch(InvalidDataException e)
			{
				Console.WriteLine("Orb Server: response to {0} cannot be sent: {1}", alias, e.Message);

				frame = OrbProtocol.BeginFrame(OrbProtocol.Message.RequestResult);
				OrbProtocol.WriteNullable(frame, "The server could not encode the response.");
			}

			OrbProtocol.Send(stream, frame);
		}

		private static object ReadArgs(BinaryReader reader, Type expected)
		{
			object args = OrbSerializer.Read(reader);
			OrbProtocol.EnsureConsumed(reader);

			if(args != null && !expected.IsInstanceOfType(args))
				throw new InvalidDataException(string.Format("{0} is not {1}.", args.GetType().FullName, expected.Name));

			return args;
		}
	}
}

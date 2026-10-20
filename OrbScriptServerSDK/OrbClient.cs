using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace OrbServerSDK
{
	public delegate void OrbErrorEvent(string message);

	/// <summary>
	/// Client side of the Orb connection (UO Architect, test utilities).
	/// One synchronous call at a time, like the former Remoting proxy.
	/// </summary>
	public class OrbClient
	{
		private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);

		private static readonly object m_Sync = new object();
		private static TcpClient m_Client = null;
		private static NetworkStream m_Stream = null;
		private static string m_UserName;

		/// <summary>
		/// Raised when a command or request fails. The SDK has no UI of its own;
		/// the application decides how to show the message.
		/// </summary>
		public static event OrbErrorEvent OnError;

		public static bool IsConnected
		{
			get{ return m_Stream != null; }
		}

		public static string UserName
		{
			get{ return m_UserName; }
		}

		public static string ResolveDNS(string server)
		{
			IPAddress[] addresses = Dns.GetHostAddresses(server);

			foreach(IPAddress address in addresses)
			{
				if(address.AddressFamily == AddressFamily.InterNetwork)
					return address.ToString();
			}

			return addresses[0].ToString();
		}

		public static void Disconnect()
		{
			lock(m_Sync)
			{
				if(!IsConnected)
					return;

				try
				{
					OrbProtocol.Send(m_Stream, OrbProtocol.BeginFrame(OrbProtocol.Message.Disconnect));
				}
				catch(Exception)
				{
				}

				Close();
			}
		}

		public static LoginResult LoginToServer(string user, string password, string serverName, int port)
		{
			lock(m_Sync)
			{
				LoginResult result;

				try
				{
					if(IsConnected)
						Close();

					Open(serverName, port);

					BinaryWriter frame = OrbProtocol.BeginFrame(OrbProtocol.Message.Login);
					frame.Write(OrbProtocol.Magic);
					frame.Write(OrbProtocol.Version);
					frame.Write(user ?? "");
					frame.Write(password ?? "");
					OrbProtocol.Send(m_Stream, frame);

					BinaryReader reply = Receive(OrbProtocol.Message.LoginResult);
					LoginCodes code = (LoginCodes)reply.ReadByte();
					string message = OrbProtocol.ReadNullable(reply);

					result = new LoginResult(code, message);
				}
				catch(Exception e)
				{
					result = new LoginResult(LoginCodes.ConnectionFailure, "Connection Failed\n" + e.Message, e);
				}

				if(result.Code == LoginCodes.Success)
					m_UserName = user;
				else
					Close();

				return result;
			}
		}

		public static void SendCommand(string alias, OrbCommandArgs args)
		{
			string error;

			lock(m_Sync)
			{
				BinaryWriter frame = BuildCall(OrbProtocol.Message.Command, alias, args, out error);

				if(frame != null)
				{
					try
					{
						OrbProtocol.Send(m_Stream, frame);

						BinaryReader reply = Receive(OrbProtocol.Message.CommandResult);
						error = OrbProtocol.ReadNullable(reply);
					}
					catch(Exception e)
					{
						error = Fail(e);
					}
				}
			}

			if(error != null)
				RaiseError(error);
		}

		public static OrbResponse SendRequest(string alias, OrbRequestArgs args)
		{
			string error;
			OrbResponse response = null;

			lock(m_Sync)
			{
				BinaryWriter frame = BuildCall(OrbProtocol.Message.Request, alias, args, out error);

				if(frame != null)
				{
					try
					{
						OrbProtocol.Send(m_Stream, frame);

						// no timeout: a request may wait for the GM to target something in game
						BinaryReader reply = Receive(OrbProtocol.Message.RequestResult);
						error = OrbProtocol.ReadNullable(reply);

						if(error == null)
						{
							response = OrbSerializer.Read(reply) as OrbResponse;
							OrbProtocol.EnsureConsumed(reply);
						}
					}
					catch(Exception e)
					{
						error = Fail(e);
					}
				}
			}

			if(error != null)
				RaiseError(error);

			return response;
		}

		// Encodes the call before anything is sent: an argument that cannot be
		// serialized is reported without dropping the connection.
		private static BinaryWriter BuildCall(OrbProtocol.Message message, string alias, object args, out string error)
		{
			error = null;

			if(!IsConnected)
			{
				error = "A connection is required before this command can be sent to the server.";
				return null;
			}

			BinaryWriter frame = OrbProtocol.BeginFrame(message);
			frame.Write(alias);

			try
			{
				OrbSerializer.Write(frame, args);
			}
			catch(InvalidDataException e)
			{
				error = "The command cannot be sent to the server.\n" + e.Message;
				return null;
			}

			return frame;
		}

		private static void Open(string serverName, int port)
		{
			TcpClient client = new TcpClient();

			try
			{
				if(!client.ConnectAsync(serverName, port).Wait(ConnectTimeout))
					throw new TimeoutException(string.Format("No answer from {0}:{1}.", serverName, port));

				client.NoDelay = true;
				m_Client = client;
				m_Stream = client.GetStream();
			}
			catch(AggregateException e)
			{
				client.Close();
				throw e.InnerException;
			}
			catch
			{
				client.Close();
				throw;
			}
		}

		private static void Close()
		{
			if(m_Client != null)
				m_Client.Close();

			m_Client = null;
			m_Stream = null;
		}

		private static BinaryReader Receive(OrbProtocol.Message expected)
		{
			OrbProtocol.Message message;
			BinaryReader reader = OrbProtocol.Receive(m_Stream, out message);

			if(reader == null)
				throw new EndOfStreamException("The server closed the connection.");

			if(message != expected)
				throw new InvalidDataException(string.Format("Unexpected reply 0x{0:X2}.", (byte)message));

			return reader;
		}

		// A broken stream cannot be resynchronized: drop the connection, the user logs in again.
		private static string Fail(Exception e)
		{
			Close();

			if(e is InvalidDataException)
				return "Communication with the server failed.\n" + e.Message;

			return "Connection to the server was lost.\n" + e.Message;
		}

		private static void RaiseError(string message)
		{
			OrbErrorEvent handler = OnError;

			if(handler != null)
				handler(message);
			else
				Console.Error.WriteLine("Orb: " + message);
		}
	}
}

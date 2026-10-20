using System;
using System.IO;
using System.Text;

namespace OrbServerSDK
{
	/// <summary>
	/// Wire format of the Orb connection (replaces .NET Remoting over TCP).
	///
	/// Frame: [int32 length][byte message][payload], little endian, length covers message + payload.
	/// Client -> server: Login, Command, Request, Disconnect.
	/// Server -> client: one reply per Login/Command/Request, in order.
	/// </summary>
	internal static class OrbProtocol
	{
		public const int Magic = 0x52414F55; // "UOAR"
		public const ushort Version = 1;

		// A build request carries the whole design; 16 MB is far above any real house.
		public const int MaxFrameSize = 16 * 1024 * 1024;

		public enum Message : byte
		{
			Login = 0x01,
			Command = 0x02,
			Request = 0x03,
			Disconnect = 0x04,

			LoginResult = 0x81,
			CommandResult = 0x82,
			RequestResult = 0x83
		}

		public static BinaryWriter BeginFrame(Message message)
		{
			MemoryStream stream = new MemoryStream();
			BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8);

			writer.Write(0); // length, patched in Send
			writer.Write((byte)message);
			return writer;
		}

		public static void Send(Stream stream, BinaryWriter frame)
		{
			frame.Flush();

			MemoryStream buffer = (MemoryStream)frame.BaseStream;
			int length = (int)buffer.Length - 4;

			if(length > MaxFrameSize)
				throw new InvalidDataException(string.Format("Message is too large ({0} bytes).", length));

			byte[] data = buffer.GetBuffer();
			data[0] = (byte)length;
			data[1] = (byte)(length >> 8);
			data[2] = (byte)(length >> 16);
			data[3] = (byte)(length >> 24);

			stream.Write(data, 0, (int)buffer.Length);
			stream.Flush();
		}

		/// <summary>
		/// Reads one frame. Returns null when the peer closed the connection between frames.
		/// </summary>
		public static BinaryReader Receive(Stream stream, out Message message)
		{
			byte[] header = new byte[4];
			message = 0;

			if(!ReadExactly(stream, header, 4, true))
				return null;

			int length = header[0] | (header[1] << 8) | (header[2] << 16) | (header[3] << 24);

			if(length < 1 || length > MaxFrameSize)
				throw new InvalidDataException(string.Format("Invalid frame length {0}.", length));

			byte[] body = new byte[length];
			ReadExactly(stream, body, length, false);

			message = (Message)body[0];
			return new BinaryReader(new MemoryStream(body, 1, length - 1, false), Encoding.UTF8);
		}

		private static bool ReadExactly(Stream stream, byte[] buffer, int count, bool allowEndOfStream)
		{
			int offset = 0;

			while(offset < count)
			{
				int read = stream.Read(buffer, offset, count - offset);

				if(read <= 0)
				{
					if(allowEndOfStream && offset == 0)
						return false;

					throw new EndOfStreamException("Connection closed in the middle of a message.");
				}

				offset += read;
			}

			return true;
		}

		public static void WriteNullable(BinaryWriter writer, string value)
		{
			writer.Write(value != null);

			if(value != null)
				writer.Write(value);
		}

		public static string ReadNullable(BinaryReader reader)
		{
			return reader.ReadBoolean() ? reader.ReadString() : null;
		}

		public static void EnsureConsumed(BinaryReader reader)
		{
			if(reader.BaseStream.Position != reader.BaseStream.Length)
				throw new InvalidDataException("Unexpected data at the end of the message.");
		}
	}
}

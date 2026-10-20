using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

// [Serializable] / [NonSerialized] only mark what may travel over the wire (an allowlist);
// no formatter-based serialization is involved.
#pragma warning disable SYSLIB0050

namespace OrbServerSDK
{
	/// <summary>
	/// Field-based serializer for command/request arguments and responses.
	/// Replaces the BinaryFormatter that .NET Remoting used.
	///
	/// Only [Serializable] types from registered assemblies can travel over the wire,
	/// so the receiving side never instantiates an arbitrary type named by the peer.
	/// Supported values: primitives, enums, string, DateTime, TimeSpan, Guid, arrays,
	/// ArrayList (as used by CollectionBase) and registered classes/structs.
	/// </summary>
	public static class OrbSerializer
	{
		private const int MaxDepth = 32;

		private static readonly object m_Lock = new object();
		private static readonly Dictionary<string, Type> m_Types = new Dictionary<string, Type>(StringComparer.Ordinal);
		private static readonly Dictionary<Type, FieldInfo[]> m_Fields = new Dictionary<Type, FieldInfo[]>();
		private static readonly HashSet<Assembly> m_Assemblies = new HashSet<Assembly>();

		static OrbSerializer()
		{
			RegisterAssembly(typeof(OrbSerializer).Assembly);
		}

		/// <summary>
		/// Allows every [Serializable] type of the assembly to be sent and received.
		/// Both sides must register the same assemblies (e.g. UOArchitectInterface).
		/// </summary>
		public static void RegisterAssembly(Assembly assembly)
		{
			if(assembly == null)
				throw new ArgumentNullException("assembly");

			lock(m_Lock)
			{
				if(!m_Assemblies.Add(assembly))
					return;

				foreach(Type type in assembly.GetTypes())
				{
					bool serializable = type.IsEnum || (type.Attributes & TypeAttributes.Serializable) != 0;

					if(serializable && !type.IsAbstract && !type.IsInterface && !type.IsGenericTypeDefinition
						&& !typeof(Delegate).IsAssignableFrom(type))
						m_Types[type.FullName] = type;
				}
			}
		}

		public static void Write(BinaryWriter writer, object value)
		{
			new Encoder(writer).WriteValue(typeof(object), value, 0);
		}

		public static object Read(BinaryReader reader)
		{
			return new Decoder(reader).ReadValue(typeof(object), 0);
		}

		private static Type ResolveType(string name)
		{
			lock(m_Lock)
			{
				Type type;
				m_Types.TryGetValue(name, out type);
				return type;
			}
		}

		private static bool IsRegistered(Type type)
		{
			lock(m_Lock)
			{
				Type registered;
				return m_Types.TryGetValue(type.FullName, out registered) && registered == type;
			}
		}

		// Instance fields of the type and all its base classes, in a stable order.
		private static FieldInfo[] GetFields(Type type)
		{
			lock(m_Lock)
			{
				FieldInfo[] fields;

				if(m_Fields.TryGetValue(type, out fields))
					return fields;

				List<FieldInfo> list = new List<FieldInfo>();

				for(Type t = type; t != null && t != typeof(object) && t != typeof(ValueType); t = t.BaseType)
				{
					FieldInfo[] declared = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
					Array.Sort(declared, (a, b) => string.CompareOrdinal(a.Name, b.Name));

					foreach(FieldInfo field in declared)
					{
						if((field.Attributes & FieldAttributes.NotSerialized) == 0)
							list.Add(field);
					}
				}

				fields = list.ToArray();
				m_Fields[type] = fields;
				return fields;
			}
		}

		private enum Tag : byte
		{
			Null = 0,
			NewType = 1,
			KnownType = 2,
			String = 3,
			Array = 4,
			ArrayList = 5
		}

		private enum ElementKind : byte
		{
			Object = 0,
			Primitive = 1,
			Named = 2
		}

		private sealed class Encoder
		{
			private readonly BinaryWriter m_Writer;
			private readonly Dictionary<Type, int> m_TypeIds = new Dictionary<Type, int>();

			public Encoder(BinaryWriter writer)
			{
				m_Writer = writer;
			}

			public void WriteValue(Type declared, object value, int depth)
			{
				if(depth > MaxDepth)
					throw new InvalidDataException("Object graph is too deep.");

				if(WritePrimitive(declared, value))
					return;

				if(declared.IsValueType)
				{
					if(!IsRegistered(declared))
						throw new InvalidDataException(string.Format("Type {0} is not registered with OrbSerializer.", declared.FullName));

					// non-primitive struct: fields inline, no tag
					WriteFields(declared, value, depth);
					return;
				}

				if(value == null)
				{
					m_Writer.Write((byte)Tag.Null);
					return;
				}

				Type type = value.GetType();

				if(type == typeof(string))
				{
					m_Writer.Write((byte)Tag.String);
					m_Writer.Write((string)value);
				}
				else if(type.IsArray)
				{
					Array array = (Array)value;

					if(array.Rank != 1)
						throw new InvalidDataException("Multidimensional arrays are not supported.");

					Type element = type.GetElementType();

					m_Writer.Write((byte)Tag.Array);
					WriteTypeRef(element);
					m_Writer.Write(array.Length);

					for(int i = 0; i < array.Length; ++i)
						WriteValue(element, array.GetValue(i), depth + 1);
				}
				else if(type == typeof(ArrayList))
				{
					ArrayList list = (ArrayList)value;

					m_Writer.Write((byte)Tag.ArrayList);
					m_Writer.Write(list.Count);

					for(int i = 0; i < list.Count; ++i)
						WriteValue(typeof(object), list[i], depth + 1);
				}
				else
				{
					if(!IsRegistered(type))
						throw new InvalidDataException(string.Format("Type {0} is not registered with OrbSerializer.", type.FullName));

					WriteTypeTag(type);
					WriteFields(type, value, depth);
				}
			}

			private void WriteFields(Type type, object value, int depth)
			{
				foreach(FieldInfo field in GetFields(type))
					WriteValue(field.FieldType, field.GetValue(value), depth + 1);
			}

			private void WriteTypeTag(Type type)
			{
				int id;

				if(m_TypeIds.TryGetValue(type, out id))
				{
					m_Writer.Write((byte)Tag.KnownType);
					m_Writer.Write7BitEncodedInt(id);
				}
				else
				{
					m_TypeIds.Add(type, m_TypeIds.Count);
					m_Writer.Write((byte)Tag.NewType);
					m_Writer.Write(type.FullName);
				}
			}

			// Array element type: object, a primitive/string type code, or a registered type/enum by name.
			private void WriteTypeRef(Type element)
			{
				if(element == typeof(object))
				{
					m_Writer.Write((byte)ElementKind.Object);
				}
				else if(element.IsEnum || IsRegistered(element))
				{
					m_Writer.Write((byte)ElementKind.Named);
					m_Writer.Write(element.FullName);
				}
				else
				{
					TypeCode code = Type.GetTypeCode(element);

					if(code == TypeCode.Object || code == TypeCode.Empty || code == TypeCode.DBNull)
						throw new InvalidDataException(string.Format("Arrays of {0} are not supported by OrbSerializer.", element.FullName));

					m_Writer.Write((byte)ElementKind.Primitive);
					m_Writer.Write((byte)code);
				}
			}

			private bool WritePrimitive(Type declared, object value)
			{
				if(declared.IsEnum)
				{
					m_Writer.Write(Convert.ToInt64(value));
					return true;
				}

				switch(Type.GetTypeCode(declared))
				{
					case TypeCode.Boolean: m_Writer.Write((bool)value); return true;
					case TypeCode.Char: m_Writer.Write((ushort)(char)value); return true;
					case TypeCode.SByte: m_Writer.Write((sbyte)value); return true;
					case TypeCode.Byte: m_Writer.Write((byte)value); return true;
					case TypeCode.Int16: m_Writer.Write((short)value); return true;
					case TypeCode.UInt16: m_Writer.Write((ushort)value); return true;
					case TypeCode.Int32: m_Writer.Write((int)value); return true;
					case TypeCode.UInt32: m_Writer.Write((uint)value); return true;
					case TypeCode.Int64: m_Writer.Write((long)value); return true;
					case TypeCode.UInt64: m_Writer.Write((ulong)value); return true;
					case TypeCode.Single: m_Writer.Write((float)value); return true;
					case TypeCode.Double: m_Writer.Write((double)value); return true;
					case TypeCode.Decimal: m_Writer.Write((decimal)value); return true;
					case TypeCode.DateTime: m_Writer.Write(((DateTime)value).ToBinary()); return true;
				}

				if(declared == typeof(TimeSpan))
				{
					m_Writer.Write(((TimeSpan)value).Ticks);
					return true;
				}

				if(declared == typeof(Guid))
				{
					m_Writer.Write(((Guid)value).ToByteArray());
					return true;
				}

				return false;
			}
		}

		private sealed class Decoder
		{
			private readonly BinaryReader m_Reader;
			private readonly List<Type> m_Types = new List<Type>();

			public Decoder(BinaryReader reader)
			{
				m_Reader = reader;
			}

			private long Remaining
			{
				get{ return m_Reader.BaseStream.Length - m_Reader.BaseStream.Position; }
			}

			public object ReadValue(Type declared, int depth)
			{
				if(depth > MaxDepth)
					throw new InvalidDataException("Object graph is too deep.");

				object primitive;

				if(ReadPrimitive(declared, out primitive))
					return primitive;

				if(declared.IsValueType)
				{
					if(!IsRegistered(declared))
						throw new InvalidDataException(string.Format("Type {0} is not registered with OrbSerializer.", declared.FullName));

					return ReadFields(declared, depth);
				}

				Tag tag = (Tag)m_Reader.ReadByte();

				switch(tag)
				{
					case Tag.Null:
						return null;

					case Tag.String:
						return Check(declared, m_Reader.ReadString());

					case Tag.Array:
					{
						Type element = ReadTypeRef();
						int length = ReadCount();
						Array array = Array.CreateInstance(element, length);

						for(int i = 0; i < length; ++i)
							array.SetValue(ReadValue(element, depth + 1), i);

						return Check(declared, array);
					}

					case Tag.ArrayList:
					{
						int count = ReadCount();
						ArrayList list = new ArrayList(count);

						for(int i = 0; i < count; ++i)
							list.Add(ReadValue(typeof(object), depth + 1));

						return Check(declared, list);
					}

					case Tag.NewType:
					case Tag.KnownType:
					{
						Type type = ReadTypeTag(tag);
						return Check(declared, ReadFields(type, depth));
					}
				}

				throw new InvalidDataException(string.Format("Unknown value tag {0}.", (byte)tag));
			}

			private object ReadFields(Type type, int depth)
			{
				// no constructor runs; every serialized field is assigned below
				object obj = RuntimeHelpers.GetUninitializedObject(type);

				foreach(FieldInfo field in GetFields(type))
					field.SetValue(obj, ReadValue(field.FieldType, depth + 1));

				return obj;
			}

			private Type ReadTypeTag(Tag tag)
			{
				if(tag == Tag.KnownType)
				{
					int id = m_Reader.Read7BitEncodedInt();

					if(id < 0 || id >= m_Types.Count)
						throw new InvalidDataException("Invalid type reference.");

					return m_Types[id];
				}

				string name = m_Reader.ReadString();
				Type type = ResolveType(name);

				if(type == null)
					throw new InvalidDataException(string.Format("Type {0} is not registered with OrbSerializer.", name));

				m_Types.Add(type);
				return type;
			}

			private Type ReadTypeRef()
			{
				ElementKind kind = (ElementKind)m_Reader.ReadByte();

				if(kind == ElementKind.Object)
					return typeof(object);

				if(kind == ElementKind.Named)
				{
					string name = m_Reader.ReadString();
					Type type = ResolveType(name);

					if(type == null)
						throw new InvalidDataException(string.Format("Type {0} is not registered with OrbSerializer.", name));

					return type;
				}

				if(kind != ElementKind.Primitive)
					throw new InvalidDataException("Unknown array element kind.");

				switch((TypeCode)m_Reader.ReadByte())
				{
					case TypeCode.Boolean: return typeof(bool);
					case TypeCode.Char: return typeof(char);
					case TypeCode.SByte: return typeof(sbyte);
					case TypeCode.Byte: return typeof(byte);
					case TypeCode.Int16: return typeof(short);
					case TypeCode.UInt16: return typeof(ushort);
					case TypeCode.Int32: return typeof(int);
					case TypeCode.UInt32: return typeof(uint);
					case TypeCode.Int64: return typeof(long);
					case TypeCode.UInt64: return typeof(ulong);
					case TypeCode.Single: return typeof(float);
					case TypeCode.Double: return typeof(double);
					case TypeCode.Decimal: return typeof(decimal);
					case TypeCode.DateTime: return typeof(DateTime);
					case TypeCode.String: return typeof(string);
				}

				throw new InvalidDataException("Unknown array element type.");
			}

			// Every element takes at least one byte, so a count larger than the rest
			// of the frame is malformed – this caps allocations by the frame size.
			private int ReadCount()
			{
				int count = m_Reader.ReadInt32();

				if(count < 0 || count > Remaining)
					throw new InvalidDataException("Invalid element count.");

				return count;
			}

			private static object Check(Type declared, object value)
			{
				if(!declared.IsInstanceOfType(value))
					throw new InvalidDataException(string.Format("{0} cannot be assigned to {1}.", value.GetType().FullName, declared.FullName));

				return value;
			}

			private bool ReadPrimitive(Type declared, out object value)
			{
				if(declared.IsEnum)
				{
					value = Enum.ToObject(declared, m_Reader.ReadInt64());
					return true;
				}

				switch(Type.GetTypeCode(declared))
				{
					case TypeCode.Boolean: value = m_Reader.ReadBoolean(); return true;
					case TypeCode.Char: value = (char)m_Reader.ReadUInt16(); return true;
					case TypeCode.SByte: value = m_Reader.ReadSByte(); return true;
					case TypeCode.Byte: value = m_Reader.ReadByte(); return true;
					case TypeCode.Int16: value = m_Reader.ReadInt16(); return true;
					case TypeCode.UInt16: value = m_Reader.ReadUInt16(); return true;
					case TypeCode.Int32: value = m_Reader.ReadInt32(); return true;
					case TypeCode.UInt32: value = m_Reader.ReadUInt32(); return true;
					case TypeCode.Int64: value = m_Reader.ReadInt64(); return true;
					case TypeCode.UInt64: value = m_Reader.ReadUInt64(); return true;
					case TypeCode.Single: value = m_Reader.ReadSingle(); return true;
					case TypeCode.Double: value = m_Reader.ReadDouble(); return true;
					case TypeCode.Decimal: value = m_Reader.ReadDecimal(); return true;
					case TypeCode.DateTime: value = DateTime.FromBinary(m_Reader.ReadInt64()); return true;
				}

				if(declared == typeof(TimeSpan))
				{
					value = new TimeSpan(m_Reader.ReadInt64());
					return true;
				}

				if(declared == typeof(Guid))
				{
					value = new Guid(m_Reader.ReadBytes(16));
					return true;
				}

				value = null;
				return false;
			}
		}
	}
}

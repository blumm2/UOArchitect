using System;

namespace UOArchitectInterface
{
	[Serializable]
	public class DesignItem
	{
		// item IDs and hues are unsigned 16 bit (art goes up to 0xFFFF); short made IDs above 0x7FFF negative
		private ushort _itemID = 0;
		private int _x = 0;
		private int _y = 0;
		private int _z = 0;
		private short _level = 0;
		private ushort _hue = 0;

		public DesignItem()
		{
		}

		public DesignItem(int itemID, int x, int y, int z, int level, int hue)
		{
			_itemID = (ushort)itemID;
			_x = x;
			_y = y;
			_z = z;
			_level = (short)level;
			_hue = (ushort)hue;
		}	

		// int so the value is never negative; a negative input (old 16-bit data) keeps its bit pattern
		public int ItemID
		{
			get{ return _itemID; }
			set{ _itemID = (ushort)value; }
		}

		public int X
		{
			get{ return _x; }
			set{ _x = value; }
		}

		public int Y
		{
			get{ return _y; }
			set{ _y = value; }
		}

		public int Z
		{
			get{ return _z; }
			set{ _z = value; }
		}

		public short Level
		{
			get{ return _level; }
			set{ _level = value; }
		}

		public int Hue
		{
			get{ return _hue; }
			set{ _hue = (ushort)value; }
		}
	}
}

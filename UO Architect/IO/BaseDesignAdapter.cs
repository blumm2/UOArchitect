using System;
using System.Globalization;
using UOArchitectInterface;

namespace UOArchitect
{
	public abstract class BaseDesignAdapter
	{
		private string _filter;
		private string _title;

		public abstract DesignData ImportDesign();
		public abstract void Export(DesignData design);

		protected virtual string GetImportFileName()
		{
			return Utility.BrowseForFile(_filter, _title);
		}

		protected virtual string GetExportFileName()
		{
			return GetExportFileName("");
		}

		protected virtual string GetExportFileName(string defaultName)
		{
			return Utility.GetSaveFileName(_filter, _title, defaultName);
		}

		private BaseDesignAdapter()
		{
		}

		public BaseDesignAdapter(string filter, string title)
		{
			_filter = filter;
			_title = title;
		}

		// Item ID from a text file. IDs go up to 0xFFFF; files exported by older versions
		// wrote IDs above 0x7FFF as negative 16-bit numbers, those are mapped back.
		public static int ParseItemID(string text)
		{
			int id = int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);

			if(id < short.MinValue || id > ushort.MaxValue)
				throw new FormatException("Item ID out of range: " + text);

			return id & 0xFFFF;
		}

		public static int ParseInt(string text)
		{
			return int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
		}

		public string filter
		{
			get{ return _filter; }
		}

		public string Title
		{
			get{ return _title; }
		}
	}
}

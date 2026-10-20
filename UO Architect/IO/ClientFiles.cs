using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace UOArchitect
{
	/// <summary>
	/// Checks that the UO client files needed for previews and the designer can be found,
	/// and tells the user what is wrong instead of failing inside the Ultima reader.
	/// </summary>
	internal static class ClientFiles
	{
		private static readonly string[] Required = { "tiledata.mul", "art.mul", "artidx.mul", "hues.mul" };

		public static bool Check(IWin32Window owner)
		{
			string error = GetError();

			if(error == null)
				return true;

			MessageBox.Show(owner, error, "UO Client Files", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			return false;
		}

		public static string GetError()
		{
			if(Ultima.Client.Directories.Count == 0)
			{
				return "The UO client directory is not set and was not found in the registry.\n\n"
					+ "Uncheck \"Auto-detect\" on the Options tab and enter the folder that contains tiledata.mul and art.mul.";
			}

			StringBuilder missing = new StringBuilder();

			foreach(string file in Required)
			{
				if(Ultima.Client.GetFilePath(file) == null)
					missing.Append("\n  ").Append(file);
			}

			if(missing.Length == 0)
				return null;

			string message = "These UO client files were not found in\n  " + string.Join("\n  ", DirectoriesAsArray()) + "\n\nMissing:" + missing;

			if(Ultima.Client.GetFilePath("art.mul") == null && Ultima.Client.GetFilePath("artLegacyMUL.uop") != null)
				message += "\n\nThe client has art only in artLegacyMUL.uop. UO Architect reads art.mul/artidx.mul; "
					+ "convert the UOP file (e.g. with UOFiddler or a MUL/UOP converter) or point it to a client folder with art.mul.";

			return message;
		}

		private static string[] DirectoriesAsArray()
		{
			string[] dirs = new string[Ultima.Client.Directories.Count];
			Ultima.Client.Directories.CopyTo(dirs);
			return dirs;
		}
	}
}

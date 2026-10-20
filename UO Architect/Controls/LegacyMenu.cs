using System;
using System.Drawing;
using System.Windows.Forms;

namespace UOArchitect
{
	/// <summary>
	/// Puts a MenuStrip on a form that was designed for the .NET 1.x MainMenu.
	///
	/// MainMenu lived outside the client area; MenuStrip is a docked control inside it.
	/// The form grows by the menu height so its content keeps the designed size and
	/// position below the menu.
	/// </summary>
	internal static class LegacyMenu
	{
		/// <param name="wrapContent">
		/// true: move the form's controls into a panel under the menu (keeps anchors and
		/// AutoScroll working). false: the form paints itself; only shift its controls down.
		/// </param>
		public static void Attach(Form form, MenuStrip menu, bool wrapContent)
		{
			int height = menu.GetPreferredSize(Size.Empty).Height;
			Size clientSize = form.ClientSize;

			if(wrapContent)
			{
				Panel content = new Panel();
				content.Dock = DockStyle.Fill;
				content.AutoScroll = form.AutoScroll;
				content.BackColor = form.BackColor;

				Control[] controls = new Control[form.Controls.Count];
				form.Controls.CopyTo(controls, 0);

				foreach(Control control in controls)
					content.Controls.Add(control);

				form.AutoScroll = false;
				form.Controls.Add(content);
			}
			else
			{
				foreach(Control control in form.Controls)
				{
					if(control.Dock == DockStyle.None)
						control.Top += height;
				}
			}

			menu.Dock = DockStyle.Top;
			form.Controls.Add(menu);
			form.MainMenuStrip = menu;
			form.ClientSize = new Size(clientSize.Width, clientSize.Height + height);
		}
	}
}

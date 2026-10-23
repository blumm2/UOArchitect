using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Windows.Forms;

namespace UOArchitect
{
	public class ToolBox : System.Windows.Forms.UserControl
	{
		// width of the resize border along the right and bottom edge
		private const int GripSize = 8;
		private const int TileMargin = 10;
		private const int PaletteCellWidth = 54, PaletteCellHeight = 74;

		private System.Windows.Forms.PictureBox picRoot;
		private System.ComponentModel.IContainer components;

		private TileSet m_Root = TileSet.Toolbox;//new RootSet();
		private System.Windows.Forms.ToolTip toolTip;
		private System.Windows.Forms.Panel pnlTileSet;

		private HouseDesigner m_Designer;

		private TileSet m_TileSet;
		private bool m_Palette;

		private bool m_ResizeX, m_ResizeY;
		private Point m_ResizeStart;
		private Size m_ResizeStartSize;

		public HouseDesigner Designer{ get{ return m_Designer; } set{ m_Designer = value; } }

		public ToolBox()
		{
			InitializeComponent();

			SetStyle( ControlStyles.ResizeRedraw, true );

			picRoot.Image = new Bitmap( "Internal/Graphics/tb_back.png" );

			for ( int i = 0; i < m_Root.Entries.Count; ++i )
			{
				PictureBox box = new PictureBox();

				box.BackColor = Color.Transparent;
				box.Tag = m_Root.Entries[i];
				box.Image = ((TileSet)m_Root.Entries[i]).Image;
				box.SetBounds( 2 + (i % 3) * 31, 2 + (i / 3) * 31, 30, 30 );
				box.MouseEnter += new EventHandler(box_MouseEnter);
				box.MouseLeave += new EventHandler(box_MouseLeave);
				box.Click += new EventHandler(box_Click);

				toolTip.SetToolTip( box,((TileSet)m_Root.Entries[i]).Name );

				picRoot.Controls.Add( box );
			}
		}

		protected override void Dispose( bool disposing )
		{
			if( disposing )
			{
				if(components != null)
				{
					components.Dispose();
				}
			}
			base.Dispose( disposing );
		}

		#region Component Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.components = new System.ComponentModel.Container();
			this.picRoot = new System.Windows.Forms.PictureBox();
			this.toolTip = new System.Windows.Forms.ToolTip(this.components);
			this.pnlTileSet = new System.Windows.Forms.Panel();
			this.SuspendLayout();
			//
			// picRoot
			//
			this.picRoot.Location = new System.Drawing.Point(8, 8);
			this.picRoot.Name = "picRoot";
			this.picRoot.Size = new System.Drawing.Size(96, 96);
			this.picRoot.TabIndex = 0;
			this.picRoot.TabStop = false;
			//
			// pnlTileSet
			//
			this.pnlTileSet.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
				| System.Windows.Forms.AnchorStyles.Left)
				| System.Windows.Forms.AnchorStyles.Right)));
			this.pnlTileSet.AutoScroll = true;
			this.pnlTileSet.BackColor = System.Drawing.Color.Black;
			this.pnlTileSet.Location = new System.Drawing.Point(112, 8);
			this.pnlTileSet.Name = "pnlTileSet";
			this.pnlTileSet.Size = new System.Drawing.Size(480, 144);
			this.pnlTileSet.TabIndex = 1;
			this.pnlTileSet.Resize += new System.EventHandler(this.pnlTileSet_Resize);
			//
			// ToolBox
			//
			this.Controls.Add(this.pnlTileSet);
			this.Controls.Add(this.picRoot);
			this.MinimumSize = new System.Drawing.Size(240, 112);
			this.Name = "ToolBox";
			this.Size = new System.Drawing.Size(600, 160);
			this.ResumeLayout(false);

		}
		#endregion

		private void box_MouseEnter(object sender, EventArgs e)
		{
			PictureBox box = (PictureBox)sender;
			TileSet tileSet = (TileSet)box.Tag;

			box.Image = tileSet.SelectedImage;
		}

		private void box_MouseLeave(object sender, EventArgs e)
		{
			PictureBox box = (PictureBox)sender;
			TileSet tileSet = (TileSet)box.Tag;

			box.Image = tileSet.Image;
		}

		private void box_Click(object sender, EventArgs e)
		{
			PictureBox box = (PictureBox)sender;
			TileSet tileSet = (TileSet)box.Tag;

			if ( tileSet.Entries.Count > 0 && tileSet.Entries[0] is TileSetEntry && ((TileSetEntry)tileSet.Entries[0]).Destroy )
				m_Designer.TileCursor = (TileSetEntry)tileSet.Entries[0];
			else
				SetTileSet( tileSet, true );
		}

		public void SetTileSet( TileSet tileSet, bool palette )
		{
			m_TileSet = tileSet;
			m_Palette = palette;

			pnlTileSet.SuspendLayout();
			pnlTileSet.Controls.Clear();
			pnlTileSet.AutoScrollPosition = Point.Empty;

			if ( tileSet != null )
			{
				for ( int i = 0; i < tileSet.Entries.Count; ++i )
				{
					PictureBox tileEntry = new PictureBox();

					Bitmap img = tileSet.Entries[i] is TileSet ?
						((TileSet)tileSet.Entries[i]).Image :
						((TileSetEntry)tileSet.Entries[i]).Image;

					tileEntry.BackColor = Color.Transparent;
					tileEntry.Image = img != null ? img : Utility.MissingArt;
					tileEntry.Tag = tileSet.Entries[i];

					if ( tileSet.Entries[i] is TileSet )
						tileEntry.Click += new EventHandler(tileSet_Click);
					else
						tileEntry.Click += new EventHandler(tileEntry_Click);

					if ( tileSet.Entries[i] is TileSet )
						toolTip.SetToolTip( tileEntry, ((TileSet)tileSet.Entries[i]).Name );

					pnlTileSet.Controls.Add( tileEntry );
				}
			}

			LayoutTiles();
			pnlTileSet.ResumeLayout();
		}

		// Wraps the tiles into rows by the current width of the panel, so the
		// toolbox can be resized and the rest is reachable by the scrollbar.
		private void LayoutTiles()
		{
			if ( m_TileSet == null )
				return;

			// always reserve room for the vertical scrollbar, otherwise its
			// appearance would change the width and re-wrap the rows again
			int width = pnlTileSet.Width - SystemInformation.VerticalScrollBarWidth - TileMargin;
			Point origin = pnlTileSet.AutoScrollPosition;
			int count = pnlTileSet.Controls.Count;

			if ( m_Palette )
			{
				int columns = Math.Max( 1, (width - TileMargin) / PaletteCellWidth );

				for ( int i = 0; i < count; ++i )
				{
					Control tileEntry = pnlTileSet.Controls[i];
					int height = count > columns ? 64 : ((PictureBox)tileEntry).Image.Height;

					tileEntry.SetBounds( origin.X + TileMargin + (i % columns) * PaletteCellWidth, origin.Y + TileMargin + (i / columns) * PaletteCellHeight, 44, height );
				}
			}
			else
			{
				int x = TileMargin, y = TileMargin, rowHeight = 0;

				for ( int i = 0; i < count; ++i )
				{
					Control tileEntry = pnlTileSet.Controls[i];
					Image img = ((PictureBox)tileEntry).Image;

					if ( x > TileMargin && x + img.Width > width )
					{
						x = TileMargin;
						y += rowHeight + TileMargin;
						rowHeight = 0;
					}

					tileEntry.SetBounds( origin.X + x, origin.Y + y, img.Width, img.Height );

					x += img.Width + TileMargin;
					rowHeight = Math.Max( rowHeight, img.Height );
				}
			}
		}

		private void pnlTileSet_Resize(object sender, EventArgs e)
		{
			pnlTileSet.SuspendLayout();
			LayoutTiles();
			pnlTileSet.ResumeLayout();
		}

		protected override void OnPaint( PaintEventArgs e )
		{
			base.OnPaint( e );

			ControlPaint.DrawSizeGrip( e.Graphics, BackColor, ClientSize.Width - 16, ClientSize.Height - 16, 16, 16 );
		}

		// The right and bottom border resize the toolbox; anywhere else the
		// mouse events go to the designer, which moves the toolbox around.
		protected override void OnMouseDown( MouseEventArgs e )
		{
			if ( e.Button == MouseButtons.Left )
			{
				m_ResizeX = e.X >= ClientSize.Width - GripSize;
				m_ResizeY = e.Y >= ClientSize.Height - GripSize;

				if ( m_ResizeX || m_ResizeY )
				{
					m_ResizeStart = Control.MousePosition;
					m_ResizeStartSize = Size;
					return;
				}
			}

			base.OnMouseDown( e );
		}

		protected override void OnMouseMove( MouseEventArgs e )
		{
			if ( m_ResizeX || m_ResizeY )
			{
				Point mouse = Control.MousePosition;
				int width = m_ResizeStartSize.Width, height = m_ResizeStartSize.Height;

				if ( m_ResizeX )
					width += mouse.X - m_ResizeStart.X;

				if ( m_ResizeY )
					height += mouse.Y - m_ResizeStart.Y;

				if ( Parent != null )
				{
					width = Math.Min( width, Parent.ClientSize.Width - Left );
					height = Math.Min( height, Parent.ClientSize.Height - Top );
				}

				Size = new Size( Math.Max( width, MinimumSize.Width ), Math.Max( height, MinimumSize.Height ) );
				return;
			}

			bool right = e.X >= ClientSize.Width - GripSize;
			bool bottom = e.Y >= ClientSize.Height - GripSize;

			if ( right && bottom )
				Cursor = Cursors.SizeNWSE;
			else if ( right )
				Cursor = Cursors.SizeWE;
			else if ( bottom )
				Cursor = Cursors.SizeNS;
			else
				Cursor = Cursors.Default;

			base.OnMouseMove( e );
		}

		protected override void OnMouseUp( MouseEventArgs e )
		{
			if ( m_ResizeX || m_ResizeY )
			{
				m_ResizeX = m_ResizeY = false;
				return;
			}

			base.OnMouseUp( e );
		}

		private void tileSet_Click(object sender, EventArgs e)
		{
			PictureBox box = (PictureBox)sender;
			TileSet tileSet = (TileSet)box.Tag;

			if ( tileSet.Entries.Count > 0 && tileSet.Entries[0] is TileSetEntry && ((TileSetEntry)tileSet.Entries[0]).Destroy )
				m_Designer.TileCursor = (TileSetEntry)tileSet.Entries[0];
			else
				SetTileSet( tileSet, false );
		}

		private void tileEntry_Click(object sender, EventArgs e)
		{
			PictureBox box = (PictureBox)sender;
			TileSetEntry tileEntry = (TileSetEntry)box.Tag;

			m_Designer.TileCursor = tileEntry;
		}
	}
}

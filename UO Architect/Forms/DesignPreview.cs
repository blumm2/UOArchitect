using System;
using System.Drawing;
using System.Collections;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Ultima;

namespace UOArchitect
{
	public class DesignPreview : System.Windows.Forms.Form
	{
		private HouseDesign m_Design = null;
		private FloorView m_Level = FloorView.Roof;

		private enum FloorView
		{
			Foundation = 0,
			First = 1,
			Second = 2,
			Third = 3,
			Roof = 4
		}

		private System.Windows.Forms.PictureBox picPreview;
		private System.Windows.Forms.MenuStrip mainMenu1;
		private System.Windows.Forms.ToolStripMenuItem mnuFile;
		private System.Windows.Forms.ToolStripMenuItem mnuExit;
		private System.Windows.Forms.ToolStripMenuItem mnuFloorView;
		private System.Windows.Forms.ToolStripMenuItem mnuFirst;
		private System.Windows.Forms.ToolStripMenuItem mnuSecond;
		private System.Windows.Forms.ToolStripMenuItem mnuThird;
		private System.Windows.Forms.ToolStripMenuItem mnuSavePicture;
		private System.Windows.Forms.ToolStripSeparator menuItem1;
		private System.Windows.Forms.ToolStripSeparator menuItem2;
		private System.Windows.Forms.ToolStripMenuItem mnuSaveBmp;
		private System.Windows.Forms.ToolStripMenuItem mnuSaveGif;
		private System.Windows.Forms.ToolStripMenuItem mnuSaveJpeg;
		private System.Windows.Forms.ToolStripMenuItem mnuSavePng;
		private System.Windows.Forms.ToolStripMenuItem mnuFoundation;
		private System.Windows.Forms.ToolStripMenuItem mnuRoof;
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.Container components = null;

		public DesignPreview()
		{
			//
			// Required for Windows Form Designer support
			//
			InitializeComponent();
		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
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

		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			System.Resources.ResourceManager resources = new System.Resources.ResourceManager(typeof(DesignPreview));
			this.picPreview = new System.Windows.Forms.PictureBox();
			this.mainMenu1 = new System.Windows.Forms.MenuStrip();
			this.mnuFile = new System.Windows.Forms.ToolStripMenuItem();
			this.menuItem1 = new System.Windows.Forms.ToolStripSeparator();
			this.mnuSavePicture = new System.Windows.Forms.ToolStripMenuItem();
			this.mnuSaveBmp = new System.Windows.Forms.ToolStripMenuItem();
			this.mnuSaveGif = new System.Windows.Forms.ToolStripMenuItem();
			this.mnuSaveJpeg = new System.Windows.Forms.ToolStripMenuItem();
			this.mnuSavePng = new System.Windows.Forms.ToolStripMenuItem();
			this.menuItem2 = new System.Windows.Forms.ToolStripSeparator();
			this.mnuExit = new System.Windows.Forms.ToolStripMenuItem();
			this.mnuFloorView = new System.Windows.Forms.ToolStripMenuItem();
			this.mnuFoundation = new System.Windows.Forms.ToolStripMenuItem();
			this.mnuFirst = new System.Windows.Forms.ToolStripMenuItem();
			this.mnuSecond = new System.Windows.Forms.ToolStripMenuItem();
			this.mnuThird = new System.Windows.Forms.ToolStripMenuItem();
			this.mnuRoof = new System.Windows.Forms.ToolStripMenuItem();
			this.SuspendLayout();
			// 
			// picPreview
			// 
			this.picPreview.Location = new System.Drawing.Point(0, 6);
			this.picPreview.Name = "picPreview";
			this.picPreview.Size = new System.Drawing.Size(512, 344);
			this.picPreview.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
			this.picPreview.TabIndex = 1;
			this.picPreview.TabStop = false;
			// 
			// mainMenu1
			// 
			this.mainMenu1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
																					  this.mnuFile,
																					  this.mnuFloorView});
			// 
			// mnuFile
			// 
			this.mnuFile.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
																					this.menuItem1,
																					this.mnuSavePicture,
																					this.menuItem2,
																					this.mnuExit});
			this.mnuFile.Text = "File";
			// 
			// menuItem1
			// 
			// 
			// mnuSavePicture
			// 
			this.mnuSavePicture.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
																						   this.mnuSaveBmp,
																						   this.mnuSaveGif,
																						   this.mnuSaveJpeg,
																						   this.mnuSavePng});
			this.mnuSavePicture.Text = "Save Picture";
			// 
			// mnuSaveBmp
			// 
			this.mnuSaveBmp.Text = "Bitmap (.bmp)";
			this.mnuSaveBmp.Click += new System.EventHandler(this.mnuSaveBmp_Click);
			// 
			// mnuSaveGif
			// 
			this.mnuSaveGif.Text = "Gif (.gif)";
			this.mnuSaveGif.Click += new System.EventHandler(this.mnuSaveGif_Click);
			// 
			// mnuSaveJpeg
			// 
			this.mnuSaveJpeg.Text = "Jpeg (.jpeg)";
			this.mnuSaveJpeg.Click += new System.EventHandler(this.mnuSaveJpeg_Click);
			// 
			// mnuSavePng
			// 
			this.mnuSavePng.Text = "Png (.png)";
			this.mnuSavePng.Click += new System.EventHandler(this.mnuSavePng_Click);
			// 
			// menuItem2
			// 
			// 
			// mnuExit
			// 
			this.mnuExit.Text = "Exit";
			this.mnuExit.Click += new System.EventHandler(this.mnuExit_Click);
			// 
			// mnuFloorView
			// 
			this.mnuFloorView.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
																						 this.mnuFoundation,
																						 this.mnuFirst,
																						 this.mnuSecond,
																						 this.mnuThird,
																						 this.mnuRoof});
			this.mnuFloorView.Text = "Floor View";
			// 
			// mnuFoundation
			// 
			this.mnuFoundation.Text = "Foundation";
			this.mnuFoundation.Click += new System.EventHandler(this.mnuFoundationl_Click);
			// 
			// mnuFirst
			// 
			this.mnuFirst.ShortcutKeys = System.Windows.Forms.Keys.F1;
			this.mnuFirst.Text = "First";
			this.mnuFirst.Click += new System.EventHandler(this.mnuFirst_Click);
			// 
			// mnuSecond
			// 
			this.mnuSecond.ShortcutKeys = System.Windows.Forms.Keys.F2;
			this.mnuSecond.Text = "Second";
			this.mnuSecond.Click += new System.EventHandler(this.mnuSecond_Click);
			// 
			// mnuThird
			// 
			this.mnuThird.ShortcutKeys = System.Windows.Forms.Keys.F3;
			this.mnuThird.Text = "Third";
			this.mnuThird.Click += new System.EventHandler(this.mnuThird_Click);
			// 
			// mnuRoof
			// 
			this.mnuRoof.ShortcutKeys = System.Windows.Forms.Keys.F4;
			this.mnuRoof.Text = "Roof";
			this.mnuRoof.Click += new System.EventHandler(this.mnuRoof_Click);
			// 
			// DesignPreview
			// 
			this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
			this.AutoScroll = true;
			this.ClientSize = new System.Drawing.Size(592, 425);
			this.Controls.Add(this.picPreview);
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			LegacyMenu.Attach(this, this.mainMenu1, true);
			this.Name = "DesignPreview";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "Preview";
			this.Closed += new System.EventHandler(this.TemplatePreview_Closed);
			this.ResumeLayout(false);

		}
		#endregion

		public void LoadForm(HouseDesign design)
		{
			m_Design = design;
			this.Text = string.Format("Design Preview - {0} ({1} items)", design.Name, design.FileHeader.RecordCount);
			m_Level = FloorView.Roof;
			this.WindowState = FormWindowState.Maximized;
			DisplayDesign();

			Show();

			Cursor.Current = Cursors.Default;

		}

		private void DisplayDesign()
		{
			this.Cursor = Cursors.WaitCursor;
			picPreview.Image = m_Design.GetPreviewImage((int)m_Level);
			this.Cursor = Cursors.Default;
		}

		private void mnuFirst_Click(object sender, System.EventArgs e)
		{
			m_Level = FloorView.First;
			DisplayDesign();
		}

		private void mnuSecond_Click(object sender, System.EventArgs e)
		{
			m_Level = FloorView.Second;
			DisplayDesign();
		}

		private void mnuThird_Click(object sender, System.EventArgs e)
		{
			m_Level = FloorView.Third;
			DisplayDesign();
		}

		private void mnuExit_Click(object sender, System.EventArgs e)
		{
			Close();
		}

		private void TemplatePreview_Closed(object sender, System.EventArgs e)
		{
			Dispose();
		}	

		private void mnuSaveBmp_Click(object sender, System.EventArgs e)
		{
			this.Cursor = Cursors.WaitCursor;
			Utility.SaveImageToDisk(picPreview.Image, ImageFormat.Bmp, this);
			this.Cursor = Cursors.Default;
		}

		private void mnuSaveGif_Click(object sender, System.EventArgs e)
		{
			this.Cursor = Cursors.WaitCursor;
			Utility.SaveImageToDisk(picPreview.Image, ImageFormat.Gif, this);
			this.Cursor = Cursors.Default;
		}

		private void mnuSaveJpeg_Click(object sender, System.EventArgs e)
		{
			this.Cursor = Cursors.WaitCursor;
			Utility.SaveImageToDisk(picPreview.Image, ImageFormat.Jpeg, this);
			this.Cursor = Cursors.Default;
		}

		private void mnuSavePng_Click(object sender, System.EventArgs e)
		{
			this.Cursor = Cursors.WaitCursor;
			Utility.SaveImageToDisk(picPreview.Image, ImageFormat.Png, this);
			this.Cursor = Cursors.Default;
		}

		private void mnuFoundationl_Click(object sender, System.EventArgs e)
		{
			m_Level = FloorView.Foundation;
			DisplayDesign();	
		}

		private void mnuRoof_Click(object sender, System.EventArgs e)
		{
			m_Level = FloorView.Roof;
			DisplayDesign();
		}

	}
}

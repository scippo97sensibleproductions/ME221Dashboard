using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace VirtualDyno;

public class ReleaseNotes : Form
{
	private IContainer components;

	private Button btnOK;

	private TextBox txtReleaseNotes;

	private SplitContainer splitTopBottom;

	public ReleaseNotes()
	{
		InitializeComponent();
		btnOK.Select();
	}

	private void ReleaseNotes_Load(object sender, EventArgs e)
	{
		try
		{
			using StreamReader streamReader = new StreamReader("ReleaseNotes.txt");
			txtReleaseNotes.Text = streamReader.ReadToEnd();
		}
		catch
		{
		}
	}

	private void btnOK_Click(object sender, EventArgs e)
	{
		Close();
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && components != null)
		{
			components.Dispose();
		}
		base.Dispose(disposing);
	}

	private void InitializeComponent()
	{
		System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VirtualDyno.ReleaseNotes));
		this.btnOK = new System.Windows.Forms.Button();
		this.txtReleaseNotes = new System.Windows.Forms.TextBox();
		this.splitTopBottom = new System.Windows.Forms.SplitContainer();
		this.splitTopBottom.Panel1.SuspendLayout();
		this.splitTopBottom.Panel2.SuspendLayout();
		this.splitTopBottom.SuspendLayout();
		base.SuspendLayout();
		this.btnOK.Dock = System.Windows.Forms.DockStyle.Right;
		this.btnOK.Location = new System.Drawing.Point(447, 0);
		this.btnOK.Name = "btnOK";
		this.btnOK.Size = new System.Drawing.Size(92, 28);
		this.btnOK.TabIndex = 1;
		this.btnOK.Text = "OK";
		this.btnOK.UseVisualStyleBackColor = true;
		this.btnOK.Click += new System.EventHandler(btnOK_Click);
		this.txtReleaseNotes.Dock = System.Windows.Forms.DockStyle.Fill;
		this.txtReleaseNotes.Location = new System.Drawing.Point(0, 0);
		this.txtReleaseNotes.Multiline = true;
		this.txtReleaseNotes.Name = "txtReleaseNotes";
		this.txtReleaseNotes.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
		this.txtReleaseNotes.Size = new System.Drawing.Size(539, 458);
		this.txtReleaseNotes.TabIndex = 3;
		this.splitTopBottom.Dock = System.Windows.Forms.DockStyle.Fill;
		this.splitTopBottom.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
		this.splitTopBottom.IsSplitterFixed = true;
		this.splitTopBottom.Location = new System.Drawing.Point(0, 0);
		this.splitTopBottom.Name = "splitTopBottom";
		this.splitTopBottom.Orientation = System.Windows.Forms.Orientation.Horizontal;
		this.splitTopBottom.Panel1.Controls.Add(this.txtReleaseNotes);
		this.splitTopBottom.Panel2.Controls.Add(this.btnOK);
		this.splitTopBottom.Panel2MinSize = 20;
		this.splitTopBottom.Size = new System.Drawing.Size(539, 487);
		this.splitTopBottom.SplitterDistance = 458;
		this.splitTopBottom.SplitterWidth = 1;
		this.splitTopBottom.TabIndex = 4;
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.ClientSize = new System.Drawing.Size(539, 487);
		base.Controls.Add(this.splitTopBottom);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
		base.Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
		base.MaximizeBox = false;
		base.MinimizeBox = false;
		base.Name = "ReleaseNotes";
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
		this.Text = "ReleaseNotes";
		base.Load += new System.EventHandler(ReleaseNotes_Load);
		this.splitTopBottom.Panel1.ResumeLayout(false);
		this.splitTopBottom.Panel1.PerformLayout();
		this.splitTopBottom.Panel2.ResumeLayout(false);
		this.splitTopBottom.ResumeLayout(false);
		base.ResumeLayout(false);
	}
}

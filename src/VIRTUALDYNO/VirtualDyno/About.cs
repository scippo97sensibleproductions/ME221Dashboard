using System;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using VirtualDyno.Core;
using VirtualDyno.Core.Datasets;
using VirtualDyno.Properties;

namespace VirtualDyno;

internal class About : Form
{
	private string _baseFilePath = "";

	private IContainer components;

	private TextBox textBoxDescription;

	private Button okButton;

	private TextBox textBox3;

	private TextBox textBox4;

	private TextBox txtVersion;

	private TextBox txtCopyright;

	private PictureBox logoPictureBox;

	private Button btnReleaseNotes;

	private LinkLabel linkMellonTuning;

	private LinkLabel linkBoostedTuning;

	private LinkLabel linkBradBarnhill;

	private PictureBox btnDonate_AboutScreen;

	private Label lblColumnsVersion;

	private Label lblCarDefinitionsVersion;

	private LinkLabel linkRoyHemrich;

	private Label lblClientId;

	public static string AssemblyTitle
	{
		get
		{
			object[] customAttributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), inherit: false);
			if (customAttributes.Length != 0)
			{
				AssemblyTitleAttribute assemblyTitleAttribute = (AssemblyTitleAttribute)customAttributes[0];
				if (!string.IsNullOrEmpty(assemblyTitleAttribute.Title))
				{
					return assemblyTitleAttribute.Title;
				}
			}
			return Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().CodeBase);
		}
	}

	public static string AssemblyDescription
	{
		get
		{
			object[] customAttributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyDescriptionAttribute), inherit: false);
			if (customAttributes.Length == 0)
			{
				return "";
			}
			return ((AssemblyDescriptionAttribute)customAttributes[0]).Description;
		}
	}

	public static string AssemblyProduct
	{
		get
		{
			object[] customAttributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyProductAttribute), inherit: false);
			if (customAttributes.Length == 0)
			{
				return "";
			}
			return ((AssemblyProductAttribute)customAttributes[0]).Product;
		}
	}

	public static string AssemblyCopyright
	{
		get
		{
			object[] customAttributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute), inherit: false);
			if (customAttributes.Length == 0)
			{
				return "";
			}
			return ((AssemblyCopyrightAttribute)customAttributes[0]).Copyright;
		}
	}

	public About(string baseFilepath)
	{
		InitializeComponent();
		_baseFilePath = baseFilepath;
		_ = Assembly.GetExecutingAssembly().GetName().Version;
		Text = $"About {AssemblyTitle}";
		txtVersion.Text = Statics.Version();
		txtCopyright.Text = AssemblyCopyright;
		textBoxDescription.Text = AssemblyDescription;
		using (CarsVersion carsVersion = new CarsVersion())
		{
			if (Directory.Exists("Cars") && File.Exists(Path.Combine(baseFilepath, VirtualDyno.Properties.Settings.Default.File_CarsVersion)))
			{
				carsVersion.ReadXml(VirtualDyno.Properties.Settings.Default.File_CarsVersion);
				lblCarDefinitionsVersion.Text = "Car Definitions Version: " + ((DataTable)(object)carsVersion.CarVersion).Rows[0]["Version"].ToString();
			}
		}
		using (Columns columns = new Columns())
		{
			columns.ReadXml(Path.Combine(baseFilepath, VirtualDyno.Properties.Settings.Default.File_ColumnNames));
			lblColumnsVersion.Text = "Column Names Version: " + ((DataTable)(object)columns.ColumnVersion).Rows[0]["Version"].ToString();
		}
		lblClientId.Text = "Client Id: " + Analytics.GetAnalyticsId().ToString();
	}

	private void okButton_Click(object sender, EventArgs e)
	{
		Close();
	}

	private void btnReleaseNotes_Click(object sender, EventArgs e)
	{
		new ReleaseNotes().ShowDialog();
	}

	private void linkMellonTuning_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
	{
	}

	private void linkBoostedTuning_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
	{
		Process.Start("http://www.boostedtuning.com");
	}

	private void linkBradBarnhill_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
	{
		Process.Start("http://www.bradbarnhill.com");
	}

	private void linkRoyHemrich_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
	{
		Process.Start("http://www.royhemrich.com");
	}

	private void btnDonate_AboutScreen_Click(object sender, EventArgs e)
	{
		Process.Start(VirtualDyno.Properties.Settings.Default.DONATE_URL);
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
		System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VirtualDyno.About));
		this.textBoxDescription = new System.Windows.Forms.TextBox();
		this.okButton = new System.Windows.Forms.Button();
		this.textBox3 = new System.Windows.Forms.TextBox();
		this.textBox4 = new System.Windows.Forms.TextBox();
		this.txtVersion = new System.Windows.Forms.TextBox();
		this.txtCopyright = new System.Windows.Forms.TextBox();
		this.logoPictureBox = new System.Windows.Forms.PictureBox();
		this.btnReleaseNotes = new System.Windows.Forms.Button();
		this.linkMellonTuning = new System.Windows.Forms.LinkLabel();
		this.linkBoostedTuning = new System.Windows.Forms.LinkLabel();
		this.linkBradBarnhill = new System.Windows.Forms.LinkLabel();
		this.btnDonate_AboutScreen = new System.Windows.Forms.PictureBox();
		this.lblColumnsVersion = new System.Windows.Forms.Label();
		this.lblCarDefinitionsVersion = new System.Windows.Forms.Label();
		this.linkRoyHemrich = new System.Windows.Forms.LinkLabel();
		this.lblClientId = new System.Windows.Forms.Label();
		((System.ComponentModel.ISupportInitialize)this.logoPictureBox).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.btnDonate_AboutScreen).BeginInit();
		base.SuspendLayout();
		this.textBoxDescription.BorderStyle = System.Windows.Forms.BorderStyle.None;
		this.textBoxDescription.Location = new System.Drawing.Point(10, 338);
		this.textBoxDescription.Margin = new System.Windows.Forms.Padding(9, 5, 4, 5);
		this.textBoxDescription.Multiline = true;
		this.textBoxDescription.Name = "textBoxDescription";
		this.textBoxDescription.ReadOnly = true;
		this.textBoxDescription.Size = new System.Drawing.Size(458, 85);
		this.textBoxDescription.TabIndex = 23;
		this.textBoxDescription.TabStop = false;
		this.textBoxDescription.Text = "Description";
		this.okButton.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
		this.okButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
		this.okButton.Location = new System.Drawing.Point(502, 417);
		this.okButton.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.okButton.Name = "okButton";
		this.okButton.Size = new System.Drawing.Size(141, 52);
		this.okButton.TabIndex = 25;
		this.okButton.Text = "&OK";
		this.okButton.Click += new System.EventHandler(okButton_Click);
		this.textBox3.BorderStyle = System.Windows.Forms.BorderStyle.None;
		this.textBox3.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
		this.textBox3.Location = new System.Drawing.Point(303, 9);
		this.textBox3.Margin = new System.Windows.Forms.Padding(9, 5, 4, 5);
		this.textBox3.Name = "textBox3";
		this.textBox3.ReadOnly = true;
		this.textBox3.Size = new System.Drawing.Size(128, 26);
		this.textBox3.TabIndex = 28;
		this.textBox3.TabStop = false;
		this.textBox3.Text = "Developer";
		this.textBox4.BorderStyle = System.Windows.Forms.BorderStyle.None;
		this.textBox4.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
		this.textBox4.Location = new System.Drawing.Point(303, 77);
		this.textBox4.Margin = new System.Windows.Forms.Padding(9, 5, 4, 5);
		this.textBox4.Multiline = true;
		this.textBox4.Name = "textBox4";
		this.textBox4.ReadOnly = true;
		this.textBox4.Size = new System.Drawing.Size(188, 32);
		this.textBox4.TabIndex = 29;
		this.textBox4.TabStop = false;
		this.textBox4.Text = "Thanks To";
		this.txtVersion.BorderStyle = System.Windows.Forms.BorderStyle.None;
		this.txtVersion.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
		this.txtVersion.ForeColor = System.Drawing.Color.DarkRed;
		this.txtVersion.Location = new System.Drawing.Point(0, 149);
		this.txtVersion.Margin = new System.Windows.Forms.Padding(9, 5, 4, 5);
		this.txtVersion.Multiline = true;
		this.txtVersion.Name = "txtVersion";
		this.txtVersion.ReadOnly = true;
		this.txtVersion.Size = new System.Drawing.Size(297, 17);
		this.txtVersion.TabIndex = 30;
		this.txtVersion.TabStop = false;
		this.txtVersion.Text = "Version";
		this.txtVersion.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
		this.txtCopyright.BorderStyle = System.Windows.Forms.BorderStyle.None;
		this.txtCopyright.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Italic, System.Drawing.GraphicsUnit.Point, 0);
		this.txtCopyright.Location = new System.Drawing.Point(10, 449);
		this.txtCopyright.Margin = new System.Windows.Forms.Padding(9, 5, 4, 5);
		this.txtCopyright.Name = "txtCopyright";
		this.txtCopyright.ReadOnly = true;
		this.txtCopyright.Size = new System.Drawing.Size(494, 19);
		this.txtCopyright.TabIndex = 31;
		this.txtCopyright.TabStop = false;
		this.txtCopyright.Text = "Copyright";
		this.txtCopyright.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
		this.logoPictureBox.Enabled = false;
		this.logoPictureBox.Image = (System.Drawing.Image)resources.GetObject("logoPictureBox.Image");
		this.logoPictureBox.Location = new System.Drawing.Point(0, 31);
		this.logoPictureBox.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.logoPictureBox.Name = "logoPictureBox";
		this.logoPictureBox.Size = new System.Drawing.Size(297, 120);
		this.logoPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
		this.logoPictureBox.TabIndex = 12;
		this.logoPictureBox.TabStop = false;
		this.btnReleaseNotes.Location = new System.Drawing.Point(502, 338);
		this.btnReleaseNotes.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.btnReleaseNotes.Name = "btnReleaseNotes";
		this.btnReleaseNotes.Size = new System.Drawing.Size(141, 52);
		this.btnReleaseNotes.TabIndex = 34;
		this.btnReleaseNotes.Text = "Release Notes";
		this.btnReleaseNotes.UseVisualStyleBackColor = true;
		this.btnReleaseNotes.Click += new System.EventHandler(btnReleaseNotes_Click);
		this.linkMellonTuning.AutoSize = true;
		this.linkMellonTuning.Enabled = false;
		this.linkMellonTuning.Location = new System.Drawing.Point(298, 131);
		this.linkMellonTuning.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
		this.linkMellonTuning.Name = "linkMellonTuning";
		this.linkMellonTuning.Size = new System.Drawing.Size(318, 20);
		this.linkMellonTuning.TabIndex = 35;
		this.linkMellonTuning.TabStop = true;
		this.linkMellonTuning.Text = "Mellon Racing (Testing and Data Aquisition)";
		this.linkMellonTuning.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(linkMellonTuning_LinkClicked);
		this.linkBoostedTuning.AutoSize = true;
		this.linkBoostedTuning.Location = new System.Drawing.Point(298, 154);
		this.linkBoostedTuning.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
		this.linkBoostedTuning.Name = "linkBoostedTuning";
		this.linkBoostedTuning.Size = new System.Drawing.Size(187, 20);
		this.linkBoostedTuning.TabIndex = 36;
		this.linkBoostedTuning.TabStop = true;
		this.linkBoostedTuning.Text = "Boosted Tuning (Testing)";
		this.linkBoostedTuning.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(linkBoostedTuning_LinkClicked);
		this.linkBradBarnhill.AutoSize = true;
		this.linkBradBarnhill.Location = new System.Drawing.Point(298, 40);
		this.linkBradBarnhill.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
		this.linkBradBarnhill.Name = "linkBradBarnhill";
		this.linkBradBarnhill.Size = new System.Drawing.Size(225, 20);
		this.linkBradBarnhill.TabIndex = 37;
		this.linkBradBarnhill.TabStop = true;
		this.linkBradBarnhill.Text = "Brad Barnhill (Lead Developer)";
		this.linkBradBarnhill.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(linkBradBarnhill_LinkClicked);
		this.btnDonate_AboutScreen.Cursor = System.Windows.Forms.Cursors.Hand;
		this.btnDonate_AboutScreen.Image = VirtualDyno.Properties.Resources.paypal_donate_large;
		this.btnDonate_AboutScreen.Location = new System.Drawing.Point(502, 272);
		this.btnDonate_AboutScreen.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.btnDonate_AboutScreen.Name = "btnDonate_AboutScreen";
		this.btnDonate_AboutScreen.Size = new System.Drawing.Size(141, 43);
		this.btnDonate_AboutScreen.TabIndex = 40;
		this.btnDonate_AboutScreen.TabStop = false;
		this.btnDonate_AboutScreen.Click += new System.EventHandler(btnDonate_AboutScreen_Click);
		this.lblColumnsVersion.AutoSize = true;
		this.lblColumnsVersion.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.lblColumnsVersion.ForeColor = System.Drawing.Color.Black;
		this.lblColumnsVersion.Location = new System.Drawing.Point(6, 222);
		this.lblColumnsVersion.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
		this.lblColumnsVersion.Name = "lblColumnsVersion";
		this.lblColumnsVersion.Size = new System.Drawing.Size(205, 20);
		this.lblColumnsVersion.TabIndex = 41;
		this.lblColumnsVersion.Text = "Column Names Version: ?";
		this.lblCarDefinitionsVersion.AutoSize = true;
		this.lblCarDefinitionsVersion.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.lblCarDefinitionsVersion.ForeColor = System.Drawing.Color.Black;
		this.lblCarDefinitionsVersion.Location = new System.Drawing.Point(6, 246);
		this.lblCarDefinitionsVersion.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
		this.lblCarDefinitionsVersion.Name = "lblCarDefinitionsVersion";
		this.lblCarDefinitionsVersion.Size = new System.Drawing.Size(207, 20);
		this.lblCarDefinitionsVersion.TabIndex = 42;
		this.lblCarDefinitionsVersion.Text = "Car Definitions Version:  ?";
		this.linkRoyHemrich.AutoSize = true;
		this.linkRoyHemrich.Location = new System.Drawing.Point(298, 108);
		this.linkRoyHemrich.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
		this.linkRoyHemrich.Name = "linkRoyHemrich";
		this.linkRoyHemrich.Size = new System.Drawing.Size(248, 20);
		this.linkRoyHemrich.TabIndex = 43;
		this.linkRoyHemrich.TabStop = true;
		this.linkRoyHemrich.Text = "Roy Hemrich (Graphics / Website)";
		this.linkRoyHemrich.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(linkRoyHemrich_LinkClicked);
		this.lblClientId.AutoSize = true;
		this.lblClientId.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.lblClientId.ForeColor = System.Drawing.Color.Black;
		this.lblClientId.Location = new System.Drawing.Point(6, 295);
		this.lblClientId.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
		this.lblClientId.Name = "lblClientId";
		this.lblClientId.Size = new System.Drawing.Size(89, 20);
		this.lblClientId.TabIndex = 44;
		this.lblClientId.Text = "Client Id: ?";
		base.AutoScaleDimensions = new System.Drawing.SizeF(9f, 20f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.ClientSize = new System.Drawing.Size(652, 472);
		base.Controls.Add(this.lblClientId);
		base.Controls.Add(this.linkRoyHemrich);
		base.Controls.Add(this.lblCarDefinitionsVersion);
		base.Controls.Add(this.lblColumnsVersion);
		base.Controls.Add(this.btnDonate_AboutScreen);
		base.Controls.Add(this.linkBradBarnhill);
		base.Controls.Add(this.linkBoostedTuning);
		base.Controls.Add(this.linkMellonTuning);
		base.Controls.Add(this.btnReleaseNotes);
		base.Controls.Add(this.txtCopyright);
		base.Controls.Add(this.textBoxDescription);
		base.Controls.Add(this.textBox4);
		base.Controls.Add(this.textBox3);
		base.Controls.Add(this.okButton);
		base.Controls.Add(this.logoPictureBox);
		base.Controls.Add(this.txtVersion);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
		base.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		base.MaximizeBox = false;
		base.MinimizeBox = false;
		base.Name = "About";
		base.Padding = new System.Windows.Forms.Padding(14);
		base.ShowIcon = false;
		base.ShowInTaskbar = false;
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
		this.Text = "About";
		((System.ComponentModel.ISupportInitialize)this.logoPictureBox).EndInit();
		((System.ComponentModel.ISupportInitialize)this.btnDonate_AboutScreen).EndInit();
		base.ResumeLayout(false);
		base.PerformLayout();
	}
}

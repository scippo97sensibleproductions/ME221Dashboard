using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using VirtualDyno.Properties;

namespace VirtualDyno;

public class PopupConfirmClose : Form
{
	private IContainer components;

	private Button btnYes;

	private Button btnNo;

	private CheckBox chkShowAgain;

	private PictureBox pictureBox1;

	private TableLayoutPanel tableLayoutPanel1;

	private Label lblMessage;

	public bool ShowEverytime
	{
		get
		{
			return chkShowAgain.Checked;
		}
		set
		{
			chkShowAgain.Checked = value;
		}
	}

	public PopupConfirmClose(int numRunOpen)
	{
		InitializeComponent();
		lblMessage.Text = Environment.NewLine + "You have " + numRunOpen + " log" + ((numRunOpen <= 1) ? "" : "s") + " open." + Environment.NewLine + Environment.NewLine + "Are you sure you want to close Virtual Dyno?";
	}

	private void btnYes_Click(object sender, EventArgs e)
	{
		base.DialogResult = DialogResult.Yes;
	}

	private void btnNo_Click(object sender, EventArgs e)
	{
		base.DialogResult = DialogResult.No;
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
		System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VirtualDyno.PopupConfirmClose));
		this.btnYes = new System.Windows.Forms.Button();
		this.btnNo = new System.Windows.Forms.Button();
		this.chkShowAgain = new System.Windows.Forms.CheckBox();
		this.pictureBox1 = new System.Windows.Forms.PictureBox();
		this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
		this.lblMessage = new System.Windows.Forms.Label();
		((System.ComponentModel.ISupportInitialize)this.pictureBox1).BeginInit();
		this.tableLayoutPanel1.SuspendLayout();
		base.SuspendLayout();
		this.btnYes.Location = new System.Drawing.Point(80, 76);
		this.btnYes.Name = "btnYes";
		this.btnYes.Size = new System.Drawing.Size(92, 27);
		this.btnYes.TabIndex = 0;
		this.btnYes.Text = "&Yes";
		this.btnYes.UseVisualStyleBackColor = true;
		this.btnYes.Click += new System.EventHandler(btnYes_Click);
		this.btnNo.Location = new System.Drawing.Point(178, 76);
		this.btnNo.Name = "btnNo";
		this.btnNo.Size = new System.Drawing.Size(92, 27);
		this.btnNo.TabIndex = 1;
		this.btnNo.Text = "&No";
		this.btnNo.UseVisualStyleBackColor = true;
		this.btnNo.Click += new System.EventHandler(btnNo_Click);
		this.chkShowAgain.AutoSize = true;
		this.chkShowAgain.Location = new System.Drawing.Point(3, 109);
		this.chkShowAgain.Name = "chkShowAgain";
		this.chkShowAgain.Size = new System.Drawing.Size(96, 17);
		this.chkShowAgain.TabIndex = 2;
		this.chkShowAgain.Text = "Show on close";
		this.chkShowAgain.UseVisualStyleBackColor = true;
		this.pictureBox1.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
		this.pictureBox1.Dock = System.Windows.Forms.DockStyle.Fill;
		this.pictureBox1.Image = VirtualDyno.Properties.Resources.question;
		this.pictureBox1.Location = new System.Drawing.Point(3, 3);
		this.pictureBox1.Name = "pictureBox1";
		this.pictureBox1.Size = new System.Drawing.Size(47, 63);
		this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
		this.pictureBox1.TabIndex = 5;
		this.pictureBox1.TabStop = false;
		this.tableLayoutPanel1.BackColor = System.Drawing.Color.White;
		this.tableLayoutPanel1.ColumnCount = 2;
		this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 19.08127f));
		this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 80.91873f));
		this.tableLayoutPanel1.Controls.Add(this.pictureBox1, 0, 0);
		this.tableLayoutPanel1.Controls.Add(this.lblMessage, 1, 0);
		this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 1);
		this.tableLayoutPanel1.Name = "tableLayoutPanel1";
		this.tableLayoutPanel1.RowCount = 1;
		this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50f));
		this.tableLayoutPanel1.Size = new System.Drawing.Size(283, 69);
		this.tableLayoutPanel1.TabIndex = 6;
		this.lblMessage.AutoSize = true;
		this.lblMessage.Dock = System.Windows.Forms.DockStyle.Fill;
		this.lblMessage.Location = new System.Drawing.Point(56, 0);
		this.lblMessage.Name = "lblMessage";
		this.lblMessage.Size = new System.Drawing.Size(224, 69);
		this.lblMessage.TabIndex = 6;
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.ClientSize = new System.Drawing.Size(282, 126);
		base.Controls.Add(this.tableLayoutPanel1);
		base.Controls.Add(this.chkShowAgain);
		base.Controls.Add(this.btnNo);
		base.Controls.Add(this.btnYes);
		this.DoubleBuffered = true;
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
		base.Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
		base.Name = "PopupConfirmClose";
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
		this.Text = "Close?";
		((System.ComponentModel.ISupportInitialize)this.pictureBox1).EndInit();
		this.tableLayoutPanel1.ResumeLayout(false);
		this.tableLayoutPanel1.PerformLayout();
		base.ResumeLayout(false);
		base.PerformLayout();
	}
}

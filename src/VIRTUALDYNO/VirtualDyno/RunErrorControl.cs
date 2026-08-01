using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using VirtualDyno.Properties;
using VirtualDyno.RunControl;

namespace VirtualDyno;

public class RunErrorControl : UserControl
{
	public delegate void OnCloseHandler(object sender, EventArgs e);

	public delegate void OnYesNoHandler(object sender, OnYesNoEventArgs e);

	private cRunControl _Run;

	private readonly int _HeightOffset;

	private IContainer components;

	private PictureBox pictureBox1;

	private TextBox txtMessage;

	private Button btnClose;

	private Label label1;

	private Timer timer1;

	private Button btnYes;

	private Button btnNo;

	public cRunControl Run => _Run;

	public string MessageText
	{
		get
		{
			return txtMessage.Text;
		}
		set
		{
			txtMessage.Text = value;
		}
	}

	public event OnCloseHandler OnClose;

	public event OnYesNoHandler OnYesNo;

	public RunErrorControl(ref cRunControl c, int heightOffset, bool yesNo)
	{
		c.ErrorControl = this;
		_Run = c;
		_HeightOffset = heightOffset;
		BringToFront();
		base.Left = 1;
		base.Top = _Run.Top + _HeightOffset;
		base.Height = _Run.Height;
		InitializeComponent();
		if (yesNo)
		{
			btnClose.Hide();
			btnNo.Show();
			btnYes.Show();
		}
		else
		{
			btnClose.Show();
			btnNo.Hide();
			btnYes.Hide();
		}
	}

	private void btnClose_Click(object sender, EventArgs e)
	{
		this.OnClose(this, new EventArgs());
	}

	private void btnYes_Click(object sender, EventArgs e)
	{
		OnYesNoEventArgs e2 = new OnYesNoEventArgs(ref _Run, YesNo: true);
		this.OnYesNo(this, e2);
	}

	private void btnNo_Click(object sender, EventArgs e)
	{
		OnYesNoEventArgs e2 = new OnYesNoEventArgs(ref _Run, YesNo: false);
		this.OnYesNo(this, e2);
	}

	private void timer1_Tick(object sender, EventArgs e)
	{
		BringToFront();
		base.Left = 1;
		base.Top = _Run.Top + _HeightOffset;
		base.Height = _Run.Height;
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
		this.components = new System.ComponentModel.Container();
		this.txtMessage = new System.Windows.Forms.TextBox();
		this.btnClose = new System.Windows.Forms.Button();
		this.label1 = new System.Windows.Forms.Label();
		this.pictureBox1 = new System.Windows.Forms.PictureBox();
		this.timer1 = new System.Windows.Forms.Timer(this.components);
		this.btnYes = new System.Windows.Forms.Button();
		this.btnNo = new System.Windows.Forms.Button();
		((System.ComponentModel.ISupportInitialize)this.pictureBox1).BeginInit();
		base.SuspendLayout();
		this.txtMessage.BackColor = System.Drawing.Color.SeaShell;
		this.txtMessage.BorderStyle = System.Windows.Forms.BorderStyle.None;
		this.txtMessage.Location = new System.Drawing.Point(42, 28);
		this.txtMessage.Multiline = true;
		this.txtMessage.Name = "txtMessage";
		this.txtMessage.ReadOnly = true;
		this.txtMessage.Size = new System.Drawing.Size(189, 59);
		this.txtMessage.TabIndex = 3;
		this.txtMessage.TabStop = false;
		this.btnClose.Location = new System.Drawing.Point(171, 93);
		this.btnClose.Name = "btnClose";
		this.btnClose.Size = new System.Drawing.Size(60, 23);
		this.btnClose.TabIndex = 4;
		this.btnClose.Text = "Close";
		this.btnClose.UseVisualStyleBackColor = true;
		this.btnClose.Click += new System.EventHandler(btnClose_Click);
		this.label1.AutoSize = true;
		this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
		this.label1.ForeColor = System.Drawing.Color.Black;
		this.label1.Location = new System.Drawing.Point(0, 0);
		this.label1.Name = "label1";
		this.label1.Size = new System.Drawing.Size(103, 16);
		this.label1.TabIndex = 5;
		this.label1.Text = "Run Message";
		this.pictureBox1.Image = VirtualDyno.Properties.Resources.arrow;
		this.pictureBox1.Location = new System.Drawing.Point(3, 38);
		this.pictureBox1.Name = "pictureBox1";
		this.pictureBox1.Size = new System.Drawing.Size(29, 45);
		this.pictureBox1.TabIndex = 2;
		this.pictureBox1.TabStop = false;
		this.timer1.Enabled = true;
		this.timer1.Interval = 200;
		this.timer1.Tick += new System.EventHandler(timer1_Tick);
		this.btnYes.Location = new System.Drawing.Point(43, 93);
		this.btnYes.Name = "btnYes";
		this.btnYes.Size = new System.Drawing.Size(60, 23);
		this.btnYes.TabIndex = 6;
		this.btnYes.Text = "Yes";
		this.btnYes.UseVisualStyleBackColor = true;
		this.btnYes.Click += new System.EventHandler(btnYes_Click);
		this.btnNo.Location = new System.Drawing.Point(105, 93);
		this.btnNo.Name = "btnNo";
		this.btnNo.Size = new System.Drawing.Size(60, 23);
		this.btnNo.TabIndex = 7;
		this.btnNo.Text = "No";
		this.btnNo.UseVisualStyleBackColor = true;
		this.btnNo.Click += new System.EventHandler(btnNo_Click);
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		this.BackColor = System.Drawing.Color.SeaShell;
		base.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		base.Controls.Add(this.btnNo);
		base.Controls.Add(this.btnYes);
		base.Controls.Add(this.btnClose);
		base.Controls.Add(this.label1);
		base.Controls.Add(this.txtMessage);
		base.Controls.Add(this.pictureBox1);
		base.Name = "RunErrorControl";
		base.Size = new System.Drawing.Size(234, 119);
		((System.ComponentModel.ISupportInitialize)this.pictureBox1).EndInit();
		base.ResumeLayout(false);
		base.PerformLayout();
	}
}

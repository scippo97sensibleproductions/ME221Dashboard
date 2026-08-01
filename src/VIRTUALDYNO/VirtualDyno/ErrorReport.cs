using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Net;
using System.Windows.Forms;
using VirtualDyno.Core.Datasets;
using VirtualDyno.Properties;

namespace VirtualDyno;

public class ErrorReport : Form
{
	private IContainer components;

	private TextBox txtMessage;

	private ComboBox cbLogType;

	private Label label1;

	private Button btnSend;

	private Button btnCancel;

	private StatusStrip statusStrip1;

	private ToolStripStatusLabel lblStatus;

	private Label lblErrorCode;

	private ErrorProvider errorProvider1;

	public string LogFileName { get; set; }

	public string ErrorCode { get; set; }

	public ErrorReport(string ErrorCode, string baseFilePath, string logfilename, string errormessage)
	{
		InitializeComponent();
		LogFileName = logfilename;
		txtMessage.Text = errormessage.Trim();
		this.ErrorCode = ErrorCode.Trim();
		lblErrorCode.Text = ErrorCode.Trim();
		using Columns columns = new Columns();
		columns.ReadXml(Path.Combine(baseFilePath, VirtualDyno.Properties.Settings.Default.File_ColumnNames));
		foreach (Columns.ColumnsRow row in columns.Tables[0].Rows)
		{
			if (row.SoftwareName.ToLower().Trim() != "custom")
			{
				cbLogType.Items.Add(row.SoftwareName);
			}
		}
	}

	private void UploadFileToFTPServer(string Folder, string LocalFilePath)
	{
		try
		{
			lblStatus.Text = "Uploading Error Report ...";
			if (File.Exists(LocalFilePath))
			{
				lblStatus.Text = "Connecting";
				lblStatus.Invalidate();
				Update();
				FtpWebRequest obj = (FtpWebRequest)WebRequest.Create(VirtualDyno.Properties.Settings.Default.Error_FTPLocation + "/" + Folder + "/" + cbLogType.Text.ToUpper().Trim() + "_" + Path.GetFileName(LocalFilePath));
				obj.Credentials = new NetworkCredential(VirtualDyno.Properties.Settings.Default.Error_FTPUsername, VirtualDyno.Properties.Settings.Default.Error_FTPPassword);
				obj.KeepAlive = true;
				obj.UseBinary = true;
				obj.UsePassive = false;
				obj.Method = "STOR";
				FileStream fileStream = File.OpenRead(LocalFilePath);
				byte[] array = new byte[fileStream.Length];
				fileStream.Read(array, 0, array.Length);
				fileStream.Close();
				Stream requestStream = obj.GetRequestStream();
				lblStatus.Text = "Sending file";
				lblStatus.Invalidate();
				Update();
				requestStream.Write(array, 0, array.Length);
				lblStatus.Invalidate();
				Update();
				requestStream.Close();
				lblStatus.Text = "Send Completed";
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("UploadFileToFTPServer: " + ex.Message);
		}
		finally
		{
			lblStatus.Text = string.Empty;
			Close();
		}
	}

	private void btnSend_Click(object sender, EventArgs e)
	{
		_ = btnSend.Text;
		_ = lblStatus.Text;
		if (cbLogType.Text.Trim() == string.Empty)
		{
			errorProvider1.SetError(cbLogType, "Please select a log type.");
			return;
		}
		btnSend.Text = "Sending ...";
		btnSend.Enabled = false;
		btnCancel.Enabled = false;
		lblStatus.Text = "Preparing to send";
		lblStatus.Invalidate();
		Update();
		UploadFileToFTPServer(ErrorCode, LogFileName);
	}

	private void btnCancel_Click(object sender, EventArgs e)
	{
		Close();
	}

	private void cbLogType_SelectedIndexChanged(object sender, EventArgs e)
	{
		errorProvider1.Clear();
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
		System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VirtualDyno.ErrorReport));
		this.txtMessage = new System.Windows.Forms.TextBox();
		this.cbLogType = new System.Windows.Forms.ComboBox();
		this.label1 = new System.Windows.Forms.Label();
		this.btnSend = new System.Windows.Forms.Button();
		this.btnCancel = new System.Windows.Forms.Button();
		this.statusStrip1 = new System.Windows.Forms.StatusStrip();
		this.lblStatus = new System.Windows.Forms.ToolStripStatusLabel();
		this.lblErrorCode = new System.Windows.Forms.Label();
		this.errorProvider1 = new System.Windows.Forms.ErrorProvider(this.components);
		this.statusStrip1.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.errorProvider1).BeginInit();
		base.SuspendLayout();
		this.txtMessage.BorderStyle = System.Windows.Forms.BorderStyle.None;
		this.txtMessage.Location = new System.Drawing.Point(12, 31);
		this.txtMessage.Multiline = true;
		this.txtMessage.Name = "txtMessage";
		this.txtMessage.ReadOnly = true;
		this.txtMessage.Size = new System.Drawing.Size(433, 146);
		this.txtMessage.TabIndex = 41;
		this.txtMessage.TabStop = false;
		this.cbLogType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
		this.cbLogType.FormattingEnabled = true;
		this.cbLogType.Location = new System.Drawing.Point(71, 189);
		this.cbLogType.Name = "cbLogType";
		this.cbLogType.Size = new System.Drawing.Size(124, 21);
		this.cbLogType.TabIndex = 1;
		this.cbLogType.SelectedIndexChanged += new System.EventHandler(cbLogType_SelectedIndexChanged);
		this.label1.AutoSize = true;
		this.label1.Location = new System.Drawing.Point(13, 192);
		this.label1.Name = "label1";
		this.label1.Size = new System.Drawing.Size(55, 13);
		this.label1.TabIndex = 2;
		this.label1.Text = "Log Type:";
		this.btnSend.Location = new System.Drawing.Point(312, 238);
		this.btnSend.Name = "btnSend";
		this.btnSend.Size = new System.Drawing.Size(133, 33);
		this.btnSend.TabIndex = 3;
		this.btnSend.Text = "Send Error Report";
		this.btnSend.UseVisualStyleBackColor = true;
		this.btnSend.Click += new System.EventHandler(btnSend_Click);
		this.btnCancel.Location = new System.Drawing.Point(12, 238);
		this.btnCancel.Name = "btnCancel";
		this.btnCancel.Size = new System.Drawing.Size(133, 33);
		this.btnCancel.TabIndex = 2;
		this.btnCancel.Text = "Cancel";
		this.btnCancel.UseVisualStyleBackColor = true;
		this.btnCancel.Click += new System.EventHandler(btnCancel_Click);
		this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[1] { this.lblStatus });
		this.statusStrip1.Location = new System.Drawing.Point(0, 285);
		this.statusStrip1.Name = "statusStrip1";
		this.statusStrip1.Size = new System.Drawing.Size(457, 22);
		this.statusStrip1.TabIndex = 5;
		this.statusStrip1.Text = "statusStrip1";
		this.lblStatus.Name = "lblStatus";
		this.lblStatus.Size = new System.Drawing.Size(442, 17);
		this.lblStatus.Spring = true;
		this.lblErrorCode.AutoSize = true;
		this.lblErrorCode.Font = new System.Drawing.Font("Microsoft Sans Serif", 15.75f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
		this.lblErrorCode.Location = new System.Drawing.Point(380, 5);
		this.lblErrorCode.Name = "lblErrorCode";
		this.lblErrorCode.Size = new System.Drawing.Size(72, 25);
		this.lblErrorCode.TabIndex = 42;
		this.lblErrorCode.Text = "EXXX";
		this.lblErrorCode.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
		this.errorProvider1.ContainerControl = this;
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.ClientSize = new System.Drawing.Size(457, 307);
		base.Controls.Add(this.lblErrorCode);
		base.Controls.Add(this.statusStrip1);
		base.Controls.Add(this.btnCancel);
		base.Controls.Add(this.btnSend);
		base.Controls.Add(this.label1);
		base.Controls.Add(this.cbLogType);
		base.Controls.Add(this.txtMessage);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
		base.Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
		base.MaximizeBox = false;
		base.MinimizeBox = false;
		base.Name = "ErrorReport";
		base.ShowInTaskbar = false;
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
		this.Text = "ErrorReport";
		this.statusStrip1.ResumeLayout(false);
		this.statusStrip1.PerformLayout();
		((System.ComponentModel.ISupportInitialize)this.errorProvider1).EndInit();
		base.ResumeLayout(false);
		base.PerformLayout();
	}
}

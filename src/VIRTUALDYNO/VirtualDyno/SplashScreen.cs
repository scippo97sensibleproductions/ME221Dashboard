using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Ionic.Zip;
using VirtualDyno.Core;
using VirtualDyno.Core.Datasets;
using VirtualDyno.Core.Resources;
using VirtualDyno.Properties;

namespace VirtualDyno;

public class SplashScreen : Form
{
	private delegate void StatusMessage(string message);

	private delegate void WriteToConsole(string message);

	private delegate void StatusResult(DialogResult dr);

	private delegate void UpdateProgressBar(int progressvalue);

	private delegate void SetConnectionIconVisibility(bool visible);

	private List<string> quotes = new List<string>();

	private DialogResult _returnResult = DialogResult.Cancel;

	private Settings _settings;

	private IContainer components;

	private PictureBox pbBackground;

	private Label lblVersion;

	private Label lblCredits;

	private Label lblStatus;

	private ProgressBar pbLoadingProgress;

	private PictureBox pbConnectionIcon;

	public SplashScreen(Settings settings)
	{
		_settings = settings;
		InitializeComponent();
		lblVersion.Text = Statics.Version();
		lblVersion.Parent = pbBackground;
		lblCredits.Parent = pbBackground;
		lblStatus.Parent = pbBackground;
		pbConnectionIcon.Parent = pbBackground;
		if (!Directory.Exists(Statics.baseFilepath))
		{
			Directory.CreateDirectory(Statics.baseFilepath);
		}
		BackgroundWorker backgroundWorker = new BackgroundWorker();
		backgroundWorker.DoWork += CheckForUpdates;
		backgroundWorker.RunWorkerCompleted += UpdatesCompleted;
		backgroundWorker.RunWorkerAsync();
		Analytics.ReportApplicationStart();
		Update();
	}

	private void CheckForUpdates(object sender, DoWorkEventArgs e)
	{
		DelegateWriteToConsole("========== Start Updates ==========");
		DelegateUpdateProgressBarValue(10);
		DelegateStatusMessage("Checking Connection");
		try
		{
			using WebClient webClient = new WebClient();
			ServicePointManager.Expect100Continue = true;
			ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
			using (webClient.OpenRead(VirtualDyno.Properties.Settings.Default.SETTINGS_URL_BASE))
			{
				DelegateSetConnectionIconVisibility(visible: true);
			}
		}
		catch
		{
			DelegateSetConnectionIconVisibility(visible: false);
			DelegateStatusMessage("No internet detected");
			DelegateUpdateProgressBarValue(100);
			Thread.Sleep(250);
			DelegateStatusMessage("Loading ...");
			Thread.Sleep(200);
			return;
		}
		DelegateUpdateProgressBarValue(25);
		DelegateStatusMessage("Checking Application");
		CheckForAppUpdates();
		DelegateUpdateProgressBarValue(50);
		Thread.Sleep(100);
		DelegateStatusMessage("Checking Columns");
		CheckForColumnUpdates();
		DelegateUpdateProgressBarValue(75);
		Thread.Sleep(100);
		DelegateStatusMessage("Checking Cars");
		CheckForCarsUpdates();
		DelegateUpdateProgressBarValue(100);
		Thread.Sleep(500);
		DelegateStatusMessage("Loading ...");
	}

	private void UpdatesCompleted(object sender, RunWorkerCompletedEventArgs e)
	{
		DelegateWriteToConsole("=========== End Updates ===========");
		base.DialogResult = _returnResult;
		Close();
	}

	private StreamReader getWebStreamReader(string urlSuffix)
	{
		WebClient webClient = new WebClient();
		ServicePointManager.Expect100Continue = true;
		ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
		return new StreamReader(webClient.OpenRead(VirtualDyno.Properties.Settings.Default.SETTINGS_URL_BASE + urlSuffix));
	}

	private void CheckForCarsUpdates()
	{
		try
		{
			int num = 0;
			int num2 = 0;
			string address = string.Empty;
			using (CarsVersion carsVersion = new CarsVersion())
			{
				using CarsVersion carsVersion2 = new CarsVersion();
				carsVersion2.ReadXml((TextReader)getWebStreamReader(VirtualDyno.Properties.Settings.Default.CARSVERSION_URL_SUFFIX));
				DelegateWriteToConsole("Cars: remote file read");
				if (Directory.Exists(Path.Combine(Statics.baseFilepath, "Cars")) && File.Exists(Path.Combine(Statics.baseFilepath, VirtualDyno.Properties.Settings.Default.File_CarsVersion)))
				{
					carsVersion.ReadXml(Path.Combine(Statics.baseFilepath, VirtualDyno.Properties.Settings.Default.File_CarsVersion));
					num2 = Convert.ToInt32(((DataTable)(object)carsVersion.CarVersion).Rows[0]["Version"]);
					DelegateWriteToConsole("Cars: local file read");
				}
				num = Convert.ToInt32(((DataTable)(object)carsVersion2.CarVersion).Rows[0]["Version"]);
				address = ((DataTable)(object)carsVersion2.CarVersion).Rows[0]["URL"].ToString();
			}
			if (num > num2)
			{
				DelegateWriteToConsole("Cars: update available");
				DelegateStatusMessage("Getting new Car definitions ...");
				using (WebClient webClient = new WebClient())
				{
					ServicePointManager.Expect100Continue = true;
					ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
					webClient.DownloadFile(address, Path.Combine(Statics.baseFilepath, "Cars.zip"));
				}
				if (File.Exists(Path.Combine(Statics.baseFilepath, "Cars.zip")))
				{
					DeleteCarFiles(Statics.baseFilepath + "\\Cars");
					using ZipFile zipFile = new ZipFile(Path.Combine(Statics.baseFilepath, "Cars.zip"));
					zipFile.ExtractAll(Path.Combine(Statics.baseFilepath, "Cars"), wantOverwrite: true);
				}
			}
			else
			{
				DelegateWriteToConsole("Cars: No update found");
			}
		}
		catch (Exception ex)
		{
			if (ex is WebException)
			{
				DelegateStatusMessage("Cars: (Error) " + ex.Message);
			}
			Statics.Error(new Exception(string.Format(ErrorMessages.E116, Environment.NewLine, ex.Message)));
		}
		finally
		{
			try
			{
				if (File.Exists(Path.Combine(Statics.baseFilepath, "Cars.zip")))
				{
					File.Delete(Path.Combine(Statics.baseFilepath, "Cars.zip"));
				}
			}
			catch (Exception ex2)
			{
				Statics.Error(new Exception(string.Format(ErrorMessages.E117, Environment.NewLine, ex2.Message)));
			}
		}
		try
		{
			if (Directory.Exists(Statics.baseFilepath + "\\Cars"))
			{
				return;
			}
			DelegateWriteToConsole("Cars: Extracting defaults");
			string text = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Cars.zip");
			if (File.Exists(text))
			{
				using (ZipFile zipFile2 = new ZipFile(text))
				{
					zipFile2.ExtractAll(Path.Combine(Statics.baseFilepath, "Cars"), wantOverwrite: true);
					return;
				}
			}
		}
		catch (Exception ex3)
		{
			Statics.Error(new Exception(string.Format(ErrorMessages.E126, Environment.NewLine, ex3.Message)));
		}
	}

	private void CheckForColumnUpdates()
	{
		try
		{
			string text = Path.Combine(Statics.baseFilepath, VirtualDyno.Properties.Settings.Default.File_ColumnNames);
			using Columns columns = new Columns();
			using Columns columns2 = new Columns();
			columns2.ReadXml((TextReader)getWebStreamReader(VirtualDyno.Properties.Settings.Default.COLUMN_URL_SUFFIX));
			try
			{
				columns.ReadXml(text);
			}
			catch
			{
			}
			if (((DataTable)(object)columns.ColumnVersion).Rows.Count <= 0 || Convert.ToInt32(((DataTable)(object)columns2.ColumnVersion).Rows[0]["Version"]) > Convert.ToInt32(((DataTable)(object)columns.ColumnVersion).Rows[0]["Version"]))
			{
				DelegateWriteToConsole("Columns: update found");
				using (FileStream stream = File.OpenWrite(text))
				{
					columns2.WriteXml((Stream)stream);
				}
				DelegateWriteToConsole("Columns: update written to local file");
			}
			else
			{
				DelegateWriteToConsole("Columns: No update found");
			}
		}
		catch (WebException ex)
		{
			Console.WriteLine("Columns: (Error) " + ex.Message);
		}
		catch (Exception ex2)
		{
			Statics.Error(new Exception(string.Format(ErrorMessages.E115, Environment.NewLine, ex2.Message)));
		}
	}

	private void CheckForAppUpdates()
	{
		if (false)
		{
			return;
		}
		try
		{
			try
			{
				if (File.Exists(Statics.baseFilepath + "\\Webupdate_Setup.exe"))
				{
					File.Delete(Statics.baseFilepath + "\\Webupdate_Setup.exe");
				}
				File.Delete(Path.Combine(Statics.baseFilepath, "setup.exe"));
				File.Delete(Path.Combine(Statics.baseFilepath, "setup.msi"));
			}
			catch (Exception ex)
			{
				Statics.Error(new Exception(string.Format(ErrorMessages.E114, Environment.NewLine, ex.Message)));
			}
			using VersionInfo versionInfo = new VersionInfo();
			versionInfo.ReadXml((TextReader)getWebStreamReader(VirtualDyno.Properties.Settings.Default.VERSION_URL_SUFFIX));
			string[] array = versionInfo.CurrentVersion[0]["Version"].ToString().Split('.');
			AssemblyName name = Assembly.GetExecutingAssembly().GetName();
			Version version = new Version(name.Version.Major, name.Version.Minor, name.Version.Build);
			Version value = new Version(Convert.ToInt32(array[0]), Convert.ToInt32(array[1]), Convert.ToInt32(array[2]));
			if (version.CompareTo(value) < 0)
			{
				DelegateUpdateStatusResult(DialogResult.No);
				DelegateWriteToConsole("App: update found");
				if (MessageBox.Show("Update available.  Would you like to update now?", "Update", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1) == DialogResult.Yes)
				{
					Process.Start(VirtualDyno.Properties.Settings.Default.SETTINGS_URL_BASE);
				}
			}
			else
			{
				DelegateUpdateStatusResult(DialogResult.Yes);
			}
		}
		catch (Exception ex2)
		{
			Console.WriteLine(ex2.Message);
		}
	}

	private void DeleteCarFiles(string Directory)
	{
		try
		{
			DirectoryInfo directoryInfo = new DirectoryInfo(Directory);
			DirectoryInfo[] directories = directoryInfo.GetDirectories();
			foreach (DirectoryInfo directoryInfo2 in directories)
			{
				if (!directoryInfo2.Name.Contains("Custom"))
				{
					DeleteCarFiles(directoryInfo2.FullName);
				}
			}
			FileInfo[] files = directoryInfo.GetFiles("*.xml");
			foreach (FileInfo fileInfo in files)
			{
				try
				{
					if (fileInfo.Name.Trim().ToLower() != "carsversion.xml")
					{
						fileInfo.Delete();
					}
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
	}

	private void DelegateStatusMessage(string message)
	{
		try
		{
			StatusMessage statusMessage = UpdateStatusMessage;
			if (base.InvokeRequired)
			{
				Invoke(statusMessage, message);
			}
			else
			{
				statusMessage(message);
			}
		}
		catch
		{
		}
	}

	private void DelegateWriteToConsole(string message)
	{
		try
		{
			WriteToConsole writeToConsole = WriteMessageToConsole;
			if (base.InvokeRequired)
			{
				Invoke(writeToConsole, message);
			}
			else
			{
				writeToConsole(message);
			}
		}
		catch
		{
		}
	}

	private void DelegateUpdateStatusResult(DialogResult dr)
	{
		try
		{
			StatusResult statusResult = UpdateStatusResult;
			if (base.InvokeRequired)
			{
				Invoke(statusResult, dr);
			}
			else
			{
				statusResult(dr);
			}
		}
		catch
		{
		}
	}

	private void DelegateUpdateProgressBarValue(int progressvalue)
	{
		try
		{
			UpdateProgressBar updateProgressBar = UpdateProgressBarValue;
			if (base.InvokeRequired)
			{
				Invoke(updateProgressBar, progressvalue);
			}
			else
			{
				updateProgressBar(progressvalue);
			}
		}
		catch
		{
		}
	}

	private void DelegateSetConnectionIconVisibility(bool visible)
	{
		try
		{
			SetConnectionIconVisibility setConnectionIconVisibility = SetConnectionIconVisibilityValue;
			if (base.InvokeRequired)
			{
				Invoke(setConnectionIconVisibility, visible);
			}
			else
			{
				setConnectionIconVisibility(visible);
			}
		}
		catch
		{
		}
	}

	private void UpdateStatusMessage(string message)
	{
		lblStatus.Text = message;
		if (message.Trim() != string.Empty)
		{
			Console.WriteLine("Status: " + message);
		}
		lblStatus.Invalidate();
		Update();
	}

	private void WriteMessageToConsole(string message)
	{
		Console.WriteLine(message);
	}

	private void UpdateStatusResult(DialogResult dr)
	{
		_returnResult = dr;
	}

	private void UpdateProgressBarValue(int progressvalue)
	{
		pbLoadingProgress.Invalidate();
		Update();
		pbLoadingProgress.Value = progressvalue;
		pbLoadingProgress.Invalidate();
		pbLoadingProgress.Update();
	}

	private void SetConnectionIconVisibilityValue(bool visible)
	{
		pbConnectionIcon.Visible = visible;
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
		System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VirtualDyno.SplashScreen));
		this.pbBackground = new System.Windows.Forms.PictureBox();
		this.lblVersion = new System.Windows.Forms.Label();
		this.lblCredits = new System.Windows.Forms.Label();
		this.lblStatus = new System.Windows.Forms.Label();
		this.pbLoadingProgress = new System.Windows.Forms.ProgressBar();
		this.pbConnectionIcon = new System.Windows.Forms.PictureBox();
		((System.ComponentModel.ISupportInitialize)this.pbBackground).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.pbConnectionIcon).BeginInit();
		base.SuspendLayout();
		this.pbBackground.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.pbBackground.Dock = System.Windows.Forms.DockStyle.Fill;
		this.pbBackground.Image = VirtualDyno.Properties.Resources.VirtualDyno_SplashBackground;
		this.pbBackground.Location = new System.Drawing.Point(0, 0);
		this.pbBackground.Name = "pbBackground";
		this.pbBackground.Size = new System.Drawing.Size(467, 281);
		this.pbBackground.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
		this.pbBackground.TabIndex = 0;
		this.pbBackground.TabStop = false;
		this.lblVersion.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
		this.lblVersion.AutoSize = true;
		this.lblVersion.BackColor = System.Drawing.Color.Transparent;
		this.lblVersion.Font = new System.Drawing.Font("Microsoft Sans Serif", 21.75f, System.Drawing.FontStyle.Bold);
		this.lblVersion.ForeColor = System.Drawing.Color.Maroon;
		this.lblVersion.Location = new System.Drawing.Point(277, 245);
		this.lblVersion.Name = "lblVersion";
		this.lblVersion.Size = new System.Drawing.Size(139, 33);
		this.lblVersion.TabIndex = 4;
		this.lblVersion.Text = "[Version]";
		this.lblVersion.TextAlign = System.Drawing.ContentAlignment.TopRight;
		this.lblCredits.Anchor = System.Windows.Forms.AnchorStyles.Right;
		this.lblCredits.AutoSize = true;
		this.lblCredits.BackColor = System.Drawing.Color.Transparent;
		this.lblCredits.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
		this.lblCredits.ForeColor = System.Drawing.Color.Gray;
		this.lblCredits.Location = new System.Drawing.Point(362, 205);
		this.lblCredits.Name = "lblCredits";
		this.lblCredits.Size = new System.Drawing.Size(93, 13);
		this.lblCredits.TabIndex = 5;
		this.lblCredits.Text = "© Brad Barnhill";
		this.lblCredits.TextAlign = System.Drawing.ContentAlignment.TopRight;
		this.lblStatus.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
		this.lblStatus.AutoSize = true;
		this.lblStatus.BackColor = System.Drawing.Color.Transparent;
		this.lblStatus.Font = new System.Drawing.Font("Microsoft Sans Serif", 9f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
		this.lblStatus.ForeColor = System.Drawing.Color.Maroon;
		this.lblStatus.Location = new System.Drawing.Point(0, 250);
		this.lblStatus.Name = "lblStatus";
		this.lblStatus.Size = new System.Drawing.Size(71, 15);
		this.lblStatus.TabIndex = 6;
		this.lblStatus.Text = "Loading...";
		this.lblStatus.TextAlign = System.Drawing.ContentAlignment.TopRight;
		this.pbLoadingProgress.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
		this.pbLoadingProgress.Location = new System.Drawing.Point(2, 268);
		this.pbLoadingProgress.Name = "pbLoadingProgress";
		this.pbLoadingProgress.Size = new System.Drawing.Size(163, 11);
		this.pbLoadingProgress.TabIndex = 7;
		this.pbConnectionIcon.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
		this.pbConnectionIcon.BackColor = System.Drawing.Color.Transparent;
		this.pbConnectionIcon.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
		this.pbConnectionIcon.Image = VirtualDyno.Properties.Resources.update;
		this.pbConnectionIcon.Location = new System.Drawing.Point(166, 266);
		this.pbConnectionIcon.Name = "pbConnectionIcon";
		this.pbConnectionIcon.Size = new System.Drawing.Size(24, 24);
		this.pbConnectionIcon.TabIndex = 10;
		this.pbConnectionIcon.TabStop = false;
		this.pbConnectionIcon.Visible = false;
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.ClientSize = new System.Drawing.Size(467, 281);
		base.ControlBox = false;
		base.Controls.Add(this.pbConnectionIcon);
		base.Controls.Add(this.pbLoadingProgress);
		base.Controls.Add(this.lblStatus);
		base.Controls.Add(this.lblCredits);
		base.Controls.Add(this.lblVersion);
		base.Controls.Add(this.pbBackground);
		this.DoubleBuffered = true;
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
		base.Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
		base.MaximizeBox = false;
		base.MinimizeBox = false;
		base.Name = "SplashScreen";
		base.ShowInTaskbar = false;
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
		this.Text = "Loading ... Please wait";
		base.TransparencyKey = System.Drawing.Color.Transparent;
		((System.ComponentModel.ISupportInitialize)this.pbBackground).EndInit();
		((System.ComponentModel.ISupportInitialize)this.pbConnectionIcon).EndInit();
		base.ResumeLayout(false);
		base.PerformLayout();
	}
}

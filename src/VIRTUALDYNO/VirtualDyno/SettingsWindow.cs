using System;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Security;
using System.Windows.Forms;
using VirtualDyno.Core;
using VirtualDyno.Core.Datasets;
using VirtualDyno.Properties;
using VirtualDyno.RunControl;

namespace VirtualDyno;

public class SettingsWindow : Form
{
	public enum HPTYPE
	{
		HP,
		PS,
		KW
	}

	public enum TQTYPE
	{
		FTLB,
		KGFM,
		NM
	}

	public delegate void SaveClickedEventHandler(object sender, EventArgs e);

	private Settings settings;

	private IContainer components;

	private TabControl tabControl1;

	private TabPage tabGraph;

	private GroupBox groupBox2;

	private CheckBox chkShowLegend;

	private CheckBox cbMetricWeightandTemp;

	private CheckBox chkShowMaximums;

	private ComboBox cbLineThickness;

	private CheckBox chkSmoothAFRBoostOption;

	private Label label7;

	private CheckBox chkShowDataPoints;

	private CheckBox chkAutoTrimTPS;

	private TabPage tabDyno;

	private GroupBox groupBox9;

	private RadioButton rbBoostDoNotConvert;

	private RadioButton rbBAR;

	private RadioButton rbMillibar;

	private RadioButton rbPSI;

	private GroupBox groupBox3;

	private ComboBox cbDynos;

	private TextBox txtDynoCorrectionFactor;

	private CheckBox cbCustomDCF;

	private GroupBox groupBox1;

	private ComboBox cbSmoothingFactor;

	private GroupBox groupBox6;

	private RadioButton rbkW;

	private RadioButton rbHP;

	private GroupBox groupBox7;

	private RadioButton rbNm;

	private RadioButton rblbft;

	private TabPage tabColumnsAndProfiles;

	private GroupBox groupBox4;

	private TextBox txtSettingBoostColumns;

	private Label label6;

	private TextBox txtSettingTPSColumns;

	private TextBox txtSettingAFRColumns;

	private TextBox txtSettingRPMColumns;

	private TextBox txtSettingTimeColumns;

	private Label label3;

	private Label label4;

	private Label label2;

	private Label label1;

	private Button btnSetProfilePath;

	private GroupBox groupBox8;

	private Label lblProfilePath;

	private Label label11;

	private Button btnSaveGraphOptions;

	private Button btnCancel;

	private Button btnGraphBackColor;

	private Button btnChartBackColor;

	private CheckBox cbBezierSmoothing;

	private CheckBox chkIncludeAFR;

	private CheckBox chkIncludeBoost;

	private Panel panel1;

	private GroupBox groupBox10;

	private ComboBox cbDefaultProfile;

	private TabPage tabBackground;

	private GroupBox groupBox5;

	private Label lblTransparencyValue;

	private Label label5;

	private Button btnBrowseBackgroundImage;

	private Button btnClearBackground;

	private TrackBar trkBackgroundTransparency;

	private CheckBox chkStretch;

	private PictureBox picBackgroundImagePreview;

	private CheckBox chkShowConfirmCloseMessage;

	private ComboBox cbRpmTrimWindow;

	private GroupBox groupBox11;

	private TextBox textBox1;

	private RadioButton rbPS;

	private RadioButton rbkgfm;

	public double LineThickness
	{
		get
		{
			try
			{
				return Convert.ToDouble(cbLineThickness.Items[cbLineThickness.SelectedIndex].ToString().Trim());
			}
			catch
			{
				return 1.75;
			}
		}
		set
		{
			try
			{
				for (int i = 0; i < cbLineThickness.Items.Count; i++)
				{
					if (Convert.ToDouble(cbLineThickness.Items[i].ToString().Trim()) == value)
					{
						cbLineThickness.SelectedIndex = i;
						break;
					}
				}
			}
			catch
			{
			}
		}
	}

	public double RpmTrimWindow
	{
		get
		{
			try
			{
				return Convert.ToDouble(cbRpmTrimWindow.Items[cbRpmTrimWindow.SelectedIndex].ToString().Trim());
			}
			catch
			{
				return VirtualDyno.Properties.Settings.Default.RPM_TRIM_WINDOW;
			}
		}
		set
		{
			try
			{
				for (int i = 0; i < cbRpmTrimWindow.Items.Count; i++)
				{
					if (Convert.ToDouble(cbRpmTrimWindow.Items[i].ToString().Trim()) == value)
					{
						cbRpmTrimWindow.SelectedIndex = i;
						break;
					}
				}
			}
			catch
			{
			}
		}
	}

	public event SaveClickedEventHandler SaveClicked;

	public SettingsWindow(ref Settings settings)
	{
		InitializeComponent();
		this.settings = settings;
		base.DialogResult = DialogResult.Cancel;
		PopulateSmoothingDropdown();
		PopulateDefaultProfileDropdown();
		LoadDynos(ref settings);
		LoadGraphSettings(ref settings);
		LoadCustomColumns(ref settings);
	}

	private void LoadGraphSettings(ref Settings settings)
	{
		lblProfilePath.Text = Statics.baseFilepath;
		try
		{
			if (settings.GraphSettingsRow.IsShowDataPointsNull())
			{
				settings.GraphSettingsRow.ShowDataPoints = false;
			}
			chkShowDataPoints.Checked = settings.GraphSettingsRow.ShowDataPoints;
			if (settings.GraphSettingsRow.IsShowLegendNull())
			{
				settings.GraphSettingsRow.ShowLegend = true;
			}
			chkShowLegend.Checked = settings.GraphSettingsRow.ShowLegend;
			chkIncludeAFR.Checked = settings.GraphSettingsRow.IncludeAFR;
			chkIncludeBoost.Checked = settings.GraphSettingsRow.IncludeBoost;
			chkAutoTrimTPS.Checked = settings.GraphSettingsRow.AutoTrimTPS;
			cbBezierSmoothing.Checked = settings.GraphSettingsRow.BezierSmoothing;
			chkSmoothAFRBoostOption.Checked = settings.GraphSettingsRow.SmoothAFRBoost;
			chkShowMaximums.Checked = settings.GraphSettingsRow.ShowMaximums;
			LineThickness = settings.GraphSettingsRow.LineThickness;
			cbSmoothingFactor.SelectedIndex = settings.GraphSettingsRow.SmoothingFactor - VirtualDyno.Properties.Settings.Default.MIN_SMOOTHING;
			cbMetricWeightandTemp.Checked = !settings.GraphSettingsRow.IsMetricWeightandTempNull() && settings.GraphSettingsRow.MetricWeightandTemp;
			chkShowConfirmCloseMessage.Checked = settings.GraphSettingsRow.ShowConfirmCloseMessage;
			RpmTrimWindow = settings.GraphSettingsRow.RpmTrimWindow;
			int num = cbDefaultProfile.FindStringExact(settings.GraphSettingsRow.DefaultProfile);
			cbDefaultProfile.SelectedIndex = ((num >= 0 && num < cbDefaultProfile.Items.Count) ? num : 0);
			switch (settings.GraphSettingsRow.ConvertBoost.ToUpper().Trim())
			{
			case "PSI":
				rbPSI.Checked = true;
				break;
			case "BAR":
				rbBAR.Checked = true;
				break;
			case "MILLIBAR":
				rbMillibar.Checked = true;
				break;
			default:
				rbBoostDoNotConvert.Checked = true;
				break;
			}
			if (settings.GraphSettingsRow.IsProfilesPathNull() || string.IsNullOrEmpty(settings.GraphSettingsRow.ProfilesPath) || settings.GraphSettingsRow.ProfilesPath.Trim() == "[Default Profile Path]")
			{
				lblProfilePath.Text = Statics.baseFilepath;
			}
			else
			{
				lblProfilePath.Text = settings.GraphSettingsRow.ProfilesPath.Trim();
			}
			try
			{
				btnGraphBackColor.BackColor = Color.FromArgb(settings.GraphSettingsRow.GraphBackgroundColor);
				btnChartBackColor.BackColor = Color.FromArgb(settings.GraphSettingsRow.ChartBackgroundColor);
			}
			catch
			{
			}
			try
			{
				if (!settings.GraphSettingsRow.IsBackgroundImageNull())
				{
					picBackgroundImagePreview.Image = Statics.CreateThumbnail(Statics.byteArrayToImage(settings.GraphSettingsRow.BackgroundImage), picBackgroundImagePreview.Width, picBackgroundImagePreview.Height);
				}
			}
			catch
			{
			}
			chkStretch.Checked = settings.GraphSettingsRow.BackgroundStretch;
			trkBackgroundTransparency.Value = settings.GraphSettingsRow.BackgroundTransparency;
			lblTransparencyValue.Text = trkBackgroundTransparency.Value + "%";
			switch (settings.GraphSettingsRow.HpType)
			{
			case 2:
				rbkW.Checked = true;
				break;
			case 1:
				rbPS.Checked = true;
				break;
			default:
				rbHP.Checked = true;
				break;
			}
			switch (settings.GraphSettingsRow.TqType)
			{
			case 1:
				rbkgfm.Checked = true;
				break;
			case 2:
				rbNm.Checked = true;
				break;
			default:
				rblbft.Checked = true;
				break;
			}
		}
		catch
		{
		}
		if (settings.GraphSettingsRow.DynoName != "Custom")
		{
			foreach (object item in cbDynos.Items)
			{
				if (((DataRowView)item).Row["DynoName"].ToString().Trim().ToLower() == settings.GraphSettingsRow.DynoName.Trim().ToLower())
				{
					cbDynos.SelectedItem = item;
					txtDynoCorrectionFactor.Text = cbDynos.SelectedValue.ToString();
					break;
				}
			}
			return;
		}
		txtDynoCorrectionFactor.ReadOnly = false;
		cbDynos.Enabled = false;
		cbCustomDCF.Checked = true;
		txtDynoCorrectionFactor.Text = settings.GraphSettingsRow.DynoCorrectionFactor.ToString();
	}

	private void LoadDynos(ref Settings settings)
	{
		try
		{
			_ = (Dynos.DynoCorrectionFactorsRow)settings.Dynos.Tables["DynoCorrectionFactors"].Rows[0];
			if (cbDynos.DataSource == null)
			{
				cbDynos.DataSource = settings.Dynos.Tables["DynoCorrectionFactors"];
				cbDynos.DisplayMember = "DynoName";
				cbDynos.ValueMember = "DynoCorrectionFactor";
			}
		}
		catch (SecurityException)
		{
			MessageBox.Show("Could not load dynos.xml\nDefault values will be used.", "Dynos Load Warning", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
		}
	}

	private void LoadCustomColumns(ref Settings settings)
	{
		DataRow[] array = settings.Columns.Tables["Columns"].Select("SoftwareName = 'Custom'");
		if (array.Length != 0)
		{
			txtSettingTimeColumns.Text += array[0]["Time"].ToString();
			txtSettingRPMColumns.Text += array[0]["RPM"].ToString();
			txtSettingTPSColumns.Text += array[0]["TPS"].ToString();
			txtSettingAFRColumns.Text += array[0]["AFR"].ToString();
			txtSettingBoostColumns.Text += array[0]["Boost"].ToString();
		}
	}

	private void SaveSettings()
	{
		if (!Directory.Exists(Statics.baseFilepath))
		{
			Directory.CreateDirectory(Statics.baseFilepath);
		}
		try
		{
			GraphSettings.GraphSettingsRow graphSettingsRow = (GraphSettings.GraphSettingsRow)settings.GraphSettings.Tables["GraphSettings"].NewRow();
			graphSettingsRow.AutoTrimTPS = chkAutoTrimTPS.Checked;
			graphSettingsRow.IncludeAFR = chkIncludeAFR.Checked;
			graphSettingsRow.IncludeBoost = chkIncludeBoost.Checked;
			graphSettingsRow.ShowDataPoints = chkShowDataPoints.Checked;
			graphSettingsRow.ShowLegend = chkShowLegend.Checked;
			graphSettingsRow.SmoothingFactor = cbSmoothingFactor.SelectedIndex + VirtualDyno.Properties.Settings.Default.MIN_SMOOTHING;
			graphSettingsRow.DynoName = (cbCustomDCF.Checked ? "Custom" : ((Dynos.DynoCorrectionFactorsRow)((DataRowView)cbDynos.SelectedItem).Row).DynoName);
			graphSettingsRow.DynoCorrectionFactor = (cbCustomDCF.Checked ? Convert.ToDouble(txtDynoCorrectionFactor.Text) : Convert.ToDouble(cbDynos.SelectedValue));
			graphSettingsRow.BackgroundImage = ((!settings.GraphSettingsRow.IsBackgroundImageNull()) ? settings.GraphSettingsRow.BackgroundImage : null);
			graphSettingsRow.BackgroundStretch = chkStretch.Checked;
			graphSettingsRow.BackgroundTransparency = trkBackgroundTransparency.Value;
			graphSettingsRow.BezierSmoothing = cbBezierSmoothing.Checked;
			graphSettingsRow.ChartBackgroundColor = btnChartBackColor.BackColor.ToArgb();
			graphSettingsRow.GraphBackgroundColor = btnGraphBackColor.BackColor.ToArgb();
			graphSettingsRow.SmoothAFRBoost = chkSmoothAFRBoostOption.Checked;
			graphSettingsRow.ShowMaximums = chkShowMaximums.Checked;
			graphSettingsRow.LineThickness = LineThickness;
			graphSettingsRow.ProfilesPath = lblProfilePath.Text.Trim();
			graphSettingsRow.MetricWeightandTemp = cbMetricWeightandTemp.Checked;
			graphSettingsRow.ShowConfirmCloseMessage = chkShowConfirmCloseMessage.Checked;
			graphSettingsRow.DefaultProfile = ((cbDefaultProfile.SelectedIndex == 0) ? null : cbDefaultProfile.SelectedItem.ToString());
			graphSettingsRow.RpmTrimWindow = (short)int.Parse(cbRpmTrimWindow.SelectedItem.ToString());
			if (rbHP.Checked)
			{
				graphSettingsRow.HpType = 0;
			}
			else if (rbkW.Checked)
			{
				graphSettingsRow.HpType = 2;
			}
			else if (rbPS.Checked)
			{
				graphSettingsRow.HpType = 1;
			}
			if (rblbft.Checked)
			{
				graphSettingsRow.TqType = 0;
			}
			else if (rbNm.Checked)
			{
				graphSettingsRow.TqType = 2;
			}
			else if (rbkgfm.Checked)
			{
				graphSettingsRow.TqType = 1;
			}
			if (rbPSI.Checked)
			{
				graphSettingsRow.ConvertBoost = "PSI";
			}
			else if (rbBAR.Checked)
			{
				graphSettingsRow.ConvertBoost = "BAR";
			}
			else if (rbMillibar.Checked)
			{
				graphSettingsRow.ConvertBoost = "MILLIBAR";
			}
			else if (rbBoostDoNotConvert.Checked)
			{
				graphSettingsRow.ConvertBoost = "DONOTCONVERT";
			}
			GraphSettings.LayoutRow layoutRow = (GraphSettings.LayoutRow)settings.GraphSettings.Tables["Layout"].NewRow();
			layoutRow.IsMaximized = settings.LayoutRow.IsMaximized;
			layoutRow.Height = settings.WindowSize.Y;
			layoutRow.Width = settings.WindowSize.X;
			layoutRow.Left = ((settings.WindowLocation.X >= 0) ? settings.WindowLocation.X : 0);
			layoutRow.Top = ((settings.WindowLocation.Y >= 0) ? settings.WindowLocation.Y : 0);
			settings.GraphSettings.Tables["GraphSettings"].Rows.Clear();
			settings.GraphSettings.Tables["Layout"].Rows.Clear();
			settings.GraphSettings.Tables["GraphSettings"].Rows.Add(graphSettingsRow);
			settings.GraphSettings.Tables["Layout"].Rows.Add(layoutRow);
		}
		catch (Exception ex)
		{
			MessageBox.Show("Failed saving graphsettings.xml:\n" + ex.Message, "Graph Settings Warning", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
		}
		try
		{
			settings.Columns.Clear();
			settings.Columns.ReadXml(Path.Combine(Statics.baseFilepath, VirtualDyno.Properties.Settings.Default.File_ColumnNames));
			int num = settings.Columns.Tables["Columns"].Rows.IndexOf(settings.Columns.Tables["Columns"].Rows.Find("Custom"));
			if (num >= 0)
			{
				((Columns.ColumnsRow)settings.Columns.Tables["Columns"].Rows[num]).AFR = txtSettingAFRColumns.Text.Trim();
				((Columns.ColumnsRow)settings.Columns.Tables["Columns"].Rows[num]).RPM = txtSettingRPMColumns.Text.Trim();
				((Columns.ColumnsRow)settings.Columns.Tables["Columns"].Rows[num]).Time = txtSettingTimeColumns.Text.Trim();
				((Columns.ColumnsRow)settings.Columns.Tables["Columns"].Rows[num]).TPS = txtSettingTPSColumns.Text.Trim();
				((Columns.ColumnsRow)settings.Columns.Tables["Columns"].Rows[num]).Boost = txtSettingBoostColumns.Text.Trim();
			}
		}
		catch (Exception ex2)
		{
			MessageBox.Show("Failed saving columnnames.xml:\n" + ex2.Message, "Column Names Warning", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
		}
		settings.SaveSettings();
		this.SaveClicked(this, new EventArgs());
	}

	private void PopulateSmoothingDropdown()
	{
		for (int i = VirtualDyno.Properties.Settings.Default.MIN_SMOOTHING; i <= VirtualDyno.Properties.Settings.Default.MAX_SMOOTHING; i++)
		{
			cbSmoothingFactor.Items.Add(i);
		}
	}

	private void PopulateDefaultProfileDropdown()
	{
		try
		{
			cbDefaultProfile.Items.Add("<None>");
			DataRow[] array = settings.CarProfiles.Tables["CarProfile"].Select("", "ProfileName asc");
			for (int i = 0; i < array.Length; i++)
			{
				CarProfile.CarProfileRow carProfileRow = (CarProfile.CarProfileRow)array[i];
				cbDefaultProfile.Items.Add(carProfileRow.ProfileName);
			}
		}
		catch
		{
		}
	}

	private void btnClearBackground_Click(object sender, EventArgs e)
	{
		picBackgroundImagePreview.Image = null;
		settings.GraphSettingsRow.BackgroundImage = null;
	}

	private void btnBrowseBackgroundImage_Click(object sender, EventArgs e)
	{
		using OpenFileDialog openFileDialog = new OpenFileDialog();
		openFileDialog.Multiselect = false;
		openFileDialog.Filter = "Image Files|*.jpg;*.gif;*.png;*.bmp";
		openFileDialog.CheckFileExists = true;
		openFileDialog.CheckPathExists = true;
		if (openFileDialog.ShowDialog() == DialogResult.OK)
		{
			picBackgroundImagePreview.Image = Statics.ResizeImage(Image.FromFile(openFileDialog.FileName), picBackgroundImagePreview.Width, picBackgroundImagePreview.Height, Statics.ResizeMode.Normal);
			settings.GraphSettingsRow.BackgroundImage = Statics.imageToByteArray(Image.FromFile(openFileDialog.FileName));
		}
	}

	private void cbDynos_SelectedValueChanged(object sender, EventArgs e)
	{
		txtDynoCorrectionFactor.Text = ((ComboBox)sender).SelectedValue.ToString();
	}

	private void trkBackgroundTransparency_Scroll(object sender, EventArgs e)
	{
		lblTransparencyValue.Text = trkBackgroundTransparency.Value + "%";
	}

	private void btnSaveGraphOptions_Click(object sender, EventArgs e)
	{
		SaveSettings();
		base.DialogResult = DialogResult.OK;
		Close();
	}

	private void btnCancel_Click(object sender, EventArgs e)
	{
		Close();
	}

	private void btnSetProfilePath_Click(object sender, EventArgs e)
	{
		FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
		folderBrowserDialog.SelectedPath = lblProfilePath.Text;
		if (DialogResult.OK != folderBrowserDialog.ShowDialog())
		{
			return;
		}
		if (!File.Exists(Path.Combine(folderBrowserDialog.SelectedPath, VirtualDyno.Properties.Settings.Default.File_Profiles)))
		{
			if (MessageBox.Show("This will overwrite the profiles already existing at: " + folderBrowserDialog.SelectedPath + "." + Environment.NewLine + Environment.NewLine + "Continue?", "Continue?", MessageBoxButtons.YesNo) == DialogResult.Yes)
			{
				lblProfilePath.Text = folderBrowserDialog.SelectedPath.Trim();
			}
		}
		else
		{
			lblProfilePath.Text = folderBrowserDialog.SelectedPath.Trim();
		}
	}

	private void cbCustomDCF_CheckedChanged(object sender, EventArgs e)
	{
		txtDynoCorrectionFactor.ReadOnly = !cbCustomDCF.Checked;
		cbDynos.Enabled = !cbCustomDCF.Checked;
		if (!cbCustomDCF.Checked)
		{
			txtDynoCorrectionFactor.Text = cbDynos.SelectedValue.ToString();
		}
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
		System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VirtualDyno.SettingsWindow));
		this.tabControl1 = new System.Windows.Forms.TabControl();
		this.tabGraph = new System.Windows.Forms.TabPage();
		this.groupBox2 = new System.Windows.Forms.GroupBox();
		this.groupBox11 = new System.Windows.Forms.GroupBox();
		this.textBox1 = new System.Windows.Forms.TextBox();
		this.cbRpmTrimWindow = new System.Windows.Forms.ComboBox();
		this.chkShowConfirmCloseMessage = new System.Windows.Forms.CheckBox();
		this.chkShowLegend = new System.Windows.Forms.CheckBox();
		this.cbMetricWeightandTemp = new System.Windows.Forms.CheckBox();
		this.chkShowMaximums = new System.Windows.Forms.CheckBox();
		this.cbLineThickness = new System.Windows.Forms.ComboBox();
		this.chkSmoothAFRBoostOption = new System.Windows.Forms.CheckBox();
		this.label7 = new System.Windows.Forms.Label();
		this.chkShowDataPoints = new System.Windows.Forms.CheckBox();
		this.chkAutoTrimTPS = new System.Windows.Forms.CheckBox();
		this.tabDyno = new System.Windows.Forms.TabPage();
		this.groupBox10 = new System.Windows.Forms.GroupBox();
		this.cbDefaultProfile = new System.Windows.Forms.ComboBox();
		this.groupBox9 = new System.Windows.Forms.GroupBox();
		this.rbBoostDoNotConvert = new System.Windows.Forms.RadioButton();
		this.rbBAR = new System.Windows.Forms.RadioButton();
		this.rbMillibar = new System.Windows.Forms.RadioButton();
		this.rbPSI = new System.Windows.Forms.RadioButton();
		this.groupBox3 = new System.Windows.Forms.GroupBox();
		this.cbDynos = new System.Windows.Forms.ComboBox();
		this.txtDynoCorrectionFactor = new System.Windows.Forms.TextBox();
		this.cbCustomDCF = new System.Windows.Forms.CheckBox();
		this.groupBox1 = new System.Windows.Forms.GroupBox();
		this.cbSmoothingFactor = new System.Windows.Forms.ComboBox();
		this.groupBox6 = new System.Windows.Forms.GroupBox();
		this.rbPS = new System.Windows.Forms.RadioButton();
		this.rbkW = new System.Windows.Forms.RadioButton();
		this.rbHP = new System.Windows.Forms.RadioButton();
		this.groupBox7 = new System.Windows.Forms.GroupBox();
		this.rbkgfm = new System.Windows.Forms.RadioButton();
		this.rbNm = new System.Windows.Forms.RadioButton();
		this.rblbft = new System.Windows.Forms.RadioButton();
		this.tabColumnsAndProfiles = new System.Windows.Forms.TabPage();
		this.groupBox4 = new System.Windows.Forms.GroupBox();
		this.chkIncludeBoost = new System.Windows.Forms.CheckBox();
		this.chkIncludeAFR = new System.Windows.Forms.CheckBox();
		this.txtSettingBoostColumns = new System.Windows.Forms.TextBox();
		this.label6 = new System.Windows.Forms.Label();
		this.txtSettingTPSColumns = new System.Windows.Forms.TextBox();
		this.txtSettingAFRColumns = new System.Windows.Forms.TextBox();
		this.txtSettingRPMColumns = new System.Windows.Forms.TextBox();
		this.txtSettingTimeColumns = new System.Windows.Forms.TextBox();
		this.label3 = new System.Windows.Forms.Label();
		this.label4 = new System.Windows.Forms.Label();
		this.label2 = new System.Windows.Forms.Label();
		this.label1 = new System.Windows.Forms.Label();
		this.btnSetProfilePath = new System.Windows.Forms.Button();
		this.groupBox8 = new System.Windows.Forms.GroupBox();
		this.lblProfilePath = new System.Windows.Forms.Label();
		this.tabBackground = new System.Windows.Forms.TabPage();
		this.groupBox5 = new System.Windows.Forms.GroupBox();
		this.lblTransparencyValue = new System.Windows.Forms.Label();
		this.label5 = new System.Windows.Forms.Label();
		this.btnBrowseBackgroundImage = new System.Windows.Forms.Button();
		this.btnClearBackground = new System.Windows.Forms.Button();
		this.trkBackgroundTransparency = new System.Windows.Forms.TrackBar();
		this.chkStretch = new System.Windows.Forms.CheckBox();
		this.picBackgroundImagePreview = new System.Windows.Forms.PictureBox();
		this.label11 = new System.Windows.Forms.Label();
		this.btnSaveGraphOptions = new System.Windows.Forms.Button();
		this.btnCancel = new System.Windows.Forms.Button();
		this.btnGraphBackColor = new System.Windows.Forms.Button();
		this.btnChartBackColor = new System.Windows.Forms.Button();
		this.cbBezierSmoothing = new System.Windows.Forms.CheckBox();
		this.panel1 = new System.Windows.Forms.Panel();
		this.tabControl1.SuspendLayout();
		this.tabGraph.SuspendLayout();
		this.groupBox2.SuspendLayout();
		this.groupBox11.SuspendLayout();
		this.tabDyno.SuspendLayout();
		this.groupBox10.SuspendLayout();
		this.groupBox9.SuspendLayout();
		this.groupBox3.SuspendLayout();
		this.groupBox1.SuspendLayout();
		this.groupBox6.SuspendLayout();
		this.groupBox7.SuspendLayout();
		this.tabColumnsAndProfiles.SuspendLayout();
		this.groupBox4.SuspendLayout();
		this.groupBox8.SuspendLayout();
		this.tabBackground.SuspendLayout();
		this.groupBox5.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.trkBackgroundTransparency).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.picBackgroundImagePreview).BeginInit();
		base.SuspendLayout();
		this.tabControl1.Controls.Add(this.tabGraph);
		this.tabControl1.Controls.Add(this.tabDyno);
		this.tabControl1.Controls.Add(this.tabColumnsAndProfiles);
		this.tabControl1.Controls.Add(this.tabBackground);
		this.tabControl1.Location = new System.Drawing.Point(2, 5);
		this.tabControl1.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.tabControl1.Name = "tabControl1";
		this.tabControl1.SelectedIndex = 0;
		this.tabControl1.Size = new System.Drawing.Size(488, 420);
		this.tabControl1.TabIndex = 45;
		this.tabGraph.BackColor = System.Drawing.Color.White;
		this.tabGraph.Controls.Add(this.groupBox2);
		this.tabGraph.Location = new System.Drawing.Point(4, 29);
		this.tabGraph.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.tabGraph.Name = "tabGraph";
		this.tabGraph.Padding = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.tabGraph.Size = new System.Drawing.Size(480, 387);
		this.tabGraph.TabIndex = 0;
		this.tabGraph.Text = "Graph";
		this.groupBox2.Controls.Add(this.groupBox11);
		this.groupBox2.Controls.Add(this.chkShowConfirmCloseMessage);
		this.groupBox2.Controls.Add(this.chkShowLegend);
		this.groupBox2.Controls.Add(this.cbMetricWeightandTemp);
		this.groupBox2.Controls.Add(this.chkShowMaximums);
		this.groupBox2.Controls.Add(this.cbLineThickness);
		this.groupBox2.Controls.Add(this.chkSmoothAFRBoostOption);
		this.groupBox2.Controls.Add(this.label7);
		this.groupBox2.Controls.Add(this.chkShowDataPoints);
		this.groupBox2.Controls.Add(this.chkAutoTrimTPS);
		this.groupBox2.Location = new System.Drawing.Point(4, 5);
		this.groupBox2.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.groupBox2.Name = "groupBox2";
		this.groupBox2.Padding = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.groupBox2.Size = new System.Drawing.Size(462, 377);
		this.groupBox2.TabIndex = 5;
		this.groupBox2.TabStop = false;
		this.groupBox2.Text = "Graphing Options";
		this.groupBox11.Controls.Add(this.textBox1);
		this.groupBox11.Controls.Add(this.cbRpmTrimWindow);
		this.groupBox11.Location = new System.Drawing.Point(248, 122);
		this.groupBox11.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.groupBox11.Name = "groupBox11";
		this.groupBox11.Padding = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.groupBox11.Size = new System.Drawing.Size(208, 138);
		this.groupBox11.TabIndex = 47;
		this.groupBox11.TabStop = false;
		this.groupBox11.Text = "RPM Trim Window";
		this.textBox1.BackColor = System.Drawing.Color.White;
		this.textBox1.BorderStyle = System.Windows.Forms.BorderStyle.None;
		this.textBox1.Location = new System.Drawing.Point(12, 66);
		this.textBox1.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.textBox1.Multiline = true;
		this.textBox1.Name = "textBox1";
		this.textBox1.ReadOnly = true;
		this.textBox1.Size = new System.Drawing.Size(189, 62);
		this.textBox1.TabIndex = 46;
		this.textBox1.Text = "(* Requires reloading logs.  Leave on 100 if you dont know what this does)";
		this.cbRpmTrimWindow.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
		this.cbRpmTrimWindow.FormattingEnabled = true;
		this.cbRpmTrimWindow.Items.AddRange(new object[5] { "10", "20", "50", "75", "100" });
		this.cbRpmTrimWindow.Location = new System.Drawing.Point(12, 29);
		this.cbRpmTrimWindow.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.cbRpmTrimWindow.Name = "cbRpmTrimWindow";
		this.cbRpmTrimWindow.Size = new System.Drawing.Size(112, 28);
		this.cbRpmTrimWindow.TabIndex = 45;
		this.chkShowConfirmCloseMessage.AutoSize = true;
		this.chkShowConfirmCloseMessage.Checked = true;
		this.chkShowConfirmCloseMessage.CheckState = System.Windows.Forms.CheckState.Checked;
		this.chkShowConfirmCloseMessage.Location = new System.Drawing.Point(9, 285);
		this.chkShowConfirmCloseMessage.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.chkShowConfirmCloseMessage.Name = "chkShowConfirmCloseMessage";
		this.chkShowConfirmCloseMessage.Size = new System.Drawing.Size(247, 24);
		this.chkShowConfirmCloseMessage.TabIndex = 44;
		this.chkShowConfirmCloseMessage.Text = "Show Confirm Close Message";
		this.chkShowConfirmCloseMessage.UseVisualStyleBackColor = true;
		this.chkShowLegend.AutoSize = true;
		this.chkShowLegend.Checked = true;
		this.chkShowLegend.CheckState = System.Windows.Forms.CheckState.Checked;
		this.chkShowLegend.Location = new System.Drawing.Point(9, 58);
		this.chkShowLegend.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.chkShowLegend.Name = "chkShowLegend";
		this.chkShowLegend.Size = new System.Drawing.Size(133, 24);
		this.chkShowLegend.TabIndex = 1;
		this.chkShowLegend.Text = "Show Legend";
		this.chkShowLegend.UseVisualStyleBackColor = true;
		this.cbMetricWeightandTemp.AutoSize = true;
		this.cbMetricWeightandTemp.Location = new System.Drawing.Point(9, 122);
		this.cbMetricWeightandTemp.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.cbMetricWeightandTemp.Name = "cbMetricWeightandTemp";
		this.cbMetricWeightandTemp.Size = new System.Drawing.Size(207, 24);
		this.cbMetricWeightandTemp.TabIndex = 5;
		this.cbMetricWeightandTemp.Text = "Metric Weight and Temp";
		this.cbMetricWeightandTemp.UseVisualStyleBackColor = true;
		this.chkShowMaximums.AutoSize = true;
		this.chkShowMaximums.Checked = true;
		this.chkShowMaximums.CheckState = System.Windows.Forms.CheckState.Checked;
		this.chkShowMaximums.Location = new System.Drawing.Point(9, 152);
		this.chkShowMaximums.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.chkShowMaximums.Name = "chkShowMaximums";
		this.chkShowMaximums.Size = new System.Drawing.Size(154, 24);
		this.chkShowMaximums.TabIndex = 4;
		this.chkShowMaximums.Text = "Show Maximums";
		this.chkShowMaximums.UseVisualStyleBackColor = true;
		this.cbLineThickness.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
		this.cbLineThickness.FormattingEnabled = true;
		this.cbLineThickness.Items.AddRange(new object[9] { "1.00", "1.25", "1.50", "1.75", "2.00", "2.25", "2.50", "2.75", "3.00" });
		this.cbLineThickness.Location = new System.Drawing.Point(260, 52);
		this.cbLineThickness.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.cbLineThickness.Name = "cbLineThickness";
		this.cbLineThickness.Size = new System.Drawing.Size(112, 28);
		this.cbLineThickness.TabIndex = 6;
		this.chkSmoothAFRBoostOption.AutoSize = true;
		this.chkSmoothAFRBoostOption.Checked = true;
		this.chkSmoothAFRBoostOption.CheckState = System.Windows.Forms.CheckState.Checked;
		this.chkSmoothAFRBoostOption.Location = new System.Drawing.Point(9, 183);
		this.chkSmoothAFRBoostOption.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.chkSmoothAFRBoostOption.Name = "chkSmoothAFRBoostOption";
		this.chkSmoothAFRBoostOption.Size = new System.Drawing.Size(174, 24);
		this.chkSmoothAFRBoostOption.TabIndex = 3;
		this.chkSmoothAFRBoostOption.Text = "Smooth AFR/Boost";
		this.chkSmoothAFRBoostOption.UseVisualStyleBackColor = true;
		this.label7.AutoSize = true;
		this.label7.Location = new System.Drawing.Point(255, 28);
		this.label7.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
		this.label7.Name = "label7";
		this.label7.Size = new System.Drawing.Size(114, 20);
		this.label7.TabIndex = 43;
		this.label7.Text = "Line Thickness";
		this.chkShowDataPoints.AutoSize = true;
		this.chkShowDataPoints.Location = new System.Drawing.Point(9, 28);
		this.chkShowDataPoints.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.chkShowDataPoints.Name = "chkShowDataPoints";
		this.chkShowDataPoints.Size = new System.Drawing.Size(162, 24);
		this.chkShowDataPoints.TabIndex = 0;
		this.chkShowDataPoints.Text = "Show Data Points";
		this.chkShowDataPoints.UseVisualStyleBackColor = true;
		this.chkAutoTrimTPS.AutoSize = true;
		this.chkAutoTrimTPS.Checked = true;
		this.chkAutoTrimTPS.CheckState = System.Windows.Forms.CheckState.Checked;
		this.chkAutoTrimTPS.Location = new System.Drawing.Point(9, 91);
		this.chkAutoTrimTPS.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.chkAutoTrimTPS.Name = "chkAutoTrimTPS";
		this.chkAutoTrimTPS.Size = new System.Drawing.Size(143, 24);
		this.chkAutoTrimTPS.TabIndex = 2;
		this.chkAutoTrimTPS.Text = "Auto trim TPS *";
		this.chkAutoTrimTPS.UseVisualStyleBackColor = true;
		this.tabDyno.BackColor = System.Drawing.Color.White;
		this.tabDyno.Controls.Add(this.groupBox10);
		this.tabDyno.Controls.Add(this.groupBox9);
		this.tabDyno.Controls.Add(this.groupBox3);
		this.tabDyno.Controls.Add(this.groupBox1);
		this.tabDyno.Controls.Add(this.groupBox6);
		this.tabDyno.Controls.Add(this.groupBox7);
		this.tabDyno.Location = new System.Drawing.Point(4, 29);
		this.tabDyno.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.tabDyno.Name = "tabDyno";
		this.tabDyno.Padding = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.tabDyno.Size = new System.Drawing.Size(480, 387);
		this.tabDyno.TabIndex = 1;
		this.tabDyno.Text = "Dyno";
		this.groupBox10.Controls.Add(this.cbDefaultProfile);
		this.groupBox10.Location = new System.Drawing.Point(18, 225);
		this.groupBox10.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.groupBox10.Name = "groupBox10";
		this.groupBox10.Padding = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.groupBox10.Size = new System.Drawing.Size(202, 77);
		this.groupBox10.TabIndex = 5;
		this.groupBox10.TabStop = false;
		this.groupBox10.Text = "Default Profile";
		this.cbDefaultProfile.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
		this.cbDefaultProfile.FormattingEnabled = true;
		this.cbDefaultProfile.Location = new System.Drawing.Point(15, 31);
		this.cbDefaultProfile.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.cbDefaultProfile.Name = "cbDefaultProfile";
		this.cbDefaultProfile.Size = new System.Drawing.Size(168, 28);
		this.cbDefaultProfile.TabIndex = 4;
		this.groupBox9.Controls.Add(this.rbBoostDoNotConvert);
		this.groupBox9.Controls.Add(this.rbBAR);
		this.groupBox9.Controls.Add(this.rbMillibar);
		this.groupBox9.Controls.Add(this.rbPSI);
		this.groupBox9.Location = new System.Drawing.Point(256, 153);
		this.groupBox9.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.groupBox9.Name = "groupBox9";
		this.groupBox9.Padding = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.groupBox9.Size = new System.Drawing.Size(195, 174);
		this.groupBox9.TabIndex = 25;
		this.groupBox9.TabStop = false;
		this.groupBox9.Text = "Boost";
		this.rbBoostDoNotConvert.AutoSize = true;
		this.rbBoostDoNotConvert.Checked = true;
		this.rbBoostDoNotConvert.Location = new System.Drawing.Point(9, 27);
		this.rbBoostDoNotConvert.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.rbBoostDoNotConvert.Name = "rbBoostDoNotConvert";
		this.rbBoostDoNotConvert.Size = new System.Drawing.Size(138, 24);
		this.rbBoostDoNotConvert.TabIndex = 9;
		this.rbBoostDoNotConvert.TabStop = true;
		this.rbBoostDoNotConvert.Text = "Do not convert";
		this.rbBoostDoNotConvert.UseVisualStyleBackColor = true;
		this.rbBAR.AutoSize = true;
		this.rbBAR.Location = new System.Drawing.Point(9, 133);
		this.rbBAR.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.rbBAR.Name = "rbBAR";
		this.rbBAR.Size = new System.Drawing.Size(145, 24);
		this.rbBAR.TabIndex = 12;
		this.rbBAR.Text = "Convert to BAR";
		this.rbBAR.UseVisualStyleBackColor = true;
		this.rbMillibar.AutoSize = true;
		this.rbMillibar.Location = new System.Drawing.Point(9, 98);
		this.rbMillibar.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.rbMillibar.Name = "rbMillibar";
		this.rbMillibar.Size = new System.Drawing.Size(159, 24);
		this.rbMillibar.TabIndex = 11;
		this.rbMillibar.Text = "Convert to Millibar";
		this.rbMillibar.UseVisualStyleBackColor = true;
		this.rbPSI.AutoSize = true;
		this.rbPSI.Location = new System.Drawing.Point(9, 61);
		this.rbPSI.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.rbPSI.Name = "rbPSI";
		this.rbPSI.Size = new System.Drawing.Size(137, 24);
		this.rbPSI.TabIndex = 10;
		this.rbPSI.Text = "Convert to PSI";
		this.rbPSI.UseVisualStyleBackColor = true;
		this.groupBox3.Controls.Add(this.cbDynos);
		this.groupBox3.Controls.Add(this.txtDynoCorrectionFactor);
		this.groupBox3.Controls.Add(this.cbCustomDCF);
		this.groupBox3.Location = new System.Drawing.Point(18, 11);
		this.groupBox3.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.groupBox3.Name = "groupBox3";
		this.groupBox3.Padding = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.groupBox3.Size = new System.Drawing.Size(202, 120);
		this.groupBox3.TabIndex = 7;
		this.groupBox3.TabStop = false;
		this.groupBox3.Text = "Dyno Correction Factor";
		this.cbDynos.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
		this.cbDynos.FormattingEnabled = true;
		this.cbDynos.Location = new System.Drawing.Point(9, 31);
		this.cbDynos.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.cbDynos.Name = "cbDynos";
		this.cbDynos.Size = new System.Drawing.Size(175, 28);
		this.cbDynos.TabIndex = 1;
		this.cbDynos.SelectedValueChanged += new System.EventHandler(cbDynos_SelectedValueChanged);
		this.txtDynoCorrectionFactor.Location = new System.Drawing.Point(14, 72);
		this.txtDynoCorrectionFactor.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.txtDynoCorrectionFactor.Name = "txtDynoCorrectionFactor";
		this.txtDynoCorrectionFactor.ReadOnly = true;
		this.txtDynoCorrectionFactor.Size = new System.Drawing.Size(78, 26);
		this.txtDynoCorrectionFactor.TabIndex = 2;
		this.cbCustomDCF.AutoSize = true;
		this.cbCustomDCF.Location = new System.Drawing.Point(100, 77);
		this.cbCustomDCF.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.cbCustomDCF.Name = "cbCustomDCF";
		this.cbCustomDCF.Size = new System.Drawing.Size(90, 24);
		this.cbCustomDCF.TabIndex = 3;
		this.cbCustomDCF.Text = "Custom";
		this.cbCustomDCF.UseVisualStyleBackColor = true;
		this.cbCustomDCF.CheckedChanged += new System.EventHandler(cbCustomDCF_CheckedChanged);
		this.groupBox1.Controls.Add(this.cbSmoothingFactor);
		this.groupBox1.Location = new System.Drawing.Point(18, 138);
		this.groupBox1.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.groupBox1.Name = "groupBox1";
		this.groupBox1.Padding = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.groupBox1.Size = new System.Drawing.Size(202, 77);
		this.groupBox1.TabIndex = 4;
		this.groupBox1.TabStop = false;
		this.groupBox1.Text = "Smoothing Factor";
		this.cbSmoothingFactor.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
		this.cbSmoothingFactor.FormattingEnabled = true;
		this.cbSmoothingFactor.Location = new System.Drawing.Point(40, 31);
		this.cbSmoothingFactor.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.cbSmoothingFactor.Name = "cbSmoothingFactor";
		this.cbSmoothingFactor.Size = new System.Drawing.Size(116, 28);
		this.cbSmoothingFactor.TabIndex = 4;
		this.groupBox6.Controls.Add(this.rbPS);
		this.groupBox6.Controls.Add(this.rbkW);
		this.groupBox6.Controls.Add(this.rbHP);
		this.groupBox6.Location = new System.Drawing.Point(255, 11);
		this.groupBox6.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.groupBox6.Name = "groupBox6";
		this.groupBox6.Padding = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.groupBox6.Size = new System.Drawing.Size(88, 132);
		this.groupBox6.TabIndex = 24;
		this.groupBox6.TabStop = false;
		this.groupBox6.Text = "Power";
		this.rbPS.AutoSize = true;
		this.rbPS.Location = new System.Drawing.Point(14, 101);
		this.rbPS.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.rbPS.Name = "rbPS";
		this.rbPS.Size = new System.Drawing.Size(55, 24);
		this.rbPS.TabIndex = 7;
		this.rbPS.Text = "PS";
		this.rbPS.UseVisualStyleBackColor = true;
		this.rbkW.AutoSize = true;
		this.rbkW.Location = new System.Drawing.Point(14, 66);
		this.rbkW.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.rbkW.Name = "rbkW";
		this.rbkW.Size = new System.Drawing.Size(57, 24);
		this.rbkW.TabIndex = 6;
		this.rbkW.Text = "kW";
		this.rbkW.UseVisualStyleBackColor = true;
		this.rbHP.AutoSize = true;
		this.rbHP.Checked = true;
		this.rbHP.Location = new System.Drawing.Point(14, 31);
		this.rbHP.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.rbHP.Name = "rbHP";
		this.rbHP.Size = new System.Drawing.Size(56, 24);
		this.rbHP.TabIndex = 5;
		this.rbHP.TabStop = true;
		this.rbHP.Text = "HP";
		this.rbHP.UseVisualStyleBackColor = true;
		this.groupBox7.Controls.Add(this.rbkgfm);
		this.groupBox7.Controls.Add(this.rbNm);
		this.groupBox7.Controls.Add(this.rblbft);
		this.groupBox7.Location = new System.Drawing.Point(352, 11);
		this.groupBox7.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.groupBox7.Name = "groupBox7";
		this.groupBox7.Padding = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.groupBox7.Size = new System.Drawing.Size(99, 132);
		this.groupBox7.TabIndex = 42;
		this.groupBox7.TabStop = false;
		this.groupBox7.Text = "Torque";
		this.rbkgfm.AutoSize = true;
		this.rbkgfm.Location = new System.Drawing.Point(14, 101);
		this.rbkgfm.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.rbkgfm.Name = "rbkgfm";
		this.rbkgfm.Size = new System.Drawing.Size(69, 24);
		this.rbkgfm.TabIndex = 43;
		this.rbkgfm.Text = "kgfm";
		this.rbkgfm.UseVisualStyleBackColor = true;
		this.rbNm.AutoSize = true;
		this.rbNm.Location = new System.Drawing.Point(14, 66);
		this.rbNm.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.rbNm.Name = "rbNm";
		this.rbNm.Size = new System.Drawing.Size(58, 24);
		this.rbNm.TabIndex = 8;
		this.rbNm.Text = "Nm";
		this.rbNm.UseVisualStyleBackColor = true;
		this.rblbft.AutoSize = true;
		this.rblbft.Checked = true;
		this.rblbft.Location = new System.Drawing.Point(14, 31);
		this.rblbft.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.rblbft.Name = "rblbft";
		this.rblbft.Size = new System.Drawing.Size(60, 24);
		this.rblbft.TabIndex = 7;
		this.rblbft.TabStop = true;
		this.rblbft.Text = "lb/ft";
		this.rblbft.UseVisualStyleBackColor = true;
		this.tabColumnsAndProfiles.BackColor = System.Drawing.Color.White;
		this.tabColumnsAndProfiles.Controls.Add(this.groupBox4);
		this.tabColumnsAndProfiles.Controls.Add(this.btnSetProfilePath);
		this.tabColumnsAndProfiles.Controls.Add(this.groupBox8);
		this.tabColumnsAndProfiles.Location = new System.Drawing.Point(4, 29);
		this.tabColumnsAndProfiles.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.tabColumnsAndProfiles.Name = "tabColumnsAndProfiles";
		this.tabColumnsAndProfiles.Padding = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.tabColumnsAndProfiles.Size = new System.Drawing.Size(480, 387);
		this.tabColumnsAndProfiles.TabIndex = 2;
		this.tabColumnsAndProfiles.Text = "Columns and Profiles";
		this.groupBox4.Controls.Add(this.chkIncludeBoost);
		this.groupBox4.Controls.Add(this.chkIncludeAFR);
		this.groupBox4.Controls.Add(this.txtSettingBoostColumns);
		this.groupBox4.Controls.Add(this.label6);
		this.groupBox4.Controls.Add(this.txtSettingTPSColumns);
		this.groupBox4.Controls.Add(this.txtSettingAFRColumns);
		this.groupBox4.Controls.Add(this.txtSettingRPMColumns);
		this.groupBox4.Controls.Add(this.txtSettingTimeColumns);
		this.groupBox4.Controls.Add(this.label3);
		this.groupBox4.Controls.Add(this.label4);
		this.groupBox4.Controls.Add(this.label2);
		this.groupBox4.Controls.Add(this.label1);
		this.groupBox4.Location = new System.Drawing.Point(9, 11);
		this.groupBox4.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.groupBox4.Name = "groupBox4";
		this.groupBox4.Padding = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.groupBox4.Size = new System.Drawing.Size(400, 228);
		this.groupBox4.TabIndex = 22;
		this.groupBox4.TabStop = false;
		this.groupBox4.Text = "Custom Column Names in Logs";
		this.chkIncludeBoost.AutoSize = true;
		this.chkIncludeBoost.Checked = true;
		this.chkIncludeBoost.CheckState = System.Windows.Forms.CheckState.Checked;
		this.chkIncludeBoost.Location = new System.Drawing.Point(10, 191);
		this.chkIncludeBoost.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.chkIncludeBoost.Name = "chkIncludeBoost";
		this.chkIncludeBoost.Size = new System.Drawing.Size(22, 21);
		this.chkIncludeBoost.TabIndex = 6;
		this.chkIncludeBoost.UseVisualStyleBackColor = true;
		this.chkIncludeAFR.AutoSize = true;
		this.chkIncludeAFR.Checked = true;
		this.chkIncludeAFR.CheckState = System.Windows.Forms.CheckState.Checked;
		this.chkIncludeAFR.Location = new System.Drawing.Point(10, 111);
		this.chkIncludeAFR.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.chkIncludeAFR.Name = "chkIncludeAFR";
		this.chkIncludeAFR.Size = new System.Drawing.Size(22, 21);
		this.chkIncludeAFR.TabIndex = 5;
		this.chkIncludeAFR.UseVisualStyleBackColor = true;
		this.txtSettingBoostColumns.Location = new System.Drawing.Point(82, 185);
		this.txtSettingBoostColumns.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.txtSettingBoostColumns.Name = "txtSettingBoostColumns";
		this.txtSettingBoostColumns.Size = new System.Drawing.Size(306, 26);
		this.txtSettingBoostColumns.TabIndex = 4;
		this.label6.AutoSize = true;
		this.label6.Location = new System.Drawing.Point(34, 191);
		this.label6.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
		this.label6.Name = "label6";
		this.label6.Size = new System.Drawing.Size(51, 20);
		this.label6.TabIndex = 29;
		this.label6.Text = "Boost";
		this.txtSettingTPSColumns.Location = new System.Drawing.Point(82, 145);
		this.txtSettingTPSColumns.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.txtSettingTPSColumns.Name = "txtSettingTPSColumns";
		this.txtSettingTPSColumns.Size = new System.Drawing.Size(306, 26);
		this.txtSettingTPSColumns.TabIndex = 3;
		this.txtSettingAFRColumns.Location = new System.Drawing.Point(82, 105);
		this.txtSettingAFRColumns.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.txtSettingAFRColumns.Name = "txtSettingAFRColumns";
		this.txtSettingAFRColumns.Size = new System.Drawing.Size(306, 26);
		this.txtSettingAFRColumns.TabIndex = 2;
		this.txtSettingRPMColumns.Location = new System.Drawing.Point(82, 65);
		this.txtSettingRPMColumns.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.txtSettingRPMColumns.Name = "txtSettingRPMColumns";
		this.txtSettingRPMColumns.Size = new System.Drawing.Size(306, 26);
		this.txtSettingRPMColumns.TabIndex = 1;
		this.txtSettingTimeColumns.Location = new System.Drawing.Point(82, 25);
		this.txtSettingTimeColumns.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.txtSettingTimeColumns.Name = "txtSettingTimeColumns";
		this.txtSettingTimeColumns.Size = new System.Drawing.Size(306, 26);
		this.txtSettingTimeColumns.TabIndex = 0;
		this.label3.AutoSize = true;
		this.label3.Location = new System.Drawing.Point(42, 111);
		this.label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
		this.label3.Name = "label3";
		this.label3.Size = new System.Drawing.Size(42, 20);
		this.label3.TabIndex = 25;
		this.label3.Text = "AFR";
		this.label4.AutoSize = true;
		this.label4.Location = new System.Drawing.Point(42, 151);
		this.label4.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
		this.label4.Name = "label4";
		this.label4.Size = new System.Drawing.Size(39, 20);
		this.label4.TabIndex = 26;
		this.label4.Text = "TPS";
		this.label2.AutoSize = true;
		this.label2.Location = new System.Drawing.Point(38, 71);
		this.label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
		this.label2.Name = "label2";
		this.label2.Size = new System.Drawing.Size(44, 20);
		this.label2.TabIndex = 23;
		this.label2.Text = "RPM";
		this.label1.AutoSize = true;
		this.label1.Location = new System.Drawing.Point(38, 31);
		this.label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
		this.label1.Name = "label1";
		this.label1.Size = new System.Drawing.Size(43, 20);
		this.label1.TabIndex = 22;
		this.label1.Text = "Time";
		this.btnSetProfilePath.Location = new System.Drawing.Point(418, 260);
		this.btnSetProfilePath.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.btnSetProfilePath.Name = "btnSetProfilePath";
		this.btnSetProfilePath.Size = new System.Drawing.Size(48, 57);
		this.btnSetProfilePath.TabIndex = 7;
		this.btnSetProfilePath.Text = "...";
		this.btnSetProfilePath.UseVisualStyleBackColor = true;
		this.groupBox8.Controls.Add(this.lblProfilePath);
		this.groupBox8.Location = new System.Drawing.Point(9, 246);
		this.groupBox8.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.groupBox8.Name = "groupBox8";
		this.groupBox8.Padding = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.groupBox8.Size = new System.Drawing.Size(400, 74);
		this.groupBox8.TabIndex = 43;
		this.groupBox8.TabStop = false;
		this.groupBox8.Text = "Profile Path";
		this.lblProfilePath.AutoSize = true;
		this.lblProfilePath.Location = new System.Drawing.Point(20, 34);
		this.lblProfilePath.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
		this.lblProfilePath.Name = "lblProfilePath";
		this.lblProfilePath.Size = new System.Drawing.Size(154, 20);
		this.lblProfilePath.TabIndex = 1;
		this.lblProfilePath.Text = "[Default Profile Path]";
		this.tabBackground.Controls.Add(this.groupBox5);
		this.tabBackground.Location = new System.Drawing.Point(4, 29);
		this.tabBackground.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.tabBackground.Name = "tabBackground";
		this.tabBackground.Size = new System.Drawing.Size(480, 387);
		this.tabBackground.TabIndex = 4;
		this.tabBackground.Text = "Background";
		this.tabBackground.UseVisualStyleBackColor = true;
		this.groupBox5.Controls.Add(this.lblTransparencyValue);
		this.groupBox5.Controls.Add(this.label5);
		this.groupBox5.Controls.Add(this.btnBrowseBackgroundImage);
		this.groupBox5.Controls.Add(this.btnClearBackground);
		this.groupBox5.Controls.Add(this.trkBackgroundTransparency);
		this.groupBox5.Controls.Add(this.chkStretch);
		this.groupBox5.Controls.Add(this.picBackgroundImagePreview);
		this.groupBox5.Location = new System.Drawing.Point(0, 11);
		this.groupBox5.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.groupBox5.Name = "groupBox5";
		this.groupBox5.Padding = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.groupBox5.Size = new System.Drawing.Size(471, 371);
		this.groupBox5.TabIndex = 24;
		this.groupBox5.TabStop = false;
		this.groupBox5.Text = "Background";
		this.lblTransparencyValue.AutoSize = true;
		this.lblTransparencyValue.Font = new System.Drawing.Font("Microsoft Sans Serif", 6.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.lblTransparencyValue.Location = new System.Drawing.Point(436, 249);
		this.lblTransparencyValue.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
		this.lblTransparencyValue.Name = "lblTransparencyValue";
		this.lblTransparencyValue.Size = new System.Drawing.Size(16, 17);
		this.lblTransparencyValue.TabIndex = 6;
		this.lblTransparencyValue.Text = "0";
		this.label5.AutoSize = true;
		this.label5.Font = new System.Drawing.Font("Microsoft Sans Serif", 6.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.label5.Location = new System.Drawing.Point(336, 249);
		this.label5.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
		this.label5.Name = "label5";
		this.label5.Size = new System.Drawing.Size(96, 17);
		this.label5.TabIndex = 5;
		this.label5.Text = "Transparency";
		this.btnBrowseBackgroundImage.Location = new System.Drawing.Point(338, 29);
		this.btnBrowseBackgroundImage.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.btnBrowseBackgroundImage.Name = "btnBrowseBackgroundImage";
		this.btnBrowseBackgroundImage.Size = new System.Drawing.Size(90, 35);
		this.btnBrowseBackgroundImage.TabIndex = 8;
		this.btnBrowseBackgroundImage.Text = "Browse";
		this.btnBrowseBackgroundImage.UseVisualStyleBackColor = true;
		this.btnBrowseBackgroundImage.Click += new System.EventHandler(btnBrowseBackgroundImage_Click);
		this.btnClearBackground.Location = new System.Drawing.Point(338, 74);
		this.btnClearBackground.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.btnClearBackground.Name = "btnClearBackground";
		this.btnClearBackground.Size = new System.Drawing.Size(90, 35);
		this.btnClearBackground.TabIndex = 9;
		this.btnClearBackground.Text = "Clear";
		this.btnClearBackground.UseVisualStyleBackColor = true;
		this.btnClearBackground.Click += new System.EventHandler(btnClearBackground_Click);
		this.trkBackgroundTransparency.AutoSize = false;
		this.trkBackgroundTransparency.BackColor = System.Drawing.Color.White;
		this.trkBackgroundTransparency.Location = new System.Drawing.Point(339, 272);
		this.trkBackgroundTransparency.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.trkBackgroundTransparency.Maximum = 100;
		this.trkBackgroundTransparency.Name = "trkBackgroundTransparency";
		this.trkBackgroundTransparency.Size = new System.Drawing.Size(123, 38);
		this.trkBackgroundTransparency.TabIndex = 10;
		this.trkBackgroundTransparency.TabStop = false;
		this.trkBackgroundTransparency.TickStyle = System.Windows.Forms.TickStyle.None;
		this.trkBackgroundTransparency.Scroll += new System.EventHandler(trkBackgroundTransparency_Scroll);
		this.chkStretch.AutoSize = true;
		this.chkStretch.Checked = true;
		this.chkStretch.CheckState = System.Windows.Forms.CheckState.Checked;
		this.chkStretch.Location = new System.Drawing.Point(338, 118);
		this.chkStretch.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.chkStretch.Name = "chkStretch";
		this.chkStretch.Size = new System.Drawing.Size(87, 24);
		this.chkStretch.TabIndex = 7;
		this.chkStretch.Text = "Stretch";
		this.chkStretch.UseVisualStyleBackColor = true;
		this.picBackgroundImagePreview.BackColor = System.Drawing.Color.White;
		this.picBackgroundImagePreview.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.picBackgroundImagePreview.Location = new System.Drawing.Point(10, 29);
		this.picBackgroundImagePreview.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.picBackgroundImagePreview.Name = "picBackgroundImagePreview";
		this.picBackgroundImagePreview.Size = new System.Drawing.Size(318, 259);
		this.picBackgroundImagePreview.TabIndex = 0;
		this.picBackgroundImagePreview.TabStop = false;
		this.label11.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
		this.label11.AutoSize = true;
		this.label11.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Italic, System.Drawing.GraphicsUnit.Point, 0);
		this.label11.Location = new System.Drawing.Point(8, 443);
		this.label11.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
		this.label11.Name = "label11";
		this.label11.Size = new System.Drawing.Size(183, 20);
		this.label11.TabIndex = 91;
		this.label11.Text = "* requires reloading run";
		this.label11.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
		this.btnSaveGraphOptions.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
		this.btnSaveGraphOptions.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
		this.btnSaveGraphOptions.Location = new System.Drawing.Point(346, 435);
		this.btnSaveGraphOptions.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.btnSaveGraphOptions.Name = "btnSaveGraphOptions";
		this.btnSaveGraphOptions.Size = new System.Drawing.Size(122, 38);
		this.btnSaveGraphOptions.TabIndex = 92;
		this.btnSaveGraphOptions.Text = "&Save";
		this.btnSaveGraphOptions.UseVisualStyleBackColor = true;
		this.btnSaveGraphOptions.Click += new System.EventHandler(btnSaveGraphOptions_Click);
		this.btnCancel.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
		this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
		this.btnCancel.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
		this.btnCancel.Location = new System.Drawing.Point(216, 435);
		this.btnCancel.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.btnCancel.Name = "btnCancel";
		this.btnCancel.Size = new System.Drawing.Size(122, 38);
		this.btnCancel.TabIndex = 93;
		this.btnCancel.Text = "&Cancel";
		this.btnCancel.UseVisualStyleBackColor = true;
		this.btnCancel.Click += new System.EventHandler(btnCancel_Click);
		this.btnGraphBackColor.BackColor = System.Drawing.Color.White;
		this.btnGraphBackColor.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.btnGraphBackColor.Location = new System.Drawing.Point(33, 114);
		this.btnGraphBackColor.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.btnGraphBackColor.Name = "btnGraphBackColor";
		this.btnGraphBackColor.Size = new System.Drawing.Size(68, 35);
		this.btnGraphBackColor.TabIndex = 94;
		this.btnGraphBackColor.UseVisualStyleBackColor = false;
		this.btnGraphBackColor.Visible = false;
		this.btnChartBackColor.BackColor = System.Drawing.Color.White;
		this.btnChartBackColor.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.btnChartBackColor.Location = new System.Drawing.Point(33, 158);
		this.btnChartBackColor.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.btnChartBackColor.Name = "btnChartBackColor";
		this.btnChartBackColor.Size = new System.Drawing.Size(68, 35);
		this.btnChartBackColor.TabIndex = 95;
		this.btnChartBackColor.UseVisualStyleBackColor = false;
		this.btnChartBackColor.Visible = false;
		this.cbBezierSmoothing.AutoSize = true;
		this.cbBezierSmoothing.Location = new System.Drawing.Point(33, 203);
		this.cbBezierSmoothing.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.cbBezierSmoothing.Name = "cbBezierSmoothing";
		this.cbBezierSmoothing.Size = new System.Drawing.Size(161, 24);
		this.cbBezierSmoothing.TabIndex = 96;
		this.cbBezierSmoothing.Text = "Bezier Smoothing";
		this.cbBezierSmoothing.UseVisualStyleBackColor = true;
		this.cbBezierSmoothing.Visible = false;
		this.panel1.Dock = System.Windows.Forms.DockStyle.Bottom;
		this.panel1.Location = new System.Drawing.Point(0, 431);
		this.panel1.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		this.panel1.Name = "panel1";
		this.panel1.Size = new System.Drawing.Size(486, 46);
		this.panel1.TabIndex = 97;
		base.AutoScaleDimensions = new System.Drawing.SizeF(9f, 20f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		this.BackColor = System.Drawing.Color.White;
		base.CancelButton = this.btnCancel;
		base.ClientSize = new System.Drawing.Size(486, 477);
		base.Controls.Add(this.tabControl1);
		base.Controls.Add(this.label11);
		base.Controls.Add(this.btnSaveGraphOptions);
		base.Controls.Add(this.btnCancel);
		base.Controls.Add(this.btnGraphBackColor);
		base.Controls.Add(this.btnChartBackColor);
		base.Controls.Add(this.cbBezierSmoothing);
		base.Controls.Add(this.panel1);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
		base.Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
		base.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
		base.MaximizeBox = false;
		base.MinimizeBox = false;
		base.Name = "SettingsWindow";
		base.ShowInTaskbar = false;
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
		this.Text = "Virtual Dyno Settings";
		this.tabControl1.ResumeLayout(false);
		this.tabGraph.ResumeLayout(false);
		this.groupBox2.ResumeLayout(false);
		this.groupBox2.PerformLayout();
		this.groupBox11.ResumeLayout(false);
		this.groupBox11.PerformLayout();
		this.tabDyno.ResumeLayout(false);
		this.groupBox10.ResumeLayout(false);
		this.groupBox9.ResumeLayout(false);
		this.groupBox9.PerformLayout();
		this.groupBox3.ResumeLayout(false);
		this.groupBox3.PerformLayout();
		this.groupBox1.ResumeLayout(false);
		this.groupBox6.ResumeLayout(false);
		this.groupBox6.PerformLayout();
		this.groupBox7.ResumeLayout(false);
		this.groupBox7.PerformLayout();
		this.tabColumnsAndProfiles.ResumeLayout(false);
		this.groupBox4.ResumeLayout(false);
		this.groupBox4.PerformLayout();
		this.groupBox8.ResumeLayout(false);
		this.groupBox8.PerformLayout();
		this.tabBackground.ResumeLayout(false);
		this.groupBox5.ResumeLayout(false);
		this.groupBox5.PerformLayout();
		((System.ComponentModel.ISupportInitialize)this.trkBackgroundTransparency).EndInit();
		((System.ComponentModel.ISupportInitialize)this.picBackgroundImagePreview).EndInit();
		base.ResumeLayout(false);
		base.PerformLayout();
	}
}

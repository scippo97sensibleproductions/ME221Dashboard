using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using VirtualDyno.Core;
using VirtualDyno.Core.Datasets;
using VirtualDyno.Core.Resources;
using VirtualDyno.Properties;
using VirtualDyno.RunControl;
using ZedGraph;

namespace VirtualDyno;

public class frmVirtualDyno : Form
{
	private static bool REGISTERED = true;

	private static char[] COLUMN_SEPERATORS;

	private static char[] COLUMN_TRIM_CHARS;

	private static string[] SUPPORTED_FILE_EXT;

	private static double BAR_CEILING_VALUE;

	private static double MILLIBAR_FLOOR_VALUE;

	private Color[] GraphingColors = new Color[VirtualDyno.Properties.Settings.Default.MAX_LOADED_RUNS];

	private Dictionary<CurveItem, string> CurveLabel = new Dictionary<CurveItem, string>();

	private Point WindowSize;

	private string RegionName = string.Empty;

	private Settings settings = new Settings();

	private int SelectedProfileId;

	private IContainer components;

	private MenuStrip MainMenuTop;

	private ToolStripMenuItem fileToolStripMenuItem;

	private ToolStripMenuItem toolStripMenuItem_Exit;

	private ZedGraphControl HPGraph;

	private ToolStripMenuItem toolStripMenuItem_LoadRuns;

	private SplitContainer splitContainerHPGraph_LeftPanelProfiles;

	private ToolStripSeparator toolStripSeparator1;

	private SplitContainer splitContainer2;

	private ToolStripSeparator toolStripSeparator2;

	private ToolStripMenuItem toolStripMenuItem_Options;

	private ToolStripMenuItem loadRunsToolStripMenuItem;

	private ToolStripMenuItem optionsToolStripMenuItem1;

	private ToolStripSeparator toolStripSeparator3;

	private ToolStripMenuItem exitToolStripMenuItem;

	private ToolStripMenuItem toolStripMenuItem_Help;

	private ToolStripMenuItem toolStripMenuItem_About;

	private ToolStrip HPGraphToolstrip;

	private ToolStripButton btnGraphToClipboard;

	private ToolStripButton btnGraphToFile;

	private ToolStripSeparator toolStripSeparator4;

	private ToolStripSeparator toolStripSeparator5;

	private ToolStripDropDownButton ddlSmoothingFactor;

	private ToolStripMenuItem toolStripMenuItem_Donate;

	private ToolStripButton btnCancelZoom;

	private System.Windows.Forms.Label lblClosestPointData;

	private ToolStripSeparator toolStripSeparator6;

	private ToolStripSeparator toolStripSeparator7;

	private StatusStrip CopyrightStrip;

	private SplitContainer splitContainer4;

	private StatusStrip statusStrip1;

	private ToolStripStatusLabel lblStatus;

	private ToolStripDropDownButton ddVersion;

	private ToolStripMenuItem releaseNotesToolStripMenuItem;

	private SplitContainer splitContainerLeftPanel_OpenedRuns;

	private System.Windows.Forms.Label lblOpenedRunsTitle;

	private FlowLayoutPanel leftPanel;

	private ToolStripMenuItem AddProfile;

	private ToolStripSeparator toolStripSeparator8;

	private ToolStripMenuItem getUpdateToolStripMenuItem;

	private ToolStripDropDownButton toolStripDropDownButton1;

	private ToolStripMenuItem toolStripMenuItem_PageSetup;

	private ToolStripMenuItem toolStripMenuItem_Print;

	private ToolStripMenuItem toolStripMenuItem_PrintPreview;

	private ContextMenuStrip contextMenuStrip_Graph;

	private ToolStripMenuItem toolStripMenuItem_GraphToClipboard;

	private ToolStripMenuItem printToolStripMenuItem1;

	private ToolStripMenuItem toolStripMenuItem_PageSetup_Context;

	private ToolStripMenuItem toolStripMenuItem_Print_Context;

	private ToolStripMenuItem toolStripMenuItem_PrintPreview_Context;

	private ToolStripMenuItem toolStripMenuItem_Profile;

	private ToolStripProgressBar pbStatus;

	private ToolStripSeparator toolStripSeparator9;

	private ToolStripMenuItem contextMenuItem_ToggleLegend;

	private ToolStripMenuItem contextMenuItem_ToggleDataPoints;

	private ToolStripMenuItem toolStripMenuItem_Smoothing;

	private ToolStripMenuItem toolStripMenuItem_ReleaseNotes;

	private ToolStripSeparator toolStripSeparator10;

	private ToolStripMenuItem toolStripMenuItem_CarEditor;

	private ToolStripSplitButton ddActiveProfile;

	private ToolStripMenuItem toolStripMenuItem_OpenDataFolder;

	private ToolStripDropDownButton toolStripDropDownButton2;

	private ToolStripMenuItem toolStripMenuItem_ToggleLegend;

	private ToolStripMenuItem toolStripMenuItem_ToggleDataPoints;

	private ToolStripMenuItem toolStripMenuItem_ToggleHP;

	private ToolStripMenuItem toolStripMenuItem_ToggleTQ;

	private ToolStripMenuItem toolStripMenuItem_GraphToFile;

	private RoundedPanel pShowValues;

	private System.Windows.Forms.Label lblPointData;

	private PictureBox pictureAdvertiseBottom;

	private Panel panelAdvertisement;

	private PictureBox btnCloseAdvertisement;

	private System.Windows.Forms.Timer tAdvertisementCloseButton;

	private ToolStripStatusLabel lblCredits;

	public string RPMcolumns { get; set; } = string.Empty;

	public string TPScolumns { get; set; } = string.Empty;

	public string Timecolumns { get; set; } = string.Empty;

	public string AFRcolumns { get; set; } = string.Empty;

	public string Boostcolumns { get; set; } = string.Empty;

	public string ProfileName
	{
		get
		{
			return ddActiveProfile.Text.Trim();
		}
		set
		{
			ddActiveProfile.Text = value.Trim();
		}
	}

	public double LineThickness
	{
		get
		{
			try
			{
				return settings.GraphSettingsRow.LineThickness;
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
				settings.GraphSettingsRow.LineThickness = value;
			}
			catch
			{
			}
		}
	}

	public Columns ColumnList
	{
		get
		{
			return ColumnList1;
		}
		set
		{
			ColumnList1 = value;
		}
	}

	public string ProfilePath
	{
		get
		{
			try
			{
				return settings.GraphSettingsRow.ProfilesPath.Trim();
			}
			catch
			{
				return Statics.baseFilepath;
			}
		}
		set
		{
			settings.GraphSettingsRow.ProfilesPath = value;
		}
	}

	public Columns ColumnList1 { get; set; } = new Columns();

	public frmVirtualDyno(string[] args)
	{
		using (SplashScreen splashScreen = new SplashScreen(settings))
		{
			splashScreen.FormClosed += SplashScreen_FormClosed;
			ColorizeVersionControl(splashScreen.ShowDialog());
		}
		if (args.Length != 0)
		{
			LoadSelectedRuns(args);
		}
	}

	private void CheckForCustomCarFolder()
	{
		if (!Directory.Exists(Path.Combine(Statics.baseFilepath, "CustomCars")))
		{
			try
			{
				Directory.CreateDirectory(Path.Combine(Statics.baseFilepath, "CustomCars"));
			}
			catch
			{
				Statics.Error(new Exception(ErrorMessages.E122));
				toolStripMenuItem_CarEditor.Enabled = false;
			}
		}
	}

	private void SplashScreen_FormClosed(object sender, FormClosedEventArgs e)
	{
		InitializeComponent();
		Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
		LoadSettings();
		leftPanel.ControlRemoved += leftPanel_ControlRemoved;
		Text = Assembly.GetExecutingAssembly().GetName().Name;
		ddVersion.Text = "Version: " + Statics.Version();
		Text = Text + " - " + Statics.Version();
		if (File.Exists(Statics.baseFilepath))
		{
			File.Delete(Statics.baseFilepath);
		}
		if (!Directory.Exists(Statics.baseFilepath))
		{
			Directory.CreateDirectory(Statics.baseFilepath);
		}
		CheckForCustomCarFolder();
		InitializeColors();
		PopulateSmoothingDropdown();
		SetupGraph();
		try
		{
			SetSmoothingSelection(settings.GraphSettingsRow.SmoothingFactor);
		}
		catch
		{
		}
	}

	private void InitializeColors()
	{
		GraphingColors[0] = Color.FromArgb(255, 255, 55, 0);
		GraphingColors[1] = Color.FromArgb(255, 0, 102, 204);
		GraphingColors[2] = Color.FromArgb(255, 0, 153, 0);
		GraphingColors[3] = Color.Orange;
		GraphingColors[4] = Color.Chartreuse;
		GraphingColors[5] = Color.FromArgb(255, 173, 127, 168);
		GraphingColors[6] = Color.HotPink;
		GraphingColors[7] = Color.MediumSpringGreen;
		GraphingColors[8] = Color.SkyBlue;
		GraphingColors[9] = Color.Brown;
	}

	protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
	{
		if (keyData == (Keys.C | Keys.Control))
		{
			btnGraphToClipboard.PerformClick();
		}
		return base.ProcessCmdKey(ref msg, keyData);
	}

	private void ResetGraphControlsLocation()
	{
		HPGraphToolstrip.Location = new Point(HPGraph.Width - HPGraphToolstrip.Width, 2);
		HPGraphToolstrip.BringToFront();
	}

	private void RefreshGraph()
	{
		HPGraph.GraphPane.Title.Text = ((settings.GraphSettingsRow.DynoName.ToLowerInvariant().Trim() == "custom") ? ("Custom CF=" + settings.GraphSettingsRow.DynoCorrectionFactor) : settings.GraphSettingsRow.DynoName);
		CalculatePoints();
	}

	private void SetGraphLayout()
	{
		using (Graphics g = CreateGraphics())
		{
			if (settings.GraphSettingsRow.IncludeAFR || settings.GraphSettingsRow.IncludeBoost)
			{
				HPGraph.MasterPane.SetLayout(g, isColumnSpecified: true, new int[2] { 1, 1 }, new float[2] { 2f, 1f });
				HPGraph.GraphPane.BaseDimension = 12f;
				HPGraph.MasterPane.PaneList[1].BaseDimension = 6f;
				HPGraph.GraphPane.XAxis.Scale.IsVisible = false;
				HPGraph.MasterPane.PaneList[1].XAxis.Scale.IsVisible = true;
				HPGraph.GraphPane.Y2Axis.Scale.IsVisible = true;
				HPGraph.MasterPane.PaneList[1].Y2Axis.Scale.IsVisible = true;
				HPGraph.AxisChange();
			}
			else
			{
				HPGraph.MasterPane.SetLayout(g, isColumnSpecified: true, new int[1] { 1 }, new float[1] { 4f });
				HPGraph.GraphPane.BaseDimension = 12f;
				HPGraph.GraphPane.XAxis.Scale.IsVisible = true;
				HPGraph.GraphPane.Y2Axis.Scale.IsVisible = true;
				HPGraph.AxisChange();
			}
		}
		HPGraph.MasterPane.Border.IsVisible = false;
	}

	private void SetupGraph()
	{
		Color.FromArgb(255, 255, 225);
		HPGraph.MasterPane.PaneList.Clear();
		HPGraph.IsShowContextMenu = false;
		GraphPane graphPane = new GraphPane(default(RectangleF), "Horsepower/Torque", "", "HORSEPOWER");
		GraphPane pane = new GraphPane(default(RectangleF), "AFR/Boost", "Engine Speed (RPM)", "Air / Fuel");
		HPGraph.MasterPane.Add(graphPane);
		HPGraph.MasterPane.Add(pane);
		SetGraphLayout();
		if (HPGraph.MasterPane.PaneList.Count > 1)
		{
			pane = HPGraph.MasterPane.PaneList[1];
		}
		if (!settings.GraphSettingsRow.IsBackgroundImageNull())
		{
			float num = (100f - (float)settings.GraphSettingsRow.BackgroundTransparency) / 100f;
			ColorMatrix newColorMatrix = new ColorMatrix(new float[5][]
			{
				new float[5] { 1f, 0f, 0f, 0f, 0f },
				new float[5] { 0f, 1f, 0f, 0f, 0f },
				new float[5] { 0f, 0f, 1f, 0f, 0f },
				new float[5] { 0f, 0f, 0f, num, 0f },
				new float[5] { 0f, 0f, 0f, 0f, 1f }
			});
			using ImageAttributes imageAttributes = new ImageAttributes();
			imageAttributes.SetColorMatrix(newColorMatrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
			try
			{
				using TextureBrush brush = new TextureBrush(dstRect: new RectangleF(0f, 0f, Statics.byteArrayToImage(settings.GraphSettingsRow.BackgroundImage).Width, Statics.byteArrayToImage(settings.GraphSettingsRow.BackgroundImage).Height), image: Statics.byteArrayToImage(settings.GraphSettingsRow.BackgroundImage), imageAttr: imageAttributes);
				if (settings.GraphSettingsRow.BackgroundStretch)
				{
					graphPane.Chart.Fill = new Fill(brush, isScaled: true);
				}
				else
				{
					graphPane.Chart.Fill = new Fill(brush, AlignH.Left, AlignV.Top);
				}
			}
			catch
			{
			}
		}
		HPGraph.MasterPane.InnerPaneGap = 0f;
		graphPane.Margin.Bottom = 0f;
		graphPane.Title.IsVisible = true;
		graphPane.Title.FontSpec.Size = 18f;
		graphPane.Title.FontSpec.IsBold = true;
		graphPane.Border.IsVisible = false;
		graphPane.YAxis.MinSpace = 68f;
		graphPane.Y2Axis.MinSpace = 68f;
		graphPane.XAxis.Title.IsVisible = true;
		graphPane.YAxis.Title.FontSpec.Size = 16f;
		graphPane.YAxis.Title.FontSpec.IsBold = true;
		graphPane.YAxis.Title.IsVisible = true;
		graphPane.XAxis.Scale.Min = 0.0;
		graphPane.XAxis.Scale.Max = 9000.0;
		graphPane.XAxis.Scale.FontSpec.Size = 16f;
		graphPane.YAxis.Scale.FontSpec.Size = 16f;
		graphPane.Y2Axis.Scale.FontSpec.Size = 16f;
		graphPane.XAxis.Scale.FontSpec.FontColor = Color.Black;
		graphPane.YAxis.Scale.FontSpec.FontColor = Color.Black;
		graphPane.Y2Axis.Scale.FontSpec.FontColor = Color.Black;
		graphPane.XAxis.Scale.FontSpec.IsBold = true;
		graphPane.YAxis.Scale.FontSpec.IsBold = true;
		graphPane.Y2Axis.Scale.FontSpec.IsBold = true;
		graphPane.Y2Axis.Scale.IsVisible = true;
		graphPane.YAxis.Scale.Min = 0.0;
		graphPane.YAxis.Scale.Max = 900.0;
		graphPane.Y2Axis.Scale.Min = 0.0;
		graphPane.Y2Axis.Scale.Max = 900.0;
		graphPane.XAxis.Title.IsVisible = false;
		graphPane.Y2Axis.IsAxisSegmentVisible = true;
		graphPane.Y2Axis.IsVisible = true;
		graphPane.Y2Axis.Title.Text = "TORQUE";
		graphPane.YAxis.Scale.IsSkipFirstLabel = true;
		graphPane.Y2Axis.Scale.IsSkipFirstLabel = true;
		graphPane.XAxis.MajorGrid.Color = Color.DarkGray;
		graphPane.XAxis.MajorGrid.IsVisible = true;
		graphPane.XAxis.MajorGrid.DashOn = 0f;
		graphPane.YAxis.MajorGrid.Color = Color.DarkGray;
		graphPane.YAxis.MajorGrid.IsVisible = true;
		graphPane.YAxis.MajorGrid.DashOn = 0f;
		pane = HPGraph.MasterPane.PaneList[1];
		pane.Margin.Top = 0f;
		pane.Title.IsVisible = false;
		pane.Border.IsVisible = false;
		pane.YAxis.MinSpace = 68f;
		pane.Y2Axis.MinSpace = 68f;
		pane.XAxis.Title.IsVisible = true;
		pane.YAxis.Title.FontSpec.Size = 16f;
		pane.YAxis.Title.FontSpec.IsBold = true;
		pane.YAxis.Title.IsVisible = true;
		pane.XAxis.Scale.Min = 0.0;
		pane.XAxis.Scale.Max = 9000.0;
		pane.XAxis.Scale.FontSpec.Size = 16f;
		pane.YAxis.Scale.FontSpec.Size = 16f;
		pane.Y2Axis.Scale.FontSpec.Size = 16f;
		pane.XAxis.Scale.FontSpec.FontColor = Color.Black;
		pane.YAxis.Scale.FontSpec.FontColor = Color.Black;
		pane.Y2Axis.Scale.FontSpec.FontColor = Color.Black;
		pane.XAxis.Scale.FontSpec.IsBold = true;
		pane.YAxis.Scale.FontSpec.IsBold = true;
		pane.Y2Axis.Scale.FontSpec.IsBold = true;
		pane.Legend.IsVisible = false;
		pane.YAxis.Scale.Min = 0.0;
		pane.YAxis.Scale.Max = 0.0;
		pane.Y2Axis.Scale.Min = 0.0;
		pane.Y2Axis.Scale.Max = 1.0;
		pane.Y2Axis.Title.IsVisible = true;
		pane.YAxis.Scale.MajorStep = 1.0;
		pane.Y2Axis.Scale.MajorStep = 2.0;
		pane.YAxis.Scale.MinorStep = 1.0;
		pane.Y2Axis.Scale.MinorStep = 2.0;
		pane.YAxis.Scale.IsSkipLastLabel = true;
		pane.Y2Axis.Scale.IsSkipLastLabel = true;
		pane.YAxis.MinorTic.IsInside = true;
		pane.Y2Axis.MinorTic.IsInside = true;
		pane.Y2Axis.IsAxisSegmentVisible = true;
		pane.Y2Axis.IsVisible = true;
		pane.Y2Axis.Title.Text = "BOOST";
		pane.XAxis.MajorGrid.Color = Color.DarkGray;
		pane.XAxis.MajorGrid.IsVisible = true;
		pane.XAxis.MajorGrid.DashOn = 0f;
		pane.YAxis.MajorGrid.Color = Color.DarkGray;
		pane.YAxis.MajorGrid.IsVisible = true;
		pane.YAxis.MajorGrid.DashOn = 0f;
		pane.YAxis.MinorGrid.Color = Color.DimGray;
		pane.YAxis.MinorGrid.IsVisible = false;
		pane.YAxis.MinorGrid.DashOn = 0f;
		pane.Y2Axis.MajorGrid.Color = Color.DarkGray;
		pane.Y2Axis.MajorGrid.IsVisible = true;
		pane.Y2Axis.MajorGrid.DashOn = 1f;
		pane.Y2Axis.MinorGrid.Color = Color.DimGray;
		pane.Y2Axis.MinorGrid.IsVisible = false;
		pane.Y2Axis.MinorGrid.DashOn = 1f;
		HPGraph.GraphPane.Y2Axis.Title.Text = GetHPCurveLabel();
		HPGraph.GraphPane.YAxis.Title.Text = GetTQCurveLabel();
		if (!REGISTERED)
		{
			TextObj textObj = new TextObj("Unlicensed", 0.5, 0.5);
			textObj.Location.CoordinateFrame = CoordType.PaneFraction;
			textObj.FontSpec.Angle = 45f;
			textObj.FontSpec.FontColor = Color.FromArgb(40, 255, 100, 100);
			textObj.FontSpec.IsBold = true;
			textObj.FontSpec.Size = 100f;
			textObj.FontSpec.Border.IsVisible = false;
			textObj.FontSpec.Fill.IsVisible = false;
			textObj.Location.AlignH = AlignH.Center;
			textObj.Location.AlignV = AlignV.Center;
			textObj.ZOrder = ZOrder.A_InFront;
			HPGraph.MasterPane.GraphObjList.Add(textObj);
		}
		Version version = Assembly.GetExecutingAssembly().GetName().Version;
		string name = Assembly.GetExecutingAssembly().GetName().Name;
		float num2 = 0.006f;
		float num3 = HPGraph.GraphPane.Chart.Rect.Width / HPGraph.GraphPane.Chart.Rect.Height;
		TextObj textObj2 = new TextObj(string.Format(General.Graph_GeneratedBy, name, version.Major, version.Minor, version.Build), num2, num3 * num2);
		textObj2.Location.CoordinateFrame = CoordType.ChartFraction;
		textObj2.FontSpec.Angle = 0f;
		textObj2.FontSpec.FontColor = ((!settings.GraphSettingsRow.IsBackgroundImageNull()) ? Color.FromArgb(255, 255, 55, 0) : Color.Gray);
		textObj2.FontSpec.IsAntiAlias = true;
		textObj2.FontSpec.IsItalic = true;
		textObj2.FontSpec.IsBold = true;
		textObj2.FontSpec.Size = 12f;
		textObj2.FontSpec.Border.IsVisible = false;
		textObj2.FontSpec.Fill.IsVisible = false;
		textObj2.Location.AlignH = AlignH.Left;
		textObj2.Location.AlignV = AlignV.Top;
		HPGraph.GraphPane.GraphObjList.Add(textObj2);
		HPGraph.IsEnableWheelZoom = true;
		HPGraph.AxisChange();
		HPGraph.Refresh();
	}

	private DataTable LoadFileFromStreamReader(StreamReader sr, string RunTitle, int TotalLines)
	{
		using DataTable dataTable = new DataTable(RunTitle);
		long length = sr.BaseStream.Length;
		long num = 0L;
		string[] array = sr.ReadLine().Split(COLUMN_SEPERATORS);
		foreach (string text in array)
		{
			try
			{
				dataTable.Columns.Add(text.Trim(COLUMN_TRIM_CHARS));
			}
			catch
			{
				dataTable.Columns.Add(text.Trim(COLUMN_TRIM_CHARS) + "_" + DateTime.Now.TimeOfDay.TotalMilliseconds);
			}
		}
		Regex regex = new Regex(",(?=(?:[^\"]*\"[^\"]*\")*(?![^\"]*\"))|[\t]");
		double num2 = 0.0;
		dataTable.Columns.Add("UNTITLED");
		while (sr.Peek() >= 0)
		{
			try
			{
				string text2 = sr.ReadLine();
				num += text2.Length;
				string[] array2 = regex.Split(text2, dataTable.Columns.Count);
				double num3 = Math.Round((double)num / (double)length * 100.0 + 0.5, MidpointRounding.AwayFromZero);
				if (num3 > num2)
				{
					num2 = num3;
					lblStatus.Text = "Loading " + Path.GetFileName(RunTitle) + " ... " + num2 + "%";
					Application.DoEvents();
				}
				if (array2.Length < dataTable.Columns.Count && sr.Peek() < 0)
				{
					continue;
				}
				bool flag = true;
				for (int j = 0; j < array2.Length; j++)
				{
					if (!string.IsNullOrEmpty(array2[j].Trim()))
					{
						flag = false;
						array2[j] = array2[j].Replace(",", ".").Trim(COLUMN_TRIM_CHARS);
					}
				}
				if (!flag)
				{
					DataRowCollection rows = dataTable.Rows;
					object[] values = array2;
					rows.Add(values);
				}
			}
			catch (ArgumentException ex)
			{
				Statics.Error(new Exception(string.Format(ErrorMessages.E109, Environment.NewLine, ex.Message)));
				return null;
			}
		}
		bool flag2 = true;
		Stack<DataColumn> stack = new Stack<DataColumn>();
		foreach (DataColumn column in dataTable.Columns)
		{
			if (flag2)
			{
				flag2 = false;
				continue;
			}
			bool flag3 = false;
			foreach (DataRow row in dataTable.Rows)
			{
				if (!string.IsNullOrEmpty(row[column].ToString().Trim()))
				{
					flag3 = true;
					break;
				}
			}
			if (!flag3)
			{
				stack.Push(column);
			}
		}
		while (stack.Count > 0)
		{
			dataTable.Columns.Remove(stack.Pop());
		}
		return dataTable;
	}

	public string[] WriteSafeReadAllLines(string path)
	{
		using FileStream stream = File.OpenRead(path);
		using StreamReader streamReader = new StreamReader(stream);
		List<string> list = new List<string>();
		while (!streamReader.EndOfStream)
		{
			list.Add(streamReader.ReadLine());
		}
		return list.ToArray();
	}

	private void LoadSelectedRuns(string[] filenames)
	{
		if (filenames.Length > 1)
		{
			pbStatus.Visible = true;
		}
		pbStatus.Value = 0;
		pbStatus.Minimum = 0;
		pbStatus.Maximum = filenames.Length;
		List<string> source = new List<string>(filenames);
		ParallelOptions parallelOptions = new ParallelOptions();
		int index = leftPanel.Controls.Count;
		object locker = new object();
		Parallel.ForEach(source, parallelOptions, delegate(string filename)
		{
			int threadIndex = 0;
			lock (locker)
			{
				threadIndex = index++;
			}
			UIThread(delegate
			{
				pbStatus.Value++;
				pbStatus.Invalidate();
				return (object)null;
			});
			if (!string.IsNullOrEmpty(filename))
			{
				if (CheckIfSupportedFileExtension(filename))
				{
					StreamReader sr = null;
					try
					{
						sr = new StreamReader(File.OpenRead(filename), Encoding.ASCII, detectEncodingFromByteOrderMarks: true);
						if (sr.BaseStream.Length <= 0)
						{
							Statics.Error(new Exception(string.Format(ErrorMessages.E119, Environment.NewLine, filename)));
						}
						string tempstring = sr.ReadToEnd();
						string SoftwareName = string.Empty;
						int EndHeaderPosition = 0;
						string TimeColumnName = string.Empty;
						string RPMColumnName = string.Empty;
						string TPSColumnName = string.Empty;
						string AFRColumnName = string.Empty;
						string BOOSTColumnName = string.Empty;
						sr.BaseStream.Seek(0L, SeekOrigin.Begin);
						FindEndOfHeader(ref EndHeaderPosition, ref tempstring, ref SoftwareName);
						sr = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(tempstring)));
						sr.BaseStream.Seek(0L, SeekOrigin.Begin);
						if (sr.BaseStream.ReadByte() == 239 && sr.BaseStream.ReadByte() == 187 && sr.BaseStream.ReadByte() == 191)
						{
							EndHeaderPosition += 3;
						}
						sr.DiscardBufferedData();
						sr.BaseStream.Seek(EndHeaderPosition, SeekOrigin.Begin);
						if (!CheckForNeededColumns(ref sr, ref TimeColumnName, ref RPMColumnName, ref TPSColumnName, ref AFRColumnName, ref BOOSTColumnName))
						{
							string text = (string.IsNullOrEmpty(TimeColumnName) ? "Time" : string.Empty);
							text += (string.IsNullOrEmpty(RPMColumnName) ? ((string.IsNullOrEmpty(text) ? string.Empty : ", ") + "RPM") : string.Empty);
							text += (string.IsNullOrEmpty(TPSColumnName) ? ((string.IsNullOrEmpty(text) ? string.Empty : ", ") + "Throttle Position") : string.Empty);
							Statics.Error(new Exception(string.Format(ErrorMessages.E101, Environment.NewLine, filename, text)));
						}
						else if (IsFileLoaded(filename))
						{
							Statics.Error(new Exception(string.Format(ErrorMessages.E102, filename)));
						}
						else
						{
							double High = 0.0;
							double Low = 0.0;
							sr.DiscardBufferedData();
							sr.BaseStream.Seek(EndHeaderPosition, SeekOrigin.Begin);
							sr.BaseStream.Position = EndHeaderPosition;
							UIThread(() => lblStatus.Text = "Loading " + Path.GetFileName(filename));
							int totalLines = WriteSafeReadAllLines(filename).Length;
							DataTable table = LoadFileFromStreamReader(sr, Path.GetFileName(filename), totalLines);
							if (table != null)
							{
								FindRPMRange(table, ref High, ref Low);
								cRunControl rc = new cRunControl(Path.GetFileName(filename), filename, GraphingColors[threadIndex], High, Low, Statics.baseFilepath, VirtualDyno.Properties.Settings.Default.MIN_SMOOTHING, VirtualDyno.Properties.Settings.Default.MAX_SMOOTHING);
								rc.RunData = table;
								rc.IsMetric = settings.GraphSettingsRow.MetricWeightandTemp;
								rc.Refreshed += rc_Refreshed;
								rc.Renamed += rc_Renamed;
								rc.RunClosed += rc_RunClosed;
								rc.GraphOtherPressed += rc_GraphOtherPressed;
								rc.TimeColumnName = TimeColumnName;
								rc.RPMColumnName = RPMColumnName;
								rc.TPSColumnName = TPSColumnName;
								rc.AFRColumnName = AFRColumnName;
								rc.BOOSTColumnName = BOOSTColumnName;
								Color backcolor = Color.Empty;
								Color forecolor = Color.Empty;
								rc.LogType = FindLogFileType(rc.TimeColumnName, rc.RPMColumnName, rc.TPSColumnName, rc.AFRColumnName, rc.BOOSTColumnName, ref backcolor, ref forecolor, SoftwareName);
								rc.SetLogType(rc.LogType, backcolor, forecolor);
								rc.AFRColumnName = FindColumnName("AFR", rc.RunData, rc.LogType);
								rc.BOOSTColumnName = FindColumnName("Boost", rc.RunData, rc.LogType);
								rc.TimeConverter = GetTimeConverter(ref rc);
								double TPSTrimValue = 0.0;
								double MinRPM = 99999.0;
								double MaxRPM = 0.0;
								Statics.RPMTrimWindow_AutoTPSValue_SmoothTimeRPM(ref table, rc.RPMColumnName, rc.TPSColumnName, rc.TimeColumnName, settings.GraphSettingsRow.RpmTrimWindow, ref TPSTrimValue, settings.GraphSettingsRow.AutoTrimTPS, ref MinRPM, ref MaxRPM);
								rc.MinRPM = MinRPM;
								rc.MaxRPM = MaxRPM;
								rc.RunData = table;
								if (GetSelectedProfileRow() != null)
								{
									rc.SetFromProfile(GetSelectedProfileRow());
								}
								rc.Anchor = AnchorStyles.None;
								rc.SmoothingFactor = settings.GraphSettingsRow.SmoothingFactor;
								UIThread(delegate
								{
									lock (leftPanel)
									{
										leftPanel.Controls.Add(rc);
										leftPanel.Controls.SetChildIndex(rc, threadIndex);
									}
									lblStatus.Text = "";
									RefreshGraph();
									return new object();
								});
							}
						}
						return;
					}
					catch (IOException ex)
					{
						Statics.Error(new Exception(string.Format(ErrorMessages.E120, ex.Message)));
						return;
					}
					finally
					{
						sr?.Dispose();
					}
				}
				Statics.Error(new Exception(string.Format(ErrorMessages.E121, Path.GetExtension(filename), filename)));
			}
		});
		pbStatus.Visible = false;
		pbStatus.Value = 0;
	}

	private void UIThread(Func<object> p)
	{
		if (base.InvokeRequired)
		{
			BeginInvoke(p);
		}
		else
		{
			p();
		}
	}

	private void CalculatePoints()
	{
		_ = DateTime.Now;
		Parallel.ForEach(HPGraph.MasterPane.PaneList, delegate(GraphPane p)
		{
			p.CurveList.Clear();
		});
		CurveLabel.Clear();
		if (leftPanel.Controls.Count == 0)
		{
			SetupGraph();
			HPGraph.AxisChange();
			HPGraph.Refresh();
			return;
		}
		int num = 0;
		double MaxGraphValue = 0.0;
		double MaxGraphValue2 = 0.0;
		double MaxGraphValue3 = 0.0;
		double MaxGraphValue4 = 0.0;
		double MaxX = 0.0;
		double MinX = 1000000.0;
		double MinGraphValue = 1000000.0;
		double MinGraphValue2 = 1000000.0;
		double XatMaxY = 0.0;
		double XatMaxY2 = 0.0;
		List<string> timeFormats = GetTimeFormats();
		string[] array = new string[timeFormats.Count];
		for (int num2 = 0; num2 < timeFormats.Count; num2++)
		{
			array[num2] = timeFormats[num2];
		}
		int num3 = 25;
		int num4 = 25;
		string item = string.Empty;
		if (settings.GraphSettingsRow.IsHpTypeNull() || settings.GraphSettingsRow.HpType == 0)
		{
			item = Abbreviations.WheelHorsepower.ToLowerInvariant();
		}
		else if (settings.GraphSettingsRow.HpType == 2)
		{
			item = Abbreviations.Kilowatt;
		}
		else if (settings.GraphSettingsRow.HpType == 1)
		{
			item = Abbreviations.PS;
		}
		string item2 = string.Empty;
		if (settings.GraphSettingsRow.IsHpTypeNull() || settings.GraphSettingsRow.TqType == 0)
		{
			item2 = Abbreviations.PoundFeet;
		}
		else if (settings.GraphSettingsRow.TqType == 2)
		{
			item2 = Abbreviations.NewtonMeter;
		}
		else if (settings.GraphSettingsRow.TqType == 1)
		{
			item2 = Abbreviations.KilogramForceMeter;
			num3 = 10;
		}
		foreach (Control control in leftPanel.Controls)
		{
			if (!(control is cRunControl) || !((cRunControl)control).ShowRun)
			{
				continue;
			}
			PointPairList pointPairList = new PointPairList();
			PointPairList pointPairList2 = new PointPairList();
			PointPairList pointPairList3 = new PointPairList();
			PointPairList pointPairList4 = new PointPairList();
			double num5 = -1.0;
			double num6 = -1.0;
			double MaxY = 0.0;
			double MaxY2 = 0.0;
			double MaxY3 = 0.0;
			double MaxY4 = -999999.0;
			double MinY = 999999.0;
			double MinY2 = 999999.0;
			Color color = Color.Black;
			Color color2 = Color.Black;
			int num7 = 0;
			double totalWeight = 0.0;
			double num8 = 0.0;
			double num9 = 0.0;
			double num10 = 0.0;
			double num11 = 0.0;
			double num12 = 0.0;
			double num13 = 29.235;
			double num14 = 77.0;
			bool flag = false;
			cRunControl cRunControl2 = null;
			double num15 = 1.0;
			try
			{
				num7 = ((cRunControl)control).Gear;
				totalWeight = Convert.ToDouble(((cRunControl)control).Weight) + Convert.ToDouble(((cRunControl)control).OccupantWeight);
				num14 = ((cRunControl)control).AtmosphericTemperture;
				num13 = ((cRunControl)control).Barometer;
				flag = ((cRunControl)control).UseSAE;
				if (flag)
				{
					num15 = Calculations.SAECorrectionFactor(num13, num14, settings.GraphSettingsRow.MetricWeightandTemp);
				}
				num8 = ((cRunControl)control).TireHeight;
				num9 = ((cRunControl)control).GearRatio;
				num10 = ((cRunControl)control).FinalDrive;
				num11 = ((cRunControl)control).DragCoefficient;
				num12 = ((cRunControl)control).FrontalArea;
				color = ((cRunControl)control).LineColor;
				color2 = ((cRunControl)control).LineColor;
				if (num7 != 0 && num8 > 0.0 && num9 > 0.0 && num10 > 0.0 && num11 > 0.0 && num12 > 0.0)
				{
					cRunControl2 = (cRunControl)control;
				}
			}
			catch
			{
			}
			if (cRunControl2 == null)
			{
				continue;
			}
			DataTable dataTable = cRunControl2.RunData.Copy();
			cRunControl2.TimeColumnName = FindColumnName(Timecolumns, cRunControl2.RunData);
			cRunControl2.RPMColumnName = FindColumnName(RPMcolumns, cRunControl2.RunData);
			cRunControl2.TPSColumnName = FindColumnName(TPScolumns, cRunControl2.RunData);
			cRunControl2.AFRColumnName = FindColumnName("AFR", cRunControl2.RunData, cRunControl2.LogType);
			cRunControl2.BOOSTColumnName = FindColumnName("Boost", cRunControl2.RunData, cRunControl2.LogType);
			int num16 = Convert.ToInt32(cRunControl2.MaxRPM);
			int num17 = Convert.ToInt32(cRunControl2.MinRPM);
			if (!cRunControl2.AllFieldsPresent())
			{
				dataTable.Dispose();
				continue;
			}
			Statics.TrimIncompleteRows(dataTable, cRunControl2.RPMColumnName, cRunControl2.TPSColumnName, cRunControl2.TimeColumnName, cRunControl2.BOOSTColumnName, cRunControl2.AFRColumnName);
			foreach (DataRow row in dataTable.Rows)
			{
				double result = -1.0;
				double num18 = 0.0;
				double num19 = 0.0;
				double result2 = -1.0;
				double result3 = -999999.0;
				CultureInfo provider = new CultureInfo("en-US", useUserOverride: true);
				if (!string.IsNullOrEmpty(cRunControl2.RPMColumnName) && !string.IsNullOrEmpty(row[cRunControl2.RPMColumnName].ToString().Trim()))
				{
					num18 = Convert.ToDouble(row[cRunControl2.RPMColumnName].ToString().Trim(), provider);
				}
				if (num6 == -1.0 && !string.IsNullOrEmpty(row[cRunControl2.TimeColumnName].ToString().Trim()))
				{
					if (row[cRunControl2.TimeColumnName].ToString().Contains(":"))
					{
						DateTime dateTime = Convert.ToDateTime("00:00:00.000");
						try
						{
							num6 = (DateTime.Parse(row[cRunControl2.TimeColumnName].ToString()) - dateTime).TotalSeconds;
						}
						catch
						{
							try
							{
								num6 = (DateTime.ParseExact(row[cRunControl2.TimeColumnName].ToString(), array, CultureInfo.InvariantCulture, DateTimeStyles.None) - dateTime).TotalSeconds;
							}
							catch
							{
								Statics.Error(new Exception(string.Format(ErrorMessages.E104, row[cRunControl2.TimeColumnName])));
							}
						}
					}
					else
					{
						num6 = Convert.ToDouble(row[cRunControl2.TimeColumnName].ToString().Replace(":", string.Empty).Trim(), provider);
					}
					num5 = num18;
					continue;
				}
				if (num18 <= num5)
				{
					num5 = num18;
					continue;
				}
				if (!string.IsNullOrEmpty(cRunControl2.AFRColumnName) && !string.IsNullOrEmpty(row[cRunControl2.AFRColumnName].ToString().Trim()))
				{
					double.TryParse(row[cRunControl2.AFRColumnName].ToString().Trim(), NumberStyles.Number, provider, out result);
				}
				if (!string.IsNullOrEmpty(cRunControl2.TimeColumnName) && !string.IsNullOrEmpty(row[cRunControl2.TimeColumnName].ToString().Trim()))
				{
					if (row[cRunControl2.TimeColumnName].ToString().Contains(":"))
					{
						DateTime dateTime2 = Convert.ToDateTime("00:00:00.000");
						try
						{
							num19 = (DateTime.Parse(row[cRunControl2.TimeColumnName].ToString()) - dateTime2).TotalSeconds;
						}
						catch
						{
							try
							{
								num19 = (DateTime.ParseExact(row[cRunControl2.TimeColumnName].ToString(), array, CultureInfo.InvariantCulture, DateTimeStyles.None) - dateTime2).TotalSeconds;
							}
							catch
							{
								Statics.Error(new Exception(string.Format(ErrorMessages.E104, row[cRunControl2.TimeColumnName])));
							}
						}
					}
					else
					{
						num19 = Convert.ToDouble(row[cRunControl2.TimeColumnName].ToString().Replace(":", "").Trim(), provider) / (double)cRunControl2.TimeConverter;
					}
				}
				if (!string.IsNullOrEmpty(cRunControl2.TPSColumnName) && !string.IsNullOrEmpty(row[cRunControl2.TPSColumnName].ToString().Trim()))
				{
					double.TryParse(row[cRunControl2.TPSColumnName].ToString().Trim(), NumberStyles.Number, provider, out result2);
				}
				if (!string.IsNullOrEmpty(cRunControl2.BOOSTColumnName) && !string.IsNullOrEmpty(row[cRunControl2.BOOSTColumnName].ToString().Trim()))
				{
					double.TryParse(row[cRunControl2.BOOSTColumnName].ToString().Trim(), NumberStyles.Number, provider, out result3);
				}
				if (result2 == -1.0 || num18 == 0.0 || num19 == 0.0 || num19 == num6)
				{
					continue;
				}
				double num20 = Calculations.Horsepower(totalWeight, num18, num5, num19, num6, num8, num9, num10, settings.GraphSettingsRow.MetricWeightandTemp);
				num20 += Calculations.DragHorsepower(Calculations.MPH(num18, num9, num10, num8), num11, num12);
				num20 *= num15;
				double num21 = num20 * 5252.0 / num18;
				num20 *= settings.GraphSettingsRow.DynoCorrectionFactor;
				num21 *= settings.GraphSettingsRow.DynoCorrectionFactor;
				if (!settings.GraphSettingsRow.IsHpTypeNull())
				{
					if (settings.GraphSettingsRow.HpType == 2)
					{
						num20 *= 0.7457;
					}
					else if (settings.GraphSettingsRow.HpType == 1)
					{
						num20 /= 0.98632;
					}
				}
				if (!settings.GraphSettingsRow.IsTqTypeNull())
				{
					if (settings.GraphSettingsRow.TqType == 2)
					{
						num21 *= 1.355817952;
					}
					else if (settings.GraphSettingsRow.TqType == 1)
					{
						num21 /= 7.233;
					}
				}
				pointPairList.Add(num18, Math.Abs(num20));
				pointPairList2.Add(num18, Math.Abs(num21));
				if (result > 0.0)
				{
					if (Math.Abs(result) > 4.0)
					{
						pointPairList3.Add(num18, Math.Abs(result));
					}
					else
					{
						pointPairList3.Add(num18, Math.Abs(result) * 14.7);
					}
				}
				if (result3 != -999999.0)
				{
					pointPairList4.Add(num18, result3);
				}
				num6 = num19;
				num5 = num18;
			}
			if (pointPairList.Count <= 2)
			{
				ShowRunErrorMessage(cRunControl2, string.Format(General.NoWOTSectionFound, Environment.NewLine));
				dataTable.Dispose();
				continue;
			}
			int smoothingFactor = (int)Math.Round((double)cRunControl2.SmoothingFactor * VirtualDyno.Properties.Settings.Default.SMOOTHING_MULTIPLIER, MidpointRounding.ToEven);
			pointPairList = (settings.GraphSettingsRow.BezierSmoothing ? Statics.SmoothListBezier(pointPairList, smoothingFactor) : Statics.SmoothListAverage(pointPairList, smoothingFactor, 0));
			pointPairList2 = (settings.GraphSettingsRow.BezierSmoothing ? Statics.SmoothListBezier(pointPairList2, smoothingFactor) : Statics.SmoothListAverage(pointPairList2, smoothingFactor, 0));
			pointPairList3 = Statics.SmoothListAverage(pointPairList3, settings.GraphSettingsRow.SmoothAFRBoost ? 1 : 0, 1);
			pointPairList4 = Statics.SmoothListAverage(pointPairList4, settings.GraphSettingsRow.SmoothAFRBoost ? 1 : 0, 1);
			cRunControl2.TQPoints = pointPairList2;
			cRunControl2.HPPoints = pointPairList;
			double MinY3 = 0.0;
			double MinGraphValue3 = 0.0;
			Statics.TrimByRPMWindow(ref pointPairList, num17, num16, ref MinX, ref MaxX, ref MinY3, ref MaxY, ref MinGraphValue3, ref MaxGraphValue, ref XatMaxY);
			Statics.TrimByRPMWindow(ref pointPairList2, num17, num16, ref MinX, ref MaxX, ref MinY3, ref MaxY2, ref MinGraphValue3, ref MaxGraphValue2, ref XatMaxY2);
			if (HPGraph.MasterPane.PaneList.IndexOf("AFR/Boost") >= 0)
			{
				Statics.TrimByRPMWindow(ref pointPairList3, num17, num16, ref MinY3, ref MinGraphValue3, ref MinY2, ref MaxY3, ref MinGraphValue2, ref MaxGraphValue3, ref MinGraphValue3);
				Statics.TrimByRPMWindow(ref pointPairList4, num17, num16, ref MinY3, ref MinGraphValue3, ref MinY, ref MaxY4, ref MinGraphValue, ref MaxGraphValue4, ref MinGraphValue3);
				if (settings.GraphSettingsRow.IncludeAFR && pointPairList3.Count > 0)
				{
					LineItem lineItem = HPGraph.MasterPane.PaneList["AFR/Boost"].AddCurve(dataTable.TableName, pointPairList3, color, settings.GraphSettingsRow.ShowDataPoints ? SymbolType.Circle : SymbolType.None);
					lineItem.Line.Width = (float)LineThickness;
					lineItem.Line.IsAntiAlias = true;
					lineItem.Line.IsSmooth = true;
					lineItem.Line.SmoothTension = 0.5f;
					lineItem.Label.IsVisible = false;
					CurveLabel.Add(lineItem, Abbreviations.AirFuelRatio);
				}
				if (settings.GraphSettingsRow.IncludeBoost && pointPairList4.Count > 0)
				{
					string strGraphLabel = "";
					double YAxisMajorStep = 2.0;
					ConvertBoostValues(ref pointPairList4, ref MaxGraphValue4, ref MinGraphValue, ref strGraphLabel, ref YAxisMajorStep);
					HPGraph.MasterPane.PaneList["AFR/Boost"].Y2Axis.Title.Text = strGraphLabel;
					HPGraph.MasterPane.PaneList["AFR/Boost"].Y2Axis.Scale.MajorStep = YAxisMajorStep;
					LineItem lineItem2 = HPGraph.MasterPane.PaneList["AFR/Boost"].AddCurve(dataTable.TableName, pointPairList4, color, settings.GraphSettingsRow.ShowDataPoints ? SymbolType.Circle : SymbolType.None);
					lineItem2.Line.Width = (float)LineThickness;
					lineItem2.Line.IsAntiAlias = true;
					lineItem2.Line.IsSmooth = true;
					lineItem2.Line.SmoothTension = 0.5f;
					lineItem2.Line.DashOn = 1f;
					lineItem2.IsY2Axis = true;
					lineItem2.Line.Style = DashStyle.Dot;
					lineItem2.Label.IsVisible = false;
					CurveLabel.Add(lineItem2, Abbreviations.Boost);
				}
			}
			if (!settings.HideHorsepower)
			{
				List<string> list = new List<string>();
				list.Add((dataTable.TableName.Length > 25) ? dataTable.TableName.Substring(0, 25) : dataTable.TableName);
				list.Add(num7.ToString());
				list.Add(totalWeight.ToString(CultureInfo.CurrentCulture));
				list.Add(num8.ToString(CultureInfo.CurrentCulture));
				list.Add(Environment.NewLine);
				list.Add(Math.Round(MaxY).ToString(CultureInfo.CurrentCulture));
				list.Add(item);
				list.Add(Math.Round(XatMaxY).ToString(CultureInfo.CurrentCulture));
				list.Add(Math.Round(MaxY2).ToString(CultureInfo.CurrentCulture));
				list.Add(item2);
				list.Add(Math.Round(XatMaxY2).ToString(CultureInfo.CurrentCulture));
				list.Add(cRunControl2.SmoothingFactor.ToString());
				string legend_RunLabel = General.Legend_RunLabel;
				object[] args = list.ToArray();
				string text = string.Format(legend_RunLabel, args);
				if (flag)
				{
					list.Clear();
					list.Add(cRunControl2.Barometer.ToString(CultureInfo.CurrentCulture));
					list.Add(settings.GraphSettingsRow.MetricWeightandTemp ? Abbreviations.Bar : Abbreviations.InchesOfMercury);
					list.Add(cRunControl2.AtmosphericTemperture.ToString());
					list.Add(settings.GraphSettingsRow.MetricWeightandTemp ? Abbreviations.Celcius : Abbreviations.Fahrenheit);
					string obj6 = text;
					string legend_RunLabel_SAE = General.Legend_RunLabel_SAE;
					args = list.ToArray();
					text = obj6 + string.Format(legend_RunLabel_SAE, args);
				}
				LineItem lineItem3 = HPGraph.GraphPane.AddCurve(text, pointPairList, color, settings.GraphSettingsRow.ShowDataPoints ? SymbolType.Circle : SymbolType.None);
				lineItem3.Tag = cRunControl2;
				lineItem3.Line.Width = (float)LineThickness;
				lineItem3.Line.IsAntiAlias = true;
				lineItem3.Line.IsSmooth = true;
				lineItem3.Line.SmoothTension = 0.5f;
				lineItem3.IsY2Axis = true;
				cRunControl2.MaxHP = MaxY;
				CurveLabel.Add(lineItem3, GetHPTypeLabel());
				if (settings.GraphSettingsRow.ShowMaximums)
				{
					HPGraph.GraphPane.AddCurve("", new PointPairList
					{
						new PointPair(XatMaxY, MaxY)
					}, color).IsY2Axis = true;
				}
			}
			if (!settings.HideTorque)
			{
				LineItem lineItem4 = HPGraph.GraphPane.AddCurve(Abbreviations.Torque, pointPairList2, color, settings.GraphSettingsRow.ShowDataPoints ? SymbolType.Circle : SymbolType.None);
				lineItem4.Tag = cRunControl2;
				lineItem4.Line.Width = (float)LineThickness;
				lineItem4.Line.Style = DashStyle.Dot;
				lineItem4.Line.IsAntiAlias = true;
				lineItem4.Line.DashOn = 1f;
				lineItem4.Line.IsSmooth = true;
				lineItem4.Line.SmoothTension = 0.5f;
				lineItem4.Label.IsVisible = false;
				CurveLabel.Add(lineItem4, GetTQTypeLabel());
				if (settings.GraphSettingsRow.ShowMaximums)
				{
					HPGraph.GraphPane.AddCurve("", new PointPairList
					{
						new PointPair(XatMaxY2, MaxY2)
					}, color2).IsY2Axis = false;
				}
			}
			num++;
		}
		Analytics.ReportDynoGraph(num);
		if (num3 == num4)
		{
			HPGraph.GraphPane.YAxis.Scale.Max = Statics.RoundUp(Math.Max(MaxGraphValue + 25.0, MaxGraphValue2 + 25.0), 25.0);
			HPGraph.GraphPane.Y2Axis.Scale.Max = HPGraph.MasterPane.PaneList[0].YAxis.Scale.Max;
		}
		else
		{
			HPGraph.GraphPane.YAxis.Scale.Max = Statics.RoundUp(MaxGraphValue2 * 1.05, num3);
			HPGraph.GraphPane.Y2Axis.Scale.Max = Statics.RoundUp(MaxGraphValue * 1.05, num4);
		}
		HPGraph.GraphPane.XAxis.Scale.Max = Statics.RoundUp(MaxX, 1.0);
		HPGraph.GraphPane.XAxis.Scale.Min = Statics.RoundDown(MinX, 1.0);
		HPGraph.MasterPane.PaneList["AFR/Boost"].XAxis.Scale.Max = HPGraph.MasterPane.PaneList[0].XAxis.Scale.Max;
		HPGraph.MasterPane.PaneList["AFR/Boost"].XAxis.Scale.Min = HPGraph.MasterPane.PaneList[0].XAxis.Scale.Min;
		if (HPGraph.MasterPane.PaneList.IndexOf("AFR/Boost") >= 0 && HPGraph.MasterPane.PaneList[1].CurveList.Count > 0)
		{
			double num22 = Statics.RoundDown(MinGraphValue2 - 1.0, 1.0);
			double num23 = Statics.RoundUp(MaxGraphValue3 + 1.0, 1.0);
			if (MaxGraphValue3 >= MinGraphValue2 && settings.GraphSettingsRow.IncludeAFR)
			{
				if (HPGraph.MasterPane.PaneList["AFR/Boost"].YAxis.Scale.Max == 0.0 || HPGraph.MasterPane.PaneList["AFR/Boost"].YAxis.Scale.Max < num23)
				{
					HPGraph.MasterPane.PaneList["AFR/Boost"].YAxis.Scale.Max = num23;
				}
				if (HPGraph.MasterPane.PaneList["AFR/Boost"].YAxis.Scale.Min == 0.0 || HPGraph.MasterPane.PaneList["AFR/Boost"].YAxis.Scale.Min > num22)
				{
					HPGraph.MasterPane.PaneList["AFR/Boost"].YAxis.Scale.Min = Statics.RoundDown(MinGraphValue2 - 1.0, 1.0);
				}
				HPGraph.MasterPane.PaneList["AFR/Boost"].AxisChange();
			}
			else
			{
				HPGraph.MasterPane.PaneList["AFR/Boost"].YAxis.Scale.Max = 1.0;
				HPGraph.MasterPane.PaneList["AFR/Boost"].YAxis.Scale.Min = 0.0;
				HPGraph.MasterPane.PaneList["AFR/Boost"].AxisChange();
			}
			if (Statics.RoundUp(MaxGraphValue4 + 2.0, 2.0) > HPGraph.MasterPane.PaneList["AFR/Boost"].Y2Axis.Scale.Max)
			{
				if (MaxGraphValue4 >= MinGraphValue && settings.GraphSettingsRow.IncludeBoost)
				{
					HPGraph.MasterPane.PaneList["AFR/Boost"].Y2Axis.Scale.Max = Statics.RoundUp(MaxGraphValue4 + 2.0, 2.0);
					HPGraph.MasterPane.PaneList["AFR/Boost"].Y2Axis.Scale.Min = 0.0;
					HPGraph.MasterPane.AxisChange();
				}
				else
				{
					HPGraph.MasterPane.PaneList["AFR/Boost"].Y2Axis.Scale.Max = 1.0;
					HPGraph.MasterPane.PaneList["AFR/Boost"].Y2Axis.Scale.Min = 0.0;
				}
			}
		}
		HPGraph.GraphPane.Legend.IsVisible = true;
		HPGraph.GraphPane.Legend.Position = LegendPos.InsideBotRight;
		HPGraph.GraphPane.Legend.FontSpec.IsBold = true;
		HPGraph.GraphPane.Legend.FontSpec.Size = 14 - CountLoadedRuns() / 3;
		if (settings.GraphSettingsRow.IsBackgroundImageNull())
		{
			foreach (GraphPane pane in HPGraph.MasterPane.PaneList)
			{
				pane.Fill = new Fill(Color.FromArgb(settings.GraphSettingsRow.GraphBackgroundColor));
				pane.Chart.Fill = new Fill(Color.FromArgb(settings.GraphSettingsRow.ChartBackgroundColor));
			}
		}
		if (settings.GraphSettingsRow.ShowDataPoints)
		{
			HPGraph.IsShowPointValues = true;
		}
		else
		{
			HPGraph.IsShowPointValues = false;
		}
		HPGraph.GraphPane.Legend.IsVisible = settings.GraphSettingsRow.ShowLegend;
		HPGraph.AxisChange();
		HPGraph.Refresh();
	}

	private bool CheckForNeededColumns(ref StreamReader sr, ref string TimeColumnName, ref string RPMColumnName, ref string TPSColumnName, ref string AFRColumnName, ref string BOOSTColumnName)
	{
		try
		{
			string[] array = sr.ReadLine().Replace("\"", "").Split(COLUMN_SEPERATORS, StringSplitOptions.None);
			using (DataTable dataTable = new DataTable())
			{
				string[] array2 = array;
				foreach (string text in array2)
				{
					try
					{
						dataTable.Columns.Add(text.Trim(COLUMN_TRIM_CHARS));
					}
					catch
					{
						Console.WriteLine("Duplicate column name encountered. Column name = " + text);
					}
				}
				TimeColumnName = FindColumnName(Timecolumns, dataTable);
				RPMColumnName = FindColumnName(RPMcolumns, dataTable);
				TPSColumnName = FindColumnName(TPScolumns, dataTable);
				AFRColumnName = FindColumnName(AFRcolumns, dataTable);
				BOOSTColumnName = FindColumnName(Boostcolumns, dataTable);
			}
			Console.WriteLine("Time column = " + TimeColumnName);
			Console.WriteLine("RPM column = " + RPMColumnName);
			Console.WriteLine("TPS column = " + TPSColumnName);
			Console.WriteLine("AFR column = " + AFRColumnName);
			Console.WriteLine("BOOST column = " + BOOSTColumnName);
			bool flag = !string.IsNullOrEmpty(TimeColumnName.Trim());
			bool num = !string.IsNullOrEmpty(RPMColumnName.Trim());
			bool flag2 = !string.IsNullOrEmpty(TPSColumnName.Trim());
			return num && flag && flag2;
		}
		catch (Exception ex)
		{
			Statics.Error(new Exception(string.Format(ErrorMessages.E105, ex.Message)));
			return false;
		}
	}

	private string FindLogFileType(string TimeColumnName, string RPMColumnName, string TPSColumnName, string AFRColumnName, string BoostColumnName, ref Color backcolor, ref Color forecolor, string PreDeterminedSoftwareName)
	{
		backcolor = VirtualDyno.Properties.Settings.Default.Color_Dark;
		forecolor = VirtualDyno.Properties.Settings.Default.Color_Light;
		string text = Path.Combine(Statics.baseFilepath, VirtualDyno.Properties.Settings.Default.File_ColumnNames);
		if (File.Exists(text))
		{
			try
			{
				using Columns columns = new Columns();
				columns.ReadXml(text);
				if (PreDeterminedSoftwareName != string.Empty)
				{
					DataRow[] array = columns.Tables[0].Select("SoftwareName = '" + PreDeterminedSoftwareName + "'");
					if (array.Length == 0)
					{
						array = columns.Tables[0].Select("SoftwareName = '" + PreDeterminedSoftwareName + "*'");
					}
					if (array.Length == 0)
					{
						Statics.Error(new Exception(string.Format(ErrorMessages.E123, PreDeterminedSoftwareName)));
						forecolor = Color.White;
						backcolor = Color.Black;
						return "unknown";
					}
					backcolor = Color.FromName(array[0]["BackColor"].ToString());
					forecolor = Color.FromName(array[0]["ForeColor"].ToString());
					return PreDeterminedSoftwareName;
				}
				List<Columns.ColumnsRow> list = new List<Columns.ColumnsRow>();
				foreach (Columns.ColumnsRow row in columns.Tables[0].Rows)
				{
					bool flag = false;
					bool flag2 = false;
					bool flag3 = false;
					string[] array2 = row.RPM.Split(',');
					for (int i = 0; i < array2.Length; i++)
					{
						if (array2[i] == RPMColumnName)
						{
							flag = true;
							break;
						}
					}
					if (!flag)
					{
						continue;
					}
					array2 = row.Time.Split(',');
					for (int i = 0; i < array2.Length; i++)
					{
						if (array2[i] == TimeColumnName)
						{
							flag2 = true;
							break;
						}
					}
					if (!flag2)
					{
						continue;
					}
					array2 = row.TPS.Split(',');
					for (int i = 0; i < array2.Length; i++)
					{
						if (array2[i] == TPSColumnName)
						{
							flag3 = true;
							break;
						}
					}
					if (flag3 && flag && flag2 && flag3)
					{
						list.Add(row);
					}
				}
				if (list.Count > 0)
				{
					Columns.ColumnsRow columnsRow2 = null;
					List<Columns.ColumnsRow> list2 = new List<Columns.ColumnsRow>();
					foreach (Columns.ColumnsRow item in list)
					{
						bool flag4 = false;
						bool flag5 = false;
						string[] array2 = item.AFR.Split(',');
						for (int i = 0; i < array2.Length; i++)
						{
							if (array2[i] == AFRColumnName)
							{
								flag5 = true;
								break;
							}
						}
						array2 = item.Boost.Split(',');
						for (int i = 0; i < array2.Length; i++)
						{
							if (array2[i] == BoostColumnName)
							{
								flag4 = true;
								break;
							}
						}
						if (flag5 && flag4)
						{
							columnsRow2 = item;
						}
						else
						{
							list2.Add(item);
						}
					}
					if (columnsRow2 == null)
					{
						columnsRow2 = list2[0];
					}
					if (columnsRow2 != null && !columnsRow2.IsBackColorNull())
					{
						if (!columnsRow2.BackColor.StartsWith("#"))
						{
							backcolor = Color.FromName(columnsRow2.BackColor);
						}
						else
						{
							backcolor = ColorTranslator.FromHtml(columnsRow2.BackColor);
						}
					}
					if (columnsRow2 != null && !columnsRow2.IsForeColorNull())
					{
						if (!columnsRow2.ForeColor.StartsWith("#"))
						{
							forecolor = Color.FromName(columnsRow2.ForeColor);
						}
						else
						{
							forecolor = ColorTranslator.FromHtml(columnsRow2.ForeColor);
						}
					}
					return columnsRow2.SoftwareName;
				}
			}
			catch (Exception ex)
			{
				Statics.Error(new Exception(string.Format(ErrorMessages.E124, ex.Message)));
			}
		}
		forecolor = Color.White;
		backcolor = Color.Black;
		return "unknown";
	}

	private bool IsFileLoaded(string filename)
	{
		foreach (Control control in leftPanel.Controls)
		{
			if (control is cRunControl && ((cRunControl)control).Filename == filename)
			{
				return true;
			}
		}
		return false;
	}

	private bool CheckIfSupportedFileExtension(string filename)
	{
		string[] sUPPORTED_FILE_EXT = SUPPORTED_FILE_EXT;
		for (int i = 0; i < sUPPORTED_FILE_EXT.Length; i++)
		{
			if (sUPPORTED_FILE_EXT[i].ToLower().Trim().Contains(Path.GetExtension(filename).ToLower().Trim()))
			{
				return true;
			}
		}
		return false;
	}

	private void FindRPMRange(DataTable dt, ref double High, ref double Low)
	{
		bool flag = false;
		try
		{
			int columnIndex = 0;
			int num = 0;
			foreach (DataColumn column in dt.Columns)
			{
				string[] array = RPMcolumns.Split(COLUMN_SEPERATORS, StringSplitOptions.RemoveEmptyEntries);
				foreach (string text in array)
				{
					if (column.ColumnName.ToLower().Trim(COLUMN_TRIM_CHARS) == text.Trim(COLUMN_TRIM_CHARS).ToLower())
					{
						columnIndex = num;
						flag = true;
						break;
					}
				}
				num++;
				if (!flag)
				{
					continue;
				}
				High = 0.0;
				Low = 999999999.0;
				{
					foreach (DataRow row in dt.Rows)
					{
						try
						{
							High = Math.Max(Convert.ToDouble(row[columnIndex].ToString().Trim()), High);
							Low = Math.Min(Convert.ToDouble(row[columnIndex].ToString().Trim()), Low);
						}
						catch (FormatException ex)
						{
							Console.WriteLine("FindRPMRange: " + ex.Message);
						}
					}
					break;
				}
			}
		}
		catch (Exception ex2)
		{
			Statics.Error(new Exception(string.Format(ErrorMessages.E107, ex2.Message)));
		}
	}

	private void SaveSettings()
	{
		if (!Directory.Exists(Statics.baseFilepath))
		{
			Directory.CreateDirectory(Statics.baseFilepath);
		}
		settings.SaveSettings();
		SaveProfiles();
		foreach (Control control in leftPanel.Controls)
		{
			if (control is cRunControl)
			{
				((cRunControl)control).SetOtherColumnsDependentParameters(settings.GraphSettingsRow.IsBackgroundImageNull() ? null : Statics.byteArrayToImage(settings.GraphSettingsRow.BackgroundImage), settings.GraphSettingsRow.BackgroundTransparency, settings.GraphSettingsRow.BackgroundStretch, Assembly.GetExecutingAssembly().GetName().Name, Assembly.GetExecutingAssembly().GetName().Version);
			}
		}
		SetupGraph();
	}

	private void SaveProfiles()
	{
		try
		{
			using CarProfile carProfile = new CarProfile();
			carProfile.Tables["CarProfile"].Clear();
			foreach (ToolStripItem dropDownItem in toolStripMenuItem_Profile.DropDownItems)
			{
				if (dropDownItem is ToolStripMenuItem && dropDownItem.Tag != null)
				{
					DataRow dataRow = (CarProfile.CarProfileRow)((ToolStripMenuItem)dropDownItem).Tag;
					if (dataRow.RowState == DataRowState.Detached)
					{
						carProfile.Tables["CarProfile"].LoadDataRow(dataRow.ItemArray, fAcceptChanges: true);
					}
					else
					{
						carProfile.Tables["CarProfile"].ImportRow(dataRow);
					}
				}
			}
			string fileName = Path.Combine(ProfilePath, VirtualDyno.Properties.Settings.Default.File_Profiles);
			carProfile.WriteXml(fileName);
		}
		catch (Exception ex)
		{
			MessageBox.Show("Failed saving profiles.xml:\n" + ex.Message, "Column Names Warning", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
		}
	}

	private void LoadSettings()
	{
		settings.LoadSettings();
		settings.GraphSettingsRow.ProfilesPath = ProfilePath;
		contextMenuItem_ToggleDataPoints.Checked = settings.GraphSettingsRow.ShowDataPoints;
		toolStripMenuItem_ToggleDataPoints.Checked = settings.GraphSettingsRow.ShowDataPoints;
		contextMenuItem_ToggleLegend.Checked = settings.GraphSettingsRow.ShowLegend;
		toolStripMenuItem_ToggleLegend.Checked = settings.GraphSettingsRow.ShowLegend;
		toolStripMenuItem_ToggleHP.Checked = settings.HideHorsepower;
		toolStripMenuItem_ToggleTQ.Checked = settings.HideTorque;
		foreach (Control control in leftPanel.Controls)
		{
			if (control is cRunControl)
			{
				((cRunControl)control).IsMetric = settings.GraphSettingsRow.MetricWeightandTemp;
			}
		}
		try
		{
			SetSmoothingSelection(settings.GraphSettingsRow.SmoothingFactor);
		}
		catch
		{
		}
		GraphSettings.LayoutRow layoutRow = (GraphSettings.LayoutRow)settings.GraphSettings.Tables["Layout"].Rows[0];
		base.Size = new Size(layoutRow.Width, layoutRow.Height);
		WindowSize = new Point(base.Size);
		if (!layoutRow.IsLeftNull() && !layoutRow.IsTopNull() && !layoutRow.IsMaximized)
		{
			base.Location = new Point(layoutRow.Left, layoutRow.Top);
		}
		else
		{
			base.StartPosition = FormStartPosition.CenterScreen;
		}
		if (!layoutRow.IsIsMaximizedNull())
		{
			base.WindowState = (layoutRow.IsMaximized ? FormWindowState.Maximized : FormWindowState.Normal);
		}
		LoadColumns();
		PopulateProfilesMenu();
		ToolStripMenuItem toolStripMenuItem = new ToolStripMenuItem();
		CarProfile.CarProfileDataTable carProfileDataTable = new CarProfile.CarProfileDataTable();
		toolStripMenuItem.Tag = carProfileDataTable.NewCarProfileRow();
		((CarProfile.CarProfileRow)toolStripMenuItem.Tag).ProfileId = FindProfileId(settings.GraphSettingsRow.DefaultProfile);
		if (((CarProfile.CarProfileRow)toolStripMenuItem.Tag).ProfileId >= 0)
		{
			ProfilesMenuProfile_Click(toolStripMenuItem, new EventArgs());
		}
		ResetGraphControlsLocation();
	}

	private void LoadColumns()
	{
		if (!File.Exists(Path.Combine(Statics.baseFilepath, VirtualDyno.Properties.Settings.Default.File_ColumnNames)))
		{
			return;
		}
		try
		{
			ColumnList = (Columns)settings.Columns.Copy();
			int num = settings.Columns.Tables["Columns"].Rows.IndexOf(settings.Columns.Tables["Columns"].Rows.Find("Custom"));
			if (num >= 0)
			{
				AFRcolumns = ((Columns.ColumnsRow)settings.Columns.Tables["Columns"].Rows[num]).AFR;
				RPMcolumns = ((Columns.ColumnsRow)settings.Columns.Tables["Columns"].Rows[num]).RPM;
				Timecolumns = ((Columns.ColumnsRow)settings.Columns.Tables["Columns"].Rows[num]).Time;
				TPScolumns = ((Columns.ColumnsRow)settings.Columns.Tables["Columns"].Rows[num]).TPS;
				Boostcolumns = ((Columns.ColumnsRow)settings.Columns.Tables["Columns"].Rows[num]).Boost;
			}
			foreach (DataRow row in settings.Columns.Tables["Columns"].Rows)
			{
				if (((Columns.ColumnsRow)row).SoftwareName.Trim().ToLower() == "custom")
				{
					continue;
				}
				Queue<string> queue = new Queue<string>();
				string[] array = ((Columns.ColumnsRow)row).AFR.Split(COLUMN_SEPERATORS);
				foreach (string item in array)
				{
					if (!queue.Contains(item))
					{
						queue.Enqueue(item);
					}
				}
				while (queue.Count > 0)
				{
					if (!string.IsNullOrEmpty(AFRcolumns) && !string.IsNullOrEmpty(queue.Peek()))
					{
						AFRcolumns += ",";
					}
					AFRcolumns += queue.Dequeue();
				}
				array = ((Columns.ColumnsRow)row).RPM.Split(COLUMN_SEPERATORS);
				foreach (string item2 in array)
				{
					if (!queue.Contains(item2))
					{
						queue.Enqueue(item2);
					}
				}
				while (queue.Count > 0)
				{
					if (!string.IsNullOrEmpty(RPMcolumns) && !string.IsNullOrEmpty(queue.Peek()))
					{
						RPMcolumns += ",";
					}
					RPMcolumns += queue.Dequeue();
				}
				array = ((Columns.ColumnsRow)row).Time.Split(COLUMN_SEPERATORS);
				foreach (string item3 in array)
				{
					if (!queue.Contains(item3))
					{
						queue.Enqueue(item3);
					}
				}
				while (queue.Count > 0)
				{
					if (!string.IsNullOrEmpty(Timecolumns) && !string.IsNullOrEmpty(queue.Peek()))
					{
						Timecolumns += ",";
					}
					Timecolumns += queue.Dequeue();
				}
				array = ((Columns.ColumnsRow)row).TPS.Split(COLUMN_SEPERATORS);
				foreach (string item4 in array)
				{
					if (!queue.Contains(item4))
					{
						queue.Enqueue(item4);
					}
				}
				while (queue.Count > 0)
				{
					if (!string.IsNullOrEmpty(TPScolumns) && !string.IsNullOrEmpty(queue.Peek()))
					{
						TPScolumns += ",";
					}
					TPScolumns += queue.Dequeue();
				}
				array = ((Columns.ColumnsRow)row).Boost.Split(COLUMN_SEPERATORS);
				foreach (string item5 in array)
				{
					if (!queue.Contains(item5))
					{
						queue.Enqueue(item5);
					}
				}
				while (queue.Count > 0)
				{
					if (!string.IsNullOrEmpty(Boostcolumns) && !string.IsNullOrEmpty(queue.Peek()))
					{
						Boostcolumns += ",";
					}
					Boostcolumns += queue.Dequeue();
				}
			}
		}
		catch
		{
			MessageBox.Show("Could not load columnnames.xml\nDefault values will be used.", "Columnnames Load Warning", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
		}
	}

	private void SetSmoothingSelection(int i)
	{
		int num = i;
		ddlSmoothingFactor.Text = "Smoothing: " + num;
		settings.GraphSettingsRow.SmoothingFactor = num - VirtualDyno.Properties.Settings.Default.MIN_SMOOTHING;
		for (int j = 0; j < ddlSmoothingFactor.DropDownItems.Count; j++)
		{
			ddlSmoothingFactor.DropDown.Items[j].BackColor = ddlSmoothingFactor.BackColor;
			toolStripMenuItem_Smoothing.DropDownItems[j].BackColor = ddlSmoothingFactor.BackColor;
			((ToolStripMenuItem)ddlSmoothingFactor.DropDownItems[j]).Checked = false;
			((ToolStripMenuItem)toolStripMenuItem_Smoothing.DropDownItems[j]).Checked = false;
		}
		i = ((i > 0) ? (i - VirtualDyno.Properties.Settings.Default.MIN_SMOOTHING) : 0);
		ddlSmoothingFactor.DropDownItems[i].BackColor = leftPanel.BackColor;
		toolStripMenuItem_Smoothing.DropDownItems[i].BackColor = leftPanel.BackColor;
		((ToolStripMenuItem)ddlSmoothingFactor.DropDownItems[i]).Checked = true;
		((ToolStripMenuItem)toolStripMenuItem_Smoothing.DropDownItems[i]).Checked = true;
		foreach (Control control in leftPanel.Controls)
		{
			if (control is cRunControl)
			{
				((cRunControl)control).PauseRefresh = true;
				((cRunControl)control).SmoothingFactor = num;
				((cRunControl)control).PauseRefresh = false;
			}
		}
	}

	private void PopulateSmoothingDropdown()
	{
		ddlSmoothingFactor.DropDownItems.Clear();
		toolStripMenuItem_Smoothing.DropDownItems.Clear();
		for (int i = VirtualDyno.Properties.Settings.Default.MIN_SMOOTHING; i <= VirtualDyno.Properties.Settings.Default.MAX_SMOOTHING; i++)
		{
			ToolStripMenuItem toolStripMenuItem = new ToolStripMenuItem(i.ToString());
			ToolStripMenuItem toolStripMenuItem2 = new ToolStripMenuItem(i.ToString());
			toolStripMenuItem.Click += SmoothingFactor_MenuDropDownClick;
			toolStripMenuItem2.Click += SmoothingFactor_MenuDropDownClick;
			switch (i)
			{
			case 0:
				toolStripMenuItem.ShortcutKeys = Keys.D0 | Keys.Control;
				toolStripMenuItem.ShortcutKeyDisplayString = " ";
				break;
			case 1:
				toolStripMenuItem.ShortcutKeys = Keys.D1 | Keys.Control;
				toolStripMenuItem.ShortcutKeyDisplayString = " ";
				break;
			case 2:
				toolStripMenuItem.ShortcutKeys = Keys.D2 | Keys.Control;
				toolStripMenuItem.ShortcutKeyDisplayString = " ";
				break;
			case 3:
				toolStripMenuItem.ShortcutKeys = Keys.D3 | Keys.Control;
				toolStripMenuItem.ShortcutKeyDisplayString = " ";
				break;
			case 4:
				toolStripMenuItem.ShortcutKeys = Keys.D4 | Keys.Control;
				toolStripMenuItem.ShortcutKeyDisplayString = " ";
				break;
			case 5:
				toolStripMenuItem.ShortcutKeys = Keys.D5 | Keys.Control;
				toolStripMenuItem.ShortcutKeyDisplayString = " ";
				break;
			case 6:
				toolStripMenuItem.ShortcutKeys = Keys.D6 | Keys.Control;
				toolStripMenuItem.ShortcutKeyDisplayString = " ";
				break;
			case 7:
				toolStripMenuItem.ShortcutKeys = Keys.D7 | Keys.Control;
				toolStripMenuItem.ShortcutKeyDisplayString = " ";
				break;
			case 8:
				toolStripMenuItem.ShortcutKeys = Keys.D8 | Keys.Control;
				toolStripMenuItem.ShortcutKeyDisplayString = " ";
				break;
			case 9:
				toolStripMenuItem.ShortcutKeys = Keys.D9 | Keys.Control;
				toolStripMenuItem.ShortcutKeyDisplayString = " ";
				break;
			}
			ddlSmoothingFactor.DropDownItems.Add(toolStripMenuItem);
			toolStripMenuItem_Smoothing.DropDownItems.Add(toolStripMenuItem2);
		}
	}

	private void PopulateProfilesMenu()
	{
		toolStripMenuItem_Profile.DropDownItems.Clear();
		ddActiveProfile.DropDownItems.Clear();
		foreach (Control control3 in leftPanel.Controls)
		{
			if (control3 is cRunControl)
			{
				((cRunControl)control3).ClearProfilesMenu();
			}
		}
		toolStripMenuItem_Profile.DropDownItems.Add("Add Profile", Resources.AddProfile, ProfilesMenuAdd_Click).ImageScaling = ToolStripItemImageScaling.None;
		toolStripMenuItem_Profile.DropDownItems.Add(new ToolStripSeparator());
		if (settings.GraphSettingsRow.IsProfilesPathNull())
		{
			return;
		}
		string text = Path.Combine(settings.GraphSettingsRow.ProfilesPath, VirtualDyno.Properties.Settings.Default.File_Profiles);
		try
		{
			settings.CarProfiles.Clear();
			if (!File.Exists(text))
			{
				return;
			}
			settings.CarProfiles.ReadXml(text);
			DataRow[] array = settings.CarProfiles.Tables["CarProfile"].Select("", "ProfileName asc");
			for (int i = 0; i < array.Length; i++)
			{
				CarProfile.CarProfileRow carProfileRow = (CarProfile.CarProfileRow)array[i];
				ToolStripItem toolStripItem = toolStripMenuItem_Profile.DropDownItems.Add(carProfileRow.ProfileName, null, ProfilesMenuProfile_Click);
				ToolStripItem toolStripItem2 = ((ToolStripMenuItem)toolStripItem).DropDownItems.Add("Edit", Resources.profile_edit, ProfilesMenuEdit_Click);
				ToolStripItem toolStripItem3 = ((ToolStripMenuItem)toolStripItem).DropDownItems.Add("Remove", Resources.profile_remove, ProfilesMenuRemove_Click);
				toolStripItem.Tag = carProfileRow;
				toolStripItem2.Tag = carProfileRow;
				toolStripItem3.Tag = carProfileRow;
				foreach (Control control4 in leftPanel.Controls)
				{
					if (control4 is cRunControl)
					{
						((cRunControl)control4).AddToProfileMenu(carProfileRow);
					}
				}
				ddActiveProfile.DropDownItems.Add(carProfileRow.ProfileName, null, ProfilesMenuProfile_Click).Tag = carProfileRow;
			}
		}
		catch
		{
			if (File.Exists(text))
			{
				MessageBox.Show("Could not load profiles.xml\nDefault values will be used.", "Profiles Load Warning", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
			}
		}
	}

	private List<string> GetTimeFormats()
	{
		return new List<string>
		{
			"HH:mm:ss.f", "HH:mm:ss.ff", "HH:mm:ss.fff", "HH:mm:ss.ffff", "HH:mm:ss.fffff", "HH:mm:ss.ffffff", "HH:mm:ss.fffffff", "HH:mm:ss.ffffffff", "HH:mm:ss.fffffffff", "HH:mm:ss.ffffffffff",
			"mm:ss.f", "mm:ss.ff", "mm:ss.fff", "mm:ss.ffff", "mm:ss.fffff", "mm:ss.ffffff", "mm:ss.fffffff", "mm:ss.ffffffff", "mm:ss.fffffffff", "mm:ss.ffffffffff",
			"HH:mm:ss:f", "HH:mm:ss:ff", "HH:mm:ss:fff", "HH:mm:ss:ffff", "HH:mm:ss:fffff", "HH:mm:ss:ffffff", "HH:mm:ss:fffffff", "HH:mm:ss:ffffffff", "HH:mm:ss:fffffffff", "HH:mm:ss:ffffffffff",
			"mm:ss:f", "mm:ss:ff", "mm:ss:fff", "mm:ss:ffff", "mm:ss:fffff", "mm:ss:ffffff", "mm:ss:fffffff", "mm:ss:ffffffff", "mm:ss:fffffffff", "mm:ss:ffffffffff"
		};
	}

	private int FindEndOfHeader(ref int EndHeaderPosition, ref string tempstring, ref string SoftwareName)
	{
		EndHeaderPosition = 0;
		SoftwareName = string.Empty;
		if (tempstring.Contains("HP Tuners") || tempstring.Contains("[Data]"))
		{
			if (tempstring.Contains("[Data]"))
			{
				EndHeaderPosition = tempstring.IndexOf("[Data]") + 6;
				EndHeaderPosition = tempstring.IndexOf(Environment.NewLine, EndHeaderPosition);
				EndHeaderPosition += Environment.NewLine.Length;
			}
			else if (tempstring.Contains("HP Tuners") && tempstring.Contains("Version: 1.") && tempstring.Contains("[Channel Information]"))
			{
				string text = "[Channel Information]";
				EndHeaderPosition = tempstring.IndexOf(text) + text.Length;
				EndHeaderPosition = tempstring.IndexOf(Environment.NewLine, EndHeaderPosition);
				EndHeaderPosition += Environment.NewLine.Length;
				EndHeaderPosition = tempstring.IndexOf(Environment.NewLine, EndHeaderPosition);
				EndHeaderPosition += Environment.NewLine.Length;
				int num = tempstring.IndexOf(Environment.NewLine, EndHeaderPosition);
				int startIndex = tempstring.IndexOf(Environment.NewLine, num + Environment.NewLine.Length) + Environment.NewLine.Length;
				startIndex = tempstring.IndexOf(Environment.NewLine, startIndex) + Environment.NewLine.Length;
				startIndex = tempstring.IndexOf(Environment.NewLine, startIndex);
				tempstring = tempstring.Remove(num, startIndex - num);
			}
			SoftwareName = "HPTuners";
		}
		else if (string.IsNullOrEmpty(SoftwareName) && tempstring.ToLower().Contains("ectune"))
		{
			EndHeaderPosition = tempstring.ToLower().IndexOf("frame,");
			SoftwareName = "eCtune";
		}
		else if (string.IsNullOrEmpty(SoftwareName) && tempstring.ToLower().Contains("proecu"))
		{
			if (tempstring.Contains("#RaceROM"))
			{
				EndHeaderPosition = tempstring.IndexOf("#RaceROM") + 8;
				EndHeaderPosition = tempstring.IndexOf(Environment.NewLine, EndHeaderPosition) + Environment.NewLine.Length;
			}
			else if (tempstring.Contains("#Ecu RaceROM"))
			{
				EndHeaderPosition = tempstring.IndexOf("#Ecu RaceROM");
				EndHeaderPosition = tempstring.IndexOf(Environment.NewLine, EndHeaderPosition) + Environment.NewLine.Length;
			}
			SoftwareName = "EcuTek";
		}
		else if (string.IsNullOrEmpty(SoftwareName) && tempstring.ToLower().Contains("marker") && tempstring.Contains("VCDS"))
		{
			EndHeaderPosition = tempstring.ToLower().IndexOf("marker") + 6;
			EndHeaderPosition = tempstring.IndexOf(Environment.NewLine, EndHeaderPosition) + Environment.NewLine.Length;
			SoftwareName = "VAGCOM";
		}
		else if ((string.IsNullOrEmpty(SoftwareName) && tempstring.Contains("#VemsTune")) || tempstring.Substring(0, 20).Contains("VEMS"))
		{
			if (tempstring.Contains("#VemsTune"))
			{
				EndHeaderPosition = tempstring.IndexOf("#VemsTune") + 9;
				EndHeaderPosition += Environment.NewLine.Length - 1;
			}
			else if (tempstring.Substring(0, 20).Contains("VEMS"))
			{
				EndHeaderPosition = tempstring.IndexOf(Environment.NewLine) + Environment.NewLine.Length;
			}
			SoftwareName = "VEMS";
		}
		else if (string.IsNullOrEmpty(SoftwareName) && tempstring.Contains("AlcaTek"))
		{
			EndHeaderPosition = tempstring.IndexOf("Time");
			SoftwareName = "AlcaTek";
		}
		else if (string.IsNullOrEmpty(SoftwareName) && tempstring.Contains("Export of") && tempstring.Contains("Number of frames") && tempstring.Contains("Length:"))
		{
			EndHeaderPosition = tempstring.LastIndexOf("frame,");
			SoftwareName = "Hondata";
		}
		else if (string.IsNullOrEmpty(SoftwareName) && tempstring.Contains("Frame Number") && tempstring.Contains("Frame Time (ms)"))
		{
			tempstring.Replace("\"", "");
			EndHeaderPosition = tempstring.LastIndexOf("Frame Number");
			SoftwareName = "ScanXL";
		}
		else if (string.IsNullOrEmpty(SoftwareName) && tempstring.Substring(0, 20).Contains("Session:"))
		{
			List<string> list = new List<string>(tempstring.Split(new string[1] { Environment.NewLine }, StringSplitOptions.None));
			int result = 0;
			for (int i = 0; i < list.Count; i++)
			{
				if (int.TryParse(list[i][0].ToString(), out result))
				{
					list.RemoveAt(i - 1);
					break;
				}
			}
			string text2 = string.Join(Environment.NewLine, list.ToArray());
			EndHeaderPosition = text2.IndexOf("time");
			SoftwareName = "Innovate";
		}
		else if (string.IsNullOrEmpty(SoftwareName) && tempstring.Substring(0, 15).Contains("DESC:"))
		{
			EndHeaderPosition = tempstring.IndexOf(Environment.NewLine) + Environment.NewLine.Length;
			SoftwareName = "Nistune";
		}
		else if (string.IsNullOrEmpty(SoftwareName) && tempstring.Contains("Firmware,Interface"))
		{
			EndHeaderPosition = tempstring.IndexOf("timestamp");
			SoftwareName = "JuiceBox";
		}
		else if (string.IsNullOrEmpty(SoftwareName) && tempstring.Contains("Procede Data Log"))
		{
			EndHeaderPosition = tempstring.IndexOf("time");
			if (EndHeaderPosition == -1)
			{
				EndHeaderPosition = tempstring.IndexOf("Time");
			}
			SoftwareName = "Procede";
		}
		else if (string.IsNullOrEmpty(SoftwareName) && (tempstring.Substring(0, 15).Contains("MS3") || tempstring.Substring(0, 15).Contains("MS2") || tempstring.Substring(0, 15).Contains("MSII") || tempstring.Substring(0, 15).Contains("MS1") || tempstring.Substring(0, 15).Contains("MSnS-extra") || tempstring.Substring(0, 15).Contains("MS/Extra")))
		{
			EndHeaderPosition = tempstring.IndexOf("Time");
			SoftwareName = "MegaSquirt";
		}
		else if (string.IsNullOrEmpty(SoftwareName) && tempstring.Contains("NismoTronic"))
		{
			EndHeaderPosition = tempstring.IndexOf("Time");
			SoftwareName = "NismoTronic";
		}
		else if (string.IsNullOrEmpty(SoftwareName) && tempstring.Contains("VersaTuner"))
		{
			EndHeaderPosition = tempstring.IndexOf("Time");
			SoftwareName = "VersaTuner";
		}
		else if (string.IsNullOrEmpty(SoftwareName) && tempstring.Contains("MoTeC"))
		{
			EndHeaderPosition = tempstring.LastIndexOf("\"Time\"");
			SoftwareName = "MoTeC";
		}
		else if (string.IsNullOrEmpty(SoftwareName) && tempstring.Substring(0, 20).Contains("Name") && tempstring.Contains("Section Time"))
		{
			EndHeaderPosition = tempstring.LastIndexOf("Section Time");
			if (tempstring.Contains("File Time") && tempstring.LastIndexOf("File Time") < EndHeaderPosition)
			{
				EndHeaderPosition = tempstring.LastIndexOf("File Time");
			}
			SoftwareName = "ViPEC";
		}
		else if (string.IsNullOrEmpty(SoftwareName) && tempstring.Contains(Environment.NewLine + "LS2" + Environment.NewLine))
		{
			EndHeaderPosition = tempstring.LastIndexOf("-----") + 5 + Environment.NewLine.Length;
			SoftwareName = "LS2Edit";
		}
		else if (string.IsNullOrEmpty(SoftwareName) && tempstring.Contains("Haltech Data Log"))
		{
			EndHeaderPosition = Environment.NewLine.Length + tempstring.IndexOf(Environment.NewLine + "time");
			SoftwareName = "Halwin";
		}
		else if (string.IsNullOrEmpty(SoftwareName) && tempstring.Contains("%DataLog%") && (tempstring.Contains("Software : ECU Manager") || tempstring.Contains("Software : Haltech ESP") || tempstring.Contains("Software : Haltech NSP")))
		{
			int num2 = tempstring.IndexOf(Environment.NewLine, tempstring.IndexOf("DownloadDateTime :")) + Environment.NewLine.Length;
			int num3 = tempstring.IndexOf(Environment.NewLine, tempstring.IndexOf(Environment.NewLine + "Log :") + Environment.NewLine.Length) + Environment.NewLine.Length;
			string[] array = tempstring.Substring(num2, num3 - num2).Split(new string[1] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
			string text3 = string.Empty;
			string[] array2 = array;
			foreach (string text4 in array2)
			{
				if (text4.StartsWith("Channel :"))
				{
					string text5 = text4.Split(':')[1].Trim().TrimEnd(',');
					text3 = text3 + "," + text5;
				}
			}
			tempstring = "Haltech_Time(Inserted by VirtualDyno)" + text3 + Environment.NewLine + tempstring.Substring(num3);
			SoftwareName = "Haltech";
		}
		else if (string.IsNullOrEmpty(SoftwareName) && tempstring.Contains("ScannerPro Engine"))
		{
			List<string> list2 = new List<string>(tempstring.Split(new string[1] { Environment.NewLine }, StringSplitOptions.None));
			int result2 = 0;
			for (int k = 0; k < list2.Count; k++)
			{
				if (int.TryParse(list2[k][0].ToString(), out result2))
				{
					list2.RemoveAt(k - 1);
					break;
				}
			}
			string text6 = string.Join(Environment.NewLine, list2.ToArray());
			EndHeaderPosition = text6.IndexOf(",Time");
			SoftwareName = "Tuner Pro";
		}
		else if (string.IsNullOrEmpty(SoftwareName) && tempstring.Contains("Time(S)"))
		{
			EndHeaderPosition = tempstring.IndexOf(Environment.NewLine) + Environment.NewLine.Length;
			SoftwareName = "FC-Datalogit";
		}
		else if (string.IsNullOrEmpty(SoftwareName) && tempstring.Contains("TrackAddict"))
		{
			EndHeaderPosition = tempstring.IndexOf("Time");
			SoftwareName = "TrackAddict HD";
		}
		else if (string.IsNullOrEmpty(SoftwareName) && tempstring.StartsWith("ME221"))
		{
			EndHeaderPosition = tempstring.IndexOf("Time,");
			SoftwareName = "ME221";
		}
		else if (string.IsNullOrEmpty(SoftwareName) && tempstring.StartsWith("\"Interval\"|\"ms\"|"))
		{
			EndHeaderPosition = 0;
			SoftwareName = "RaceCapture";
		}
		else if (string.IsNullOrEmpty(SoftwareName) && tempstring.Contains("DDText2") && tempstring.Contains("DDTable2"))
		{
			EndHeaderPosition = tempstring.IndexOf("T13D");
			SoftwareName = "Digital Dyno";
		}
		return EndHeaderPosition;
	}

	private string FindColumnName(string ColumnType, DataTable table, string SoftwareName)
	{
		string text = string.Empty;
		try
		{
			DataRow[] array = ColumnList.Tables[0].Select("SoftwareName = 'Custom'");
			if (array.Length != 0)
			{
				text = FindColumnName(array[0][ColumnType].ToString(), table);
			}
			DataRow[] array2 = ColumnList.Tables[0].Select("SoftwareName = '" + SoftwareName + "'");
			if (text == string.Empty && array2.Length != 0)
			{
				text = FindColumnName(array2[0][ColumnType].ToString(), table);
			}
		}
		catch
		{
		}
		return text;
	}

	private string FindColumnName(string ColumnsLookedFor, DataTable table)
	{
		string[] array = ColumnsLookedFor.Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries);
		foreach (DataColumn column in table.Columns)
		{
			string[] array2 = array;
			foreach (string text in array2)
			{
				Decoder decoder = Encoding.UTF8.GetDecoder();
				byte[] bytes = Encoding.UTF8.GetBytes(column.ColumnName);
				char[] array3 = new char[bytes.Length];
				decoder.GetChars(bytes, 0, bytes.Length, array3, 0);
				string text2 = "";
				char[] array4 = array3;
				foreach (char c in array4)
				{
					text2 += c;
				}
				string text3 = text2.Trim(COLUMN_TRIM_CHARS).Replace("\"", "");
				if (text3.IndexOf("|") > 0)
				{
					text3 = text3.Substring(0, text3.IndexOf("|"));
				}
				string strB = text.Trim(COLUMN_TRIM_CHARS);
				if (string.Compare(text3, strB, StringComparison.OrdinalIgnoreCase) == 0)
				{
					return column.ColumnName.Trim(COLUMN_TRIM_CHARS);
				}
			}
		}
		return string.Empty;
	}

	private int FindProfileId(string ProfileName)
	{
		foreach (CarProfile.CarProfileRow row in settings.CarProfiles.Tables[0].Rows)
		{
			if (row.ProfileName.Equals(ProfileName))
			{
				return row.ProfileId;
			}
		}
		return -1;
	}

	private string GetTQCurveLabel()
	{
		return string.Format(General.Graph_Axis_Label_Torque, GetTQTypeLabel());
	}

	private string GetHPCurveLabel()
	{
		return string.Format(General.Graph_Axis_Label_Power, GetHPTypeLabel());
	}

	private string GetHPTypeLabel()
	{
		if (settings.GraphSettingsRow.IsHpTypeNull() || settings.GraphSettingsRow.HpType == 0)
		{
			return Abbreviations.WheelHorsepower;
		}
		if (settings.GraphSettingsRow.HpType == 2)
		{
			return Abbreviations.Kilowatt;
		}
		if (settings.GraphSettingsRow.HpType == 1)
		{
			return Abbreviations.PS;
		}
		return "";
	}

	private string GetTQTypeLabel()
	{
		if (settings.GraphSettingsRow.IsTqTypeNull() || settings.GraphSettingsRow.TqType == 0)
		{
			return Abbreviations.PoundFeet;
		}
		if (settings.GraphSettingsRow.TqType == 2)
		{
			return Abbreviations.NewtonMeter;
		}
		if (settings.GraphSettingsRow.TqType == 1)
		{
			return Abbreviations.KilogramForceMeter;
		}
		return "";
	}

	private CarProfile.CarProfileRow GetSelectedProfileRow()
	{
		if (SelectedProfileId != 0)
		{
			return GetProfileRowById(SelectedProfileId, ProfilePath);
		}
		return null;
	}

	public static CarProfile.CarProfileRow GetProfileRowById(int RowId, string profilePath)
	{
		if (RowId == 0)
		{
			return null;
		}
		string text = Path.Combine(profilePath, VirtualDyno.Properties.Settings.Default.File_Profiles);
		try
		{
			using CarProfile carProfile = new CarProfile();
			if (File.Exists(text))
			{
				carProfile.ReadXml(text);
			}
			return (CarProfile.CarProfileRow)carProfile.Tables[0].Select("ProfileId = " + RowId)[0];
		}
		catch (Exception ex)
		{
			Statics.Error(new Exception(string.Format(ErrorMessages.E110, ex.Message)));
		}
		return null;
	}

	private void CheckForScrollBarsAndResize()
	{
		splitContainerHPGraph_LeftPanelProfiles.SplitterDistance = ((leftPanel.Controls.Count > 1) ? (leftPanel.Controls[1].Margin.All * 2 + leftPanel.Controls[1].Width + (leftPanel.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0)) : 300);
		leftPanel.Invalidate();
	}

	private void ColorizeVersionControl(DialogResult dr)
	{
		getUpdateToolStripMenuItem.Visible = false;
		getUpdateToolStripMenuItem.Click += null;
		Color backColor;
		switch (dr)
		{
		case DialogResult.Yes:
			backColor = Color.YellowGreen;
			break;
		case DialogResult.No:
			backColor = Color.Tomato;
			getUpdateToolStripMenuItem.Visible = true;
			getUpdateToolStripMenuItem.Click += GetUpdateToolStripMenuItem_Click;
			break;
		default:
			backColor = Color.Yellow;
			break;
		}
		ddVersion.BackColor = backColor;
		ddVersion.Invalidate();
	}

	private void GetUpdateToolStripMenuItem_Click(object sender, EventArgs e)
	{
		Process.Start(VirtualDyno.Properties.Settings.Default.SETTINGS_URL_BASE);
	}

	private void ShowRunErrorMessage(cRunControl rc, string strMessage)
	{
		if (rc.ErrorControl == null)
		{
			RunErrorControl runErrorControl = new RunErrorControl(ref rc, splitContainerLeftPanel_OpenedRuns.Panel1.Height + splitContainerLeftPanel_OpenedRuns.SplitterWidth + rc.Padding.Top, yesNo: true);
			runErrorControl.MessageText = strMessage;
			HPGraph.Controls.Add(runErrorControl);
			runErrorControl.OnClose += RunErrorMessage_OnClose;
			runErrorControl.OnYesNo += RunErrorMessage_OnYesNo;
		}
	}

	private int GetTimeConverter(ref cRunControl rc)
	{
		int result = 1;
		switch (rc.LogType)
		{
		case "Cobb Access Port":
			if (rc.RunData.Rows.Count > 2 && double.Parse(rc.RunData.Rows[2][rc.TimeColumnName].ToString()) > 100.0)
			{
				result = 1000;
			}
			break;
		case "ECM Link":
		case "AEM / DSM Link":
			if (rc.TimeColumnName == "Timestamp (us)")
			{
				result = 1000000;
			}
			break;
		case "RomRaider":
			if (!rc.RunData.Rows[1][rc.TimeColumnName].ToString().Contains(":") || !rc.RunData.Rows[1][rc.TimeColumnName].ToString().Contains("."))
			{
				result = 1000;
			}
			break;
		case "JuiceBox":
			result = 10;
			break;
		case "Eurodyne Maestro":
			result = 10000;
			break;
		case "VEMS":
			if (!rc.RunData.Rows[1][rc.TimeColumnName].ToString().Contains("."))
			{
				result = 1000;
			}
			break;
		case "EcuTek":
			if (!rc.RunData.Rows[0][rc.TimeColumnName].ToString().Contains("."))
			{
				result = 1000;
			}
			break;
		case "EFI Live":
			if (rc.TimeColumnName == "Timestamp ms")
			{
				result = 1000;
			}
			break;
		case "Hondata":
			result = ((!rc.TimeColumnName.Contains("ms")) ? ((!rc.TimeColumnName.Contains("us")) ? 1 : 1000000) : 1000);
			break;
		case "Procede":
			result = 1000;
			if (rc.TimeColumnName.Contains("[s]"))
			{
				result = 1;
			}
			break;
		case "GReddy EManage":
		case "Hand Held Halo":
		case "MazdaEdit":
		case "Doctronic":
		case "Hydra EMS":
		case "ScanXL":
		case "UVScan":
		case "Forscan":
		case "DiabloSport":
		case "RaceCapture":
		case "ECU Explorer":
		case "UpRev Cipher":
		case "BtSsm":
		case "Enduring Solutions":
		case "DiabloTrinity":
			result = 1000;
			break;
		}
		return result;
	}

	private int CountLoadedRuns()
	{
		int num = 0;
		foreach (Control control in leftPanel.Controls)
		{
			if (control is cRunControl)
			{
				num++;
			}
		}
		return num;
	}

	private void ConvertBoostValues(ref PointPairList points, ref double max, ref double min, ref string strGraphLabel, ref double YAxisMajorStep)
	{
		min = 99999.0;
		max = -99999.0;
		double num = 1.0;
		foreach (PointPair point in points)
		{
			if (point.Y < min)
			{
				min = point.Y;
			}
			else if (point.Y > max)
			{
				max = point.Y;
			}
		}
		if (max < BAR_CEILING_VALUE)
		{
			strGraphLabel = "Boost (BAR)";
			if (settings.GraphSettingsRow.ConvertBoost.ToLowerInvariant() == "millibar")
			{
				num = 1000.0;
			}
			if (settings.GraphSettingsRow.ConvertBoost.ToLowerInvariant() == "psi")
			{
				num = 14.5037738;
			}
			if (settings.GraphSettingsRow.ConvertBoost.ToLowerInvariant() == "donotconvert")
			{
				num = 1.0;
			}
		}
		else if (max > MILLIBAR_FLOOR_VALUE)
		{
			strGraphLabel = "Boost (MBar)";
			if (settings.GraphSettingsRow.ConvertBoost.ToLowerInvariant() == "psi")
			{
				num = 0.001;
			}
			if (settings.GraphSettingsRow.ConvertBoost.ToLowerInvariant() == "bar")
			{
				num = 0.0145037738;
			}
			if (settings.GraphSettingsRow.ConvertBoost.ToLowerInvariant() == "donotconvert")
			{
				num = 1.0;
			}
		}
		else if (max > MILLIBAR_FLOOR_VALUE && min > BAR_CEILING_VALUE)
		{
			strGraphLabel = "Boost (kPa)";
			num = 1.0;
		}
		else
		{
			strGraphLabel = "Boost (PSI)";
			if (settings.GraphSettingsRow.ConvertBoost.ToLowerInvariant() == "bar")
			{
				num = 0.06894757280343135;
			}
			if (settings.GraphSettingsRow.ConvertBoost.ToLowerInvariant() == "millibar")
			{
				num = 68.94757280343134;
			}
			if (settings.GraphSettingsRow.ConvertBoost.ToLowerInvariant() == "donotconvert")
			{
				num = 1.0;
			}
		}
		if (settings.GraphSettingsRow.ConvertBoost.ToLowerInvariant() == "psi")
		{
			strGraphLabel = "Boost (PSI)";
			YAxisMajorStep = 2.0;
		}
		else if (settings.GraphSettingsRow.ConvertBoost.ToLowerInvariant() == "millibar")
		{
			strGraphLabel = "Boost (MBar)";
			YAxisMajorStep = 100.0;
		}
		else if (settings.GraphSettingsRow.ConvertBoost.ToLowerInvariant() == "bar")
		{
			strGraphLabel = "Boost (BAR)";
			YAxisMajorStep = 0.1;
		}
		if (num == 1.0)
		{
			return;
		}
		min *= num;
		max *= num;
		foreach (PointPair point2 in points)
		{
			point2.Y *= num;
		}
	}

	private void frmVirtualDyno_ResizeEnd(object sender, EventArgs e)
	{
		settings.LayoutRow.IsMaximized = base.WindowState == FormWindowState.Maximized;
		if (base.WindowState != FormWindowState.Maximized && base.WindowState != FormWindowState.Minimized)
		{
			settings.WindowLocation = base.Location;
			settings.WindowSize = new Point(base.Size);
		}
		ResetGraphControlsLocation();
	}

	private void frmVirtualDyno_Move(object sender, EventArgs e)
	{
		if (settings.LayoutRow.IsMaximized && base.WindowState != FormWindowState.Maximized)
		{
			base.Location = settings.WindowLocation;
		}
		settings.LayoutRow.IsMaximized = base.WindowState == FormWindowState.Maximized;
		if (base.WindowState != FormWindowState.Maximized && base.WindowState != FormWindowState.Minimized)
		{
			settings.WindowLocation = base.Location;
		}
	}

	private void leftPanel_ControlRemoved(object sender, ControlEventArgs e)
	{
		for (int i = 0; i < leftPanel.Controls.Count; i++)
		{
			Control control = leftPanel.Controls[i];
			if (control is cRunControl)
			{
				((cRunControl)control).LineColor = GraphingColors[i];
			}
		}
		RefreshGraph();
	}

	private void leftPanel_Resize(object sender, EventArgs e)
	{
	}

	private void frmVirtualDyno_FormClosing(object sender, FormClosingEventArgs e)
	{
		if (CountLoadedRuns() > 0)
		{
			PopupConfirmClose popupConfirmClose = new PopupConfirmClose(CountLoadedRuns());
			popupConfirmClose.ShowEverytime = settings.GraphSettingsRow.ShowConfirmCloseMessage;
			if (settings.GraphSettingsRow.ShowConfirmCloseMessage && popupConfirmClose.ShowDialog() != DialogResult.Yes)
			{
				e.Cancel = true;
			}
			settings.GraphSettingsRow.ShowConfirmCloseMessage = popupConfirmClose.ShowEverytime;
		}
		if (!e.Cancel)
		{
			SaveSettings();
		}
	}

	private void Runs_DragDrop(object sender, DragEventArgs e)
	{
		if (CountLoadedRuns() < VirtualDyno.Properties.Settings.Default.MAX_LOADED_RUNS)
		{
			LoadSelectedRuns((string[])e.Data.GetData(DataFormats.FileDrop));
		}
		else
		{
			Statics.Error(new Exception(ErrorMessages.E108));
		}
	}

	private void Runs_DragEnter(object sender, DragEventArgs e)
	{
		if (e.Data.GetDataPresent(DataFormats.FileDrop, autoConvert: false))
		{
			e.Effect = DragDropEffects.All;
		}
	}

	private void frmHPCalc_Load(object sender, EventArgs e)
	{
		RefreshGraph();
	}

	private void HPGraph_ZoomEvent(ZedGraphControl sender, ZoomState oldState, ZoomState newState)
	{
		btnCancelZoom.Enabled = false;
		foreach (GraphPane pane in HPGraph.MasterPane.PaneList)
		{
			if (pane.IsZoomed)
			{
				btnCancelZoom.Enabled = true;
				break;
			}
		}
	}

	private string HPGraph_PointValueEvent(ZedGraphControl sender, GraphPane pane, CurveItem curve, int iPt)
	{
		return "";
	}

	private void lblHideUpdateNotification_MouseEnter(object sender, EventArgs e)
	{
		Cursor = Cursors.Hand;
	}

	private void lblHideUpdateNotification_MouseLeave(object sender, EventArgs e)
	{
		Cursor = Cursors.Default;
	}

	private void HPGraph_MouseMove(object sender, MouseEventArgs e)
	{
		lblPointData.Text = "";
		CurveItem nearestCurve = null;
		GraphPane pane = null;
		object nearestObj = null;
		int index = 0;
		PointF mousePt = new PointF(e.X, e.Y);
		HPGraph.MasterPane.FindNearestPaneObject(mousePt, HPGraph.CreateGraphics(), out pane, out nearestObj, out index);
		if (pane != null && pane.FindNearestPoint(mousePt, out nearestCurve, out index) && CurveLabel.ContainsKey(nearestCurve))
		{
			pShowValues.BringToFront();
			pShowValues.Show();
			pShowValues.Location = new Point((int)mousePt.X, (int)mousePt.Y);
			lblPointData.Text = "RPM = " + nearestCurve.Points[index].X + Environment.NewLine + CurveLabel[nearestCurve] + " = " + nearestCurve.Points[index].Y;
			pShowValues.Size = new Size(lblPointData.Size.Width + 16, lblPointData.Size.Height + 9);
			pShowValues.Refresh();
		}
		else
		{
			pShowValues.Hide();
			lblStatus.Text = "";
		}
		pShowValues.Refresh();
		HPGraph.Refresh();
	}

	private void rc_Refreshed(object sender, EventArgs e)
	{
		RefreshGraph();
	}

	private void rc_Renamed(object sender, OnRunRenamedEventArgs e)
	{
		RefreshGraph();
	}

	private void rc_RunClosed(object sender, OnRunClosedEventArgs e)
	{
		RefreshGraph();
	}

	private void rc_GraphOtherPressed(object sender)
	{
		((cRunControl)sender).SetOtherColumnsDependentParameters(settings.GraphSettingsRow.IsBackgroundImageNull() ? null : Statics.byteArrayToImage(settings.GraphSettingsRow.BackgroundImage), settings.GraphSettingsRow.BackgroundTransparency, settings.GraphSettingsRow.BackgroundStretch, Assembly.GetExecutingAssembly().GetName().Name, Assembly.GetExecutingAssembly().GetName().Version);
	}

	private void ap_Submitted(object sender, OnAddProfileSubmitEventArgs e)
	{
		ToolStripMenuItem toolStripMenuItem = null;
		foreach (ToolStripItem dropDownItem in toolStripMenuItem_Profile.DropDownItems)
		{
			if (dropDownItem is ToolStripMenuItem && dropDownItem.Tag != null && ((CarProfile.CarProfileRow)dropDownItem.Tag).ProfileId == e.Row.ProfileId)
			{
				toolStripMenuItem = (ToolStripMenuItem)dropDownItem;
			}
		}
		if (toolStripMenuItem == null)
		{
			toolStripMenuItem_Profile.DropDownItems.Add(e.Row["ProfileName"].ToString().Trim(), null, ProfilesMenuProfile_Click).Tag = e.Row;
		}
		else
		{
			toolStripMenuItem.Text = e.Row.ProfileName;
			toolStripMenuItem.Tag = e.Row;
		}
		SaveSettings();
		PopulateProfilesMenu();
		RefreshGraph();
	}

	private void RunErrorMessage_OnYesNo(object sender, OnYesNoEventArgs e)
	{
		if (e.YesNo)
		{
			e.RunControl.CloseRun();
		}
		else
		{
			RunErrorMessage_OnClose(sender, new EventArgs());
		}
	}

	private void RunErrorMessage_OnClose(object sender, EventArgs e)
	{
		((RunErrorControl)sender).Run.ErrorControl = null;
		HPGraph.Controls.Remove((RunErrorControl)sender);
	}

	private void btnCloseAdvertisement_MouseEnter(object sender, EventArgs e)
	{
		Cursor = Cursors.Hand;
	}

	private void btnCloseAdvertisement_MouseLeave(object sender, EventArgs e)
	{
		Cursor = Cursors.Default;
	}

	private void tAdvertisementCloseButton_Tick(object sender, EventArgs e)
	{
		btnCloseAdvertisement.Visible = true;
		tAdvertisementCloseButton.Enabled = false;
		tAdvertisementCloseButton.Stop();
	}

	private void toolStripMenuItem_LoadRuns_Click(object sender, EventArgs e)
	{
		try
		{
			using OpenFileDialog openFileDialog = new OpenFileDialog();
			openFileDialog.Filter = General.FileDialogFilter_OpenFile;
			openFileDialog.Multiselect = true;
			if (openFileDialog.ShowDialog() == DialogResult.OK)
			{
				LoadSelectedRuns(openFileDialog.FileNames);
			}
		}
		catch (Exception ex)
		{
			Statics.Error(new Exception(string.Format(ErrorMessages.E106, ex.Message)));
		}
	}

	private void toolStripMenuItem_Exit_Click(object sender, EventArgs e)
	{
		Close();
	}

	private void toolStripMenuItem_About_Click(object sender, EventArgs e)
	{
		using About about = new About(Statics.baseFilepath);
		about.ShowDialog();
	}

	private void toolStripMenuItem_PageSetup_Click(object sender, EventArgs e)
	{
		using PageSetupDialog pageSetupDialog = new PageSetupDialog();
		pageSetupDialog.Document = HPGraph.PrintDocument;
		pageSetupDialog.PageSettings = new PageSettings();
		pageSetupDialog.PrinterSettings = new PrinterSettings();
		if (pageSetupDialog.ShowDialog() == DialogResult.OK)
		{
			HPGraph.PrintDocument.PrinterSettings = pageSetupDialog.PrinterSettings;
			HPGraph.PrintDocument.DefaultPageSettings = pageSetupDialog.PageSettings;
		}
	}

	private void toolStripMenuItem_Print_Click(object sender, EventArgs e)
	{
		using PrintDialog printDialog = new PrintDialog();
		printDialog.Document = HPGraph.PrintDocument;
		printDialog.UseEXDialog = true;
		if (printDialog.ShowDialog() == DialogResult.OK)
		{
			HPGraph.PrintDocument.Print();
			PopupMessage popupMessage = new PopupMessage("Graph printed to " + HPGraph.PrintDocument.PrinterSettings.PrinterName, 3);
			popupMessage.Location = new Point(base.Width / 2 - popupMessage.Width / 2 + base.Location.X, base.Height / 2 - popupMessage.Height / 2 + base.Location.Y);
			popupMessage.Show(this);
		}
	}

	private void toolStripMenuItem_PrintPreview_Click(object sender, EventArgs e)
	{
		using PrintPreviewDialog printPreviewDialog = new PrintPreviewDialog();
		printPreviewDialog.Document = HPGraph.PrintDocument;
		printPreviewDialog.SetBounds(0, 0, 800, 600);
		printPreviewDialog.UseAntiAlias = true;
		printPreviewDialog.ShowDialog();
	}

	private void toolStripMenuItem_Options_Click(object sender, EventArgs e)
	{
		using SettingsWindow settingsWindow = new SettingsWindow(ref settings);
		settingsWindow.SaveClicked += SettingsWindow_SaveClicked;
		settingsWindow.Location = new Point(HPGraph.Width / 2 - settingsWindow.Width / 2, HPGraph.Height / 2 - settingsWindow.Height / 2);
		if (settingsWindow.ShowDialog() == DialogResult.OK)
		{
			LoadSettings();
			SetSmoothingSelection(settings.GraphSettingsRow.SmoothingFactor);
			SetupGraph();
			RefreshGraph();
		}
	}

	private void SettingsWindow_SaveClicked(object sender, EventArgs e)
	{
		SaveSettings();
	}

	private void toolStripMenuItem_ReleaseNotes_Click(object sender, EventArgs e)
	{
		new ReleaseNotes().ShowDialog();
	}

	private void toolStripMenuItem_Donate_Click(object sender, EventArgs e)
	{
		Process.Start(VirtualDyno.Properties.Settings.Default.DONATE_URL);
	}

	private void toolStripMenuItem_CarEditor_Click(object sender, EventArgs e)
	{
		CarEditor carEditor = new CarEditor();
		carEditor.StartPosition = FormStartPosition.CenterParent;
		carEditor.ShowDialog(this);
	}

	private void toolStripMenuItem_OpenDataFolder_Click(object sender, EventArgs e)
	{
		Process.Start(Statics.baseFilepath);
	}

	private void toolStripMenuItem_ShowDataPoints_Click(object sender, EventArgs e)
	{
		settings.GraphSettingsRow.ShowDataPoints = !settings.GraphSettingsRow.ShowDataPoints;
		contextMenuItem_ToggleDataPoints.Checked = settings.GraphSettingsRow.ShowDataPoints;
		toolStripMenuItem_ToggleDataPoints.Checked = settings.GraphSettingsRow.ShowDataPoints;
		foreach (GraphPane pane in HPGraph.MasterPane.PaneList)
		{
			foreach (LineItem curve in pane.CurveList)
			{
				curve.Symbol = new Symbol(settings.GraphSettingsRow.ShowDataPoints ? SymbolType.Circle : SymbolType.None, curve.Color);
			}
		}
		HPGraph.IsShowPointValues = settings.GraphSettingsRow.ShowDataPoints;
		HPGraph.Refresh();
	}

	private void toolStripMenuItem_ToggleLegend_Click(object sender, EventArgs e)
	{
		settings.GraphSettingsRow.ShowLegend = !settings.GraphSettingsRow.ShowLegend;
		contextMenuItem_ToggleLegend.Checked = settings.GraphSettingsRow.ShowLegend;
		toolStripMenuItem_ToggleLegend.Checked = settings.GraphSettingsRow.ShowLegend;
		HPGraph.GraphPane.Legend.IsVisible = settings.GraphSettingsRow.ShowLegend;
		HPGraph.Refresh();
	}

	private void toolStripMenuItem_ToggleHP_Click(object sender, EventArgs e)
	{
		settings.HideHorsepower = !settings.HideHorsepower;
		toolStripMenuItem_ToggleHP.Checked = settings.HideHorsepower;
		RefreshGraph();
	}

	private void toolStripMenuItem_ToggleTQ_Click(object sender, EventArgs e)
	{
		settings.HideTorque = !settings.HideTorque;
		toolStripMenuItem_ToggleTQ.Checked = settings.HideTorque;
		RefreshGraph();
	}

	private void lblCredits_Click(object sender, EventArgs e)
	{
		Process.Start("http://www.bradbarnhill.com");
	}

	private void btnGraphSelected_Click(object sender, EventArgs e)
	{
		RefreshGraph();
	}

	private void btnGraphToClipboard_Click(object sender, EventArgs e)
	{
		using (Bitmap bitmap = new Bitmap(HPGraph.ClientRectangle.Width, HPGraph.ClientRectangle.Height))
		{
			HPGraph.DrawToBitmap(bitmap, HPGraph.ClientRectangle);
			Clipboard.SetImage(bitmap);
		}
		PopupMessage popupMessage = new PopupMessage("Graph copied to clipboard.", 2);
		popupMessage.Location = new Point(base.Width / 2 - popupMessage.Width / 2 + base.Location.X, base.Height / 2 - popupMessage.Height / 2 + base.Location.Y);
		popupMessage.Show(this);
	}

	private void btnGraphToFile_Click(object sender, EventArgs e)
	{
		try
		{
			using Bitmap bitmap = new Bitmap(HPGraph.ClientRectangle.Width, HPGraph.ClientRectangle.Height);
			HPGraph.DrawToBitmap(bitmap, HPGraph.ClientRectangle);
			using SaveFileDialog saveFileDialog = new SaveFileDialog();
			saveFileDialog.Filter = General.FileDialogFilter_SaveImage;
			if (DialogResult.OK == saveFileDialog.ShowDialog())
			{
				ImageFormat format = ImageFormat.Jpeg;
				switch (Path.GetExtension(saveFileDialog.FileName).ToLower())
				{
				case ".jpg":
					format = ImageFormat.Jpeg;
					break;
				case ".gif":
					format = ImageFormat.Gif;
					break;
				case ".png":
					format = ImageFormat.Png;
					break;
				case ".bmp":
					format = ImageFormat.Bmp;
					break;
				case ".tif":
					format = ImageFormat.Tiff;
					break;
				}
				bitmap.Save(saveFileDialog.FileName, format);
				PopupMessage popupMessage = new PopupMessage(General.SaveImage_SuccessMessage, 2);
				popupMessage.Location = new Point(base.Width / 2 - popupMessage.Width / 2 + base.Location.X, base.Height / 2 - popupMessage.Height / 2 + base.Location.Y);
				popupMessage.Show(this);
			}
		}
		catch (Exception ex)
		{
			Statics.Error(new Exception(string.Format(ErrorMessages.E113, Environment.NewLine, ex.Message)));
		}
	}

	private void btnCancelZoom_Click(object sender, EventArgs e)
	{
		foreach (GraphPane pane in HPGraph.MasterPane.PaneList)
		{
			HPGraph.ZoomOutAll(pane);
		}
		btnCancelZoom.Enabled = false;
	}

	private void btnGraphBackColor_Click(object sender, EventArgs e)
	{
		using ColorDialog colorDialog = new ColorDialog();
		colorDialog.AnyColor = true;
		if (colorDialog.ShowDialog() == DialogResult.OK)
		{
			((Button)sender).BackColor = colorDialog.Color;
		}
	}

	private void btnCloseAdvertisement_Click(object sender, EventArgs e)
	{
		panelAdvertisement.Hide();
	}

	private void ddActiveProfile_ButtonClick(object sender, EventArgs e)
	{
		ddActiveProfile.ShowDropDown();
	}

	private void SmoothingFactor_MenuDropDownClick(object sender, EventArgs e)
	{
		SetSmoothingSelection(Convert.ToInt32(((ToolStripMenuItem)sender).Text.Trim()));
		RefreshGraph();
	}

	private void ProfilesMenuProfile_Click(object sender, EventArgs e)
	{
		int num = ((CarProfile.CarProfileRow)((ToolStripMenuItem)sender).Tag).ProfileId;
		if (((ToolStripMenuItem)sender).Checked)
		{
			num = 0;
		}
		SelectedProfileId = num;
		toolStripMenuItem_Profile.HideDropDown();
		foreach (ToolStripItem dropDownItem in toolStripMenuItem_Profile.DropDownItems)
		{
			if (dropDownItem is ToolStripMenuItem)
			{
				ToolStripMenuItem toolStripMenuItem = dropDownItem as ToolStripMenuItem;
				if (toolStripMenuItem.Tag != null && ((CarProfile.CarProfileRow)toolStripMenuItem.Tag).ProfileId == num && !toolStripMenuItem.Checked)
				{
					toolStripMenuItem.Checked = true;
					toolStripMenuItem.BackColor = VirtualDyno.Properties.Settings.Default.Color_Medium;
				}
				else
				{
					toolStripMenuItem.Checked = false;
					toolStripMenuItem.BackColor = Color.Transparent;
				}
			}
		}
		ddActiveProfile.Text = General.Profile_NoProfileSelected;
		foreach (ToolStripItem dropDownItem2 in ddActiveProfile.DropDownItems)
		{
			if (dropDownItem2 is ToolStripMenuItem)
			{
				ToolStripMenuItem toolStripMenuItem2 = dropDownItem2 as ToolStripMenuItem;
				if (((CarProfile.CarProfileRow)toolStripMenuItem2.Tag).ProfileId == num && !toolStripMenuItem2.Checked)
				{
					toolStripMenuItem2.Checked = true;
					toolStripMenuItem2.BackColor = VirtualDyno.Properties.Settings.Default.Color_Medium;
					ddActiveProfile.Text = GetProfileRowById(num, ProfilePath).ProfileName;
				}
				else
				{
					toolStripMenuItem2.Checked = false;
					toolStripMenuItem2.BackColor = Color.Transparent;
				}
			}
		}
		foreach (Control control in leftPanel.Controls)
		{
			if (control is cRunControl)
			{
				((cRunControl)control).PauseRefresh = true;
				((cRunControl)control).SetFromProfile(GetProfileRowById(num, ProfilePath));
				((cRunControl)control).PauseRefresh = false;
			}
		}
		RefreshGraph();
	}

	private void ProfilesMenuEdit_Click(object sender, EventArgs e)
	{
		int profileId = ((CarProfile.CarProfileRow)((ToolStripMenuItem)sender).Tag).ProfileId;
		using AddProfile addProfile = new AddProfile(Statics.baseFilepath);
		addProfile.IsMetric = settings.GraphSettingsRow.MetricWeightandTemp;
		addProfile.PopulateFromProfileRow(GetProfileRowById(profileId, ProfilePath));
		addProfile.Submitted += ap_Submitted;
		addProfile.ShowDialog();
	}

	private void ProfilesMenuRemove_Click(object sender, EventArgs e)
	{
		int profileId = ((CarProfile.CarProfileRow)((ToolStripMenuItem)sender).Tag).ProfileId;
		if (MessageBox.Show(string.Format(General.RemoveProfile_Text, GetProfileRowById(profileId, ProfilePath).ProfileName, Environment.NewLine), General.RemoveProfile_Caption, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
		{
			toolStripMenuItem_Profile.DropDownItems.Remove(((ToolStripMenuItem)sender).OwnerItem);
		}
	}

	private void ProfilesMenuAdd_Click(object sender, EventArgs e)
	{
		string text = Path.Combine(Statics.baseFilepath, VirtualDyno.Properties.Settings.Default.File_Profiles);
		try
		{
			using CarProfile carProfile = new CarProfile();
			if (File.Exists(text))
			{
				carProfile.ReadXml(text);
			}
			int num = 1;
			while (true)
			{
				bool flag = false;
				foreach (CarProfile.CarProfileRow row in carProfile.Tables["CarProfile"].Rows)
				{
					if (row.ProfileId == num)
					{
						flag = true;
					}
				}
				if (!flag)
				{
					break;
				}
				num++;
			}
			using AddProfile addProfile = new AddProfile(Statics.baseFilepath);
			addProfile.Id = num;
			addProfile.Submitted += ap_Submitted;
			addProfile.IsMetric = settings.GraphSettingsRow.MetricWeightandTemp;
			addProfile.ShowDialog();
		}
		catch (Exception ex)
		{
			Statics.Error(new Exception(string.Format(ErrorMessages.E103, ex.Message)));
		}
		SaveSettings();
		PopulateProfilesMenu();
	}

	private void TestColors()
	{
		SetupGraph();
		HPGraph.GraphPane.YAxis.Scale.Max = VirtualDyno.Properties.Settings.Default.MAX_LOADED_RUNS + 1;
		HPGraph.GraphPane.XAxis.Scale.Max = 20.0;
		HPGraph.GraphPane.XAxis.Scale.Min = 0.0;
		for (int i = 0; i < VirtualDyno.Properties.Settings.Default.MAX_LOADED_RUNS; i++)
		{
			PointPairList pointPairList = new PointPairList();
			pointPairList.Add(1.0, i + 1);
			pointPairList.Add(6.0, (double)i + 0.5);
			pointPairList.Add(12.0, i + 1);
			pointPairList.Add(18.0, (double)i + 0.5);
			LineItem lineItem = HPGraph.MasterPane.PaneList[0].AddCurve("Color " + i, pointPairList, GraphingColors[VirtualDyno.Properties.Settings.Default.MAX_LOADED_RUNS - i - 1]);
			lineItem.Line.Width = 2.5f;
			lineItem.Line.Style = DashStyle.Solid;
			lineItem.Line.IsAntiAlias = true;
			lineItem.Line.DashOn = 1f;
			lineItem.Line.IsSmooth = true;
			lineItem.Line.SmoothTension = 0.5f;
			lineItem.Label.IsVisible = false;
		}
		HPGraph.AxisChange();
		HPGraph.Refresh();
	}

	private void leftPanel_Layout(object sender, LayoutEventArgs e)
	{
		CheckForScrollBarsAndResize();
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
		System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VirtualDyno.frmVirtualDyno));
		this.splitContainerHPGraph_LeftPanelProfiles = new System.Windows.Forms.SplitContainer();
		this.splitContainer2 = new System.Windows.Forms.SplitContainer();
		this.splitContainerLeftPanel_OpenedRuns = new System.Windows.Forms.SplitContainer();
		this.MainMenuTop = new System.Windows.Forms.MenuStrip();
		this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.toolStripMenuItem_LoadRuns = new System.Windows.Forms.ToolStripMenuItem();
		this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
		this.toolStripMenuItem_Options = new System.Windows.Forms.ToolStripMenuItem();
		this.toolStripMenuItem_CarEditor = new System.Windows.Forms.ToolStripMenuItem();
		this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
		this.toolStripMenuItem_Exit = new System.Windows.Forms.ToolStripMenuItem();
		this.toolStripMenuItem_Profile = new System.Windows.Forms.ToolStripMenuItem();
		this.toolStripMenuItem_Help = new System.Windows.Forms.ToolStripMenuItem();
		this.toolStripMenuItem_ReleaseNotes = new System.Windows.Forms.ToolStripMenuItem();
		this.toolStripMenuItem_OpenDataFolder = new System.Windows.Forms.ToolStripMenuItem();
		this.toolStripSeparator10 = new System.Windows.Forms.ToolStripSeparator();
		this.toolStripMenuItem_About = new System.Windows.Forms.ToolStripMenuItem();
		this.toolStripMenuItem_Donate = new System.Windows.Forms.ToolStripMenuItem();
		this.lblOpenedRunsTitle = new System.Windows.Forms.Label();
		this.leftPanel = new System.Windows.Forms.FlowLayoutPanel();
		this.CopyrightStrip = new System.Windows.Forms.StatusStrip();
		this.lblCredits = new System.Windows.Forms.ToolStripStatusLabel();
		this.lblClosestPointData = new System.Windows.Forms.Label();
		this.splitContainer4 = new System.Windows.Forms.SplitContainer();
		this.pShowValues = new VirtualDyno.RoundedPanel();
		this.lblPointData = new System.Windows.Forms.Label();
		this.HPGraphToolstrip = new System.Windows.Forms.ToolStrip();
		this.btnCancelZoom = new System.Windows.Forms.ToolStripButton();
		this.toolStripDropDownButton2 = new System.Windows.Forms.ToolStripDropDownButton();
		this.toolStripMenuItem_ToggleLegend = new System.Windows.Forms.ToolStripMenuItem();
		this.toolStripMenuItem_ToggleDataPoints = new System.Windows.Forms.ToolStripMenuItem();
		this.toolStripMenuItem_ToggleHP = new System.Windows.Forms.ToolStripMenuItem();
		this.toolStripMenuItem_ToggleTQ = new System.Windows.Forms.ToolStripMenuItem();
		this.toolStripSeparator6 = new System.Windows.Forms.ToolStripSeparator();
		this.btnGraphToClipboard = new System.Windows.Forms.ToolStripButton();
		this.btnGraphToFile = new System.Windows.Forms.ToolStripButton();
		this.toolStripDropDownButton1 = new System.Windows.Forms.ToolStripDropDownButton();
		this.toolStripMenuItem_PageSetup = new System.Windows.Forms.ToolStripMenuItem();
		this.toolStripMenuItem_Print = new System.Windows.Forms.ToolStripMenuItem();
		this.toolStripMenuItem_PrintPreview = new System.Windows.Forms.ToolStripMenuItem();
		this.toolStripSeparator7 = new System.Windows.Forms.ToolStripSeparator();
		this.ddlSmoothingFactor = new System.Windows.Forms.ToolStripDropDownButton();
		this.HPGraph = new ZedGraph.ZedGraphControl();
		this.contextMenuStrip_Graph = new System.Windows.Forms.ContextMenuStrip(this.components);
		this.contextMenuItem_ToggleLegend = new System.Windows.Forms.ToolStripMenuItem();
		this.contextMenuItem_ToggleDataPoints = new System.Windows.Forms.ToolStripMenuItem();
		this.toolStripMenuItem_Smoothing = new System.Windows.Forms.ToolStripMenuItem();
		this.toolStripSeparator9 = new System.Windows.Forms.ToolStripSeparator();
		this.toolStripMenuItem_GraphToClipboard = new System.Windows.Forms.ToolStripMenuItem();
		this.toolStripMenuItem_GraphToFile = new System.Windows.Forms.ToolStripMenuItem();
		this.printToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
		this.toolStripMenuItem_PageSetup_Context = new System.Windows.Forms.ToolStripMenuItem();
		this.toolStripMenuItem_Print_Context = new System.Windows.Forms.ToolStripMenuItem();
		this.toolStripMenuItem_PrintPreview_Context = new System.Windows.Forms.ToolStripMenuItem();
		this.statusStrip1 = new System.Windows.Forms.StatusStrip();
		this.lblStatus = new System.Windows.Forms.ToolStripStatusLabel();
		this.pbStatus = new System.Windows.Forms.ToolStripProgressBar();
		this.ddActiveProfile = new System.Windows.Forms.ToolStripSplitButton();
		this.ddVersion = new System.Windows.Forms.ToolStripDropDownButton();
		this.releaseNotesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.getUpdateToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.panelAdvertisement = new System.Windows.Forms.Panel();
		this.btnCloseAdvertisement = new System.Windows.Forms.PictureBox();
		this.pictureAdvertiseBottom = new System.Windows.Forms.PictureBox();
		this.toolStripSeparator8 = new System.Windows.Forms.ToolStripSeparator();
		this.toolStripSeparator4 = new System.Windows.Forms.ToolStripSeparator();
		this.toolStripSeparator5 = new System.Windows.Forms.ToolStripSeparator();
		this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
		this.AddProfile = new System.Windows.Forms.ToolStripMenuItem();
		this.loadRunsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.optionsToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
		this.tAdvertisementCloseButton = new System.Windows.Forms.Timer(this.components);
		((System.ComponentModel.ISupportInitialize)this.splitContainerHPGraph_LeftPanelProfiles).BeginInit();
		this.splitContainerHPGraph_LeftPanelProfiles.Panel1.SuspendLayout();
		this.splitContainerHPGraph_LeftPanelProfiles.Panel2.SuspendLayout();
		this.splitContainerHPGraph_LeftPanelProfiles.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.splitContainer2).BeginInit();
		this.splitContainer2.Panel1.SuspendLayout();
		this.splitContainer2.Panel2.SuspendLayout();
		this.splitContainer2.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.splitContainerLeftPanel_OpenedRuns).BeginInit();
		this.splitContainerLeftPanel_OpenedRuns.Panel1.SuspendLayout();
		this.splitContainerLeftPanel_OpenedRuns.Panel2.SuspendLayout();
		this.splitContainerLeftPanel_OpenedRuns.SuspendLayout();
		this.MainMenuTop.SuspendLayout();
		this.CopyrightStrip.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.splitContainer4).BeginInit();
		this.splitContainer4.Panel1.SuspendLayout();
		this.splitContainer4.Panel2.SuspendLayout();
		this.splitContainer4.SuspendLayout();
		this.pShowValues.SuspendLayout();
		this.HPGraphToolstrip.SuspendLayout();
		this.contextMenuStrip_Graph.SuspendLayout();
		this.statusStrip1.SuspendLayout();
		this.panelAdvertisement.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.btnCloseAdvertisement).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.pictureAdvertiseBottom).BeginInit();
		base.SuspendLayout();
		this.splitContainerHPGraph_LeftPanelProfiles.BackColor = System.Drawing.SystemColors.Control;
		this.splitContainerHPGraph_LeftPanelProfiles.Dock = System.Windows.Forms.DockStyle.Fill;
		this.splitContainerHPGraph_LeftPanelProfiles.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
		this.splitContainerHPGraph_LeftPanelProfiles.IsSplitterFixed = true;
		this.splitContainerHPGraph_LeftPanelProfiles.Location = new System.Drawing.Point(0, 0);
		this.splitContainerHPGraph_LeftPanelProfiles.Name = "splitContainerHPGraph_LeftPanelProfiles";
		this.splitContainerHPGraph_LeftPanelProfiles.Panel1.Controls.Add(this.splitContainer2);
		this.splitContainerHPGraph_LeftPanelProfiles.Panel1MinSize = 290;
		this.splitContainerHPGraph_LeftPanelProfiles.Panel2.Controls.Add(this.lblClosestPointData);
		this.splitContainerHPGraph_LeftPanelProfiles.Panel2.Controls.Add(this.splitContainer4);
		this.splitContainerHPGraph_LeftPanelProfiles.Size = new System.Drawing.Size(957, 692);
		this.splitContainerHPGraph_LeftPanelProfiles.SplitterDistance = 290;
		this.splitContainerHPGraph_LeftPanelProfiles.SplitterWidth = 1;
		this.splitContainerHPGraph_LeftPanelProfiles.TabIndex = 28;
		this.splitContainer2.BackColor = System.Drawing.Color.White;
		this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
		this.splitContainer2.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
		this.splitContainer2.IsSplitterFixed = true;
		this.splitContainer2.Location = new System.Drawing.Point(0, 0);
		this.splitContainer2.Name = "splitContainer2";
		this.splitContainer2.Orientation = System.Windows.Forms.Orientation.Horizontal;
		this.splitContainer2.Panel1.Controls.Add(this.splitContainerLeftPanel_OpenedRuns);
		this.splitContainer2.Panel2.Controls.Add(this.CopyrightStrip);
		this.splitContainer2.Panel2MinSize = 12;
		this.splitContainer2.Size = new System.Drawing.Size(290, 692);
		this.splitContainer2.SplitterDistance = 666;
		this.splitContainer2.SplitterWidth = 1;
		this.splitContainer2.TabIndex = 1;
		this.splitContainerLeftPanel_OpenedRuns.BackColor = System.Drawing.Color.FromArgb(0, 131, 215);
		this.splitContainerLeftPanel_OpenedRuns.Dock = System.Windows.Forms.DockStyle.Fill;
		this.splitContainerLeftPanel_OpenedRuns.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
		this.splitContainerLeftPanel_OpenedRuns.IsSplitterFixed = true;
		this.splitContainerLeftPanel_OpenedRuns.Location = new System.Drawing.Point(0, 0);
		this.splitContainerLeftPanel_OpenedRuns.Name = "splitContainerLeftPanel_OpenedRuns";
		this.splitContainerLeftPanel_OpenedRuns.Orientation = System.Windows.Forms.Orientation.Horizontal;
		this.splitContainerLeftPanel_OpenedRuns.Panel1.BackColor = System.Drawing.Color.FromArgb(0, 131, 215);
		this.splitContainerLeftPanel_OpenedRuns.Panel1.Controls.Add(this.MainMenuTop);
		this.splitContainerLeftPanel_OpenedRuns.Panel1.Controls.Add(this.lblOpenedRunsTitle);
		this.splitContainerLeftPanel_OpenedRuns.Panel1MinSize = 22;
		this.splitContainerLeftPanel_OpenedRuns.Panel2.Controls.Add(this.leftPanel);
		this.splitContainerLeftPanel_OpenedRuns.Panel2MinSize = 22;
		this.splitContainerLeftPanel_OpenedRuns.Size = new System.Drawing.Size(290, 666);
		this.splitContainerLeftPanel_OpenedRuns.SplitterWidth = 1;
		this.splitContainerLeftPanel_OpenedRuns.TabIndex = 1;
		this.splitContainerLeftPanel_OpenedRuns.TabStop = false;
		this.MainMenuTop.BackColor = System.Drawing.Color.White;
		this.MainMenuTop.ImageScalingSize = new System.Drawing.Size(24, 24);
		this.MainMenuTop.Items.AddRange(new System.Windows.Forms.ToolStripItem[4] { this.fileToolStripMenuItem, this.toolStripMenuItem_Profile, this.toolStripMenuItem_Help, this.toolStripMenuItem_Donate });
		this.MainMenuTop.Location = new System.Drawing.Point(0, 0);
		this.MainMenuTop.Name = "MainMenuTop";
		this.MainMenuTop.Padding = new System.Windows.Forms.Padding(4, 1, 0, 1);
		this.MainMenuTop.Size = new System.Drawing.Size(290, 24);
		this.MainMenuTop.TabIndex = 0;
		this.MainMenuTop.Text = "menuStrip1";
		this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[6] { this.toolStripMenuItem_LoadRuns, this.toolStripSeparator2, this.toolStripMenuItem_Options, this.toolStripMenuItem_CarEditor, this.toolStripSeparator1, this.toolStripMenuItem_Exit });
		this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
		this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 22);
		this.fileToolStripMenuItem.Text = "&File";
		this.toolStripMenuItem_LoadRuns.Image = (System.Drawing.Image)resources.GetObject("toolStripMenuItem_LoadRuns.Image");
		this.toolStripMenuItem_LoadRuns.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
		this.toolStripMenuItem_LoadRuns.ImageTransparentColor = System.Drawing.Color.Black;
		this.toolStripMenuItem_LoadRuns.Name = "toolStripMenuItem_LoadRuns";
		this.toolStripMenuItem_LoadRuns.ShortcutKeys = System.Windows.Forms.Keys.L | System.Windows.Forms.Keys.Control;
		this.toolStripMenuItem_LoadRuns.Size = new System.Drawing.Size(169, 22);
		this.toolStripMenuItem_LoadRuns.Text = "&Load Runs";
		this.toolStripMenuItem_LoadRuns.Click += new System.EventHandler(toolStripMenuItem_LoadRuns_Click);
		this.toolStripSeparator2.Name = "toolStripSeparator2";
		this.toolStripSeparator2.Size = new System.Drawing.Size(166, 6);
		this.toolStripMenuItem_Options.Image = (System.Drawing.Image)resources.GetObject("toolStripMenuItem_Options.Image");
		this.toolStripMenuItem_Options.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
		this.toolStripMenuItem_Options.ImageTransparentColor = System.Drawing.Color.Black;
		this.toolStripMenuItem_Options.Name = "toolStripMenuItem_Options";
		this.toolStripMenuItem_Options.ShortcutKeys = System.Windows.Forms.Keys.O | System.Windows.Forms.Keys.Control;
		this.toolStripMenuItem_Options.Size = new System.Drawing.Size(169, 22);
		this.toolStripMenuItem_Options.Text = "&Options";
		this.toolStripMenuItem_Options.Click += new System.EventHandler(toolStripMenuItem_Options_Click);
		this.toolStripMenuItem_CarEditor.Image = (System.Drawing.Image)resources.GetObject("toolStripMenuItem_CarEditor.Image");
		this.toolStripMenuItem_CarEditor.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
		this.toolStripMenuItem_CarEditor.Name = "toolStripMenuItem_CarEditor";
		this.toolStripMenuItem_CarEditor.ShortcutKeys = System.Windows.Forms.Keys.E | System.Windows.Forms.Keys.Control;
		this.toolStripMenuItem_CarEditor.Size = new System.Drawing.Size(169, 22);
		this.toolStripMenuItem_CarEditor.Text = "Car &Editor";
		this.toolStripMenuItem_CarEditor.Click += new System.EventHandler(toolStripMenuItem_CarEditor_Click);
		this.toolStripSeparator1.Name = "toolStripSeparator1";
		this.toolStripSeparator1.Size = new System.Drawing.Size(166, 6);
		this.toolStripMenuItem_Exit.Image = (System.Drawing.Image)resources.GetObject("toolStripMenuItem_Exit.Image");
		this.toolStripMenuItem_Exit.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
		this.toolStripMenuItem_Exit.ImageTransparentColor = System.Drawing.Color.Black;
		this.toolStripMenuItem_Exit.Name = "toolStripMenuItem_Exit";
		this.toolStripMenuItem_Exit.Size = new System.Drawing.Size(169, 22);
		this.toolStripMenuItem_Exit.Text = "E&xit";
		this.toolStripMenuItem_Exit.Click += new System.EventHandler(toolStripMenuItem_Exit_Click);
		this.toolStripMenuItem_Profile.Name = "toolStripMenuItem_Profile";
		this.toolStripMenuItem_Profile.Size = new System.Drawing.Size(58, 22);
		this.toolStripMenuItem_Profile.Text = "&Profiles";
		this.toolStripMenuItem_Help.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[4] { this.toolStripMenuItem_ReleaseNotes, this.toolStripMenuItem_OpenDataFolder, this.toolStripSeparator10, this.toolStripMenuItem_About });
		this.toolStripMenuItem_Help.Name = "toolStripMenuItem_Help";
		this.toolStripMenuItem_Help.Size = new System.Drawing.Size(44, 22);
		this.toolStripMenuItem_Help.Text = "&Help";
		this.toolStripMenuItem_ReleaseNotes.Image = (System.Drawing.Image)resources.GetObject("toolStripMenuItem_ReleaseNotes.Image");
		this.toolStripMenuItem_ReleaseNotes.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
		this.toolStripMenuItem_ReleaseNotes.ImageTransparentColor = System.Drawing.Color.Magenta;
		this.toolStripMenuItem_ReleaseNotes.Name = "toolStripMenuItem_ReleaseNotes";
		this.toolStripMenuItem_ReleaseNotes.Size = new System.Drawing.Size(166, 22);
		this.toolStripMenuItem_ReleaseNotes.Text = "&Release Notes";
		this.toolStripMenuItem_ReleaseNotes.Click += new System.EventHandler(toolStripMenuItem_ReleaseNotes_Click);
		this.toolStripMenuItem_OpenDataFolder.Image = VirtualDyno.Properties.Resources._00023;
		this.toolStripMenuItem_OpenDataFolder.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
		this.toolStripMenuItem_OpenDataFolder.ImageTransparentColor = System.Drawing.Color.Black;
		this.toolStripMenuItem_OpenDataFolder.Name = "toolStripMenuItem_OpenDataFolder";
		this.toolStripMenuItem_OpenDataFolder.Size = new System.Drawing.Size(166, 22);
		this.toolStripMenuItem_OpenDataFolder.Text = "&Open Data Folder";
		this.toolStripMenuItem_OpenDataFolder.Click += new System.EventHandler(toolStripMenuItem_OpenDataFolder_Click);
		this.toolStripSeparator10.Name = "toolStripSeparator10";
		this.toolStripSeparator10.Size = new System.Drawing.Size(163, 6);
		this.toolStripMenuItem_About.Image = VirtualDyno.Properties.Resources.help;
		this.toolStripMenuItem_About.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
		this.toolStripMenuItem_About.ImageTransparentColor = System.Drawing.Color.Magenta;
		this.toolStripMenuItem_About.Name = "toolStripMenuItem_About";
		this.toolStripMenuItem_About.Size = new System.Drawing.Size(166, 22);
		this.toolStripMenuItem_About.Text = "&About";
		this.toolStripMenuItem_About.Click += new System.EventHandler(toolStripMenuItem_About_Click);
		this.toolStripMenuItem_Donate.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
		this.toolStripMenuItem_Donate.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
		this.toolStripMenuItem_Donate.Image = (System.Drawing.Image)resources.GetObject("toolStripMenuItem_Donate.Image");
		this.toolStripMenuItem_Donate.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
		this.toolStripMenuItem_Donate.ImageTransparentColor = System.Drawing.Color.White;
		this.toolStripMenuItem_Donate.Name = "toolStripMenuItem_Donate";
		this.toolStripMenuItem_Donate.Size = new System.Drawing.Size(69, 22);
		this.toolStripMenuItem_Donate.Text = "toolStripMenuItem1";
		this.toolStripMenuItem_Donate.ToolTipText = "Support Virtual Dyno thru Paypal";
		this.toolStripMenuItem_Donate.Click += new System.EventHandler(toolStripMenuItem_Donate_Click);
		this.lblOpenedRunsTitle.BackColor = System.Drawing.Color.FromArgb(0, 131, 215);
		this.lblOpenedRunsTitle.Dock = System.Windows.Forms.DockStyle.Bottom;
		this.lblOpenedRunsTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 9f, System.Drawing.FontStyle.Bold);
		this.lblOpenedRunsTitle.ForeColor = System.Drawing.Color.AliceBlue;
		this.lblOpenedRunsTitle.Location = new System.Drawing.Point(0, 25);
		this.lblOpenedRunsTitle.Margin = new System.Windows.Forms.Padding(3, 5, 3, 0);
		this.lblOpenedRunsTitle.Name = "lblOpenedRunsTitle";
		this.lblOpenedRunsTitle.Size = new System.Drawing.Size(290, 25);
		this.lblOpenedRunsTitle.TabIndex = 23;
		this.lblOpenedRunsTitle.Text = "Opened Runs";
		this.lblOpenedRunsTitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.leftPanel.AutoScroll = true;
		this.leftPanel.BackColor = System.Drawing.Color.FromArgb(194, 224, 255);
		this.leftPanel.Dock = System.Windows.Forms.DockStyle.Fill;
		this.leftPanel.Location = new System.Drawing.Point(0, 0);
		this.leftPanel.Name = "leftPanel";
		this.leftPanel.Size = new System.Drawing.Size(290, 615);
		this.leftPanel.TabIndex = 1;
		this.leftPanel.ControlAdded += new System.Windows.Forms.ControlEventHandler(leftPanel_ControlRemoved);
		this.leftPanel.ControlRemoved += new System.Windows.Forms.ControlEventHandler(leftPanel_ControlRemoved);
		this.leftPanel.Layout += new System.Windows.Forms.LayoutEventHandler(leftPanel_Layout);
		this.leftPanel.Resize += new System.EventHandler(leftPanel_Resize);
		this.CopyrightStrip.BackColor = System.Drawing.Color.White;
		this.CopyrightStrip.ImageScalingSize = new System.Drawing.Size(24, 24);
		this.CopyrightStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[1] { this.lblCredits });
		this.CopyrightStrip.Location = new System.Drawing.Point(0, -3);
		this.CopyrightStrip.Name = "CopyrightStrip";
		this.CopyrightStrip.Padding = new System.Windows.Forms.Padding(1, 0, 15, 0);
		this.CopyrightStrip.Size = new System.Drawing.Size(290, 28);
		this.CopyrightStrip.SizingGrip = false;
		this.CopyrightStrip.TabIndex = 2;
		this.CopyrightStrip.Text = "statusStrip1";
		this.lblCredits.AutoSize = false;
		this.lblCredits.BackColor = System.Drawing.SystemColors.Control;
		this.lblCredits.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
		this.lblCredits.Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
		this.lblCredits.ImageTransparentColor = System.Drawing.Color.Black;
		this.lblCredits.IsLink = true;
		this.lblCredits.LinkBehavior = System.Windows.Forms.LinkBehavior.HoverUnderline;
		this.lblCredits.LinkColor = System.Drawing.Color.FromArgb(0, 131, 215);
		this.lblCredits.Name = "lblCredits";
		this.lblCredits.Size = new System.Drawing.Size(274, 23);
		this.lblCredits.Spring = true;
		this.lblCredits.Text = "© Brad Barnhill 2024";
		this.lblCredits.Click += new System.EventHandler(lblCredits_Click);
		this.lblClosestPointData.AutoSize = true;
		this.lblClosestPointData.BackColor = System.Drawing.Color.Maroon;
		this.lblClosestPointData.Location = new System.Drawing.Point(3, 4);
		this.lblClosestPointData.Name = "lblClosestPointData";
		this.lblClosestPointData.Size = new System.Drawing.Size(0, 13);
		this.lblClosestPointData.TabIndex = 19;
		this.splitContainer4.BackColor = System.Drawing.Color.White;
		this.splitContainer4.Dock = System.Windows.Forms.DockStyle.Fill;
		this.splitContainer4.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
		this.splitContainer4.IsSplitterFixed = true;
		this.splitContainer4.Location = new System.Drawing.Point(0, 0);
		this.splitContainer4.Name = "splitContainer4";
		this.splitContainer4.Orientation = System.Windows.Forms.Orientation.Horizontal;
		this.splitContainer4.Panel1.BackColor = System.Drawing.Color.AliceBlue;
		this.splitContainer4.Panel1.Controls.Add(this.pShowValues);
		this.splitContainer4.Panel1.Controls.Add(this.HPGraphToolstrip);
		this.splitContainer4.Panel1.Controls.Add(this.HPGraph);
		this.splitContainer4.Panel2.Controls.Add(this.statusStrip1);
		this.splitContainer4.Panel2MinSize = 22;
		this.splitContainer4.Size = new System.Drawing.Size(666, 692);
		this.splitContainer4.SplitterDistance = 666;
		this.splitContainer4.SplitterWidth = 1;
		this.splitContainer4.TabIndex = 21;
		this.pShowValues.BackColor = System.Drawing.Color.FromArgb(194, 224, 255);
		this.pShowValues.BorderRadius = 8;
		this.pShowValues.Controls.Add(this.lblPointData);
		this.pShowValues.Location = new System.Drawing.Point(584, 624);
		this.pShowValues.Name = "pShowValues";
		this.pShowValues.Size = new System.Drawing.Size(84, 42);
		this.pShowValues.TabIndex = 22;
		this.pShowValues.Text = "Values";
		this.pShowValues.Visible = false;
		this.lblPointData.AutoSize = true;
		this.lblPointData.Location = new System.Drawing.Point(10, 5);
		this.lblPointData.Name = "lblPointData";
		this.lblPointData.Size = new System.Drawing.Size(0, 13);
		this.lblPointData.TabIndex = 1;
		this.HPGraphToolstrip.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
		this.HPGraphToolstrip.BackColor = System.Drawing.Color.White;
		this.HPGraphToolstrip.Dock = System.Windows.Forms.DockStyle.None;
		this.HPGraphToolstrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
		this.HPGraphToolstrip.ImageScalingSize = new System.Drawing.Size(24, 24);
		this.HPGraphToolstrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[8] { this.btnCancelZoom, this.toolStripDropDownButton2, this.toolStripSeparator6, this.btnGraphToClipboard, this.btnGraphToFile, this.toolStripDropDownButton1, this.toolStripSeparator7, this.ddlSmoothingFactor });
		this.HPGraphToolstrip.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.HorizontalStackWithOverflow;
		this.HPGraphToolstrip.Location = new System.Drawing.Point(431, 0);
		this.HPGraphToolstrip.Name = "HPGraphToolstrip";
		this.HPGraphToolstrip.RenderMode = System.Windows.Forms.ToolStripRenderMode.Professional;
		this.HPGraphToolstrip.Size = new System.Drawing.Size(233, 25);
		this.HPGraphToolstrip.TabIndex = 1;
		this.btnCancelZoom.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
		this.btnCancelZoom.Enabled = false;
		this.btnCancelZoom.Image = (System.Drawing.Image)resources.GetObject("btnCancelZoom.Image");
		this.btnCancelZoom.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
		this.btnCancelZoom.ImageTransparentColor = System.Drawing.Color.Black;
		this.btnCancelZoom.Name = "btnCancelZoom";
		this.btnCancelZoom.Size = new System.Drawing.Size(23, 22);
		this.btnCancelZoom.Text = "Cancel Zoom";
		this.btnCancelZoom.Click += new System.EventHandler(btnCancelZoom_Click);
		this.toolStripDropDownButton2.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
		this.toolStripDropDownButton2.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[4] { this.toolStripMenuItem_ToggleLegend, this.toolStripMenuItem_ToggleDataPoints, this.toolStripMenuItem_ToggleHP, this.toolStripMenuItem_ToggleTQ });
		this.toolStripDropDownButton2.Image = VirtualDyno.Properties.Resources.Options_16;
		this.toolStripDropDownButton2.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
		this.toolStripDropDownButton2.ImageTransparentColor = System.Drawing.Color.White;
		this.toolStripDropDownButton2.Name = "toolStripDropDownButton2";
		this.toolStripDropDownButton2.Size = new System.Drawing.Size(29, 22);
		this.toolStripDropDownButton2.Text = "Options";
		this.toolStripMenuItem_ToggleLegend.Image = VirtualDyno.Properties.Resources.legend;
		this.toolStripMenuItem_ToggleLegend.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
		this.toolStripMenuItem_ToggleLegend.ImageTransparentColor = System.Drawing.Color.Magenta;
		this.toolStripMenuItem_ToggleLegend.Name = "toolStripMenuItem_ToggleLegend";
		this.toolStripMenuItem_ToggleLegend.ShortcutKeys = System.Windows.Forms.Keys.G | System.Windows.Forms.Keys.Control;
		this.toolStripMenuItem_ToggleLegend.Size = new System.Drawing.Size(214, 22);
		this.toolStripMenuItem_ToggleLegend.Text = "Toggle Legend";
		this.toolStripMenuItem_ToggleLegend.Click += new System.EventHandler(toolStripMenuItem_ToggleLegend_Click);
		this.toolStripMenuItem_ToggleDataPoints.Image = VirtualDyno.Properties.Resources.ShowPoints;
		this.toolStripMenuItem_ToggleDataPoints.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
		this.toolStripMenuItem_ToggleDataPoints.ImageTransparentColor = System.Drawing.Color.White;
		this.toolStripMenuItem_ToggleDataPoints.Name = "toolStripMenuItem_ToggleDataPoints";
		this.toolStripMenuItem_ToggleDataPoints.ShortcutKeys = System.Windows.Forms.Keys.D | System.Windows.Forms.Keys.Control;
		this.toolStripMenuItem_ToggleDataPoints.Size = new System.Drawing.Size(214, 22);
		this.toolStripMenuItem_ToggleDataPoints.Text = "Toggle Data Points";
		this.toolStripMenuItem_ToggleDataPoints.Click += new System.EventHandler(toolStripMenuItem_ShowDataPoints_Click);
		this.toolStripMenuItem_ToggleHP.Name = "toolStripMenuItem_ToggleHP";
		this.toolStripMenuItem_ToggleHP.ShortcutKeys = System.Windows.Forms.Keys.H | System.Windows.Forms.Keys.Control;
		this.toolStripMenuItem_ToggleHP.Size = new System.Drawing.Size(214, 22);
		this.toolStripMenuItem_ToggleHP.Text = "Hide HP";
		this.toolStripMenuItem_ToggleHP.Click += new System.EventHandler(toolStripMenuItem_ToggleHP_Click);
		this.toolStripMenuItem_ToggleTQ.Name = "toolStripMenuItem_ToggleTQ";
		this.toolStripMenuItem_ToggleTQ.ShortcutKeys = System.Windows.Forms.Keys.T | System.Windows.Forms.Keys.Control;
		this.toolStripMenuItem_ToggleTQ.Size = new System.Drawing.Size(214, 22);
		this.toolStripMenuItem_ToggleTQ.Text = "Hide TQ";
		this.toolStripMenuItem_ToggleTQ.Click += new System.EventHandler(toolStripMenuItem_ToggleTQ_Click);
		this.toolStripSeparator6.Name = "toolStripSeparator6";
		this.toolStripSeparator6.Size = new System.Drawing.Size(6, 25);
		this.btnGraphToClipboard.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
		this.btnGraphToClipboard.Image = (System.Drawing.Image)resources.GetObject("btnGraphToClipboard.Image");
		this.btnGraphToClipboard.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
		this.btnGraphToClipboard.ImageTransparentColor = System.Drawing.Color.Magenta;
		this.btnGraphToClipboard.Name = "btnGraphToClipboard";
		this.btnGraphToClipboard.Size = new System.Drawing.Size(23, 22);
		this.btnGraphToClipboard.Text = "Copy Graph To Clipboard";
		this.btnGraphToClipboard.Click += new System.EventHandler(btnGraphToClipboard_Click);
		this.btnGraphToFile.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
		this.btnGraphToFile.Image = (System.Drawing.Image)resources.GetObject("btnGraphToFile.Image");
		this.btnGraphToFile.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
		this.btnGraphToFile.ImageTransparentColor = System.Drawing.Color.Magenta;
		this.btnGraphToFile.Name = "btnGraphToFile";
		this.btnGraphToFile.Size = new System.Drawing.Size(23, 22);
		this.btnGraphToFile.Text = "Save Graph to File";
		this.btnGraphToFile.Click += new System.EventHandler(btnGraphToFile_Click);
		this.toolStripDropDownButton1.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
		this.toolStripDropDownButton1.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[3] { this.toolStripMenuItem_PageSetup, this.toolStripMenuItem_Print, this.toolStripMenuItem_PrintPreview });
		this.toolStripDropDownButton1.Image = (System.Drawing.Image)resources.GetObject("toolStripDropDownButton1.Image");
		this.toolStripDropDownButton1.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
		this.toolStripDropDownButton1.ImageTransparentColor = System.Drawing.Color.Magenta;
		this.toolStripDropDownButton1.Name = "toolStripDropDownButton1";
		this.toolStripDropDownButton1.Size = new System.Drawing.Size(28, 22);
		this.toolStripMenuItem_PageSetup.Image = (System.Drawing.Image)resources.GetObject("toolStripMenuItem_PageSetup.Image");
		this.toolStripMenuItem_PageSetup.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
		this.toolStripMenuItem_PageSetup.ImageTransparentColor = System.Drawing.Color.Black;
		this.toolStripMenuItem_PageSetup.Name = "toolStripMenuItem_PageSetup";
		this.toolStripMenuItem_PageSetup.Size = new System.Drawing.Size(143, 22);
		this.toolStripMenuItem_PageSetup.Text = "Page &Setup";
		this.toolStripMenuItem_PageSetup.Click += new System.EventHandler(toolStripMenuItem_PageSetup_Click);
		this.toolStripMenuItem_Print.Image = (System.Drawing.Image)resources.GetObject("toolStripMenuItem_Print.Image");
		this.toolStripMenuItem_Print.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
		this.toolStripMenuItem_Print.ImageTransparentColor = System.Drawing.Color.Magenta;
		this.toolStripMenuItem_Print.Name = "toolStripMenuItem_Print";
		this.toolStripMenuItem_Print.ShortcutKeys = System.Windows.Forms.Keys.P | System.Windows.Forms.Keys.Control;
		this.toolStripMenuItem_Print.Size = new System.Drawing.Size(143, 22);
		this.toolStripMenuItem_Print.Text = "&Print";
		this.toolStripMenuItem_Print.Click += new System.EventHandler(toolStripMenuItem_Print_Click);
		this.toolStripMenuItem_PrintPreview.Image = (System.Drawing.Image)resources.GetObject("toolStripMenuItem_PrintPreview.Image");
		this.toolStripMenuItem_PrintPreview.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
		this.toolStripMenuItem_PrintPreview.ImageTransparentColor = System.Drawing.Color.Magenta;
		this.toolStripMenuItem_PrintPreview.Name = "toolStripMenuItem_PrintPreview";
		this.toolStripMenuItem_PrintPreview.Size = new System.Drawing.Size(143, 22);
		this.toolStripMenuItem_PrintPreview.Text = "Print Pre&view";
		this.toolStripMenuItem_PrintPreview.Click += new System.EventHandler(toolStripMenuItem_PrintPreview_Click);
		this.toolStripSeparator7.Name = "toolStripSeparator7";
		this.toolStripSeparator7.Size = new System.Drawing.Size(6, 25);
		this.ddlSmoothingFactor.BackColor = System.Drawing.Color.Transparent;
		this.ddlSmoothingFactor.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
		this.ddlSmoothingFactor.Image = (System.Drawing.Image)resources.GetObject("ddlSmoothingFactor.Image");
		this.ddlSmoothingFactor.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
		this.ddlSmoothingFactor.ImageTransparentColor = System.Drawing.Color.Magenta;
		this.ddlSmoothingFactor.Name = "ddlSmoothingFactor";
		this.ddlSmoothingFactor.Size = new System.Drawing.Size(92, 22);
		this.ddlSmoothingFactor.Text = "Smoothing: X";
		this.HPGraph.BackColor = System.Drawing.Color.White;
		this.HPGraph.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
		this.HPGraph.ContextMenuStrip = this.contextMenuStrip_Graph;
		this.HPGraph.Dock = System.Windows.Forms.DockStyle.Fill;
		this.HPGraph.IsAntiAlias = true;
		this.HPGraph.IsEnableHPan = false;
		this.HPGraph.IsEnableVPan = false;
		this.HPGraph.IsEnableWheelZoom = false;
		this.HPGraph.Location = new System.Drawing.Point(0, 0);
		this.HPGraph.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
		this.HPGraph.Name = "HPGraph";
		this.HPGraph.ScrollGrace = 0.0;
		this.HPGraph.ScrollMaxX = 0.0;
		this.HPGraph.ScrollMaxY = 0.0;
		this.HPGraph.ScrollMaxY2 = 0.0;
		this.HPGraph.ScrollMinX = 0.0;
		this.HPGraph.ScrollMinY = 0.0;
		this.HPGraph.ScrollMinY2 = 0.0;
		this.HPGraph.Size = new System.Drawing.Size(666, 666);
		this.HPGraph.TabIndex = 17;
		this.HPGraph.UseExtendedPrintDialog = true;
		this.HPGraph.ZoomEvent += new ZedGraph.ZedGraphControl.ZoomEventHandler(HPGraph_ZoomEvent);
		this.HPGraph.PointValueEvent += new ZedGraph.ZedGraphControl.PointValueHandler(HPGraph_PointValueEvent);
		this.HPGraph.MouseMove += new System.Windows.Forms.MouseEventHandler(HPGraph_MouseMove);
		this.contextMenuStrip_Graph.ImageScalingSize = new System.Drawing.Size(24, 24);
		this.contextMenuStrip_Graph.Items.AddRange(new System.Windows.Forms.ToolStripItem[7] { this.contextMenuItem_ToggleLegend, this.contextMenuItem_ToggleDataPoints, this.toolStripMenuItem_Smoothing, this.toolStripSeparator9, this.toolStripMenuItem_GraphToClipboard, this.toolStripMenuItem_GraphToFile, this.printToolStripMenuItem1 });
		this.contextMenuStrip_Graph.Name = "contextMenuStrip1";
		this.contextMenuStrip_Graph.Size = new System.Drawing.Size(181, 190);
		this.contextMenuItem_ToggleLegend.Checked = true;
		this.contextMenuItem_ToggleLegend.CheckState = System.Windows.Forms.CheckState.Checked;
		this.contextMenuItem_ToggleLegend.Image = (System.Drawing.Image)resources.GetObject("contextMenuItem_ToggleLegend.Image");
		this.contextMenuItem_ToggleLegend.ImageTransparentColor = System.Drawing.Color.Magenta;
		this.contextMenuItem_ToggleLegend.Name = "contextMenuItem_ToggleLegend";
		this.contextMenuItem_ToggleLegend.Size = new System.Drawing.Size(180, 30);
		this.contextMenuItem_ToggleLegend.Text = "Toggle Legend";
		this.contextMenuItem_ToggleLegend.Click += new System.EventHandler(toolStripMenuItem_ToggleLegend_Click);
		this.contextMenuItem_ToggleDataPoints.Image = (System.Drawing.Image)resources.GetObject("contextMenuItem_ToggleDataPoints.Image");
		this.contextMenuItem_ToggleDataPoints.ImageTransparentColor = System.Drawing.Color.White;
		this.contextMenuItem_ToggleDataPoints.Name = "contextMenuItem_ToggleDataPoints";
		this.contextMenuItem_ToggleDataPoints.Size = new System.Drawing.Size(180, 30);
		this.contextMenuItem_ToggleDataPoints.Text = "Toggle Data Points";
		this.contextMenuItem_ToggleDataPoints.Click += new System.EventHandler(toolStripMenuItem_ShowDataPoints_Click);
		this.toolStripMenuItem_Smoothing.Name = "toolStripMenuItem_Smoothing";
		this.toolStripMenuItem_Smoothing.Size = new System.Drawing.Size(180, 30);
		this.toolStripMenuItem_Smoothing.Text = "Smoothing";
		this.toolStripSeparator9.Name = "toolStripSeparator9";
		this.toolStripSeparator9.Size = new System.Drawing.Size(177, 6);
		this.toolStripMenuItem_GraphToClipboard.Image = (System.Drawing.Image)resources.GetObject("toolStripMenuItem_GraphToClipboard.Image");
		this.toolStripMenuItem_GraphToClipboard.ImageTransparentColor = System.Drawing.Color.Magenta;
		this.toolStripMenuItem_GraphToClipboard.Name = "toolStripMenuItem_GraphToClipboard";
		this.toolStripMenuItem_GraphToClipboard.Size = new System.Drawing.Size(180, 30);
		this.toolStripMenuItem_GraphToClipboard.Text = "&Copy to Clipboard";
		this.toolStripMenuItem_GraphToClipboard.Click += new System.EventHandler(btnGraphToClipboard_Click);
		this.toolStripMenuItem_GraphToFile.Image = VirtualDyno.Properties.Resources.Save;
		this.toolStripMenuItem_GraphToFile.ImageTransparentColor = System.Drawing.Color.Magenta;
		this.toolStripMenuItem_GraphToFile.Name = "toolStripMenuItem_GraphToFile";
		this.toolStripMenuItem_GraphToFile.Size = new System.Drawing.Size(180, 30);
		this.toolStripMenuItem_GraphToFile.Text = "&Graph To File...";
		this.toolStripMenuItem_GraphToFile.Click += new System.EventHandler(btnGraphToFile_Click);
		this.printToolStripMenuItem1.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[3] { this.toolStripMenuItem_PageSetup_Context, this.toolStripMenuItem_Print_Context, this.toolStripMenuItem_PrintPreview_Context });
		this.printToolStripMenuItem1.Image = (System.Drawing.Image)resources.GetObject("printToolStripMenuItem1.Image");
		this.printToolStripMenuItem1.ImageTransparentColor = System.Drawing.Color.Magenta;
		this.printToolStripMenuItem1.Name = "printToolStripMenuItem1";
		this.printToolStripMenuItem1.Size = new System.Drawing.Size(180, 30);
		this.printToolStripMenuItem1.Text = "&Print";
		this.toolStripMenuItem_PageSetup_Context.Image = VirtualDyno.Properties.Resources.PageSetup;
		this.toolStripMenuItem_PageSetup_Context.ImageTransparentColor = System.Drawing.Color.Black;
		this.toolStripMenuItem_PageSetup_Context.Name = "toolStripMenuItem_PageSetup_Context";
		this.toolStripMenuItem_PageSetup_Context.Size = new System.Drawing.Size(143, 22);
		this.toolStripMenuItem_PageSetup_Context.Text = "Page &Setup";
		this.toolStripMenuItem_PageSetup_Context.Click += new System.EventHandler(toolStripMenuItem_PageSetup_Click);
		this.toolStripMenuItem_Print_Context.Image = (System.Drawing.Image)resources.GetObject("toolStripMenuItem_Print_Context.Image");
		this.toolStripMenuItem_Print_Context.ImageTransparentColor = System.Drawing.Color.Magenta;
		this.toolStripMenuItem_Print_Context.Name = "toolStripMenuItem_Print_Context";
		this.toolStripMenuItem_Print_Context.Size = new System.Drawing.Size(143, 22);
		this.toolStripMenuItem_Print_Context.Text = "&Print";
		this.toolStripMenuItem_Print_Context.Click += new System.EventHandler(toolStripMenuItem_Print_Click);
		this.toolStripMenuItem_PrintPreview_Context.Image = (System.Drawing.Image)resources.GetObject("toolStripMenuItem_PrintPreview_Context.Image");
		this.toolStripMenuItem_PrintPreview_Context.ImageTransparentColor = System.Drawing.Color.Magenta;
		this.toolStripMenuItem_PrintPreview_Context.Name = "toolStripMenuItem_PrintPreview_Context";
		this.toolStripMenuItem_PrintPreview_Context.Size = new System.Drawing.Size(143, 22);
		this.toolStripMenuItem_PrintPreview_Context.Text = "Print Pre&view";
		this.toolStripMenuItem_PrintPreview_Context.Click += new System.EventHandler(toolStripMenuItem_PrintPreview_Click);
		this.statusStrip1.BackColor = System.Drawing.Color.White;
		this.statusStrip1.GripMargin = new System.Windows.Forms.Padding(0);
		this.statusStrip1.ImageScalingSize = new System.Drawing.Size(24, 24);
		this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[4] { this.lblStatus, this.pbStatus, this.ddActiveProfile, this.ddVersion });
		this.statusStrip1.Location = new System.Drawing.Point(0, 3);
		this.statusStrip1.Name = "statusStrip1";
		this.statusStrip1.Padding = new System.Windows.Forms.Padding(1, 0, 15, 0);
		this.statusStrip1.RenderMode = System.Windows.Forms.ToolStripRenderMode.Professional;
		this.statusStrip1.Size = new System.Drawing.Size(666, 22);
		this.statusStrip1.SizingGrip = false;
		this.statusStrip1.TabIndex = 0;
		this.statusStrip1.Text = "statusStrip1";
		this.lblStatus.Name = "lblStatus";
		this.lblStatus.Size = new System.Drawing.Size(436, 17);
		this.lblStatus.Spring = true;
		this.lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
		this.pbStatus.Name = "pbStatus";
		this.pbStatus.Size = new System.Drawing.Size(99, 25);
		this.pbStatus.Visible = false;
		this.ddActiveProfile.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
		this.ddActiveProfile.ForeColor = System.Drawing.Color.Firebrick;
		this.ddActiveProfile.Image = (System.Drawing.Image)resources.GetObject("ddActiveProfile.Image");
		this.ddActiveProfile.ImageTransparentColor = System.Drawing.Color.Magenta;
		this.ddActiveProfile.Name = "ddActiveProfile";
		this.ddActiveProfile.Size = new System.Drawing.Size(123, 20);
		this.ddActiveProfile.Text = "No Profile Selected";
		this.ddActiveProfile.ButtonClick += new System.EventHandler(ddActiveProfile_ButtonClick);
		this.ddVersion.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
		this.ddVersion.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[2] { this.releaseNotesToolStripMenuItem, this.getUpdateToolStripMenuItem });
		this.ddVersion.Image = (System.Drawing.Image)resources.GetObject("ddVersion.Image");
		this.ddVersion.ImageTransparentColor = System.Drawing.Color.Magenta;
		this.ddVersion.Name = "ddVersion";
		this.ddVersion.Size = new System.Drawing.Size(91, 20);
		this.ddVersion.Text = "Version: X.X.X";
		this.releaseNotesToolStripMenuItem.Image = (System.Drawing.Image)resources.GetObject("releaseNotesToolStripMenuItem.Image");
		this.releaseNotesToolStripMenuItem.ImageTransparentColor = System.Drawing.Color.Magenta;
		this.releaseNotesToolStripMenuItem.Name = "releaseNotesToolStripMenuItem";
		this.releaseNotesToolStripMenuItem.Size = new System.Drawing.Size(147, 22);
		this.releaseNotesToolStripMenuItem.Text = "Release &Notes";
		this.releaseNotesToolStripMenuItem.Click += new System.EventHandler(toolStripMenuItem_ReleaseNotes_Click);
		this.getUpdateToolStripMenuItem.Image = (System.Drawing.Image)resources.GetObject("getUpdateToolStripMenuItem.Image");
		this.getUpdateToolStripMenuItem.Name = "getUpdateToolStripMenuItem";
		this.getUpdateToolStripMenuItem.Size = new System.Drawing.Size(147, 22);
		this.getUpdateToolStripMenuItem.Text = "Get &Update";
		this.getUpdateToolStripMenuItem.Visible = false;
		this.panelAdvertisement.BackColor = System.Drawing.Color.Transparent;
		this.panelAdvertisement.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.panelAdvertisement.Controls.Add(this.btnCloseAdvertisement);
		this.panelAdvertisement.Controls.Add(this.pictureAdvertiseBottom);
		this.panelAdvertisement.Location = new System.Drawing.Point(3, 3);
		this.panelAdvertisement.Name = "panelAdvertisement";
		this.panelAdvertisement.Size = new System.Drawing.Size(277, 102);
		this.panelAdvertisement.TabIndex = 23;
		this.btnCloseAdvertisement.BackColor = System.Drawing.Color.White;
		this.btnCloseAdvertisement.BackgroundImage = VirtualDyno.Properties.Resources.profile_remove;
		this.btnCloseAdvertisement.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
		this.btnCloseAdvertisement.Location = new System.Drawing.Point(253, 0);
		this.btnCloseAdvertisement.Name = "btnCloseAdvertisement";
		this.btnCloseAdvertisement.Size = new System.Drawing.Size(21, 22);
		this.btnCloseAdvertisement.TabIndex = 24;
		this.btnCloseAdvertisement.TabStop = false;
		this.btnCloseAdvertisement.Visible = false;
		this.btnCloseAdvertisement.Click += new System.EventHandler(btnCloseAdvertisement_Click);
		this.btnCloseAdvertisement.MouseEnter += new System.EventHandler(btnCloseAdvertisement_MouseEnter);
		this.btnCloseAdvertisement.MouseLeave += new System.EventHandler(btnCloseAdvertisement_MouseLeave);
		this.pictureAdvertiseBottom.BackColor = System.Drawing.Color.Transparent;
		this.pictureAdvertiseBottom.Dock = System.Windows.Forms.DockStyle.Fill;
		this.pictureAdvertiseBottom.Image = VirtualDyno.Properties.Resources.DefaultAd;
		this.pictureAdvertiseBottom.Location = new System.Drawing.Point(0, 0);
		this.pictureAdvertiseBottom.Name = "pictureAdvertiseBottom";
		this.pictureAdvertiseBottom.Size = new System.Drawing.Size(275, 100);
		this.pictureAdvertiseBottom.TabIndex = 0;
		this.pictureAdvertiseBottom.TabStop = false;
		this.toolStripSeparator8.Name = "toolStripSeparator8";
		this.toolStripSeparator8.Size = new System.Drawing.Size(149, 6);
		this.toolStripSeparator4.Name = "toolStripSeparator4";
		this.toolStripSeparator4.Size = new System.Drawing.Size(6, 25);
		this.toolStripSeparator5.Name = "toolStripSeparator5";
		this.toolStripSeparator5.Size = new System.Drawing.Size(6, 25);
		this.exitToolStripMenuItem.ImageTransparentColor = System.Drawing.Color.White;
		this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
		this.exitToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
		this.exitToolStripMenuItem.Text = "E&xit";
		this.toolStripSeparator3.Name = "toolStripSeparator3";
		this.toolStripSeparator3.Size = new System.Drawing.Size(149, 6);
		this.AddProfile.Image = (System.Drawing.Image)resources.GetObject("AddProfile.Image");
		this.AddProfile.Name = "AddProfile";
		this.AddProfile.Size = new System.Drawing.Size(152, 22);
		this.AddProfile.Text = "Add Profile";
		this.AddProfile.Click += new System.EventHandler(ProfilesMenuAdd_Click);
		this.loadRunsToolStripMenuItem.Image = (System.Drawing.Image)resources.GetObject("loadRunsToolStripMenuItem.Image");
		this.loadRunsToolStripMenuItem.ImageTransparentColor = System.Drawing.Color.White;
		this.loadRunsToolStripMenuItem.Name = "loadRunsToolStripMenuItem";
		this.loadRunsToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
		this.loadRunsToolStripMenuItem.Text = "&Load Runs";
		this.optionsToolStripMenuItem1.Image = (System.Drawing.Image)resources.GetObject("optionsToolStripMenuItem1.Image");
		this.optionsToolStripMenuItem1.ImageTransparentColor = System.Drawing.Color.Black;
		this.optionsToolStripMenuItem1.Name = "optionsToolStripMenuItem1";
		this.optionsToolStripMenuItem1.Size = new System.Drawing.Size(152, 22);
		this.optionsToolStripMenuItem1.Text = "&Options";
		this.tAdvertisementCloseButton.Enabled = true;
		this.tAdvertisementCloseButton.Interval = 6000;
		this.tAdvertisementCloseButton.Tick += new System.EventHandler(tAdvertisementCloseButton_Tick);
		this.AllowDrop = true;
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		this.BackColor = System.Drawing.SystemColors.ActiveCaption;
		base.ClientSize = new System.Drawing.Size(957, 692);
		base.Controls.Add(this.splitContainerHPGraph_LeftPanelProfiles);
		this.DoubleBuffered = true;
		base.Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
		this.MinimumSize = new System.Drawing.Size(872, 590);
		base.Name = "frmVirtualDyno";
		base.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
		this.Text = "Virtual Dyno";
		base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(frmVirtualDyno_FormClosing);
		base.Load += new System.EventHandler(frmHPCalc_Load);
		base.ResizeEnd += new System.EventHandler(frmVirtualDyno_ResizeEnd);
		base.DragDrop += new System.Windows.Forms.DragEventHandler(Runs_DragDrop);
		base.DragEnter += new System.Windows.Forms.DragEventHandler(Runs_DragEnter);
		base.Move += new System.EventHandler(frmVirtualDyno_Move);
		this.splitContainerHPGraph_LeftPanelProfiles.Panel1.ResumeLayout(false);
		this.splitContainerHPGraph_LeftPanelProfiles.Panel2.ResumeLayout(false);
		this.splitContainerHPGraph_LeftPanelProfiles.Panel2.PerformLayout();
		((System.ComponentModel.ISupportInitialize)this.splitContainerHPGraph_LeftPanelProfiles).EndInit();
		this.splitContainerHPGraph_LeftPanelProfiles.ResumeLayout(false);
		this.splitContainer2.Panel1.ResumeLayout(false);
		this.splitContainer2.Panel2.ResumeLayout(false);
		this.splitContainer2.Panel2.PerformLayout();
		((System.ComponentModel.ISupportInitialize)this.splitContainer2).EndInit();
		this.splitContainer2.ResumeLayout(false);
		this.splitContainerLeftPanel_OpenedRuns.Panel1.ResumeLayout(false);
		this.splitContainerLeftPanel_OpenedRuns.Panel1.PerformLayout();
		this.splitContainerLeftPanel_OpenedRuns.Panel2.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)this.splitContainerLeftPanel_OpenedRuns).EndInit();
		this.splitContainerLeftPanel_OpenedRuns.ResumeLayout(false);
		this.MainMenuTop.ResumeLayout(false);
		this.MainMenuTop.PerformLayout();
		this.CopyrightStrip.ResumeLayout(false);
		this.CopyrightStrip.PerformLayout();
		this.splitContainer4.Panel1.ResumeLayout(false);
		this.splitContainer4.Panel1.PerformLayout();
		this.splitContainer4.Panel2.ResumeLayout(false);
		this.splitContainer4.Panel2.PerformLayout();
		((System.ComponentModel.ISupportInitialize)this.splitContainer4).EndInit();
		this.splitContainer4.ResumeLayout(false);
		this.pShowValues.ResumeLayout(false);
		this.pShowValues.PerformLayout();
		this.HPGraphToolstrip.ResumeLayout(false);
		this.HPGraphToolstrip.PerformLayout();
		this.contextMenuStrip_Graph.ResumeLayout(false);
		this.statusStrip1.ResumeLayout(false);
		this.statusStrip1.PerformLayout();
		this.panelAdvertisement.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)this.btnCloseAdvertisement).EndInit();
		((System.ComponentModel.ISupportInitialize)this.pictureAdvertiseBottom).EndInit();
		base.ResumeLayout(false);
	}

	static frmVirtualDyno()
	{
		char[] obj = new char[4] { ',', '\t', ';', '\0' };
		obj[3] = Convert.ToChar(30);
		COLUMN_SEPERATORS = obj;
		char[] obj2 = new char[6] { '"', ' ', '\t', '\0', '\0', '\0' };
		obj2[4] = Convert.ToChar(65279);
		obj2[5] = Convert.ToChar(194);
		COLUMN_TRIM_CHARS = obj2;
		SUPPORTED_FILE_EXT = new string[4] { ".CSV", ".TXT", ".MSL", ".LOG" };
		BAR_CEILING_VALUE = 4.0;
		MILLIBAR_FLOOR_VALUE = 100.0;
	}
}

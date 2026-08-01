using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using VirtualDyno.Core;
using VirtualDyno.Core.Datasets;
using VirtualDyno.Properties;
using VirtualDyno.RunControl;

namespace VirtualDyno;

public class Settings
{
	private GraphSettings _GraphSettings = new GraphSettings();

	private Columns _Columns = new Columns();

	private CarProfile _CarProfiles = new CarProfile();

	private Dynos _Dynos = new Dynos();

	private Registration _Registration = new Registration();

	public GraphSettings GraphSettings
	{
		get
		{
			return _GraphSettings;
		}
		set
		{
			_GraphSettings = value;
		}
	}

	public Columns Columns
	{
		get
		{
			return _Columns;
		}
		set
		{
			_Columns = value;
		}
	}

	public CarProfile CarProfiles
	{
		get
		{
			return _CarProfiles;
		}
		set
		{
			_CarProfiles = value;
		}
	}

	public Dynos Dynos
	{
		get
		{
			return _Dynos;
		}
		set
		{
			_Dynos = value;
		}
	}

	public Registration Registration
	{
		get
		{
			return _Registration;
		}
		set
		{
			_Registration = value;
		}
	}

	public Point WindowLocation
	{
		get
		{
			return new Point(LayoutRow.Left, LayoutRow.Top);
		}
		set
		{
			LayoutRow.Left = value.X;
			LayoutRow.Top = value.Y;
		}
	}

	public Point WindowSize
	{
		get
		{
			return new Point(LayoutRow.Width, LayoutRow.Height);
		}
		set
		{
			LayoutRow.Width = value.X;
			LayoutRow.Height = value.Y;
		}
	}

	public GraphSettings.GraphSettingsRow GraphSettingsRow
	{
		get
		{
			if (GraphSettings.Tables["GraphSettings"].Rows.Count > 0)
			{
				return (GraphSettings.GraphSettingsRow)GraphSettings.Tables["GraphSettings"].Rows[0];
			}
			return null;
		}
	}

	public GraphSettings.LayoutRow LayoutRow
	{
		get
		{
			if (GraphSettings.Tables["Layout"].Rows.Count > 0)
			{
				return (GraphSettings.LayoutRow)GraphSettings.Tables["Layout"].Rows[0];
			}
			return null;
		}
	}

	public bool HideHorsepower { get; set; }

	public bool HideTorque { get; set; }

	public RegionInfo Region { get; set; }

	public Settings()
	{
		try
		{
			Region = RegionInfo.CurrentRegion;
			if (File.Exists(Path.Combine(Statics.baseFilepath, VirtualDyno.Properties.Settings.Default.File_Registration)) && Registration.Tables["Registration"].Rows.Count <= 0)
			{
				Registration.ReadXml(Path.Combine(Statics.baseFilepath, VirtualDyno.Properties.Settings.Default.File_Registration));
			}
		}
		catch
		{
			Registration.Tables["Registration"].Rows[0]["InstallID"] = "Error";
		}
	}

	public void LoadSettings()
	{
		GraphSettings.Clear();
		Columns.Clear();
		if (File.Exists(Path.Combine(Statics.baseFilepath, VirtualDyno.Properties.Settings.Default.File_GraphSettings)))
		{
			GraphSettings.ReadXml(Path.Combine(Statics.baseFilepath, VirtualDyno.Properties.Settings.Default.File_GraphSettings));
		}
		else
		{
			GraphSettings.ReadXml(Assembly.GetExecutingAssembly().GetManifestResourceStream("VirtualDyno.XMLs." + VirtualDyno.Properties.Settings.Default.File_GraphSettings));
		}
		if (File.Exists(Path.Combine(Statics.baseFilepath, VirtualDyno.Properties.Settings.Default.File_Profiles)) && CarProfiles.Tables["CarProfile"].Rows.Count <= 0)
		{
			CarProfiles.ReadXml(Path.Combine(Statics.baseFilepath, VirtualDyno.Properties.Settings.Default.File_Profiles));
		}
		if (Dynos.Tables["DynoCorrectionFactors"].Rows.Count <= 0)
		{
			Dynos.ReadXml(Assembly.GetExecutingAssembly().GetManifestResourceStream(VirtualDyno.Properties.Settings.Default.File_Dynos));
		}
		try
		{
			bool flag = true;
			while (flag)
			{
				try
				{
					Columns.ReadXml(Path.Combine(Statics.baseFilepath, VirtualDyno.Properties.Settings.Default.File_ColumnNames));
					flag = false;
				}
				catch (UnauthorizedAccessException ex)
				{
					Console.WriteLine("File in use ... waiting: " + ex.Message);
					Thread.Sleep(500);
				}
			}
		}
		catch (Exception ex2)
		{
			Console.WriteLine(ex2.Message);
		}
	}

	public void SaveSettings()
	{
		string text = string.Empty;
		try
		{
			File.Delete(Path.Combine(Statics.baseFilepath, VirtualDyno.Properties.Settings.Default.File_GraphSettings));
			GraphSettings.WriteXml(Path.Combine(Statics.baseFilepath, VirtualDyno.Properties.Settings.Default.File_GraphSettings));
		}
		catch (Exception ex)
		{
			text = text + "Failed saving graphsettings.xml: " + ex.Message + Environment.NewLine + Environment.NewLine;
		}
		try
		{
			File.Delete(Path.Combine(Statics.baseFilepath, VirtualDyno.Properties.Settings.Default.File_ColumnNames));
			Columns.WriteXml(Path.Combine(Statics.baseFilepath, VirtualDyno.Properties.Settings.Default.File_ColumnNames));
		}
		catch (Exception ex2)
		{
			text = text + "Failed saving columnnames.xml: " + ex2.Message + Environment.NewLine + Environment.NewLine;
		}
		try
		{
			File.Delete(Path.Combine(Statics.baseFilepath, VirtualDyno.Properties.Settings.Default.File_Registration));
			Registration.WriteXml(Path.Combine(Statics.baseFilepath, VirtualDyno.Properties.Settings.Default.File_Registration));
		}
		catch (Exception ex3)
		{
			text = text + "Failed saving registration.xml: " + ex3.Message + Environment.NewLine + Environment.NewLine;
		}
		if (!string.IsNullOrEmpty(text))
		{
			MessageBox.Show(text, "Save Settings Warning", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
		}
	}
}

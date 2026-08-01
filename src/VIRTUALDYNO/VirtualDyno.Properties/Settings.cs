using System;
using System.CodeDom.Compiler;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.CompilerServices;

namespace VirtualDyno.Properties;

[CompilerGenerated]
[GeneratedCode("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "17.12.0.0")]
internal sealed class Settings : ApplicationSettingsBase
{
	private static Settings defaultInstance = (Settings)SettingsBase.Synchronized(new Settings());

	public static Settings Default => defaultInstance;

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("profiles.xml")]
	public string File_Profiles
	{
		get
		{
			return (string)this["File_Profiles"];
		}
		set
		{
			this["File_Profiles"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("columnnames.xml")]
	public string File_ColumnNames
	{
		get
		{
			return (string)this["File_ColumnNames"];
		}
		set
		{
			this["File_ColumnNames"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("graphsettings.xml")]
	public string File_GraphSettings
	{
		get
		{
			return (string)this["File_GraphSettings"];
		}
		set
		{
			this["File_GraphSettings"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("VirtualDyno.XMLs.dynos.xml")]
	public string File_Dynos
	{
		get
		{
			return (string)this["File_Dynos"];
		}
		set
		{
			this["File_Dynos"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("10")]
	public int MAX_LOADED_RUNS
	{
		get
		{
			return (int)this["MAX_LOADED_RUNS"];
		}
		set
		{
			this["MAX_LOADED_RUNS"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("0")]
	public int MIN_SMOOTHING
	{
		get
		{
			return (int)this["MIN_SMOOTHING"];
		}
		set
		{
			this["MIN_SMOOTHING"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("6")]
	public int MAX_SMOOTHING
	{
		get
		{
			return (int)this["MAX_SMOOTHING"];
		}
		set
		{
			this["MAX_SMOOTHING"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("3")]
	public double SMOOTHING_MULTIPLIER
	{
		get
		{
			return (double)this["SMOOTHING_MULTIPLIER"];
		}
		set
		{
			this["SMOOTHING_MULTIPLIER"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("100")]
	public int RPM_TRIM_WINDOW
	{
		get
		{
			return (int)this["RPM_TRIM_WINDOW"];
		}
		set
		{
			this["RPM_TRIM_WINDOW"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=KHEKM6EDA8GGW")]
	public string DONATE_URL
	{
		get
		{
			return (string)this["DONATE_URL"];
		}
		set
		{
			this["DONATE_URL"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("AliceBlue")]
	public Color Color_Light
	{
		get
		{
			return (Color)this["Color_Light"];
		}
		set
		{
			this["Color_Light"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("153, 204, 255")]
	public Color Color_Medium
	{
		get
		{
			return (Color)this["Color_Medium"];
		}
		set
		{
			this["Color_Medium"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("0, 131, 215")]
	public Color Color_Dark
	{
		get
		{
			return (Color)this["Color_Dark"];
		}
		set
		{
			this["Color_Dark"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("Cars\\carsversion.xml")]
	public string File_CarsVersion
	{
		get
		{
			return (string)this["File_CarsVersion"];
		}
		set
		{
			this["File_CarsVersion"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("virtualdyno")]
	public string Error_FTPUsername
	{
		get
		{
			return (string)this["Error_FTPUsername"];
		}
		set
		{
			this["Error_FTPUsername"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("1virtualdyno")]
	public string Error_FTPPassword
	{
		get
		{
			return (string)this["Error_FTPPassword"];
		}
		set
		{
			this["Error_FTPPassword"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("registration.xml")]
	public string File_Registration
	{
		get
		{
			return (string)this["File_Registration"];
		}
		set
		{
			this["File_Registration"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("ftp://www.virtualdyno.net/ErrorReports")]
	public string Error_FTPLocation
	{
		get
		{
			return (string)this["Error_FTPLocation"];
		}
		set
		{
			this["Error_FTPLocation"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("00000000-0000-0000-0000-000000000000")]
	public Guid GoogleAnalytics_InstallationId
	{
		get
		{
			return (Guid)this["GoogleAnalytics_InstallationId"];
		}
		set
		{
			this["GoogleAnalytics_InstallationId"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("/public/columnnames.xml")]
	public string COLUMN_URL_SUFFIX
	{
		get
		{
			return (string)this["COLUMN_URL_SUFFIX"];
		}
		set
		{
			this["COLUMN_URL_SUFFIX"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("/public/carsversion.xml")]
	public string CARSVERSION_URL_SUFFIX
	{
		get
		{
			return (string)this["CARSVERSION_URL_SUFFIX"];
		}
		set
		{
			this["CARSVERSION_URL_SUFFIX"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("/public/version.xml")]
	public string VERSION_URL_SUFFIX
	{
		get
		{
			return (string)this["VERSION_URL_SUFFIX"];
		}
		set
		{
			this["VERSION_URL_SUFFIX"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("https://www.virtualdyno.net")]
	public string SETTINGS_URL_BASE
	{
		get
		{
			return (string)this["SETTINGS_URL_BASE"];
		}
		set
		{
			this["SETTINGS_URL_BASE"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("setup.zip")]
	public string UPDATE_FILENAME
	{
		get
		{
			return (string)this["UPDATE_FILENAME"];
		}
		set
		{
			this["UPDATE_FILENAME"] = value;
		}
	}
}

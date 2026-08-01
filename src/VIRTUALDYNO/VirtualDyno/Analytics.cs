using System;
using System.Collections.Specialized;
using System.Net;
using System.Windows.Forms;
using VirtualDyno.Properties;

namespace VirtualDyno;

public class Analytics
{
	private static readonly WebClientEx Client = new WebClientEx();

	public static void ReportApplicationStart()
	{
		if (!GetAnalyticsId().Equals(Guid.Empty))
		{
			NameValueCollection nameValueCollection = new NameValueCollection
			{
				{ "v", "1" },
				{ "tid", "UA-12996660-3" },
				{
					"cid",
					GetAnalyticsId().ToString()
				},
				{ "t", "event" },
				{ "ec", "application" },
				{ "ea", "start" },
				{ "el", "ApplicationStart" },
				{ "dt", "Start" },
				{ "sc", "start" },
				{
					"vp",
					Screen.PrimaryScreen.Bounds.Width + "x" + Screen.PrimaryScreen.Bounds.Height
				}
			};
			Console.WriteLine("Report Application Start (AnalyticsId=" + GetAnalyticsId().ToString() + ")");
			Send(nameValueCollection);
		}
	}

	public static void ReportDynoGraph(int numRuns)
	{
		if (!GetAnalyticsId().Equals(Guid.Empty))
		{
			NameValueCollection nameValueCollection = new NameValueCollection
			{
				{ "v", "1" },
				{ "tid", "UA-12996660-3" },
				{
					"cid",
					GetAnalyticsId().ToString()
				},
				{ "t", "event" },
				{ "ec", "dyno" },
				{ "ea", "graphed" },
				{ "el", "DynoGraphed" },
				{
					"ev",
					numRuns.ToString()
				},
				{
					"vp",
					Screen.PrimaryScreen.Bounds.Width + "x" + Screen.PrimaryScreen.Bounds.Height
				}
			};
			Console.WriteLine("Report Dyno Graphed (AnalyticsId=" + GetAnalyticsId().ToString() + ")");
			Send(nameValueCollection);
		}
	}

	private static void Initialize()
	{
		VirtualDyno.Properties.Settings.Default.Upgrade();
		_ = VirtualDyno.Properties.Settings.Default.GoogleAnalytics_InstallationId;
		if (VirtualDyno.Properties.Settings.Default.GoogleAnalytics_InstallationId.Equals(Guid.Empty))
		{
			VirtualDyno.Properties.Settings.Default.GoogleAnalytics_InstallationId = Guid.NewGuid();
			VirtualDyno.Properties.Settings.Default.Save();
		}
	}

	public static Guid GetAnalyticsId()
	{
		if (!VirtualDyno.Properties.Settings.Default.GoogleAnalytics_InstallationId.Equals(Guid.Empty))
		{
			return VirtualDyno.Properties.Settings.Default.GoogleAnalytics_InstallationId;
		}
		Initialize();
		return VirtualDyno.Properties.Settings.Default.GoogleAnalytics_InstallationId;
	}

	private static HttpStatusCode Send(NameValueCollection nameValueCollection)
	{
		Client.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
		try
		{
			Client.UploadString("https://www.google-analytics.com/collect", BuildQueryString(nameValueCollection));
		}
		catch (WebException)
		{
		}
		return Client.StatusCode;
	}

	private static string BuildQueryString(NameValueCollection nameValueCollection)
	{
		string text = string.Empty;
		foreach (string item in nameValueCollection)
		{
			string text3 = nameValueCollection.Get(item);
			if (text.Length > 0)
			{
				text += "&";
			}
			text = text + item + "=" + text3;
		}
		return Uri.EscapeUriString(text + "z=" + Guid.NewGuid().ToString());
	}
}

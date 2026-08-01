using System;
using System.Windows.Forms;
using VirtualDyno.Core;

namespace VirtualDyno;

internal static class Program
{
	[STAThread]
	private static void Main(string[] args)
	{
		try
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(defaultValue: false);
			Application.Run(new frmVirtualDyno(args));
		}
		catch (Exception ex)
		{
			Statics.Error(ex);
		}
	}
}

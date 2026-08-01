using System;
using VirtualDyno.RunControl;

namespace VirtualDyno;

public class OnYesNoEventArgs : EventArgs
{
	private cRunControl _RunControl;

	private bool _YesNo;

	public cRunControl RunControl
	{
		get
		{
			return _RunControl;
		}
		set
		{
			_RunControl = value;
		}
	}

	public bool YesNo
	{
		get
		{
			return _YesNo;
		}
		set
		{
			_YesNo = value;
		}
	}

	public OnYesNoEventArgs(ref cRunControl rc, bool YesNo)
	{
		RunControl = rc;
		this.YesNo = YesNo;
	}
}

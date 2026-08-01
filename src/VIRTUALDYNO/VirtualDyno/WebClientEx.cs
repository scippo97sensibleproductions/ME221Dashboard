using System;
using System.Net;

namespace VirtualDyno;

internal class WebClientEx : WebClient
{
	private WebResponse _response;

	public HttpStatusCode StatusCode => (_response as HttpWebResponse)?.StatusCode ?? HttpStatusCode.OK;

	protected override WebResponse GetWebResponse(WebRequest req, IAsyncResult ar)
	{
		ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
		return _response = base.GetWebResponse(req, ar);
	}
}

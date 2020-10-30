﻿#region -- copyright --
//
// Licensed under the EUPL, Version 1.1 or - as soon they will be approved by the
// European Commission - subsequent versions of the EUPL(the "Licence"); You may
// not use this work except in compliance with the Licence.
//
// You may obtain a copy of the Licence at:
// http://ec.europa.eu/idabc/eupl
//
// Unless required by applicable law or agreed to in writing, software distributed
// under the Licence is distributed on an "AS IS" basis, WITHOUT WARRANTIES OR
// CONDITIONS OF ANY KIND, either express or implied. See the Licence for the
// specific language governing permissions and limitations under the Licence.
//
#endregion
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using TecWare.DE.Stuff;
using TecWare.PPSn.Networking;

namespace TecWare.PPSn.Controls
{
	/// <summary>WebView Edge based</summary>
	public class PpsWebView : WebView2
	{
		private readonly Lazy<IPpsShell> shell;

		private string currentFilterUri = null;

		/// <summary></summary>
		public PpsWebView()
		{
			shell = new Lazy<IPpsShell>(GetShell);

			CoreWebView2Ready += PpsWebView_CoreWebView2Ready;
		} // ctor

		private IPpsShell GetShell()
			=> this.GetControlService<IPpsShell>(true);

		#region -- Redirect Core Web View features ------------------------------------

		private void PpsWebView_CoreWebView2Ready(object sender, EventArgs e)
		{
			var v = ((WebView2)sender).CoreWebView2;
#if !DEBUG
			v.Settings.AreDefaultContextMenusEnabled = false;
			v.Settings.AreDevToolsEnabled = false;
#endif
			v.Settings.IsWebMessageEnabled = false;
			v.Settings.IsZoomControlEnabled = false;

			v.DocumentTitleChanged += WebView_DocumentTitleChanged;
			v.HistoryChanged += WebView_HistoryChanged;
			v.NavigationStarting += WebView_NavigationStarting;
			v.NavigationCompleted += WebView_NavigationCompleted;
			v.NewWindowRequested += WebView_NewWindowRequested;
			v.PermissionRequested += WebView_PermissionRequested;
			v.WindowCloseRequested += WebView_WindowCloseRequested;
			v.WebResourceRequested += WebView_WebResourceRequested;

			UpdateResourceRequest();
		} // event PpsWebView_CoreWebView2Ready

		private void WebView_DocumentTitleChanged(object sender, object e)
		{
			//if (sender is CoreWebView2 nWebView
			//	&& Element is ITwWebViewController xWebView)
			//	xWebView.SetTitle(nWebView.DocumentTitle);
		} // event WebView_DocumentTitleChanged

		private void WebView_HistoryChanged(object sender, object e)
		{
			// todo: Notify Back,Forward
		} // event WebView_HistoryChanged

		private void WebView_NewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
		{
			e.Handled = true;
		} // event WebView_NewWindowRequested

		private void WebView_WindowCloseRequested(object sender, object e)
		{
		} // event WebView_WindowCloseRequested


		private void WebView_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
		{
#if DEBUG_NAV
			Debug.WriteLine("WebView_NavigationStarting[{0}]: {1} (user={2}, redirect={3})", e.NavigationId, e.Uri, e.IsUserInitiated, e.IsRedirected);
#endif

			//if (Controller != null)
			//{
			//	var uri = e.Uri;
			//	if (Controller.OnPreNavigateUrl(uri))
			//		e.Cancel = true;
			//	else if (e.IsUserInitiated)
			//	{
			//		e.Cancel = false;

			//		// send page start
			//		currentNavigating[e.NavigationId] = uri;
			//		if (currentNavigating.Count == 1)
			//			Controller.FirePageStarted(new TwWebViewPageEventArgs(uri));
			//	}
			//}
			//else
				e.Cancel = false;
		} // event WebView_NavigationStarting

		private void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
		{
#if DEBUG_NAV
			Debug.WriteLine("WebView_NavigationCompleted[{0}]: {1} (code={2})", e.NavigationId, e.IsSuccess, e.WebErrorStatus);
#endif

			//if (Controller != null)
			//{
			//	// send page finished
			//	if (currentNavigating.TryGetValue(e.NavigationId, out var uri))
			//	{
			//		if (currentNavigating.Count == 1)
			//			Controller.FirePageFinished(new TwWebViewPageFinishEventArgs(uri, true));
			//		currentNavigating.Remove(e.NavigationId);
			//	}

			//	// check if a body-tag is present
			//	ExecuteScriptAsync("document.getElementsByTagName(\"body\").item(0) && document.getElementsByTagName(\"body\").item(0).firstChild")
			//		.ContinueWith(t =>
			//		{
			//			SetHasBody(t.Result != "null");
			//		}
			//	);

			//	// request content height
			//	ExecuteScriptAsync("document.body.scrollHeight.toString()")
			//		.ContinueWith(t =>
			//		{
			//			SetContentHeight(Int32.TryParse(t.Result, out var contentHeight) ? contentHeight : -1);
			//		}
			//	);

			//}
		} // event WebView_NavigationCompleted

		private void WebView_PermissionRequested(object sender, CoreWebView2PermissionRequestedEventArgs e)
		{
			Debug.Print("Permission {0}: {1}", e.PermissionKind, e.Uri);
			e.State = CoreWebView2PermissionState.Deny;
		} // event WebView_PermissionRequested

		#endregion

		#region -- Redirect WebRequest to Shell http ----------------------------------

		private static string GetFilterUri(Uri uri)
			=> uri.Scheme + "://" + uri.Host + (uri.Port == 80 || uri.Port <= 0 ? String.Empty : ":" + uri.Port.ToString()) + "/*";

		private void UpdateResourceRequest()
		{
			if (shell.Value.Http != null && CoreWebView2 != null)
			{
				var http = shell.Value.Http;

				if (currentFilterUri != null)
					CoreWebView2.RemoveWebResourceRequestedFilter(currentFilterUri, CoreWebView2WebResourceContext.All);

				currentFilterUri = GetFilterUri(http.BaseAddress);
				CoreWebView2.AddWebResourceRequestedFilter(currentFilterUri, CoreWebView2WebResourceContext.All);
			}
		} // proc UpdateResourceRequest

		private async Task<bool> TryRedirectToInternalViewerAsync(HttpContent content)
		{
			// is content marked as attachment
			if (!content.TryGetExtensionFromContent(true, out var extension))
				return false;

			//// read external data
			//var data = await content.ReadAsByteArrayAsync();
			//if (String.Compare(extension, ".pdf", StringComparison.OrdinalIgnoreCase) == 0)
			//	await AppBase.Current.Navigation.PushAsync(ModulPage.Create("pdfviewer", new LuaTable { ["bytes"] = data }));

			return true;
		} // func TryRedirectToInternalViewerAsync

		private void SetResponse(CoreWebView2WebResourceRequestedEventArgs e, string responseHeader, int responseCode, string responseMessage, Stream responseStream)
		{
			#region var nativeArgs = e._nativeCoreWebView2PermissionRequestedEventArgs
			var eventType = e.GetType();
			var field = eventType.GetField("_nativeCoreWebView2WebResourceRequestedEventArgs", BindingFlags.NonPublic | BindingFlags.Instance);
			var nativeArgs = field.GetValue(e);
			#endregion
			#region var nativeEnvironment = Control.Environment._nativeCoreWebView2Environment
			var environment = (CoreWebView2Environment)GetType().GetProperty("Environment", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(this);
			field = environment.GetType().GetField("_nativeCoreWebView2Environment", BindingFlags.NonPublic | BindingFlags.Instance);
			var nativeEnvironment = field.GetValue(environment);
			#endregion

			#region var managedStream = new ManagedIStream(responseStream)
			var managedStream = Activator.CreateInstance(eventType.Assembly.GetType("Microsoft.Web.WebView2.Core.ManagedIStream"),
				BindingFlags.NonPublic | BindingFlags.Instance,
				null,
				new object[] { responseStream },
				null
			);
			#endregion
			#region var response = CreateWebResourceResponse(managedStream, responseCode, responseMessage, responseHeader)
			var method = eventType.Assembly.GetType("Microsoft.Web.WebView2.Core.Raw.ICoreWebView2Environment").GetMethod("CreateWebResourceResponse");
			var response = method.Invoke(nativeEnvironment, new object[] { managedStream, responseCode, responseMessage, responseHeader });
			#endregion

			#region e.Response = response
			var property = eventType.Assembly.GetType("Microsoft.Web.WebView2.Core.Raw.ICoreWebView2WebResourceRequestedEventArgs").GetProperty("Response");
			property.SetValue(nativeArgs, response);
			#endregion
		} // proc SetResponse

		private void SetEmptyResponse(CoreWebView2WebResourceRequestedEventArgs e)
			=> SetResponse(e, String.Empty, (int)HttpStatusCode.NoContent, "NoContent", new MemoryStream());

		private void SetErrorResponse(CoreWebView2WebResourceRequestedEventArgs e, Exception ex)
		{
			// todo: error-html laden
			SetResponse(e, String.Empty, 600, ex.Message, new MemoryStream());
		} // SetErrorResponse

		private void SetErrorResponse(CoreWebView2WebResourceRequestedEventArgs e, int responseCode, string text)
		{
			// todo: error-html laden
			SetResponse(e, String.Empty, responseCode, "Error", new MemoryStream());
		} // SetErrorResponse

		private async Task InterceptWebRequestAsync(CoreWebView2WebResourceRequestedEventArgs e, Uri uri)
		{
			// create a relative uri to the communication
			var http = shell.Value.Http;
			var relativeUri = http.BaseAddress.MakeRelativeUri(uri);
			var httpRequest = new HttpRequestMessage(HttpMethod.Get, relativeUri.ToString());

			var httpResponse = await http.SendAsync(httpRequest);

			if (httpResponse.IsSuccessStatusCode
				&& e.ResourceContext == CoreWebView2WebResourceContext.Document
				&& await TryRedirectToInternalViewerAsync(httpResponse.Content))
			{
				httpResponse.Dispose();
				SetEmptyResponse(e);
			}
			else if (httpResponse.StatusCode == HttpStatusCode.Moved
				|| httpResponse.StatusCode == HttpStatusCode.Found
				|| httpResponse.StatusCode == HttpStatusCode.TemporaryRedirect) // process move
			{
				var navUri = new Uri(uri, httpResponse.Headers.Location);
				//if (Controller != null)
				//{
				//	if (Controller.OnPreNavigateUrl(navUri.ToString()))
				//		SetEmptyResponse(e);
				//	else
				//		await InterceptWebRequestAsync(e, navUri);
				//}
				//else
					await InterceptWebRequestAsync(e, navUri);
			}
			else if (httpResponse.StatusCode == HttpStatusCode.InternalServerError)
			{
				var (image, text) = httpResponse.DecodeReasonPhrase();
				
				var ui = shell.Value.GetService<IPpsUIService>(false);
				if (ui != null && image.HasValue)
				{
					ui.ShowNotification(text, image.Value.ToPpsImage());
					SetEmptyResponse(e);
				}
				else
					SetErrorResponse(e, (int)HttpStatusCode.InternalServerError, text);
			}
			else
			{
				var headers = new StringBuilder();

				// concat headers
				foreach (var h in httpResponse.Headers)
				{
					headers.Append(h.Key)
					   .Append(": ")
					   .Append(String.Join("; ", h.Value))
					   .AppendLine();
				}
				foreach (var h in httpResponse.Content.Headers)
				{
					headers.Append(h.Key)
					   .Append(": ")
					   .Append(String.Join("; ", h.Value))
					   .AppendLine();
				}

				// set response
				SetResponse(e, headers.ToString(), (int)httpResponse.StatusCode, httpResponse.ReasonPhrase, await httpResponse.Content.ReadAsStreamAsync());
			}
		} // proc InterceptWebRequestAsync

		private async void WebView_WebResourceRequested(object sender, CoreWebView2WebResourceRequestedEventArgs e)
		{
#if DEBUG_NAV
			Debug.WriteLine("WebView_WebResourceRequested[{1}]: {0}", e.Request.RequestUri, e.ResourceContext);
#endif

			// get request uri
			var uri = new Uri(e.Request.Uri);
			if (!uri.IsAbsoluteUri)
				throw new ArgumentException("Only absolute uri's allowed.");

			using (var defer = e.GetDeferral())
			{
				try
				{
					await InterceptWebRequestAsync(e, uri);
				}
				catch (Exception ex)
				{
					SetErrorResponse(e, ex);
				}
			}
		} // event WebView_WebResourceRequested

		#endregion

	} // class PpsWebView
}
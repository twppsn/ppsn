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
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Neo.IronLua;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.Networking;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Controls
{
	#region -- class PpsWebViewHistoryItem --------------------------------------------

	/// <summary>History element</summary>
	public class PpsWebViewHistoryItem
	{
		public Uri Uri => null;
	} // class PpsWebViewHistoryItem

	#endregion

	#region -- class PpsWebViewNavigationEventArgs ------------------------------------

	public class PpsWebViewNavigationEventArgs : RoutedEventArgs
	{
		private readonly object uri;

		public PpsWebViewNavigationEventArgs(RoutedEvent routedEvent, object uri)
			: base(routedEvent)
		{
			this.uri = uri ?? throw new ArgumentNullException(nameof(uri));
		} // ctor

		/// <summary>Request uri</summary>
		public object Uri => uri;
	} // class PpsWebViewNavigationEventArgs

	#endregion

	#region -- class PpsWebViewNavigationCancelEventArgs ------------------------------

	public class PpsWebViewNavigationCancelEventArgs : PpsWebViewNavigationEventArgs
	{
		private bool? cancel = null;

		public PpsWebViewNavigationCancelEventArgs(RoutedEvent routedEvent, object uri)
			: base(routedEvent, uri)
		{
		} // ctor

		/// <summary>Cancel the request.</summary>
		public bool Cancel { get => cancel ?? false; set => cancel = value; }
		/// <summary>Is cancel setted.</summary>
		public bool IsHandled => cancel.HasValue;
	} // class PpsWebViewNavigationCancelEventArgs

	#endregion

	#region -- class PpsWebViewNavigationStartedEventArgs -----------------------------

	public class PpsWebViewNavigationStartedEventArgs : PpsWebViewNavigationCancelEventArgs
	{
		private readonly bool newWindow;

		public PpsWebViewNavigationStartedEventArgs(object uri, bool newWindow = false)
			: base(PpsWebView.NavigationStartedEvent, uri)
		{
			this.newWindow = newWindow;
		} // ctor

		/// <summary>Request will try create a new window.</summary>
		public bool NewWindow => newWindow;
	} // class PpsWebViewNavigationStartedEventArgs

	public delegate void PpsWebViewNavigationStartedHandler(object sender, PpsWebViewNavigationStartedEventArgs e);

	#endregion

	#region -- class PpsWebViewNavigationContentEventArgs -----------------------------

	public class PpsWebViewNavigationContentEventArgs : PpsWebViewNavigationCancelEventArgs
	{
		public PpsWebViewNavigationContentEventArgs(object uri, HttpContent content)
			: base(PpsWebView.NavigationContentEvent, uri)
		{
			Content = content ?? throw new ArgumentNullException(nameof(content));
		} // ctor

		public HttpContent Content { get; }
	} // class PpsWebViewNavigationContentEventArgs

	public delegate void PpsWebViewNavigationContentHandler(object sender, PpsWebViewNavigationContentEventArgs e);

	#endregion

	#region -- class PpsWebViewNavigationCompletedEventArgs ---------------------------

	public class PpsWebViewNavigationCompletedEventArgs : PpsWebViewNavigationEventArgs
	{
		public PpsWebViewNavigationCompletedEventArgs(object uri, bool isCanceled)
			: base(PpsWebView.NavigationCompletedEvent, uri)
		{
			IsCanceled = isCanceled;
		} // ctor

		public bool IsCanceled { get; }
	} // class PpsWebViewNavigationCompletedEventArgs

	public delegate void PpsWebViewNavigationCompletedHandler(object sender, PpsWebViewNavigationCompletedEventArgs e);

	#endregion

	#region -- class PpsWebViewNavigationXamlCodeEventArgs ----------------------------

	public class PpsWebViewNavigationXamlCodeEventArgs : PpsWebViewNavigationEventArgs
	{
		public PpsWebViewNavigationXamlCodeEventArgs(object uri) 
			: base(PpsWebView.NavigationXamlCodeEvent, uri)
		{
		} // ctor

		public IPpsLuaCodeBehind Code { get; set; } = null;
	} // class PpsWebViewNavigationXamlCodeEventArgs

	public delegate void PpsWebViewNavigationXamlCodeHandler(object sender, PpsWebViewNavigationXamlCodeEventArgs e);

	#endregion

	/// <summary>WebView that can seemless switch between html/xaml-content.</summary>
	public class PpsWebView : FrameworkElement
	{
		#region -- enum ViewState -----------------------------------------------------

		private enum ViewState
		{
			Empty,
			Xaml,
			Html
		} // enum ViewState

		#endregion

		/// <summary>Is raised on the start of an navigation.</summary>
		public static readonly RoutedEvent NavigationStartedEvent = EventManager.RegisterRoutedEvent(nameof(NavigationStarted), RoutingStrategy.Bubble, typeof(PpsWebViewNavigationStartedHandler), typeof(PpsWebView));
		/// <summary>Is raised after the http request is finished.</summary>
		public static readonly RoutedEvent NavigationContentEvent = EventManager.RegisterRoutedEvent(nameof(NavigationContent), RoutingStrategy.Bubble, typeof(PpsWebViewNavigationContentHandler), typeof(PpsWebView));
		/// <summary></summary>
		public static readonly RoutedEvent NavigationXamlCodeEvent = EventManager.RegisterRoutedEvent(nameof(NavigationXamlCode), RoutingStrategy.Bubble, typeof(PpsWebViewNavigationXamlCodeHandler), typeof(PpsWebView));
		/// <summary>Is raised on the end of an navigation.</summary>
		public static readonly RoutedEvent NavigationCompletedEvent = EventManager.RegisterRoutedEvent(nameof(NavigationCompleted), RoutingStrategy.Bubble, typeof(PpsWebViewNavigationCompletedHandler), typeof(PpsWebView));

		/// <summary>Is raised on the start of an navigation.</summary>
		public event PpsWebViewNavigationStartedHandler NavigationStarted { add => AddHandler(NavigationStartedEvent, value); remove => RemoveHandler(NavigationStartedEvent, value); }
		/// <summary>Is raised after the http request is finished.</summary>
		public event PpsWebViewNavigationContentHandler NavigationContent { add => AddHandler(NavigationContentEvent, value); remove => RemoveHandler(NavigationContentEvent, value); }
		/// <summary></summary>
		public event PpsWebViewNavigationXamlCodeHandler NavigationXamlCode { add => AddHandler(NavigationXamlCodeEvent, value); remove => RemoveHandler(NavigationXamlCodeEvent, value); }
		/// <summary>Is raised on the end of an navigation.</summary>
		public event PpsWebViewNavigationCompletedHandler NavigationCompleted { add => AddHandler(NavigationCompletedEvent, value); remove => RemoveHandler(NavigationCompletedEvent, value); }

		private Lazy<IPpsShell> shell;

		private readonly WebView2 htmlView;
		private readonly ContentControl xamlView;
		private readonly object[] logicalChilds;

		private ViewState viewState = ViewState.Empty;
		private readonly ObservableCollection<PpsWebViewHistoryItem> history = new ObservableCollection<PpsWebViewHistoryItem>();

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		public PpsWebView()
		{
			htmlView = new WebView2();
			xamlView = new ContentControl();

			AddLogicalChild(htmlView);
			AddLogicalChild(xamlView);

			logicalChilds = new object[] { htmlView, xamlView };

			htmlView.CoreWebView2Ready += HtmlView_CoreWebView2Ready;

			SetValue(historyPropertyKey, history);
		} // ctor

		protected override void OnInitialized(EventArgs e)
		{
			base.OnInitialized(e);
			ResetGetShell();
		} // proc OnInitialized

		protected override void OnVisualParentChanged(DependencyObject oldParent)
		{
			base.OnVisualParentChanged(oldParent);
			ResetGetShell();
		} // proc OnVisualParentChanged

		private void ResetGetShell()
			=> shell = new Lazy<IPpsShell>(GetShellLazy);

		private IPpsShell GetShellLazy()
		{
			var shell = this.GetShell();
			UpdateResourceRequest(shell);
			return shell;
		} // func GetShellLazy

		#endregion

		#region -- Childs -------------------------------------------------------------

		private void SetViewState(ViewState newState)
		{
			if (newState != viewState)
			{
				switch (viewState)
				{
					case ViewState.Html:
						HideHtml();
						break;
					case ViewState.Xaml:
						HideXaml();
						break;
				}
				viewState = newState;
				switch (viewState)
				{
					case ViewState.Html:
						ShowHtml();
						break;
					case ViewState.Xaml:
						ShowXaml();
						break;
				}

				InvalidateMeasure();
			}
		} // proc SetViewState

		protected override Visual GetVisualChild(int index)
		{
			switch (viewState)
			{
				case ViewState.Html:
					return htmlView;
				case ViewState.Xaml:
					return xamlView;
				default:
					return null;
			}
		} // GetVisualChild

		protected override Size MeasureOverride(Size availableSize)
		{
			switch (viewState)
			{
				case ViewState.Html:
					htmlView.Measure(availableSize);
					return htmlView.DesiredSize;
				case ViewState.Xaml:
					xamlView.Measure(availableSize);
					return xamlView.DesiredSize;
				default:
					return base.MeasureOverride(availableSize);
			}
		} // func MeasureOverride

		protected override Size ArrangeOverride(Size finalSize)
		{
			switch (viewState)
			{
				case ViewState.Html:
					htmlView.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
					return htmlView.RenderSize;
				case ViewState.Xaml:
					xamlView.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
					return xamlView.RenderSize;
				default:
					return base.ArrangeOverride(finalSize);
			}
		} // func ArrangeOverride

		protected override IEnumerator LogicalChildren
			=> Procs.CombineEnumerator(base.LogicalChildren, logicalChilds.GetEnumerator());

		protected override int VisualChildrenCount => viewState == ViewState.Empty ? 0 : 1;

		#endregion

		#region -- Html-View ----------------------------------------------------------

		private string currentFilterUri = null;
		private readonly Dictionary<ulong, ViewNavigationToken> htmlTokens = new Dictionary<ulong, ViewNavigationToken>();
		private ViewResponseMessage cachedResponseMessage = null;

		private void HtmlView_DocumentTitleChanged(object sender, object e)
		{
			if (sender is CoreWebView2 nWebView)
				Title = nWebView.DocumentTitle;
		} // event HtmlView_DocumentTitleChanged

		private void HtmlView_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
		{
#if DEBUG_NAV
			Debug.WriteLine("WebView_NavigationStarting[{0}]: {1} (user={2}, redirect={3})", e.NavigationId, e.Uri, e.IsUserInitiated, e.IsRedirected);
#endif
			if (e.IsUserInitiated && TryGetNavigationToken(out var token))
			{
				htmlTokens[e.NavigationId] = token;

				if (token.OnStarted(e.Uri, false, true))
					e.Cancel = false;
				else
				{
					token.Dispose();
					e.Cancel = true;
				}
			}
			else
				e.Cancel = false;
		} // event HtmlView_NavigationStarting

		private void HtmlView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
		{
#if DEBUG_NAV
			Debug.WriteLine("WebView_NavigationCompleted[{0}]: {1} (code={2})", e.NavigationId, e.IsSuccess, e.WebErrorStatus);
#endif
			if (htmlTokens.TryGetValue(e.NavigationId, out var token))
			{
				token.Dispose();
				htmlTokens.Remove(e.NavigationId);
			}

			// check if a body-tag is present
			htmlView.ExecuteScriptAsync("document.getElementsByTagName(\"body\").item(0) && document.getElementsByTagName(\"body\").item(0).firstChild")
				.ContinueWith(t =>
				{
					HasContent = t.Result != "null";
				},
				TaskContinuationOptions.ExecuteSynchronously
			);

			// request content height
			htmlView.ExecuteScriptAsync("document.body.scrollHeight.toString()")
				.ContinueWith(t =>
				{
					ContentHeight = Int32.TryParse(t.Result, out var contentHeight) ? contentHeight : -1;
				},
				TaskContinuationOptions.ExecuteSynchronously
			);
		} // event HtmlView_NavigationCompleted

		private void HtmlView_NewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
		{
			throw new NotImplementedException("todo: implement new window");
			// e.Handled = true;
		} // event HtmlView_NewWindowRequested

		private void HtmlView_WindowCloseRequested(object sender, object e)
		{
		} // event HtmlView_WindowCloseRequested

		private void HtmlView_PermissionRequested(object sender, CoreWebView2PermissionRequestedEventArgs e)
		{
			Debug.Print("Permission {0}: {1}", e.PermissionKind, e.Uri);
			e.State = CoreWebView2PermissionState.Deny;
		} // event HtmlView_PermissionRequested

		private static string GetFilterUri(Uri uri)
			=> uri.Scheme + "://" + uri.Host + (uri.Port == 80 || uri.Port <= 0 ? String.Empty : ":" + uri.Port.ToString()) + "/*";

		private void UpdateResourceRequest(IPpsShell shell)
		{
			if (shell.Http != null && htmlView.CoreWebView2 != null)
			{
				var http = shell.Http;

				if (currentFilterUri != null)
					htmlView.CoreWebView2.RemoveWebResourceRequestedFilter(currentFilterUri, CoreWebView2WebResourceContext.All);

				currentFilterUri = GetFilterUri(http.BaseAddress);
				htmlView.CoreWebView2.AddWebResourceRequestedFilter(currentFilterUri, CoreWebView2WebResourceContext.All);
			}
		} // proc UpdateResourceRequest

		private void SetHtmlResponse(CoreWebView2WebResourceRequestedEventArgs e, string responseHeader, int responseCode, string responseMessage, Stream responseStream)
		{
			e.Response = htmlView.CoreWebView2.Environment.CreateWebResourceResponse(
				responseStream,
				responseCode,
				responseMessage,
				responseHeader
			);
		} // proc SetHtmlResponse

		private void SetHtmlEmptyResponse(CoreWebView2WebResourceRequestedEventArgs e)
			=> SetHtmlResponse(e, String.Empty, (int)HttpStatusCode.NoContent, "NoContent", null);

		private static string CreateHttpHeaders(IEnumerable<KeyValuePair<string, string>> headers)
		{
			var sb = new StringBuilder();

			// concat headers
			foreach (var h in headers)
			{
				sb.Append(h.Key)
				   .Append(": ")
				   .Append(h.Value)
				   .AppendLine();
			}

			return sb.ToString();
		} // proc CreateHttpHeaders

		private static KeyValuePair<string, string> ConvertHttpHeader(KeyValuePair<string, IEnumerable<string>> h)
			=> new KeyValuePair<string, string>(h.Key, String.Join("; ", h.Value));

		private static bool TryAddToContentHeader(HttpContent content, string key, IEnumerable<string> value)
			=> false;

		private async Task HtmlViewSetMainResourceAsync(ViewNavigationToken token, CoreWebView2WebResourceRequestedEventArgs e, ViewResponseMessage response)
		{
			if (response.State == ViewState.Html)
			{
				var responseHeaders = (IEnumerable<KeyValuePair<string, string>>)response.Headers;
				if (response.Content != null)
					responseHeaders = responseHeaders.Concat(response.Content.Headers.Select(ConvertHttpHeader));

				// set response
				SetHtmlResponse(e,
					CreateHttpHeaders(responseHeaders),
					200,
					"OK",
					await response.Content.ReadAsStreamAsync()
				);
			}
			else // set other response and cancel request
			{
				await SetResponseMessageAsync(token, response);
				SetHtmlEmptyResponse(e);
			}
		} // proc HtmlViewSetMainResourceAsync

		private async Task HtmlViewSetMainResourceAsync(ViewNavigationToken token, CoreWebView2WebResourceRequestedEventArgs e, Uri uri, Uri relativeUri)
		{
			try
			{
				if (cachedResponseMessage != null && cachedResponseMessage.SourceUri == uri)
					await HtmlViewSetMainResourceAsync(token, e, cachedResponseMessage);
				else
				{
					using (var request = new ViewRequestMessage(e.Request.Method, relativeUri, null, null))
					{
						// send the shell request
						var response = await SendShellAsync(token, request); // todo: check Dispose

						// set result
						await HtmlViewSetMainResourceAsync(token, e, response);
					}
				}
				cachedResponseMessage = null;
			}
			catch (Exception ex)
			{
				SetHtmlEmptyResponse(e);
				await SetResponseMessageAsync(currentNavigationToken, new ViewResponseMessage(uri, ex));
			}
		} // proc HtmlViewSetMainResourceAsync

		private async Task HtmlViewSetOtherResourceAsync(CoreWebView2WebResourceRequestedEventArgs e, Uri uri)
		{
			if (TryGetHttp(out var http))
			{
				var request = new HttpRequestMessage(new HttpMethod(e.Request.Method), uri)
				{
					Content = e.Request.Content != null ? new StreamContent(e.Request.Content) : null
				};

				// copy headers for request
				foreach (var h in e.Request.Headers)
				{
					var values = h.Value.Split(';');
					if (!TryAddToContentHeader(request.Content, h.Key, values))
						request.Headers.TryAddWithoutValidation(h.Key, values);
				}

				var response = await SendCoreAsync(http, request, false); // todo: check Dispose

				// copy headers for response
				var responseHeaders = response.Headers.Select(ConvertHttpHeader);
				if (response.Content != null)
					responseHeaders = responseHeaders.Concat(response.Content.Headers.Select(ConvertHttpHeader));

				SetHtmlResponse(e, CreateHttpHeaders(responseHeaders), (int)response.StatusCode, response.ReasonPhrase, await response.Content.ReadAsStreamAsync());
			}
			else
				throw new InvalidOperationException();
		} // proc HtmlViewSetOtherResourceAsync

		private async void HtmlView_WebResourceRequested(object sender, CoreWebView2WebResourceRequestedEventArgs e)
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
				// get the current navigation token
				ViewNavigationToken token = null;
				if (e.ResourceContext == CoreWebView2WebResourceContext.Document) // is this the main document
					token = htmlTokens.Values.Where(c => c.IsActive).FirstOrDefault();

				if (TryGetRelativePath(uri, out var relativeUri))
				{
					if (token == null)
						await HtmlViewSetOtherResourceAsync(e, uri);
					else
						await HtmlViewSetMainResourceAsync(token, e, uri, relativeUri);
				}
				else
					throw new ArgumentException("Uri format is wrong.");
			}
		} // event HtmlView_WebResourceRequested

		private void HtmlView_CoreWebView2Ready(object sender, EventArgs e)
		{
			var v = htmlView.CoreWebView2;
#if !DEBUG
			v.Settings.AreDefaultContextMenusEnabled = false;
			v.Settings.AreDevToolsEnabled = false;
#endif
			v.Settings.IsWebMessageEnabled = false;
			v.Settings.IsZoomControlEnabled = false;

			v.DocumentTitleChanged += HtmlView_DocumentTitleChanged;
			// v.HistoryChanged += ;
			v.NavigationStarting += HtmlView_NavigationStarting;
			v.NavigationCompleted += HtmlView_NavigationCompleted;
			v.NewWindowRequested += HtmlView_NewWindowRequested;
			v.PermissionRequested += HtmlView_PermissionRequested;
			v.WindowCloseRequested += HtmlView_WindowCloseRequested;
			v.WebResourceRequested += HtmlView_WebResourceRequested;

			UpdateResourceRequest(shell.Value);
		} // event HtmlView_CoreWebView2Ready

		private void ShowHtml()
		{
			AddVisualChild(htmlView);
		} // proc ShowHtml

		private async Task SetHtmlAsync(Uri uri, ViewResponseMessage message)
		{
			SetViewState(ViewState.Html);

			// sets also sourceUri
			if (htmlView.CoreWebView2 == null)
				await htmlView.EnsureCoreWebView2Async();

			cachedResponseMessage = message;
			htmlView.Source = uri;

			SetValue(sourceUriPropertyKey, uri);
		} // proc SetHtmlAsync

		private void HideHtml()
		{
			if (htmlView.CoreWebView2 != null)
				htmlView.CoreWebView2.Navigate("about:blank");
			RemoveVisualChild(htmlView);
		} // proc HideHtml

		#endregion

		#region -- Xaml - view --------------------------------------------------------

		private void ShowXaml()
		{
			AddVisualChild(xamlView);
		} // proc ShowXaml

		private async Task<FrameworkElement> GetXamlControlAsync(Uri sourceUri, HttpContent content)
		{
			using (var xml = XmlReader.Create(await content.ReadAsStreamAsync(), Procs.XmlReaderSettings))
			{
				// get the code modul for the control
				var e = new PpsWebViewNavigationXamlCodeEventArgs(sourceUri);
				RaiseEvent(e);

				// create the control
				var control = await PpsXamlParser.LoadAsync<FrameworkElement>(xml, new PpsXamlReaderSettings { BaseUri = sourceUri, Code = e.Code });

				if (e.Code != null)
				{
					// copy arguments
					var args = new LuaTable();
					foreach (var kv in sourceUri.ParseQuery().Cast<DictionaryEntry>())
						args[kv.Key] = kv.Value;

					// mark control as created
					e.Code.OnControlCreated(control, args);
				}

				return control;
			}
		} // func GetXamlControlAsync

		private async Task SetXamlAsync(Uri sourceUri, object content, DataTemplate template)
		{
			if (content is HttpContent httpContent)
			{
				content = await GetXamlControlAsync(sourceUri, httpContent);
				template = null;
			}

			SetViewState(ViewState.Xaml);

			xamlView.Content = content;
			xamlView.ContentTemplate = template;

			SetValue(sourceUriPropertyKey, sourceUri);
		} // proc SetXamlAsync

		private void HideXaml()
		{
			xamlView.Content = null;
			xamlView.ContentTemplate = null;

			RemoveVisualChild(xamlView);
		} // proc HideXaml

		#endregion

		#region -- Redirect WebRequest to Shell http ----------------------------------

		private bool TryRedirectToPaneDefault(object uri, HttpContent content)
		{
			if (content == null)
			{
				return false;
			}
			else
			{
				// is content marked as attachment
				if (!content.TryGetExtensionFromContent(true, out var mimeType, out var _))
					return false;

				// find pane register
				var paneRegistrar = this.GetControlService<IPpsKnownWindowPanes>(false);
				if (paneRegistrar == null)
					return false;

				// find pane managar
				var paneManager = this.GetControlService<IPpsWindowPaneManager>(false);
				if (paneManager == null)
					return false;

				// find pane type
				var paneType = paneRegistrar.GetPaneTypeMimeType(mimeType, false);
				if (paneType == null)
					return false;

				// todo: create IPpsDataObject
				paneManager.OpenPaneAsync(paneType, arguments: new LuaTable { ["Object"] = content.ReadAsByteArrayAsync().Await() }).Spawn(paneManager);
				return true;
			}
		} // proc TryRedirectToPaneDefault

		#endregion

		#region -- Core request methods -----------------------------------------------

		#region -- class ViewRequestMessage -------------------------------------------

		private sealed class ViewRequestMessage : IDisposable
		{
			private readonly string method;
			private readonly Uri relativeUri;
			private readonly HttpContent content;
			private readonly KeyValuePair<string, string>[] headers;

			public ViewRequestMessage(string method, Uri relativeUri, HttpContent content, IEnumerable<KeyValuePair<string, string>> headers)
			{
				this.method = method ?? throw new ArgumentNullException(nameof(method));
				this.relativeUri = relativeUri ?? throw new ArgumentNullException(nameof(relativeUri));

				if (relativeUri.IsAbsoluteUri)
					throw new ArgumentException("Relative uri expected.");

				this.content = content;
				this.headers = headers?.ToArray();
			} // ctor

			public void Dispose()
				=> content?.Dispose();

			public string Method => method;
			public Uri Uri => relativeUri;
			public HttpContent Content => content;
			public IReadOnlyList<KeyValuePair<string, string>> Headers => headers;
		} // class ViewRequestMessage

		#endregion

		#region -- class ViewResponseMessage ------------------------------------------

		private sealed class ViewResponseMessage : IDisposable
		{
			private readonly ViewState state;
			private readonly Uri sourceUri;
			private readonly Exception exception = null;
			private readonly HttpContent content = null;
			private readonly KeyValuePair<string, string>[] headers;

			public ViewResponseMessage(Uri sourceUri)
			{
				state = ViewState.Empty;
				this.sourceUri = sourceUri;
			} // ctor

			public ViewResponseMessage(Uri sourceUri, Exception exception)
			{
				state = ViewState.Xaml;
				this.sourceUri = sourceUri;
				this.exception = exception ?? throw new ArgumentNullException(nameof(exception));
			} // ctor

			public ViewResponseMessage(Uri sourceUri, HttpResponseMessage message)
			{
				this.sourceUri = sourceUri ?? throw new ArgumentNullException(nameof(sourceUri));

				switch (message.StatusCode)
				{
					case HttpStatusCode.NoContent:
						content = null;
						headers = null;

						state = ViewState.Empty;
						break;
					case HttpStatusCode.OK:
						content = message.Content;
						headers = message.Headers.Select(c => new KeyValuePair<string, string>(c.Key, String.Join(";", c.Value))).ToArray();

						state = content != null && content.Headers.ContentType != null && content.Headers.ContentType.MediaType == MimeTypes.Application.Xaml ? ViewState.Xaml : ViewState.Html;
						break;
					default:
						throw new ArgumentException("Only successful requests allowed.");
				}
			} // ctor

			public void Dispose()
			{
				content?.Dispose();
			} // proc Dispose

			public ViewState State => state;
			public Uri SourceUri => sourceUri;
			public Exception Exception => exception;

			public HttpContent Content => content;
			public IReadOnlyList<KeyValuePair<string, string>> Headers => headers;
		} // class ViewResponseMessage

		#endregion

		private DataTemplate GetErrorTemplate()
			=> null;

		private bool TryGetRelativePath(Uri absoluteUri, out Uri relativeUri)
		{
			if (absoluteUri.IsFile)
			{
				relativeUri = absoluteUri;
				return true;
			}
			else if (TryGetHttp(out var http))
			{
				relativeUri = http.BaseAddress.MakeRelativeUri(absoluteUri);
				return !relativeUri.IsAbsoluteUri;
			}
			else
			{
				relativeUri = absoluteUri;
				return false;
			}
		} // func TryGetRelativePath

		private async Task<HttpResponseMessage> SendCoreAsync(DEHttpClient http, HttpRequestMessage request, bool throwException)
		{
			var response = await http.SendAsync(request);

			if (response.StatusCode == HttpStatusCode.Moved
				|| response.StatusCode == HttpStatusCode.MovedPermanently
				|| response.StatusCode == HttpStatusCode.Found
				|| response.StatusCode == HttpStatusCode.TemporaryRedirect) // process redirect
			{
				var navUri = new Uri(request.RequestUri, response.Headers.Location);
				var navRequest = new HttpRequestMessage(request.Method, navUri)
				{
					Content = request.Content
				};
				return await SendCoreAsync(http, navRequest, throwException);
			}
			else if (throwException && response.StatusCode != HttpStatusCode.OK) // process exception
			{
				var (severity, msg) = response.DecodeReasonPhrase();

				var ui = shell.Value.GetService<IPpsUIService>(false);
				if (ui != null && severity.HasValue)
				{
					ui.ShowNotificationAsync(msg, severity.Value.ToPpsImage()).Silent();
					return new HttpResponseMessage(HttpStatusCode.NoContent);
				}
				else
					throw new HttpResponseException(response);
			}
			else
				return response;
		} // proc SendCoreAsync

		private async Task<ViewResponseMessage> SendShellAsync(ViewNavigationToken token, ViewRequestMessage request)
		{
			if (!TryGetHttp(out var http))
				throw new InvalidOperationException();

			var sourceUri = new Uri(http.BaseAddress, request.Uri);
			using (var httpRequest = new HttpRequestMessage(new HttpMethod(request.Method), sourceUri))
			{
				var httpResponse = await SendCoreAsync(http, httpRequest, true);
				var e = new PpsWebViewNavigationContentEventArgs(request.Uri, httpResponse.Content);
				OnNavigationContent(e);
				if (e.Cancel)
				{
					token.NoHistory();
					return new ViewResponseMessage(httpResponse.RequestMessage.RequestUri);
				}

				return new ViewResponseMessage(httpResponse.RequestMessage.RequestUri, httpResponse);
			}
		} // proc SendShellAsync

		private Task SetResponseMessageAsync(ViewNavigationToken token, ViewResponseMessage response)
		{
			if (!token.IsActive)
				return Task.CompletedTask;

			if (response.Exception != null)
				return SetXamlAsync(null, response.Exception, GetErrorTemplate());
			else
			{
				switch (response.State)
				{
					case ViewState.Html:
						return SetHtmlAsync(response.SourceUri, response);
					case ViewState.Xaml:
						return SetXamlAsync(response.SourceUri, response.Content, null);
					default:
						SetViewState(ViewState.Empty);
						HasContent = false;
						Title = null;
						return Task.CompletedTask;
				}
			}
		} // proc SetResponseMessageAsync

		#endregion

		#region -- Navigation - management --------------------------------------------

		#region -- class ViewNavigationToken ------------------------------------------

		private sealed class ViewNavigationToken : IDisposable
		{
			private readonly PpsWebView webView;
			private object uri = null;
			private bool addToHistory = false;

			private ViewNavigationToken()
			{
				webView = null;
			} // ctor

			public ViewNavigationToken(PpsWebView webView)
			{
				this.webView = webView ?? throw new ArgumentNullException(nameof(webView));
			} // ctor

			public void Dispose()
			{
				// clear navigation
				if (ReferenceEquals(this, Interlocked.CompareExchange(ref webView.currentNavigationToken, null, this)) && IsStarted)
					OnCompleted(false);
			} // proc Dispose

			public bool OnStarted(object uri, bool newWindow, bool appendHistory)
			{
				var e = new PpsWebViewNavigationStartedEventArgs(uri.ToString(), newWindow);
				if (IsActive)
				{
					webView.OnNavigationStarted(e);
					if (e.Cancel)
						return false;
					else
					{
						addToHistory = appendHistory && !newWindow;

						this.uri = uri;
						return !e.Cancel;
					}
				}
				else
					return false;
			} // proc OnStarted

			private void OnCompleted(bool isCanceled)
			{
				webView.OnNavigationCompleted(new PpsWebViewNavigationCompletedEventArgs(uri, isCanceled));
				if (addToHistory)
					webView.AppendHistory(uri);
			} // proc OnCompleted

			public void Cancel()
			{
				if (IsStarted)
				{
					OnCompleted(true);
					uri = null;
				}
			} // proc Cancel

			public void NoHistory()
				=> addToHistory = false;

			public bool IsStarted => uri != null;
			public bool IsActive => webView != null && ReferenceEquals(webView.currentNavigationToken, this);

			public static ViewNavigationToken Empty { get; } = new ViewNavigationToken();
		} // class ViewNavigationToken

		#endregion

		private ViewNavigationToken currentNavigationToken = null;

		private void OnNavigationStarted(PpsWebViewNavigationStartedEventArgs e)
		{
			RaiseEvent(e);

			if (!e.IsHandled && TryRedirectToPaneDefault(e.Uri, null))
				e.Cancel = true;
		} // proc OnNavigationStarted

		private void OnNavigationContent(PpsWebViewNavigationContentEventArgs e)
		{
			RaiseEvent(e);
			if (!e.IsHandled && TryRedirectToPaneDefault(e.Uri, e.Content))
				e.Cancel = true;
		} // proc OnRedirectNavigationContent

		private void OnNavigationCompleted(PpsWebViewNavigationCompletedEventArgs e)
			=> RaiseEvent(e);

		private bool TryGetNavigationToken(out ViewNavigationToken token)
		{
			token = new ViewNavigationToken(this);
			Interlocked.CompareExchange(ref currentNavigationToken, token, null);
			return token.IsActive;
		} // proc TryGetNavigationToken

		private ViewNavigationToken EnforceNavigationToken()
		{
			var token = new ViewNavigationToken(this);
			var oldToken = Interlocked.Exchange(ref currentNavigationToken, token);
			if (oldToken != null)
				oldToken.Cancel();
			return token;
		} // func EnforceNavigationToken

		#endregion

		#region -- Source - management ------------------------------------------------

		private bool TryGetHttp(out DEHttpClient http)
		{
			http = shell.Value.Http;
			return http != null;
		} // func TryGetHttp

		private void AppendHistory(object uri)
		{

		} // proc AppendHistory

		private void UpdateHistroyIndex(PpsWebViewHistoryItem item)
		{
		} // proc UpdateHistroyIndex

		private void ClearHistory()
		{
			SetValue(curentHistoryIndexPropertyKey, -1);
			SetValue(canForwardPropertyKey, BooleanBox.False);
			SetValue(canBackPropertyKey, BooleanBox.False);

			history.Clear();
		} // proc ClearHistory

		public Task GoForwardAsync()
		{
			return Task.CompletedTask;
		} // proc GoForwardAsync

		public Task GoBackAsync()
		{
			return Task.CompletedTask;
		} // proc GoBackAsync

		private async Task SetExceptionCoreAsync(ViewNavigationToken token, Exception ex)
		{
			if (token.IsActive)
			{
				using (var response = new ViewResponseMessage(null, ex))
					await SetResponseMessageAsync(token, response);
			}
			shell.Value.LogProxy().LogMsg(LogMsgType.Error, ex);
		} // proc SetExceptionAsync

		public async Task SetExceptionAsync(Exception ex)
		{
			using (var token = EnforceNavigationToken())
			{
				if (!token.OnStarted("xaml:exception", false, false))
					return;

				await SetExceptionCoreAsync(token, ex);
			}
		} // proc SetExceptionAsync

		public async Task SetUriAsync(Uri newUri, bool setAsHome = true)
		{
			if (setAsHome)
				ClearHistory();

			if (newUri == null) // blank site
				SetViewState(ViewState.Empty);
			else if (!newUri.IsAbsoluteUri)
				throw new ArgumentException(nameof(newUri));
			else if (TryGetRelativePath(newUri, out var relativeUri)) // start web request on shell
			{
				using (var token = EnforceNavigationToken())
				{
					if (!token.OnStarted(newUri, false, true))
						return;

					try
					{
						using (var request = new ViewRequestMessage(HttpMethod.Get.Method, relativeUri, null, null))
							await SetResponseMessageAsync(token, await SendShellAsync(token, request));
					}
					catch (HttpResponseException e)
					{
						await SetExceptionCoreAsync(token, e);
					}
					catch (Exception e)
					{
						await SetExceptionCoreAsync(token, e);
					}
				}
			}
			else // activate html view
				await SetHtmlAsync(newUri, null);
		} // proc SetUriAsync

		private void SetUri(Uri uri)
		{
			SetUriAsync(uri)
				.ContinueWith(t => Source = t.Exception.InnerException, TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted);
		} // proc SetUri

		#endregion

		#region -- Source - property --------------------------------------------------

		private static readonly DependencyPropertyKey sourceUriPropertyKey = DependencyProperty.RegisterReadOnly(nameof(SourceUri), typeof(Uri), typeof(PpsWebView), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty SourceUriProperty = sourceUriPropertyKey.DependencyProperty;

		public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(nameof(Source), typeof(object), typeof(PpsWebView), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnSourceChanged)));

		private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsWebView)d).OnSourceChanged(e.NewValue);

		private void OnSourceChanged(object newSource)
		{
			switch (newSource)
			{
				case null:
					SetUri(null);
					break;
				case string path:
					if (Path.IsPathRooted(path)) // create file uri
						SetUri(new Uri("file://" + path.Replace(Path.DirectorySeparatorChar, '/')));
					else if (Uri.TryCreate(path, UriKind.Absolute, out var tmp))
						SetUri(tmp);
					else if (TryGetHttp(out var http))
						SetUri(http.CreateFullUri(path));
					break;
				case Uri uri:
					if (uri.IsAbsoluteUri)
						SetUri(uri);
					else if (TryGetHttp(out var http))
						SetUri(new Uri(http.BaseAddress, uri));
					else
						throw new ArgumentException("Absolute uri expected.");
					break;
				case PpsWebViewHistoryItem his:
					UpdateHistroyIndex(his);
					SetUri(his.Uri);
					break;
				case Exception ex:
					SetExceptionAsync(ex).Silent();
					break;
				default:
					throw new ArgumentException("Unsupported source type.", nameof(Source));
			}
		} // proc OnSourceChanged

		public object Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

		public Uri SourceUri => (Uri)GetValue(SourceUriProperty);

		#endregion

		#region -- HasContent, ContentHeight - property -------------------------------

		private static readonly DependencyPropertyKey hasContentPropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasContent), typeof(bool), typeof(PpsWebView), new FrameworkPropertyMetadata(BooleanBox.False));
		private static readonly DependencyPropertyKey contentHeightPropertyKey = DependencyProperty.RegisterReadOnly(nameof(ContentHeight), typeof(int), typeof(PpsWebView), new FrameworkPropertyMetadata(-1));

		/// <summary>Has this control content.</summary>
		public static readonly DependencyProperty HasContentProperty = hasContentPropertyKey.DependencyProperty;
		/// <summary>Current content height</summary>
		public static readonly DependencyProperty ContentHeightProperty = contentHeightPropertyKey.DependencyProperty;

		/// <summary>Has this control content.</summary>
		public bool HasContent { get => BooleanBox.GetBool(GetValue(HasContentProperty)); private set => SetValue(hasContentPropertyKey, BooleanBox.GetBool(value)); }
		/// <summary>Current content height</summary>
		public int ContentHeight { get => (int)GetValue(ContentHeightProperty); private set => SetValue(contentHeightPropertyKey, value); }

		#endregion

		#region -- Title - property ---------------------------------------------------

		/// <summary>Title of the current content.</summary>
		public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(PpsWebView), new FrameworkPropertyMetadata(null));

		/// <summary>Title of the current content.</summary>
		public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

		#endregion

		#region -- CanForward - property ----------------------------------------------

		private static readonly DependencyPropertyKey canForwardPropertyKey = DependencyProperty.RegisterReadOnly(nameof(CanForward), typeof(bool), typeof(PpsWebView), new FrameworkPropertyMetadata(BooleanBox.False));
		public static readonly DependencyProperty CanForwardProperty = canForwardPropertyKey.DependencyProperty;

		public bool CanForward => BooleanBox.GetBool(GetValue(CanForwardProperty));

		#endregion

		#region -- CanBack - property -------------------------------------------------

		private static readonly DependencyPropertyKey canBackPropertyKey = DependencyProperty.RegisterReadOnly(nameof(CanBack), typeof(bool), typeof(PpsWebView), new FrameworkPropertyMetadata(BooleanBox.False));
		public static readonly DependencyProperty CanBackProperty = canBackPropertyKey.DependencyProperty;

		public bool CanBack => BooleanBox.GetBool(GetValue(CanBackProperty));

		#endregion

		#region -- CurentHistoryIndex - property --------------------------------------

		private static readonly DependencyPropertyKey curentHistoryIndexPropertyKey = DependencyProperty.RegisterReadOnly(nameof(CurentHistoryIndex), typeof(int), typeof(PpsWebView), new FrameworkPropertyMetadata(-1));
		public static readonly DependencyProperty CurentHistoryIndexProperty = curentHistoryIndexPropertyKey.DependencyProperty;

		public int CurentHistoryIndex => (int)GetValue(CurentHistoryIndexProperty);

		#endregion

		#region -- History - property -------------------------------------------------

		private static readonly DependencyPropertyKey historyPropertyKey = DependencyProperty.RegisterReadOnly(nameof(History), typeof(IReadOnlyList<PpsWebViewHistoryItem>), typeof(PpsWebView), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty HistoryProperty = historyPropertyKey.DependencyProperty;

		public IReadOnlyList<PpsWebViewHistoryItem> History => (IReadOnlyList<PpsWebViewHistoryItem>)GetValue(HistoryProperty);

		#endregion
	} // class PpsWebView
}
/*
 * // CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, )
*/

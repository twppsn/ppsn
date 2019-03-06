#region -- copyright --
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
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xaml;
using System.Xml;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using TecWare.PPSn.Stuff;
using TecWare.PPSn.UI;

namespace TecWare.PPSn
{
	#region -- class PpsEnvironment ---------------------------------------------------

	public partial class PpsEnvironment : IPpsXamlCode
	{
		private async Task<Tuple<XDocument, DateTime>> GetXmlDocumentAsync(string path, bool isXaml, bool isOptional)
		{
			try
			{
				var acceptedMimeType = isXaml ? MimeTypes.Application.Xaml : MimeTypes.Text.Xml;
				using (var r = await Request.GetResponseAsync(path, acceptedMimeType))
				using (var xml = await r.GetXmlStreamAsync(acceptedMimeType))
				{
					var dt = DateTime.MinValue;
					return new Tuple<XDocument, DateTime>(await Task.Run(() => XDocument.Load(xml)), dt);
				}
			}
			catch (WebException e)
			{
				if (e.Status == WebExceptionStatus.ProtocolError && isOptional)
					return null;
				throw;
			}
		} // func GetXmlDocumentAsync

		private async Task<XmlReader> OpenXmlDocumentAsync(string path, bool isXaml, bool isOptional)
		{
			try
			{
				var acceptedMimeType = isXaml ? MimeTypes.Application.Xaml : MimeTypes.Text.Xml;
				var r = await Request.GetResponseAsync(path, acceptedMimeType);
				return await r.GetXmlStreamAsync(acceptedMimeType,
					new XmlReaderSettings()
					{
						Async = true,
						IgnoreComments = !isXaml,
						IgnoreWhitespace = !isXaml
					}
				);
			}
			catch (WebException e)
			{
				if (e.Status == WebExceptionStatus.ProtocolError && isOptional)
					return null;
				throw;
			}
		} // func OpenXmlDocumentAsync

		private async Task RefreshDefaultResourcesAsync()
		{
			// update the resources, load a server site resource dictionary
			using (var xml = PpsXmlPosition.CreateLinePositionReader(await OpenXmlDocumentAsync("wpf/styles.xaml", true, true)))
			{
				if (xml == null)
					return; // no styles found

				// move to "theme"
				await xml.ReadStartElementAsync(StuffUI.xnTheme);

				// parse resources
				await xml.ReadStartElementAsync(StuffUI.xnResources);

				await UpdateResourcesAsync(xml);

				await xml.ReadEndElementAsync(); // resource
				await xml.ReadEndElementAsync(); // theme
			}
		} // proc RefreshDefaultResourcesAsync

		private async Task RefreshTemplatesAsync()
		{
			//var priority = 1;

			using (var xml = PpsXmlPosition.CreateLinePositionReader(await OpenXmlDocumentAsync("wpf/templates.xaml", true, true)))
			{
				if (xml == null)
					return; // no templates found

				// read root
				await xml.ReadStartElementAsync(StuffUI.xnTemplates);

				while (xml.NodeType != XmlNodeType.EndElement)
				{
					if (xml.NodeType == XmlNodeType.Element)
					{
						if (xml.IsName(StuffUI.xnResources))
						{
							if (!await xml.ReadAsync())
								break; // fetch element

							// check for a global resource dictionary, and update the main resources
							await UpdateResourcesAsync(xml);

							await xml.ReadEndElementAsync(); // resource
						}
						else if (xml.IsName(StuffUI.xnTemplate))
						{
							var key = xml.GetAttribute("key", String.Empty);
							if (String.IsNullOrEmpty(key))
							{
								xml.Skip();
								break;
							}

							//var templateDefinition = templateDefinitions[key];
							//if (templateDefinition == null)
							//{
							//	templateDefinition = new PpsDataListItemDefinition(this, key);
							//	templateDefinitions.AppendItem(templateDefinition);
							//}
							//priority = await templateDefinition.AppendTemplateAsync(xml, priority);
							//await xml.ReadEndElementAsync();

							await xml.SkipAsync();
						}
						else
							await xml.SkipAsync();
					}
					else
						await xml.ReadAsync();
				}

				await xml.ReadEndElementAsync(); // templates
			} // using xml

			// remove unused templates
			// todo:
		} // proc RefreshTemplatesAsync

		private async Task UpdateResourcesAsync(XmlReader xml)
		{
			while (await xml.MoveToContentAsync() != XmlNodeType.EndElement)
				await UpdateResourceAsync(xml);
		} // proc UpdateResources

		#region -- UI - Helper --------------------------------------------------------

		/// <summary>Show Trace as window.</summary>
		/// <param name="owner"></param>
		public Task ShowTraceAsync(Window owner)
			=> OpenPaneAsync(typeof(PpsTracePane), PpsOpenPaneMode.NewSingleDialog, new LuaTable() { ["DialogOwner"] = owner });

		/// <summary>Display the exception dialog.</summary>
		/// <param name="flags"></param>
		/// <param name="exception"></param>
		/// <param name="alternativeMessage"></param>
		public override void ShowException(ExceptionShowFlags flags, Exception exception, string alternativeMessage = null)
		{
			var exceptionToShow = exception.UnpackException();

			// always add the exception to the list
			Log.Append(PpsLogType.Exception, exception, alternativeMessage ?? exceptionToShow.Message);

			// show the exception if it is not marked as background
			if ((flags & ExceptionShowFlags.Background) != ExceptionShowFlags.Background
				&& Application.Current != null)
			{
				var dialogOwner = Application.Current.Windows.OfType<Window>().FirstOrDefault(c => c.IsActive);
				if (ShowExceptionDialog(dialogOwner, flags, exceptionToShow, alternativeMessage)) // should follow a detailed dialog
					ShowTraceAsync(dialogOwner).AwaitTask();

				if ((flags & ExceptionShowFlags.Shutown) != 0) // close application
					Application.Current.Shutdown(1);
			}
		} // proc ShowException

		/// <summary>Display the exception dialog.</summary>
		/// <param name="dialogOwner"></param>
		/// <param name="flags"></param>
		/// <param name="exception"></param>
		/// <param name="alternativeMessage"></param>
		/// <returns></returns>
		public bool ShowExceptionDialog(Window dialogOwner, ExceptionShowFlags flags, Exception exception, string alternativeMessage)
		{
			switch (exception)
			{
				case PpsEnvironmentOnlineFailedException ex:
					return MsgBox(ex.Message, MessageBoxButton.OK, MessageBoxImage.Information) != MessageBoxResult.OK;
				case ILuaUserRuntimeException urex:
					return MsgBox(urex.Message, MessageBoxButton.OK, MessageBoxImage.Information) != MessageBoxResult.OK;
				default:
					var shutDown = (flags & ExceptionShowFlags.Shutown) != 0;

					if (!shutDown && alternativeMessage != null)
					{
						MsgBox(alternativeMessage, MessageBoxButton.OK, MessageBoxImage.Information);
						return false;
					}
					else
					{
						MsgBox(alternativeMessage ?? exception.Message, MessageBoxButton.OK, MessageBoxImage.Error);
						return false;
						//var dialog = new PpsMessageDialog
						//{
						//	MessageType = shutDown ? PpsTraceItemType.Fail : PpsTraceItemType.Exception,
						//	MessageText = alternativeMessage ?? exception.Message,
						//	SkipVisible = !shutDown,

						//	Owner = dialogOwner
						//};
						//return dialog.ShowDialog() ?? false; // show the dialog
					}
			}
		} // func ShowExceptionDialog

		private static string GetMessageCaptionFromImage(MessageBoxImage image)
		{
			switch (image)
			{
				case MessageBoxImage.Error:
					return "Fehler";
				case MessageBoxImage.Warning:
					return "Warnung";
				case MessageBoxImage.Question:
					return "Frage";
				default:
					return "Information";
			}
		} // func GetMessageCaptionFromImage

		/// <summary>Display a simple messagebox</summary>
		/// <param name="text"></param>
		/// <param name="button"></param>
		/// <param name="image"></param>
		/// <param name="defaultResult"></param>
		/// <returns></returns>
		public override MessageBoxResult MsgBox(string text, MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Information, MessageBoxResult defaultResult = MessageBoxResult.OK)
			=> MessageBox.Show(text, GetMessageCaptionFromImage(image), button, image, defaultResult);

		/// <summary></summary>
		/// <param name="paneType"></param>
		/// <returns></returns>
		public override Type GetPaneTypeFromString(string paneType)
		{
			switch (paneType)
			{
				case "mask":
					return typeof(PpsGenericMaskWindowPane);
				default:
					return base.GetPaneTypeFromString(paneType);
			}
		} // func GetPaneTypeFromString

		#endregion
	} // class PpsEnvironment

	#endregion
}

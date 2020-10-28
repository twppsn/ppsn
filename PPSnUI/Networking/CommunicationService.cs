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
using System.ComponentModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Networking
{
	#region -- interface IPpsCommunicationService  ------------------------------------

	/// <summary>Access the communication service.</summary>
	public interface IPpsCommunicationService : INotifyPropertyChanged
	{
		/// <summary>Current communication class.</summary>
		DEHttpClient Http { get; }
		/// <summary>Is a user attached to the http-context.</summary>
		bool IsAuthentificated { get; }
		/// <summary>Is the connection alive.</summary>
		bool IsOnline { get; }
	} // interface IPpsCommunicationService

	#endregion

	#region -- class PpsCommunicationHelper --------------------------------------------

	/// <summary>Helper for <see cref="IPpsCommunicationService"/>.</summary>
	public static class PpsCommunicationHelper
	{
		private static bool TryGetSeverity(string severity, out LogMsgType severityCode)
		{
			switch (severity)
			{
				case "low":
				case "info":
					severityCode = LogMsgType.Information;
					return true;
				case "medium":
				case "warn":
					severityCode = LogMsgType.Warning;
					return true;
				case "error":
					severityCode = LogMsgType.Error;
					return true;
				default:
					severityCode = (LogMsgType)(-1);
					return false;
			}
		} // func TryGetSeverity

		/// <summary>todo: move to des.core?</summary>
		/// <param name="httpResponse"></param>
		public static (LogMsgType? severity, string reasonPhrase) DecodeReasonPhrase(this HttpResponseMessage httpResponse)
		{
			var text = httpResponse.ReasonPhrase;

			// overwrite text, with utf8 encoded version
			if (httpResponse.Headers.TryGetFirstValue("x-reason-utf8", out var value))
				text = Encoding.UTF8.GetString(Convert.FromBase64String(value));

			// check if we only should toast the message
			if (httpResponse.Headers.TryGetFirstValue("x-reason-severity", out var severity) && TryGetSeverity(severity, out var severityCode))
				return (severityCode, text);
			else
				return (null, text);
		} // func DecodeReasonPhrase

		private static readonly string[] redirectMimeTypes = new string[]
		{
			MimeTypes.Application.Pdf,
			MimeTypes.Image.Bmp,
			MimeTypes.Image.Png,
			MimeTypes.Image.Jpeg,
			MimeTypes.Image.Gif
		}; // redirectMimeTypes

		/// <summary></summary>
		/// <param name="contentDisposition"></param>
		/// <param name="name"></param>
		/// <returns></returns>
		private static bool TryGetAttachment(this ContentDispositionHeaderValue contentDisposition, out string name)
		{
			if (contentDisposition == null)
			{
				name = null;
				return false;
			}
			else
			{
				name = contentDisposition.FileName;
				return contentDisposition.DispositionType == "attachment";
			}
		} // func TryGetAttachment

		/// <summary></summary>
		/// <param name="httpContent"></param>
		/// <param name="enforceExternalViewer"></param>
		/// <param name="extension"></param>
		/// <returns></returns>
		public static bool TryGetExtensionFromContent(this HttpContent httpContent, bool enforceExternalViewer, out string extension)
		{
			// get media type
			var mediaType = httpContent.Headers.ContentType?.MediaType;
			if (String.IsNullOrEmpty(mediaType))
			{
				extension = null;
				return false;
			}

			//System.Diagnostics.Debug.Print("Content: " + httpContent.Headers.ContentDisposition ?? "null");
			if (TryGetAttachment(httpContent.Headers.ContentDisposition, out var fileName) // is an attachment
				|| (enforceExternalViewer && Array.Exists(redirectMimeTypes, c => c == mediaType))  // enforce some mime types
			)
			{
				extension = String.IsNullOrEmpty(fileName) ? System.IO.Path.GetExtension(fileName) : null;
				if (String.IsNullOrEmpty(extension))
					extension = MimeTypeMapping.GetExtensionFromMimeType(mediaType);
				return !String.IsNullOrEmpty(extension);
			}
			else
			{
				extension = null;
				return false;
			}
		} // func TryGetExtensionFromContent
	} // class PpsCommunicationHelper

	#endregion
}

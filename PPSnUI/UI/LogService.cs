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

namespace TecWare.PPSn.UI
{
	#region -- enum PpsLogType --------------------------------------------------------

	/// <summary>Log item classification, compatible to <c>LogMsgType</c></summary>
	public enum PpsLogType
	{
		/// <summary>No information</summary>
		None = -1,
		/// <summary>This TraceItem is neutral</summary>
		Information = 0,
		/// <summary>This TraceItem marks an recoverable error</summary>
		Warning,
		/// <summary>This TraceItem marks an event, which may reduce the useability</summary>
		Fail,
		/// <summary>This TraceItem is only for internal debugging</summary>
		Debug,
		/// <summary>This TraceItem shows an Exception</summary>
		Exception
	} // enum PpsTraceItemType

	#endregion

	#region -- interface IPpsLogger ---------------------------------------------------

	/// <summary>Pps Log interface</summary>
	public interface IPpsLogger
	{
		/// <summary>Append a simple message to the log.</summary>
		/// <param name="type"></param>
		/// <param name="message"></param>
		void Append(PpsLogType type, string message);
		/// <summary>Append a exception to the log.</summary>
		/// <param name="type"></param>
		/// <param name="exception"></param>
		/// <param name="alternativeMessage"></param>
		void Append(PpsLogType type, Exception exception, string alternativeMessage = null);
	} // interface IPpsLogger

	#endregion
}

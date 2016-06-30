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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Data;

namespace TecWare.PPSn
{
	#region -- enum ExceptionShowFlags --------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Wie soll die Nachricht angezeigt werden.</summary>
	[Flags]
	public enum ExceptionShowFlags
	{
		/// <summary>Keine näheren Angaben</summary>
		None = 0,
		/// <summary>Start the shutdown of the application.</summary>
		Shutown = 1,
		/// <summary>Do not show any user message.</summary>
		Background = 4
	} // enum ExceptionShowFlags

	#endregion

	#region -- struct PpsShellGetList ---------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsShellGetList
	{
		/// <summary></summary>
		/// <param name="viewId"></param>
		public PpsShellGetList(string viewId)
		{
			this.ViewId = viewId;
		} // ctor

		public PpsShellGetList(PpsShellGetList copy)
		{
			if (copy == null)
				copy = PpsShellGetList.Empty;

      this.ViewId = copy.ViewId;
			this.Filter = copy.Filter;
			this.Order = copy.Order;
			this.Start = copy.Start;
			this.Count = copy.Count;
		} // ctor

		public string ViewId { get; }
		public string Filter { get; set; }
		public string Order { get; set; }
		public int Start { get; set; } = -1;
		public int Count { get; set; } = -1;

		public bool IsEmpty => String.IsNullOrEmpty(ViewId);

		private static readonly PpsShellGetList empty = new PpsShellGetList((string)null);

		public static PpsShellGetList Empty => empty;
	} // class PpsShellDataFilterParameter

	#endregion

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Basic UI functions that must provider to use this library.</summary>
	public interface IPpsShell
	{
		/// <summary></summary>
		/// <param name="arguments"></param>
		/// <returns></returns>
		IEnumerable<IDataRow> GetViewData(PpsShellGetList arguments);

		/// <summary>Synchronization with UI, old style.</summary>
		/// <param name="action"></param>
		void BeginInvoke(Action action);
		/// <summary>Synchronization with UI, old style.</summary>
		/// <param name="action"></param>
		/// <returns></returns>
		Task InvokeAsync(Action action);
		/// <summary>Synchronization with UI. new style.</summary>
		/// <typeparam name="T">Return type of the async function.</typeparam>
		/// <param name="func">Function that will executed in the ui context.</param>
		/// <returns></returns>
		Task<T> InvokeAsync<T>(Func<T> func);

		/// <summary>Notifies a exception in the UI context.</summary>
		/// <param name="flags"></param>
		/// <param name="exception"></param>
		/// <param name="alternativeMessage"></param>
		/// <returns></returns>
		Task ShowExceptionAsync(ExceptionShowFlags flags, Exception exception, string alternativeMessage = null);

		// todo: Trace

		/// <summary>Synchronization with UI</summary>
		SynchronizationContext Context { get; }

		/// <summary>Access to the current lua engine.</summary>
		Lua Lua { get; }
		/// <summary>Base uri for request of complex data.</summary>
		Uri BaseUri { get; }
		/// <summary>Returns the default Encoding for the Application.</summary>
		Encoding Encoding { get; }
	} // interface IPpsShell
}

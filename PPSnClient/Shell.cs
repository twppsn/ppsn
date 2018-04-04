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
using System.Web;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.PPSn.Data;

namespace TecWare.PPSn
{
	#region -- enum ExceptionShowFlags ------------------------------------------------

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

	#region -- struct PpsShellGetList -------------------------------------------------

	/// <summary>Define all parameter of the ppsn viewget command.</summary>
	public sealed class PpsShellGetList
	{
		/// <summary></summary>
		/// <param name="viewId"></param>
		public PpsShellGetList(string viewId)
		{
			this.ViewId = viewId;
		} // ctor

		/// <summary>Copy parameter.</summary>
		/// <param name="copy"></param>
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

		private StringBuilder ToString(StringBuilder sb )
		{
			sb.Append("v=");
			sb.Append(ViewId);

			if (Filter != null && Filter != PpsDataFilterExpression.True)
				sb.Append("&f=").Append(Uri.EscapeDataString(Filter.ToString()));
			if (Columns != null && Columns.Length > 0)
				sb.Append("&l=").Append(PpsDataColumnExpression.ToString(Columns));
			if (Order != null && Order.Length > 0)
				sb.Append("&o=").Append(Uri.EscapeDataString(PpsDataOrderExpression.ToString(Order)));
			if (Start != -1)
				sb.Append("&s=").Append(Start);
			if (Count != -1)
				sb.Append("&c=").Append(Count);
			if (!String.IsNullOrEmpty(AttributeSelector))
				sb.Append("&a=").Append(AttributeSelector);

			return sb;
		} // func ToString

		/// <summary>Gets a uri-style query string for the properties.</summary>
		/// <returns></returns>
		public override string ToString()
			=> ToString(new StringBuilder()).ToString();

		/// <summary>Create request path.</summary>
		/// <param name="path"></param>
		/// <returns></returns>
		public string ToQuery(string path = null)
			=> ToString(new StringBuilder((path ?? String.Empty) + "?action=viewget&")).ToString();

		/// <summary>View to select.</summary>
		public string ViewId { get; }
		/// <summary>Columns to return</summary>
		public PpsDataColumnExpression[] Columns { get; set; }
		/// <summary>Filter</summary>
		public PpsDataFilterExpression Filter { get; set; }
		/// <summary>Row order</summary>
		public PpsDataOrderExpression[] Order { get; set; }
		/// <summary>Pagination</summary>
		public int Start { get; set; } = -1;
		/// <summary>Pagination</summary>
		public int Count { get; set; } = -1;
		/// <summary>Attribute</summary>
		public string AttributeSelector { get; set; } = String.Empty;

		/// <summary>Empty parameter.</summary>
		public bool IsEmpty => String.IsNullOrEmpty(ViewId);

		/// <summary>Parse string representation from ToString.</summary>
		/// <param name="data"></param>
		/// <param name="list"></param>
		/// <returns></returns>
		public static bool TryParse(string data, out PpsShellGetList list)
		{
			var arguments = HttpUtility.ParseQueryString(data, Encoding.UTF8);
			var viewId = arguments["v"];
			if (String.IsNullOrEmpty(viewId))
			{
				list = null;
				return false;
			}

			list = new PpsShellGetList(viewId);

			var f = arguments["f"];
			if (!String.IsNullOrEmpty(f))
				list.Filter = PpsDataFilterExpression.Parse(f);

			var l = arguments["l"];
			if (!String.IsNullOrEmpty(l))
				list.Columns = PpsDataColumnExpression.Parse(l).ToArray();

			var o = arguments["o"];
			if (!String.IsNullOrEmpty(o))
				list.Order = PpsDataOrderExpression.Parse(o).ToArray();

			var s = arguments["s"];
			if (!String.IsNullOrEmpty(s))
				list.Start = Int32.Parse(s);
			var c = arguments["c"];
			if (!String.IsNullOrEmpty(c))
				list.Count = Int32.Parse(c);

			var a = arguments["a"];
			if (!String.IsNullOrEmpty(a))
				list.AttributeSelector = a;

			return true;
		} // func TryParse

		/// <summary>Parse string representation from ToString.</summary>
		/// <param name="data"></param>
		/// <returns></returns>
		public static PpsShellGetList Parse(string data)
			=> TryParse(data, out var t) ? t : throw new FormatException();

		/// <summary>Representation of an empty selector.</summary>
		public static PpsShellGetList Empty { get; } = new PpsShellGetList((string)null);
	} // class PpsShellGetList

	#endregion

	#region -- interface IPpsShell ----------------------------------------------------

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

		/// <summary>Await for a task</summary>
		/// <param name="task"></param>
		void Await(Task task);
		/// <summary>Await for a task</summary>
		/// <param name="task"></param>
		/// <returns></returns>
		T Await<T>(Task<T> task);
		
		/// <summary>Notifies a exception in the UI context.</summary>
		/// <param name="flags"></param>
		/// <param name="exception"></param>
		/// <param name="alternativeMessage"></param>
		/// <returns></returns>
		Task ShowExceptionAsync(ExceptionShowFlags flags, Exception exception, string alternativeMessage = null);
		/// <summary></summary>
		/// <param name="message"></param>
		/// <returns></returns>
		Task ShowMessageAsync(string message);

		/// <summary>Synchronization with UI</summary>
		SynchronizationContext Context { get; }

		/// <summary>Access to the current lua engine.</summary>
		Lua Lua { get; }
		/// <summary>Interface to the basic functionality of the current system.</summary>
		LuaTable LuaLibrary { get; }
		/// <summary>Base uri for request of complex data.</summary>
		Uri BaseUri { get; }
		/// <summary>Returns the default Encoding for the Application.</summary>
		Encoding Encoding { get; }
	} // interface IPpsShell

	#endregion
}

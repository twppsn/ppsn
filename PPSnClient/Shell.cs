using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DES.Networking;

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
		/// <param name="listId"></param>
		public PpsShellGetList(string listId)
		{
			ListId = listId;
		} // ctor

		public PpsShellGetList(PpsShellGetList copy)
		{
			if (copy == null)
				copy = PpsShellGetList.Empty;

      this.ListId = copy.ListId;
			this.PreFilterId = copy.PreFilterId;
			this.OrderId = copy.OrderId;
			this.CustomFilter = copy.CustomFilter;
			this.Detailed = copy.Detailed;
			this.Start = copy.Start;
			this.Count = copy.Count;
		} // ctor

		public string ListId { get; }
		public string PreFilterId { get; set; }
		public string OrderId { get; set; }
		public string CustomFilter { get; set; }
		public bool Detailed { get; set; } = false;
		public int Start { get; set; } = -1;
		public int Count { get; set; } = -1;

		public bool IsEmpty => String.IsNullOrEmpty(ListId);

		private static readonly PpsShellGetList empty = new PpsShellGetList((string)null);

		public static PpsShellGetList Empty => empty;
	} // class PpsShellDataFilterParameter

	#endregion

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public interface IPpsShell
	{
		/// <summary>Retrieve a data list from the source.</summary>
		/// <param name="arguments"></param>
		/// <returns></returns>
		IEnumerable<IDataRecord> GetListData(PpsShellGetList arguments);

		/// <summary>Synchronization with UI</summary>
		/// <param name="action"></param>
		void BeginInvoke(Action action);
		/// <summary>Synchronization with UI</summary>
		/// <param name="action"></param>
		/// <returns></returns>
		Task InvokeAsync(Action action);
		/// <summary>Synchronization with UI</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="func"></param>
		/// <returns></returns>
		Task<T> InvokeAsync<T>(Func<T> func);

		/// <summary></summary>
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

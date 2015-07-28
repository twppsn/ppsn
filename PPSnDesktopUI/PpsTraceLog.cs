using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TecWare.PPSn
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsTraceItemBase
	{
		private int count;
		private DateTime stamp;
		private string sMessage;
	} // class PpsTraceItem

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsExceptionItem : PpsTraceItemBase
	{
		private Exception exception;
	} // class PpsExceptionItem

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsTraceItem : PpsTraceItemBase
	{
		private TraceEventType eventType;
		private TraceEventCache eventCache;
	} // class PpsTraceItem

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsTextItem : PpsTraceItemBase
	{
	} // class PpsTextItem
	
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsTraceLog
	{
	} // class PpsTraceLog
}

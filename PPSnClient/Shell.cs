using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Neo.IronLua;

namespace TecWare.PPSn
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public interface IPpsShell
	{
		/// <summary>Synchronisation</summary>
		SynchronizationContext Context { get; }
		/// <summary>Access to the current lua engine.</summary>
		Lua Lua { get; }
	} // interface IPpsShell
}

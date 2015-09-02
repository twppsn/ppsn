using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DES.Stuff;

namespace TecWare.PPSn.Data
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public static class PpsDataHelperClient
	{
		internal static readonly XName xnTable = "table";
		internal static readonly XName xnColumn = "column";
		internal static readonly XName xnRelation = "relation";
		internal static readonly XName xnPrimary = "primary";
		internal static readonly XName xnMeta = "meta";

		internal static void AddMetaGroup(XElement xMetaGroup, Action<string, Func<Type>, object> add)
		{
			foreach (XElement c in xMetaGroup.Elements())
				add(c.Name.LocalName, () => LuaType.GetType(c.GetAttribute("datatype", "object"), lLateAllowed: false), c.Value);
		} // proc AddMetaGroup
	} // class PpsDataHelperClient
}

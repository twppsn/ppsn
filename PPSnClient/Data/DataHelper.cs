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
using System.Threading.Tasks;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Stuff;

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
				add(c.Name.LocalName, () => LuaType.GetType(c.GetAttribute("datatype", "object"), lateAllowed: false), c.Value);
		} // proc AddMetaGroup
	} // class PpsDataHelperClient
}

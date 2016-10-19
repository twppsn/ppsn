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
using TecWare.PPSn.Data;

namespace TecWare.PPSn
{
	#region -- class PpsMainViewOrder ---------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsMainViewOrder
	{
		private readonly string name;
		private readonly string displayName;
		private readonly string orderExpression;
		private readonly int priority;

		internal PpsMainViewOrder(XElement x, ref int priority)
		{
			this.name = x.GetAttribute("name", String.Empty);
			if (String.IsNullOrEmpty(this.Name))
				throw new ArgumentNullException("@name");

			this.displayName = x.GetAttribute("displayName", Name);
			this.priority = priority = x.GetAttribute("priority", priority + 1);

			var t = x.Value;
			this.orderExpression = String.IsNullOrWhiteSpace(t) ? name : t;
		} // ctor

		public string Name => name;
		public string DisplayName => displayName;
		public int Priority => priority;

		public string OrderExpression => orderExpression;
	} // class PpsMainViewSort

	#endregion

	#region -- class PpsMainViewFilter --------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsMainViewFilter
	{
		private readonly string name;
		private readonly string displayName;
		private readonly PpsDataFilterExpression filterExpression;
		private readonly int priority;

		internal PpsMainViewFilter(XElement x, ref int priority)
		{
			this.name = x.GetAttribute("name", String.Empty);
			if (String.IsNullOrEmpty(name))
        throw new ArgumentNullException("@name");

      this.displayName = x.GetAttribute("displayName", name);
			this.priority = priority = x.GetAttribute("priority", priority + 1);

			var expr = x.Value;
			this.filterExpression = String.IsNullOrEmpty(expr) ? new PpsDataFilterNativeExpression(name) : PpsDataFilterExpression.Parse(expr);
		} // ctor

		public string Name => name;
		public string DisplayName => displayName;
		public PpsDataFilterExpression FilterExpression => filterExpression;
		public int Priority => priority;
	} // class PpsMainViewFilter

	#endregion

	#region -- class PpsMainViewDefinition ----------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsMainViewDefinition : PpsEnvironmentDefinition
	{
		public static readonly XName xnViews = "views";
		public static readonly XName xnView = "view";
		public static readonly XName xnVisible = "visible";

		public static readonly XName xnFilter = "filter";
		public static readonly XName xnOrder = "order";

		private readonly string viewId;
		private readonly PpsDataFilterExpression viewBaseFilter;
		private readonly string displayName;
		private readonly string displayImage;

		private readonly LuaChunk visibleCondition;

		private PpsMainViewOrder[] sortOrders;
		private PpsMainViewFilter[] filters;

		internal PpsMainViewDefinition(PpsMainEnvironment environment, XElement xDefinition)
			: base(environment, xDefinition.GetAttribute("name", null))
		{
			this.viewId = xDefinition.GetAttribute("view", String.Empty);
			if (String.IsNullOrEmpty(viewId))
				throw new ArgumentException("List viewId is missing.");

			this.viewBaseFilter = PpsDataFilterExpression.Parse(xDefinition.GetAttribute("filter", String.Empty));
			this.displayName = xDefinition.GetAttribute("displayName", this.Name);
			this.displayImage = xDefinition.GetAttribute("displayImage", this.Name);

			this.visibleCondition = environment.CreateLuaChunk(xDefinition.Element(xnVisible));

			// parse the filters
			var priority = 0;
			this.filters = (from c in xDefinition.Elements(xnFilter) select new PpsMainViewFilter(c, ref priority)).OrderBy(c => c.Priority).ToArray();
			// parse orders
			priority = 0;
			this.sortOrders = (from c in xDefinition.Elements(xnOrder) select new PpsMainViewOrder(c, ref priority)).OrderBy(c => c.Priority).ToArray();
		} // ctor
		
		public string ViewId => viewId;
		public PpsDataFilterExpression ViewFilterExpression => viewBaseFilter;
		public string DisplayName => displayName;
		public string DisplayImage => displayImage;
		public bool IsVisible => visibleCondition == null ? true : (bool)Environment.RunScriptWithReturn<bool>(visibleCondition, Environment, false);
		public IEnumerable<PpsMainViewFilter> Filters => filters;
		public IEnumerable<PpsMainViewOrder> SortOrders => sortOrders;
	} // class PpsMainViewDefinition

	#endregion
}

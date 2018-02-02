﻿#region -- copyright --
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
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;

namespace TecWare.PPSn
{
	#region -- class PpsMainViewOrder -------------------------------------------------

	/// <summary>View order definition for the navigator</summary>
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

		/// <summary>Internal name for the order.</summary>
		public string Name => name;
		/// <summary>Display name for the order.</summary>
		public string DisplayName => displayName;
		/// <summary>Sort order.</summary>
		public int Priority => priority;

		/// <summary>Order expression.</summary>
		public string OrderExpression => orderExpression;
	} // class PpsMainViewSort

	#endregion

	#region -- class PpsMainViewFilter ------------------------------------------------

	/// <summary>Predefined filter definition for the navigator.</summary>
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

		/// <summary>Internal filter name.</summary>
		public string Name => name;
		/// <summary>Display name for the filter</summary>
		public string DisplayName => displayName;
		/// <summary>Sort order.</summary>
		public int Priority => priority;

		/// <summary>Filter expression.</summary>
		public PpsDataFilterExpression FilterExpression => filterExpression;
	} // class PpsMainViewFilter

	#endregion

	#region -- class PpsMainViewDefinition --------------------------------------------

	/// <summary>View definition for the navigator.</summary>
	public sealed class PpsMainViewDefinition : PpsEnvironmentDefinition
	{
		/// <summary>Xml-Tag to define views.</summary>
		public static readonly XName xnViews = "views";
		/// <summary>Xml-Tag to define one view.</summary>
		public static readonly XName xnView = "view";
		/// <summary>Lua rule for the visibility of this view.</summary>
		public static readonly XName xnVisible = "visible";

		/// <summary>Filter expression of the view.</summary>
		public static readonly XName xnFilter = "filter";
		/// <summary>Order expression of the view.</summary>
		public static readonly XName xnOrder = "order";

		private readonly string viewId;
		private readonly PpsDataFilterExpression viewBaseFilter;
		private readonly string displayName;
		private readonly string displayImage;

		private readonly LuaChunk visibleCondition;

		private readonly PpsMainViewOrder[] sortOrders;
		private readonly PpsMainViewFilter[] filters;

		internal PpsMainViewDefinition(PpsMainEnvironment environment, XElement xDefinition)
			: base(environment, xDefinition.GetAttribute("name", null))
		{
			this.viewId = xDefinition.GetAttribute("view", String.Empty);
			if (String.IsNullOrEmpty(viewId))
				throw new ArgumentException("List viewId is missing.");

			this.viewBaseFilter = PpsDataFilterExpression.Parse(xDefinition.GetAttribute("filter", String.Empty));
			this.displayName = xDefinition.GetAttribute("displayName", this.Name);
			this.displayImage = xDefinition.GetAttribute("displayImage", this.Name);

			this.visibleCondition = environment.CreateChunk(xDefinition.Element(xnVisible), true);

			// parse the filters
			var priority = 0;
			this.filters = (
				from c in xDefinition.Elements(xnFilter)
				select new PpsMainViewFilter(c, ref priority)
			).OrderBy(c => c.Priority).ToArray();

			// parse orders
			priority = 0;
			this.sortOrders = (
				from c in xDefinition.Elements(xnOrder)
				select new PpsMainViewOrder(c, ref priority)
			).OrderBy(c => c.Priority).ToArray();
		} // ctor

		/// <summary>Internal name of the view.</summary>
		public string ViewId => viewId;
		/// <summary>Display name of the view.</summary>
		public string DisplayName => displayName;
		/// <summary>Display image of the view.</summary>
		public string DisplayImage => displayImage;

		/// <summary>Expression to define the view.</summary>
		public PpsDataFilterExpression ViewFilterExpression => viewBaseFilter;
		/// <summary>Is this view visible</summary>
		public bool IsVisible => visibleCondition == null ? true : (bool)Environment.RunScriptWithReturn<bool>(visibleCondition, Environment, false);

		/// <summary>Predefined filter rules.</summary>
		public IEnumerable<PpsMainViewFilter> Filters => filters;
		/// <summary>Predefined orders.</summary>
		public IEnumerable<PpsMainViewOrder> SortOrders => sortOrders;
	} // class PpsMainViewDefinition

	#endregion

	#region -- class PpsMainActionDefinition ------------------------------------------

	/// <summary>Global action definitions.</summary>
	public class PpsMainActionDefinition : PpsEnvironmentDefinition
	{
		/// <summary>Xml-Tag to define actions.</summary>
		public static readonly XName xnActions = "actions";
		/// <summary>Xml-Tag to define one action.</summary>
		public static readonly XName xnAction = "action";
		/// <summary>Lua rule to define the visibility of the action.</summary>
		public static readonly XName xnCondition = "condition";
		/// <summary>Lua code of the action.</summary>
		public static readonly XName xnCode = "code";

		private readonly string displayName;
		private readonly string displayImage;
		private readonly bool isHidden;
		private readonly LuaChunk condition;
		private readonly LuaChunk code;

		internal PpsMainActionDefinition(PpsMainEnvironment environment, XElement xCur, ref int priority)
			: base(environment, xCur.GetAttribute("name", String.Empty))
		{
			this.displayName = xCur.GetAttribute("displayName", this.Name);
			this.displayImage = xCur.GetAttribute("displayImage", "star");
			this.isHidden = xCur.GetAttribute("isHidden", false);
			this.Priority = priority = xCur.GetAttribute("priority", priority + 1);

			// compile condition
			condition = environment.CreateChunk(xCur.Element(xnCondition), true);
			// compile action
			code = environment.CreateChunk(xCur.Element(xnCode), true);
		} // ctor

		/// <summary>Can this command applied to this context.</summary>
		/// <param name="context"></param>
		/// <returns></returns>
		public bool CheckCondition(LuaTable context)
			=> condition == null ? true : (bool)Environment.RunScriptWithReturn<bool>(condition, context, false);

		/// <summary>Execute command on this context.</summary>
		/// <param name="context"></param>
		/// <returns></returns>
		public LuaResult Execute(LuaTable context)
		{
			try
			{
				return new LuaResult(true, Environment.RunScript(code, context, true));
			}
			catch (Exception e)
			{
				Environment.ShowException(ExceptionShowFlags.None, e);
				return new LuaResult(false);
			}
		} // func Execute

		/// <summary>Display name of the command.</summary>
		public string DisplayName => displayName;
		/// <summary>Display image of the command.</summary>
		public string DisplayImage => displayImage;
		/// <summary>Sort order of the command.</summary>
		public int Priority { get; }
		/// <summary>Is this an always hidden command.</summary>
		public bool IsHidden => isHidden;
	} // class PpsMainActionDefinition

	#endregion
}

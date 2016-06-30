using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;

namespace TecWare.PPSn
{
	#region -- class PpsMainViewOrder ---------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsMainViewOrder
	{
		internal PpsMainViewOrder(XElement x, ref int priority)
		{
			this.Name = x.GetAttribute("name", String.Empty);
			if (String.IsNullOrEmpty(this.Name))
				throw new ArgumentNullException("@name");

			this.DisplayName = x.GetAttribute("displayName", Name);

			this.Priority = priority = x.GetAttribute("priority", priority + 1);
		} // ctor

		public string Name { get; }
		public string DisplayName { get; }
		public int Priority { get; }
	} // class PpsMainViewSort

	#endregion

	#region -- class PpsMainViewFilter --------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsMainViewFilter
	{
		internal PpsMainViewFilter(XElement x, string globalFilter, ref int priority)
		{
			this.Name = x.GetAttribute("name", String.Empty);
			if (String.IsNullOrEmpty(this.Name))
        throw new ArgumentNullException("@name");

      this.DisplayName = x.GetAttribute("displayName", this.Name);

			this.Priority = priority = x.GetAttribute("priority", priority + 1);

			//this.Filter = String.IsNullOrEmpty(globalFilter) ? x.Value : "and(" + globalFilter + "," + x.Value + ")";
		} // ctor

		public string Name { get; }
		public string DisplayName { get; }
		public string Filter { get; }
		public int Priority { get; }
	} // class PpsMainViewFilter

	#endregion

	#region -- class PpsMainViewDefinition ----------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsMainViewDefinition : PpsEnvironmentDefinition
	{
		public static readonly XName xnViews = "views";
		public static readonly XName xnView = "view";

		public static readonly XName xnFilter = "filter";
		public static readonly XName xnOrder = "order";

		private readonly string viewId;
		private readonly string displayName;
		private readonly string displayImage;

		private PpsMainViewOrder[] sortOrders;
		private PpsMainViewFilter[] filters;

		internal PpsMainViewDefinition(PpsEnvironment environment, XElement xDefinition)
			: base(environment, xDefinition.GetAttribute("name", null))
		{
			this.viewId = xDefinition.GetAttribute("view", String.Empty);
			if (String.IsNullOrEmpty(viewId))
				throw new ArgumentException("List viewId is missing.");

			var globalFilter = xDefinition.GetAttribute("filter", String.Empty);
			this.displayName = xDefinition.GetAttribute("displayName", this.Name);
			this.displayImage = xDefinition.GetAttribute("displayGlyph", this.Name);

			// parse the filters
			var priority = 0;
			this.filters = (from c in xDefinition.Elements(xnFilter) select new PpsMainViewFilter(c, globalFilter, ref priority)).OrderBy(c => c.Priority).ToArray();
			// parse orders
			priority = 0;
			this.sortOrders = (from c in xDefinition.Elements(xnOrder) select new PpsMainViewOrder(c, ref priority)).OrderBy(c => c.Priority).ToArray();
		} // ctor

		public string ViewId => viewId;
		public string DisplayName => displayName;
		public string DisplayImage => displayImage;
		public IEnumerable<PpsMainViewFilter> Filters => filters;
		public IEnumerable<PpsMainViewOrder> SortOrders => sortOrders;
	} // class PpsMainViewDefinition

	#endregion
}

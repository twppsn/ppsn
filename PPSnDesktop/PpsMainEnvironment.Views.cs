using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TecWare.DES.Stuff;

namespace TecWare.PPSn
{
	#region -- class PpsMainViewOrder ---------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsMainViewOrder
	{
		internal PpsMainViewOrder(XElement x, ref int priority)
		{
			this.ColumnName = x.Value;
			if (String.IsNullOrEmpty(this.ColumnName))
				throw new ArgumentNullException("@name");

			this.DisplayName = x.GetAttribute("displayname", ColumnName);

			this.Priority = priority = x.GetAttribute("priority", priority + 1);
		} // ctor

		public string DisplayName { get; }
		public string ColumnName { get; }
		public int Priority { get; }
	} // class PpsMainViewSort

	#endregion

	#region -- class PpsMainViewFilter --------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsMainViewFilter
	{
		internal PpsMainViewFilter(XElement x, ref int priority)
		{
			this.Name = x.GetAttribute("name", String.Empty);
			if (String.IsNullOrEmpty(this.Name))
        throw new ArgumentNullException("@name");

      this.DisplayName = x.GetAttribute("displayname", this.Name);
			this.ShortCut = x.GetAttribute("shortcut", String.Empty);

			this.Priority = priority = x.GetAttribute("priority", priority + 1);
		} // ctor

		public string Name { get; }
		public string ShortCut { get; }
		public string DisplayName { get; }
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

		public static readonly XName xnSource = "source";
		public static readonly XName xnFilter = "filter";
		public static readonly XName xnOrder = "order";
		
		private string shortCut;
		private string displayName;
		private string listSource;

		private PpsMainViewOrder[] sortOrders;
		private PpsMainViewFilter[] filters;

		internal PpsMainViewDefinition(PpsEnvironment environment, PpsEnvironmentDefinitionSource source, XElement xDefinition)
			: base(environment, source, xDefinition.GetAttribute("id", null))
		{
			this.displayName = xDefinition.GetAttribute("displayname", this.Name);
			this.shortCut = xDefinition.GetAttribute("shortcut", null);

			// parse the data source
			this.listSource = xDefinition.Element(xnSource)?.Value;
			if (String.IsNullOrEmpty(listSource))
				throw new ArgumentException("List source missing."); // todo: exception

			// parse the filters
			var priority = 0;
			this.filters = (from c in xDefinition.Elements(xnFilter) select new PpsMainViewFilter(c, ref priority)).OrderBy(c => c.Priority).ToArray();
			// parse orders
			priority = 0;
      this.sortOrders = (from c in xDefinition.Elements(xnOrder) select new PpsMainViewOrder(c, ref priority)).OrderBy(c => c.Priority).ToArray();
		} // ctor

		public string DisplayName => displayName;
		public IEnumerable<PpsMainViewFilter> Filters => filters;
		public IEnumerable<PpsMainViewOrder> SortOrders => sortOrders;
	} // class PpsMainViewDefinition

	#endregion
}

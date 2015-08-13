using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TecWare.DES.Stuff;

namespace TecWare.PPSn
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsMainViewOrder
	{
		internal PpsMainViewOrder(XElement x)
		{
			this.ColumnName = x.Value;
			if (String.IsNullOrEmpty(this.ColumnName))
				throw new ArgumentNullException("@name");
			this.DisplayName = x.GetAttribute("displayname", ColumnName);
		} // ctor

		public string DisplayName { get; }
		public string ColumnName { get; }
	} // class PpsMainViewSort

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsMainViewFilter
	{
		internal PpsMainViewFilter(XElement x)
		{
			this.Name = x.GetAttribute("name", String.Empty);
			if (String.IsNullOrEmpty(this.Name))
        throw new ArgumentNullException("@name");
      this.DisplayName = x.GetAttribute("displayname", this.Name);
			this.ShortCut = x.GetAttribute("shortcut", String.Empty);
		} // ctor

		public string Name { get; }
		public string ShortCut { get; }
		public string DisplayName { get; }
	} // class PpsMainViewFilter

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsMainViewDefinition : PpsEnvironmentDefinition
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
			this.filters = (from c in xDefinition.Elements(xnFilter) select new PpsMainViewFilter(c)).ToArray();
			// parse orders
			this.sortOrders = (from c in xDefinition.Elements(xnOrder) select new PpsMainViewOrder(c)).ToArray();
		} // ctor

		public string DisplayName => displayName;
		public IEnumerable<PpsMainViewFilter> Filters => filters;
		public IEnumerable<PpsMainViewOrder> SortOrders => sortOrders;
	} // class PpsMainViewDefinition
}

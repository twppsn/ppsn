using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TecWare.PPSn
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsMainViewSort
	{
		public PpsMainViewSort(string displayName)
		{
			this.DisplayName = displayName;
		} // ctor

		public string DisplayName { get; }
	} // class PpsMainViewSort

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsMainViewFilter
	{
		public PpsMainViewFilter(string displayName)
		{
			this.DisplayName = displayName;
		} // ctor

		public string DisplayName { get; }
	} // class PpsMainViewFilter

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsMainViewDefinition : PpsEnvironmentDefinition
	{
		private string displayName;
		private PpsMainViewSort[] sortOrders;
		private PpsMainViewFilter[] filters;

		public PpsMainViewDefinition(PpsEnvironment environment, PpsEnvironmentDefinitionSource source, string name, string displayName, PpsMainViewFilter[] filters, PpsMainViewSort[] sortOrders)
			: base(environment, source, name)
		{
			this.displayName = displayName;

			this.filters = filters;
			this.sortOrders = sortOrders;
		} // ctor

		public string DisplayName => displayName;
		public IEnumerable<PpsMainViewFilter> Filters => filters;
		public IEnumerable<PpsMainViewSort> SortOrders => sortOrders;
	} // class PpsMainViewDefinition
}

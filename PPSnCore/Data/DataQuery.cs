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
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.Core.Data
{
	#region -- class PpsDataQuery -----------------------------------------------------

	/// <summary>Define all parameter of the ppsn viewget command.</summary>
	public sealed class PpsDataQuery
	{
		/// <summary></summary>
		/// <param name="viewId"></param>
		public PpsDataQuery(string viewId)
		{
			ViewId = viewId;
		} // ctor

		/// <summary>Copy parameter.</summary>
		/// <param name="copy"></param>
		/// <param name="filter">combine filter</param>
		/// <param name="columns"></param>
		/// <param name="order"></param>
		public PpsDataQuery(PpsDataQuery copy, PpsDataFilterExpression filter = null, IEnumerable<PpsDataColumnExpression> columns = null, IEnumerable<PpsDataOrderExpression> order = null)
		{
			if (copy == null)
				copy = Empty;

			ViewId = copy.ViewId;
			Filter = filter == null ? copy.Filter : PpsDataFilterExpression.Combine(copy.Filter, filter);
			Columns = columns == null ? copy.Columns : columns.ToArray();
			Order = order == null ? copy.Order : order.ToArray();
			Start = copy.Start;
			Count = copy.Count;
		} // ctor

		private StringBuilder ToString(StringBuilder sb, IFormatProvider formatProvider = null)
		{
			sb.Append("v=");
			sb.Append(ViewId);

			if (Filter != null && Filter != PpsDataFilterExpression.True)
				sb.Append("&f=").Append(Uri.EscapeDataString(Filter.ToString(formatProvider ?? CultureInfo.CurrentUICulture)));
			if (Columns != null && Columns.Length > 0)
				sb.Append("&r=").Append(PpsDataColumnExpression.ToString(Columns));
			if (Order != null && Order.Length > 0)
				sb.Append("&o=").Append(Uri.EscapeDataString(PpsDataOrderExpression.ToString(Order)));
			if (Start != -1)
				sb.Append("&s=").Append(Start);
			if (Count != -1)
				sb.Append("&c=").Append(Count);
			if (!String.IsNullOrEmpty(AttributeSelector))
				sb.Append("&a=").Append(AttributeSelector);

			return sb;
		} // func ToString

		/// <summary>Gets a uri-style query string for the properties.</summary>
		/// <returns></returns>
		public override string ToString()
			=> ToString(new StringBuilder()).ToString();

		/// <summary>Create request path.</summary>
		/// <param name="path"></param>
		/// <param name="formatProvider"></param>
		/// <returns></returns>
		public string ToQuery(string path = null, IFormatProvider formatProvider = null)
			=> ToString(new StringBuilder((path ?? String.Empty) + "?action=viewget&"), formatProvider).ToString();

		/// <summary>View to select.</summary>
		public string ViewId { get; }
		/// <summary>Columns to return</summary>
		public PpsDataColumnExpression[] Columns { get; set; }
		/// <summary>Filter</summary>
		public PpsDataFilterExpression Filter { get; set; }
		/// <summary>Row order</summary>
		public PpsDataOrderExpression[] Order { get; set; }
		/// <summary>Pagination</summary>
		public int Start { get; set; } = -1;
		/// <summary>Pagination</summary>
		public int Count { get; set; } = -1;
		/// <summary>Attribute</summary>
		public string AttributeSelector { get; set; } = String.Empty;

		/// <summary>Empty parameter.</summary>
		public bool IsEmpty => String.IsNullOrEmpty(ViewId);

		/// <summary>Parse string representation from ToString.</summary>
		/// <param name="data"></param>
		/// <param name="list"></param>
		/// <returns></returns>
		public static bool TryParse(string data, out PpsDataQuery list)
			=> TryParse(data, null, out list);

		/// <summary>Parse string representation from ToString.</summary>
		/// <param name="data"></param>
		/// <param name="formatProvider"></param>
		/// <param name="list"></param>
		/// <returns></returns>
		public static bool TryParse(string data, IFormatProvider formatProvider, out PpsDataQuery list)
		{
			var arguments = HttpUtility.ParseQueryString(data, Encoding.UTF8);
			var viewId = arguments["v"];
			if (String.IsNullOrEmpty(viewId))
			{
				list = null;
				return false;
			}

			list = new PpsDataQuery(viewId);

			var f = arguments["f"];
			if (!String.IsNullOrEmpty(f))
				list.Filter = PpsDataFilterExpression.Parse(f, formatProvider: formatProvider);

			var l = arguments["l"];
			if (!String.IsNullOrEmpty(l))
				list.Columns = PpsDataColumnExpression.Parse(l).ToArray();

			var o = arguments["o"];
			if (!String.IsNullOrEmpty(o))
				list.Order = PpsDataOrderExpression.Parse(o).ToArray();

			var s = arguments["s"];
			if (!String.IsNullOrEmpty(s))
				list.Start = Int32.Parse(s);
			var c = arguments["c"];
			if (!String.IsNullOrEmpty(c))
				list.Count = Int32.Parse(c);

			var a = arguments["a"];
			if (!String.IsNullOrEmpty(a))
				list.AttributeSelector = a;

			return true;
		} // func TryParse

		/// <summary>Parse string representation from ToString.</summary>
		/// <param name="data"></param>
		/// <param name="formatProvider"></param>
		/// <returns></returns>
		public static PpsDataQuery Parse(string data, IFormatProvider formatProvider = null)
			=> TryParse(data, formatProvider, out var t) ? t : throw new FormatException();

		/// <summary>Representation of an empty selector.</summary>
		public static PpsDataQuery Empty { get; } = new PpsDataQuery((string)null);
	} // class PpsDataQuery

	#endregion
}

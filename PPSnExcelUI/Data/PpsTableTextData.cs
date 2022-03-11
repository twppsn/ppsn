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
using TecWare.DE.Data;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.Data
{
	public sealed class PpsTableTextData : ObservableObject, IPpsTableData
	{
		#region -- class PpsColumnInfo ------------------------------------------------

		private sealed class PpsColumnInfo : IPpsTableColumn
		{
			public PpsColumnInfo(IPpsTableColumn c)
			{
				Name = c.Name;
				Ascending = c.Ascending;
				Expression = c.Expression;
			} // ctor

			public PpsColumnInfo(string expr, int offset, int length)
			{
				if (expr[offset] == '+')
				{
					Ascending = true;
					offset++;
					length--;
				}
				else if (expr[offset] == '-')
				{
					Ascending = false;
					offset++;
					length--;
				}

				var sep = expr.IndexOf('=', offset);
				if (sep > 0 && sep < offset + length)
				{
					Name = expr.Substring(sep + 1, length - sep + offset - 1).Trim();
					Expression = expr.Substring(offset, sep - offset);
				}
				else
				{
					Name = null;
					Expression = expr.Substring(offset, length);
				}
			} // ctor

			public StringBuilder ToString(StringBuilder sb)
			{
				if (Ascending.HasValue)
					sb.Append(Ascending.Value ? '+' : '-');

				sb.Append(Expression);

				if (!String.IsNullOrEmpty(Name))
					sb.Append('=').Append(Name);

				return sb;
			} // func ToString

			public override string ToString()
				=> ToString(new StringBuilder()).ToString();

			public bool Equals(PpsColumnInfo other)
				=> Ascending == other.Ascending && Expression != other.Expression;

			public PpsDataOrderExpression ToOrder()
				=> Ascending.HasValue ? new PpsDataOrderExpression(!Ascending.Value, Expression) : null;

			public PpsDataColumnExpression ToColumn(bool includeColumnAlias)
				=> includeColumnAlias ? new PpsDataColumnExpression(Expression, Name) : new PpsDataColumnExpression(Expression);

			public string Name { get; }
			public string Expression { get; }
			public bool? Ascending { get; }
			public PpsTableColumnType Type => PpsTableColumnType.Data;
		} // class class PpsColumnInfo

		#endregion

		private string displayName = null;
		private string views = null;
		private string filter = null;
		private PpsColumnInfo[] columnInfos = Array.Empty<PpsColumnInfo>();

		private void LoadCore(string displayName, string views, string filter, IEnumerable<IPpsTableColumn> columns)
		{
			DisplayName = displayName;
			Views = views;
			Filter = filter;

			// todo: accept all types of columns
			SetColumnCore(columns.Where(c => c.Type == PpsTableColumnType.Data).Select(c => new PpsColumnInfo(c)));
		} // proc Load

		public void Load(IPpsTableData tableData)
			=> LoadCore(tableData.DisplayName, tableData.Views, tableData.Filter, tableData.Columns);

		Task IPpsTableData.UpdateAsync(string views, string filter, IEnumerable<IPpsTableColumn> columns, bool anonymize)
		{
			LoadCore(null, views, filter, columns);
			return Task.CompletedTask;
		} // func UpdateAsync

		private IEnumerable<PpsColumnInfo> ParseColumnInfo(string value)
		{
			foreach (var (startAt, len) in Procs.SplitNewLinesTokens(value))
			{
				if (len > 0)
					yield return new PpsColumnInfo(value, startAt, len);
			}
		} // func ParseColumnInfo

		public string DisplayName
		{
			get => displayName;
			set => Set(ref displayName, value, nameof(DisplayName));
		} // prop DisplayName

		public string Views
		{
			get => views;
			set
			{
				if (Set(ref views, value, nameof(Views)))
					OnPropertyChanged(nameof(IsEmpty));
			}
		} // prop Views

		public string Filter
		{
			get => filter;
			set => Set(ref filter, value, nameof(Filter));
		} // prop Filter

		private static PpsColumnInfo[] EqualColumns(PpsColumnInfo[] columnInfos, IEnumerable<PpsColumnInfo> newColumnInfos)
		{
			using (var nc = newColumnInfos.GetEnumerator())
			{
				var result = new List<PpsColumnInfo>();
				var idx = 0;
				var isNew = columnInfos.Length == 0;
				while (nc.MoveNext())
				{
					if (isNew)
						result.Add(nc.Current);
					else if (idx < columnInfos.Length)
					{
						result.Add(nc.Current);

						isNew = !nc.Current.Equals(columnInfos[idx]);
					}
					else
						isNew = true;
				}

				if (!isNew && result.Count == columnInfos.Length)
					return null;

				return result.ToArray();
			}
		} // func EqualColumns

		private void SetColumnCore(IEnumerable<PpsColumnInfo> newColumnInfos)
		{
			var newColumnsArray = EqualColumns(columnInfos, newColumnInfos);
			if (newColumnsArray != null)
			{
				columnInfos = newColumnsArray;
				OnPropertyChanged(nameof(Columns));
			}
		} // proc SetColumnCore

		public string Columns
		{
			get
			{
				if (columnInfos.Length == 0)
					return String.Empty;
				else
				{
					var sb = new StringBuilder();
					foreach (var c in columnInfos)
						c.ToString(sb).AppendLine();
					return sb.ToString();
				}
			}
			set => SetColumnCore(ParseColumnInfo(value));
		} // prop Columns

		public IEnumerable<PpsDataColumnExpression> GetColumnExpressions(bool includeColumnAlias)
			=> columnInfos.Select(c => c.ToColumn(includeColumnAlias));

		public IEnumerable<PpsDataOrderExpression> GetOrderExpression()
		{
			return from col in columnInfos
				   let o = col.ToOrder()
				   where o != null
				   select o;
		} // func GetOrderExpression

		IEnumerable<IPpsTableColumn> IPpsTableData.Columns => columnInfos;

		IEnumerable<string> IPpsTableData.DefinedNames => null;

		public bool IsEmpty => String.IsNullOrEmpty(views);
	} // class PpsTableTextData
}

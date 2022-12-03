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

namespace TecWare.PPSn.Data
{
	#region -- enum PpsTableColumnType ------------------------------------------------

	/// <summary>Column type</summary>
	[Flags]
	public enum PpsTableColumnType
	{
		/// <summary>Unknown column: Expression is the name.</summary>
		User = 0 | ReadOnly,
		/// <summary>Data column: expression the full qualified column name.</summary>
		Data = 1,
		/// <summary>Formula column: Expression is the name.</summary>
		Formula = 2 | ReadOnly,
		ReadOnly = 0x100
	} // enum PpsTableColumnType

	#endregion

	#region -- interface IPpsTableColumn ----------------------------------------------

	public interface IPpsTableColumn
	{
		/// <summary>Display name of the column.</summary>
		string Name { get; }
		/// <summary>Type of the column.</summary>
		PpsTableColumnType Type { get; }
		/// <summary>Expression to descripe the column.</summary>
		string Expression { get; }
		/// <summary>Sort order</summary>
		bool? Ascending { get; }
	} // interface IPpsTableColumn

	#endregion

	#region -- interface IPpsTableData ------------------------------------------------

	/// <summary>Table data representation</summary>
	public interface IPpsTableData
	{
		/// <summary>Update the table data.</summary>
		/// <param name="views"></param>
		/// <param name="filter"></param>
		/// <param name="columns"></param>
		Task UpdateAsync(string views, string filter, IEnumerable<IPpsTableColumn> columns, bool anonymize);

		/// <summary>Change the displayname of the table.</summary>
		string DisplayName { get; set; }

		/// <summary>Get all views</summary>
		string Views { get; }
		/// <summary></summary>
		string Filter { get; }
		/// <summary></summary>
		IEnumerable<IPpsTableColumn> Columns { get; }
		/// <summary>Defined Cells name </summary>
		IEnumerable<string> DefinedNames { get; }

		/// <summary>Is this an empty view.</summary>
		bool IsEmpty { get; }
	} // interface IPpsTableData

	#endregion
}

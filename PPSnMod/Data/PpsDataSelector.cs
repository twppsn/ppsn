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
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using TecWare.DE.Data;
using TecWare.DE.Server;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.Server.Data
{
	#region -- class PpsDataSelector --------------------------------------------------

	/// <summary>Minimal function set for a selector.</summary>
	public abstract class PpsDataSelector : IDERangeEnumerable<IDataRow>, IDataRowEnumerable
	{
		private readonly PpsDataSource source;

		public PpsDataSelector(PpsDataSource source)
		{
			this.source = source;
		} // ctor
		
		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator(0, Int32.MaxValue);

		IEnumerator<IDataRow> IEnumerable<IDataRow>.GetEnumerator()
			=> GetEnumerator(0, Int32.MaxValue);

		public virtual IEnumerator<IDataRow> GetEnumerator()
			=> GetEnumerator(0, Int32.MaxValue);

		/// <summary>Returns a enumerator for the range.</summary>
		/// <param name="start">Start of the enumerator</param>
		/// <param name="count">Number of elements that should be returned,</param>
		/// <returns></returns>
		public abstract IEnumerator<IDataRow> GetEnumerator(int start, int count);

		IDataRowEnumerable IDataRowEnumerable.ApplyOrder(IEnumerable<PpsDataOrderExpression> expressions, Func<string, string> lookupNative)
			=> ApplyOrder(expressions, lookupNative);

		public virtual PpsDataSelector ApplyOrder(IEnumerable<PpsDataOrderExpression> expressions, Func<string, string> lookupNative = null)
			=> this;

		IDataRowEnumerable IDataRowEnumerable.ApplyFilter(PpsDataFilterExpression expression, Func<string, string> lookupNative)
			=> ApplyFilter(expression, lookupNative);

		public virtual PpsDataSelector ApplyFilter(PpsDataFilterExpression expression, Func<string, string> lookupNative = null)
			=> this;

		IDataRowEnumerable IDataRowEnumerable.ApplyColumns(IEnumerable<PpsDataColumnExpression> columns)
			=> ApplyColumns(columns);

		public virtual PpsDataSelector ApplyColumns(IEnumerable<PpsDataColumnExpression> columns)
			=> this;
		
		/// <summary>Returns the field description for the name in the resultset</summary>
		/// <param name="nativeColumnName"></param>
		/// <returns></returns>
		public abstract IPpsColumnDescription GetFieldDescription(string nativeColumnName);

		/// <summary>by default we do not know the number of items</summary>
		public virtual int Count => -1;

		/// <summary></summary>
		public PpsDataSource DataSource => source;
	} // class PpsDataSelector

	#endregion

	#region -- class PpsGenericSelector<T> --------------------------------------------

	/// <summary>Creates a selection for a typed list.</summary>
	/// <typeparam name="T"></typeparam>
	public sealed class PpsGenericSelector<T> : PpsDataSelector
	{
		private readonly string viewId;
		private readonly IEnumerable<T> enumerable;
		private readonly PpsApplication application;

		public PpsGenericSelector(PpsDataSource source, string viewId, IEnumerable<T> enumerable) 
			: base(source)
		{
			this.viewId = viewId;
			this.enumerable = enumerable;
			this.application = source.GetService<PpsApplication>(true);
		} // ctor
		
		public override IEnumerator<IDataRow> GetEnumerator(int start, int count)
			=> new GenericDataRowEnumerator<T>(enumerable.GetEnumerator());
		
		public override IPpsColumnDescription GetFieldDescription(string nativeColumnName)
				=> application.GetFieldDescription(viewId + "." + nativeColumnName, false);

		public override PpsDataSelector ApplyFilter(PpsDataFilterExpression expression, Func<string, string> lookupNative = null)
		{
			var predicate = PpsDataFilterVisitorLambda.CompileTypedFilter<T>(expression);
			return new PpsGenericSelector<T>(DataSource, viewId, enumerable.Where(new Func<T, bool>(predicate)));
		} // func ApplyFilter
	} // class PpsGenericSelector

	#endregion
}

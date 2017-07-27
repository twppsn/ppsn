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
using System.Text;
using System.Threading.Tasks;
using TecWare.DE.Server;
using TecWare.DE.Stuff;
using TecWare.DE.Data;
using System.Reflection;
using TecWare.PPSn.Data;
using System.Linq.Expressions;

namespace TecWare.PPSn.Server.Data
{
	#region -- class PpsDataSelector ----------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public abstract class PpsDataSelector : IDERangeEnumerable<IDataRow>, IEnumerable<IDataRow>
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

		public virtual PpsDataSelector ApplyOrder(IEnumerable<PpsDataOrderExpression> expressions, Func<string, string> lookupNative = null)
			=> this;

		public virtual PpsDataSelector ApplyFilter(PpsDataFilterExpression expression, Func<string, string> lookupNative = null)
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

	#region -- class PpsGenericSelector<T> ----------------------------------------------

	public sealed class PpsGenericSelector<T> : PpsDataSelector
	{
		#region -- class FilterCompiler -------------------------------------------------

		private sealed class FilterCompiler : PpsDataFilterVisitor<Expression>
		{
			private readonly ParameterExpression currentRowParameter;

			public FilterCompiler()
			{
				this.currentRowParameter = ParameterExpression.Parameter(typeof(T));
			} // ctor

			private static Exception CreateCompareException(PpsDataFilterCompareExpression expression)
				=> new ArgumentOutOfRangeException(nameof(expression.Operator), expression.Operator, $"Operator '{expression.Operator}' is not defined for the value type '{expression.Value.Type}'.");

			public override Expression CreateCompareFilter(PpsDataFilterCompareExpression expression)
			{
				ExpressionType GetBinaryExpressionType()
				{
					switch (expression.Operator)
					{
						case PpsDataFilterCompareOperator.Contains:
						case PpsDataFilterCompareOperator.Equal:
							return ExpressionType.Equal;
						case PpsDataFilterCompareOperator.NotContains:
						case PpsDataFilterCompareOperator.NotEqual:
							return ExpressionType.NotEqual;
						case PpsDataFilterCompareOperator.Greater:
							return ExpressionType.GreaterThan;
						case PpsDataFilterCompareOperator.GreaterOrEqual:
							return ExpressionType.GreaterThanOrEqual;
						case PpsDataFilterCompareOperator.Lower:
							return ExpressionType.LessThan;
						case PpsDataFilterCompareOperator.LowerOrEqual:
							return ExpressionType.LessThanOrEqual;
						default:
							throw CreateCompareException(expression);
					}
				} // func GetBinaryExpressionType

				// left site
				var propertyInfo = typeof(T).GetRuntimeProperty(expression.Operand) 
					?? throw new ArgumentNullException(nameof(expression.Operand), $"Property {expression.Operand} not declared in type {typeof(T).Name}.");
				var left = Expression.Property(currentRowParameter, propertyInfo);

				// right site depends of the operator
				switch (expression.Value.Type)
				{
					case PpsDataFilterCompareValueType.Text:
						{
							var right = Expression.Constant(((PpsDataFilterCompareTextValue)expression.Value).Text);
							switch (expression.Operator)
							{
								case PpsDataFilterCompareOperator.Contains:
									return Expression.MakeBinary(ExpressionType.GreaterThanOrEqual, Expression.Call(left, stringIndexOfMethodInfo, right, Expression.Constant(StringComparison.OrdinalIgnoreCase)), Expression.Constant(0));
								case PpsDataFilterCompareOperator.NotContains:
									return Expression.MakeBinary(ExpressionType.LessThan, Expression.Call(left, stringIndexOfMethodInfo, right, Expression.Constant(StringComparison.OrdinalIgnoreCase)), Expression.Constant(0));
								default:
									return Expression.MakeBinary(GetBinaryExpressionType(), Expression.Call(stringCompareMethodInfo, left, right, Expression.Constant(StringComparison.OrdinalIgnoreCase)), Expression.Constant(0));
							}
						}
					case PpsDataFilterCompareValueType.Integer:
						{
							var right = Expression.Constant(((PpsDataFilterCompareIntegerValue)expression.Value).Value);
							return Expression.MakeBinary(GetBinaryExpressionType(), left, right);
						}
					case PpsDataFilterCompareValueType.Number:
						{
							var right = Expression.Constant(((PpsDataFilterCompareNumberValue)expression.Value).Text);
							switch (expression.Operator)
							{
								case PpsDataFilterCompareOperator.Contains:
								//	return column.Item1 + " LIKE " + CreateLikeString(value, PpsSqlLikeStringEscapeFlag.Trailing);
								case PpsDataFilterCompareOperator.NotContains:
									//	return "NOT " + column.Item1 + " LIKE " + CreateLikeString(value, PpsSqlLikeStringEscapeFlag.Trailing);
									throw new NotImplementedException();

								default:
									return Expression.MakeBinary(GetBinaryExpressionType(), Expression.Call(stringCompareMethodInfo, left, right, Expression.Constant(StringComparison.OrdinalIgnoreCase)), Expression.Constant(0));
							}
						}
					case PpsDataFilterCompareValueType.Null:
						{
							switch (expression.Operator)
							{
								case PpsDataFilterCompareOperator.Contains:
								case PpsDataFilterCompareOperator.Equal:
									return Expression.MakeBinary(ExpressionType.Equal, left, Expression.Default(left.Type));
								case PpsDataFilterCompareOperator.NotContains:
								case PpsDataFilterCompareOperator.NotEqual:
									return Expression.MakeBinary(ExpressionType.NotEqual, left, Expression.Default(left.Type));
								case PpsDataFilterCompareOperator.Greater:
								case PpsDataFilterCompareOperator.GreaterOrEqual:
								case PpsDataFilterCompareOperator.Lower:
								case PpsDataFilterCompareOperator.LowerOrEqual:
									return Expression.Constant(false);
								default:
									throw CreateCompareException(expression);
							}

							throw new NotImplementedException();
						}
					case PpsDataFilterCompareValueType.Date:
						{
							var a = (PpsDataFilterCompareDateValue)expression.Value;
							switch (expression.Operator)
							{
								case PpsDataFilterCompareOperator.Contains:
								case PpsDataFilterCompareOperator.Equal:
									return Expression.AndAlso(Expression.MakeBinary(ExpressionType.GreaterThanOrEqual, left, Expression.Constant(a.From)), Expression.MakeBinary(ExpressionType.LessThanOrEqual, left, Expression.Constant(a.To)));
								case PpsDataFilterCompareOperator.NotContains:
								case PpsDataFilterCompareOperator.NotEqual:
									return Expression.AndAlso(Expression.MakeBinary(ExpressionType.LessThan, left, Expression.Constant(a.From)), Expression.MakeBinary(ExpressionType.GreaterThan, left, Expression.Constant(a.To)));
								case PpsDataFilterCompareOperator.Greater:
									return Expression.MakeBinary(ExpressionType.GreaterThan, left, Expression.Constant(a.To));
								case PpsDataFilterCompareOperator.GreaterOrEqual:
									return Expression.MakeBinary(ExpressionType.GreaterThanOrEqual, left, Expression.Constant(a.From));
								case PpsDataFilterCompareOperator.Lower:
									return Expression.MakeBinary(ExpressionType.LessThan, left, Expression.Constant(a.From));
								case PpsDataFilterCompareOperator.LowerOrEqual:
									return Expression.MakeBinary(ExpressionType.LessThanOrEqual, left, Expression.Constant(a.To));
								default:
									throw CreateCompareException(expression);
							}
						}
					default:
						throw CreateCompareException(expression);
				}
			} // func CreateCompareFilter

			public override Expression CreateLogicFilter(PpsDataFilterExpressionType method, IEnumerable<Expression> arguments)
			{
				var expr = (Expression)null;
				bool negResult;
				ExpressionType type;
				switch (method)
				{
					case PpsDataFilterExpressionType.And:
						type = ExpressionType.AndAlso;
						negResult = false;
						break;
					case PpsDataFilterExpressionType.NAnd:
						type = ExpressionType.AndAlso;
						negResult = true;
						break;

					case PpsDataFilterExpressionType.Or:
						type = ExpressionType.OrElse;
						negResult = false;
						break;
					case PpsDataFilterExpressionType.NOr:
						type = ExpressionType.OrElse;
						negResult = true;
						break;

					default:
						throw new ArgumentOutOfRangeException(nameof(method), method, "Invalid operation.");
				}

				foreach (var a in arguments)
				{
					if (expr == null)
						expr = a;
					else
						expr = Expression.MakeBinary(type, expr, a);
				}

				if (negResult)
					expr = Expression.Not(expr);

				return expr;
			} // func CreateLogicFilter

			public override Expression CreateNativeFilter(PpsDataFilterNativeExpression expression) 
				=> throw new NotSupportedException(); 

			public override Expression CreateTrueFilter()
				=> Expression.Constant(true);

			public Predicate<T> CompileFilter(PpsDataFilterExpression expression)
			{
				var filterExpr = CreateFilter(expression);
				return LambdaExpression.Lambda<Predicate<T>>(filterExpr, currentRowParameter).Compile();
			} // func CompileFilter

			private static readonly MethodInfo stringIndexOfMethodInfo;
			private static readonly MethodInfo stringCompareMethodInfo;

			static FilterCompiler()
			{
				stringIndexOfMethodInfo = typeof(string).GetMethod(nameof(String.IndexOf), new Type[] { typeof(string), typeof(StringComparison) });
				stringCompareMethodInfo = typeof(string).GetMethod(nameof(String.Compare), new Type[] { typeof(string), typeof(string), typeof(StringComparison) });
			}
		} // class FilterCompiler

		#endregion

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
			var predicate = new FilterCompiler().CompileFilter(expression);
			return new PpsGenericSelector<T>(DataSource, viewId, enumerable.Where(new Func<T, bool>(predicate)));
		} // func ApplyFilter
	} // class PpsGenericSelector

	#endregion
}

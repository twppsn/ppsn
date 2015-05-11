using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace TecWare.PPSn.Data
{
	#region -- enum PpsDataColumnMetaData ----------------------------------------------+

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Vordefinierte Meta-Daten an der Spalte.</summary>
	public enum PpsDataColumnMetaData
	{
		/// <summary>Beschreibt die maximale Länge einer Zeichenfolge.</summary>
		MaxLength,
		/// <summary>Kurztext der Spalte</summary>
		Caption,
		/// <summary>Beschreibungstext der Spalte</summary>
		Description
	} // enum PpsDataColumnMetaData

	#endregion

	#region -- class PpsDataColumnDefinition --------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Basisklasse für die Spaltendefinitionen.</summary>
	public abstract class PpsDataColumnDefinition : IDynamicMetaObjectProvider
	{
		#region -- WellKnownTypes ---------------------------------------------------------

		/// <summary>Definiert die bekannten Meta Informationen.</summary>
		private static readonly Dictionary<string, Type> wellknownMetaTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
		{
			{ PpsDataColumnMetaData.MaxLength.ToString(), typeof(int) },
			{ PpsDataColumnMetaData.Caption.ToString(), typeof(string) },
			{ PpsDataColumnMetaData.Description.ToString(), typeof(string) }
		};

		#endregion

		#region -- class PpsDataColumnMetaCollection --------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		public class PpsDataColumnMetaCollection : PpsMetaCollection
		{
			public T Get<T>(PpsDataColumnMetaData key, T @default)
			{
				return Get<T>(key.ToString(), @default);
			} // func Get

			public override IReadOnlyDictionary<string, Type> WellknownMetaTypes { get { return wellknownMetaTypes; } }
		} // class PpsDataColumnMetaCollection

		#endregion

		#region -- class PpsDataColumnMetaObject ------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class PpsDataColumnMetaObject : DynamicMetaObject
		{
			public PpsDataColumnMetaObject(Expression expr, object value)
				: base(expr, BindingRestrictions.Empty, value)
			{
			} // ctor

			public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
			{
				// todo: (ms) Fest Namen sind schlecht, generischer Aufbauen
				if (String.Compare(binder.Name, "Name", StringComparison.OrdinalIgnoreCase) == 0 ||
					String.Compare(binder.Name, "DataType", StringComparison.OrdinalIgnoreCase) == 0 ||
					String.Compare(binder.Name, "Index", StringComparison.OrdinalIgnoreCase) == 0 ||
					String.Compare(binder.Name, "Table", StringComparison.OrdinalIgnoreCase) == 0)
				{
					return base.BindGetMember(binder);
				}
				else
				{
					PpsDataColumnDefinition column = (PpsDataColumnDefinition)Value;

					return new DynamicMetaObject(
						column.Meta.GetMetaConstantExpression(binder.Name),
						BindingRestrictions.GetInstanceRestriction(Expression, Value) // todo: (ms) Performt schlecht, Definition Restriction wäre wohl besser
					);
				}
			} // func BindGetMemger
		} // class PpsDataColumnMetaObject

		#endregion

		private PpsDataTableDefinition table;

		private readonly string sColumnName;	// Interne Bezeichnung der Spalte
		private Type dataType;								// Datentyp des des Wertes der Spalte innerhalb der Zeile

		/// <summary>Erzeugt eine neue Spaltendefinition.</summary>
		/// <param name="table">Zugehörige Tabelle</param>
		/// <param name="sColumnName">Name der Spalte</param>
		/// <param name="dataType">Zugeordneter Datentyp.</param>
		public PpsDataColumnDefinition(PpsDataTableDefinition table, string sColumnName, Type dataType)
		{
			this.table = table;
			this.sColumnName = sColumnName;
			this.dataType = dataType;
		} // ctor

		DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter)
		{
			return new PpsDataColumnMetaObject(parameter, this);
		} // func IDynamicMetaObjectProvider.GetMetaObject

		/// <summary>Zugehörige Tabelle</summary>
		public PpsDataTableDefinition Table { get { return table; } }

		/// <summary>Name der Spalte</summary>
		public string Name { get { return sColumnName; } }
		/// <summary>Datentyp der Spalte</summary>
		public Type DataType { get { return dataType; } protected set { dataType = value; } }
		/// <summary>Index der Spalte innerhalb der Datentabelle</summary>
		public int Index { get { return table.Columns.IndexOf(this); } }

		/// <summary>Zugriff auf die zugeordneten Meta-Daten der Spalte.</summary>
		public abstract PpsDataColumnMetaCollection Meta { get; }
	} // class PpsDataColumnDefinition

	#endregion
}

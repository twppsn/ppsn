using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using TecWare.DES.Stuff;

namespace TecWare.PPSn.Data
{
	#region -- enum PpsDataSetMetaData --------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Definiert Standardattribute am DataSet.</summary>
	public enum PpsDataSetMetaData
	{
		/// <summary>Titel der Datensammlung.</summary>
		Caption
	} // enum PpsDataSetMetaData

	#endregion

	#region -- class PpsDataSetDefinition -----------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Schema für eine Datensammlung</summary>
	public abstract class PpsDataSetDefinition
	{
		#region -- WellKnownTypes ---------------------------------------------------------

		private static readonly Dictionary<string, Type> wellKnownMetaTypes = new Dictionary<string, Type>()
		{
			{ PpsDataSetMetaData.Caption.ToString(), typeof(string) }
		};

		#endregion

		#region -- class PPSnDataSetMetaCollection ----------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		public class PpsDataSetMetaCollection : PpsMetaCollection
		{
			public T Get<T>(PpsDataSetMetaData key, T @default)
			{
				return Get<T>(key.ToString(), @default);
			} // func Get<T>

			public override IReadOnlyDictionary<string, Type> WellknownMetaTypes { get { return wellKnownMetaTypes; } }
		} // class PpsDataSetMetaCollection

		#endregion

		private List<PpsDataTableDefinition> tables;
		private ReadOnlyCollection<PpsDataTableDefinition> tableDefinitions;

		protected PpsDataSetDefinition()
		{
			this.tables = new List<PpsDataTableDefinition>();
			this.tableDefinitions = new ReadOnlyCollection<PpsDataTableDefinition>(tables);
		} // ctor

		/// <summary>Beendet die Initialisierung des DataSet's</summary>
		public virtual void EndInit()
		{
			foreach (var t in TableDefinitions)
				t.EndInit();
		} // proc EndInit

		/// <summary>Durch die Logik, darf die Auflistung der Tabellen nicht geändert werden. Damit die dynamischen Zugriffe nicht gebrochen werden.</summary>
		/// <param name="table"></param>
		protected void Add(PpsDataTableDefinition table)
		{
			if (table == null)
				throw new ArgumentNullException();
			if (FindTable(table.Name) != null)
				throw new ArgumentOutOfRangeException();

			tables.Add(table);
		} // proc Add

		/// <summary>Erzeugt eine Datensammlung aus der Definition</summary>
		/// <returns></returns>
		public virtual PpsDataSet CreateDataSet()
		{
			return new PpsDataSet(this);
		} // func CreateDataSet

		public PpsDataTableDefinition FindTable(string sName)
		{
			return tables.Find(c => String.Compare(c.Name, sName, StringComparison.OrdinalIgnoreCase) == 0);
		} // func FindTable

		/// <summary>Zugriff auf die Tabellendaten</summary>
		public ReadOnlyCollection<PpsDataTableDefinition> TableDefinitions { get { return tableDefinitions; } }
		/// <summary>Zugriff auf die MetaInformationen</summary>
		public abstract PpsDataSetMetaCollection Meta { get; }
	} // class PpsDataSetDefinition

	#endregion

	#region -- class PpsDataSet ---------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsDataSet : IDynamicMetaObjectProvider
	{
		#region -- Element/Atributenamen des Daten-XML --

		internal static readonly XName xnData = "data";
		internal static readonly XName xnTable = "t";
		internal static readonly XName xnRow = "r";

		internal static readonly XName xnRowValue = "v";
		internal static readonly XName xnRowValueOriginal = "o";
		internal static readonly XName xnRowValueCurrent = "c";

		internal static readonly XName xnRowState = "s";
		internal static readonly XName xnRowAdd = "a";
		internal static readonly XName xnRowName = "n";

		public static readonly XName xnCombine = "combine";

		#endregion

		#region -- class PpsDataSetMetaObject --------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class PpsDataSetMetaObject : DynamicMetaObject
		{
			public PpsDataSetMetaObject(Expression expression, object value)
				: base(expression, BindingRestrictions.Empty, value)
			{
			} // ctor

			private BindingRestrictions GetRestrictions(PpsDataSet dataset)
			{
				return BindingRestrictions.GetExpressionRestriction(
					Expression.AndAlso(
						Expression.TypeIs(Expression, typeof(PpsDataSet)),
						Expression.Equal(
							Expression.Property(Expression.Convert(Expression, typeof(PpsDataSet)), DefinitionPropertyInfo),
							Expression.Constant(dataset.DataSetDefinition)
						)
					)
				);
			} // func GetRestrictions

			public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
			{
				Expression expr;
				PpsDataSet dataset = (PpsDataSet)Value;

				// Suche die Entsprechende Tabelle
				int iTableIndex = Array.FindIndex(dataset.tables, c => String.Compare(c.Name, binder.Name) == 0);
				if (iTableIndex == -1) // Suche die Meta-Daten
					expr = dataset.DataSetDefinition.Meta.GetMetaConstantExpression(binder.Name);
				else
					expr = Expression.ArrayIndex(Expression.Field(Expression.Convert(Expression, typeof(PpsDataSet)), TableFieldInfo), Expression.Constant(iTableIndex));

				return new DynamicMetaObject(expr, GetRestrictions(dataset));
			} // func BindGetMember

			public override IEnumerable<string> GetDynamicMemberNames()
			{
				PpsDataSet dataset = (PpsDataSet)Value;

				return
					(from t in dataset.Tables select t.Name)
					.Concat(from m in dataset.DataSetDefinition.Meta select m.Key);
			} // func GetDynamicMemberNames
		} // class PpsDataSetMetaObject

		#endregion

		private PpsDataSetDefinition datasetDefinition;
		private PpsDataTable[] tables;
		private ReadOnlyCollection<PpsDataTable> tableCollection;

		private IPpsUndoSink undoSink = null;

		public PpsDataSet(PpsDataSetDefinition datasetDefinition)
		{
			this.datasetDefinition = datasetDefinition;
			this.tables = new PpsDataTable[datasetDefinition.TableDefinitions.Count];

			for (int i = 0; i < tables.Length; i++)
				tables[i] = datasetDefinition.TableDefinitions[i].CreateDataTable(this);

			this.tableCollection = new ReadOnlyCollection<PpsDataTable>(tables);
		} // ctor

		public DynamicMetaObject GetMetaObject(Expression parameter)
		{
			return new PpsDataSetMetaObject(parameter, this);
		} // func GetMetaObject

		public void RegisterUndoSink(IPpsUndoSink undoSink)
		{
			this.undoSink = undoSink;
		} // proc RegisterUndoSink

		private int FindTableIndex(string sTableName)
		{
			return Array.FindIndex(tables, dt => String.Compare(dt.Name, sTableName, StringComparison.OrdinalIgnoreCase) == 0);
		} // func FindTableIndex

		private void ClearInternal()
		{
			// Tabellen
			for (int i = 0; i < tables.Length; i++)
				tables[i].ClearInternal();
		} // proc ClearInternal

		public void Read(XElement x)
		{
			if (x.Name != xnData)
				throw new ArgumentException();

			bool lReadCombine = x.GetAttribute(xnCombine, false);

			// Lösche die vorhanden Daten
			if (!lReadCombine)
				ClearInternal();

			// Lade die entsprechenden Tabellen
			int iTableIndex = 0;
			foreach (XElement xTable in x.Elements(xnTable)) // lese die Tabellen
			{
				if (lReadCombine) // suche die Tabelle
				{
					string sTableName = xTable.GetAttribute(xnRowName, String.Empty);
					if (String.IsNullOrEmpty(sTableName) || (iTableIndex = FindTableIndex(sTableName)) == -1)
						throw new ArgumentException();
				}

				PpsDataTable t = tables[iTableIndex]; // Voraussetzung: Schema-Datei und Daten-Datei haben identische Tabellensequenz, bezogen auf Tabellentypen
				if (!lReadCombine)
					t.ClearInternal();
				t.Read(xTable);

				if (!lReadCombine) // nächste Tabelles
					iTableIndex++;
			}
		} // proc Read

		public void Write(XmlWriter x)
		{
			x.WriteStartElement(xnData.LocalName);
			foreach (var table in tables)
				table.Write(x);
			x.WriteEndElement();
		} // proc Write

		public void Commit()
		{
			foreach (PpsDataTable t in Tables)
				t.Commit();
		} // proc Commit

		public void Reset()
		{
			foreach (PpsDataTable t in Tables)
				t.Reset();
		} // proc Reset

		protected internal virtual void OnTableColumnValueChanged(PpsDataTable table, PpsDataRow row, int iColumnIndex, object oldValue, object value)
		{
		} // proc OnTableColumnValueChanged

		/// <summary>Zugriff auf die Definition der Datensammlung</summary>
		public PpsDataSetDefinition DataSetDefinition { get { return datasetDefinition; } }
		/// <summary>Zugriff auf die Tabellendaten.</summary>
		public ReadOnlyCollection<PpsDataTable> Tables { get { return tableCollection; } }
		/// <summary></summary>
		public IPpsUndoSink UndoSink { get { return undoSink; } }

		// -- Static --------------------------------------------------------------

		//private static readonly ConstructorInfo ArgumentOutOfRangeExceptionConstructorInfo;
		private static readonly FieldInfo TableFieldInfo;
		private static readonly PropertyInfo DefinitionPropertyInfo;

		static PpsDataSet()
		{
			var typeInfo = typeof(ArgumentOutOfRangeException).GetTypeInfo();

			//ArgumentOutOfRangeExceptionConstructorInfo =
			//	(
			//		from ci in typeInfo.DeclaredConstructors
			//		let pi = ci.GetParameters()
			//		where pi.Length == 2 && pi[0].ParameterType == typeof(string) && pi[1].ParameterType == typeof(string)
			//		select ci
			//	).FirstOrDefault(); ArgumentOutOfRangeExceptionConstructorInfo == null ||

			typeInfo = typeof(PpsDataSet).GetTypeInfo();
			TableFieldInfo = typeInfo.GetDeclaredField("tables");
			DefinitionPropertyInfo = typeInfo.GetDeclaredProperty("DataSetDefinition");


			if (TableFieldInfo == null || DefinitionPropertyInfo == null)
				throw new ArgumentException("sctor @ PpsDataSet");
		} // sctor
	} // class PpsDataSet

	#endregion
}

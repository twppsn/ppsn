using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DES.Networking;
using TecWare.DES.Stuff;

namespace TecWare.PPSn.Data
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsLocalDataStore : PpsDataStore
	{
		#region Q&D
		private class QDRowDataRecord : IDataRecord
		{
			private System.Data.DataRowView row;

			public QDRowDataRecord(System.Data.DataRowView row)
			{
				this.row = row;
			}

			public object this[string fieldName] => row[fieldName];

			public object this[int fieldIndex] => row[fieldIndex];

			public int FieldCount => row.DataView.Table.Columns.Count;

			public Type GetFieldType(int fieldIndex) => row.DataView.Table.Columns[fieldIndex].DataType;

			public string GetName(int fieldIndex) => row.DataView.Table.Columns[fieldIndex].ColumnName;

			public bool IsNull(int fieldIndex) => row[fieldIndex] == DBNull.Value || row[fieldIndex] == null;
		}

		private Dictionary<string, System.Data.DataTable> localData = new Dictionary<string, System.Data.DataTable>(StringComparer.OrdinalIgnoreCase);
		#endregion

		public PpsLocalDataStore(PpsEnvironment environment)
			: base(environment)
		{
		} // ctor

		private void LoadTestData(string fileName, ref System.Data.DataTable dt)
		{
			if (dt != null)
				return;

			dt = new System.Data.DataTable();

			var xDoc = XDocument.Load(fileName);
			var xColumns = xDoc.Root.Element("columns");
			if (xColumns != null && dt.Columns.Count == 0)
			{
				foreach (var c in xColumns.Elements())
					dt.Columns.Add(c.GetAttribute("name", String.Empty), LuaType.GetType(c.GetAttribute("type", "string")));
			}

			var xElements = xDoc.Root.Element("items");
			var values = new object[dt.Columns.Count];
			foreach (var cur in xElements.Elements())
			{
				for (int i = 0; i < values.Length; i++)
				{
					var v = cur.Element(dt.Columns[i].ColumnName)?.Value;
					if (v == null)
						values[i] = null;
					else
						values[i] = Lua.RtConvertValue(v, dt.Columns[i].DataType);
				}
				dt.Rows.Add(values);
			}

			dt.AcceptChanges();
		} // proc LoadTestData
				
		protected override void GetResponseDataStream(PpsStoreResponse r)
		{
			var actionName = r.Request.Arguments.Get("action");
			//if (actionName == "getlist") // todo support getlist
			//{
			//	GetLocalList(r);
			//	return;
			//}

			var fi = new FileInfo(Path.Combine(@"..\..\Local", r.Request.Path.Substring(1).Replace('/', '\\')));
			if (!fi.Exists)
				throw new WebException("File not found.", WebExceptionStatus.ProtocolError);

			var contentType = String.Empty;
			if (fi.Extension == ".xaml")
				contentType = MimeTypes.Xaml;
			else if (fi.Extension == ".lua")
				contentType = MimeTypes.Text;

			r.SetResponseData(fi.OpenRead(), contentType);
		} // func GetResponseDataStream

		/// <summary>Override to support a better stream of the locally stored data.</summary>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public override IEnumerable<IDataRecord> GetListData(PpsShellGetList arguments)
		{
			// get the table
			string filterExpression = String.Empty;
			System.Data.DataTable dt = null;

			// get the datatable
			if (!localData.TryGetValue(arguments.ListId, out dt))
			{
				LoadTestData(@"..\..\Local\Data\" + arguments.ListId + ".xml", ref dt);
				localData[arguments.ListId] = dt;
			}

			#region Q&D
			var filterId = arguments.PreFilterId;
			if (!String.IsNullOrEmpty(filterId))
			{
				switch (arguments.ListId.ToLower())
				{
					case "parts":
						if (filterId == "active")
							filterExpression = "TEILSTATUS = '10'";
						else if (filterId == "inactive")
							filterExpression = "TEILSTATUS = '90'";
						break;

					case "contacts":
						if (!String.IsNullOrEmpty(filterId))
							if (filterId == "liefonly")
								filterExpression = "KONTDEBNR is null";
							else if (filterId == "kundonly")
								filterExpression = "KONTKREDNR is null";
							else if (filterId == "intonly")
								filterExpression = "1 = 0";
						break;
				}
			}
			#endregion

			// filter data
			var orderDef = arguments.OrderId;
			if (orderDef != null)
				orderDef = orderDef.Replace("+", " asc").Replace("-", " desc");
				
			// enumerate lines
			using (var dv = new System.Data.DataView(dt, filterExpression, orderDef, System.Data.DataViewRowState.CurrentRows))
				for (int i = 0; i < arguments.Count; i++)
				{
					var index = arguments.Start + i;
					if (index < dv.Count)
						yield return new QDRowDataRecord(dv[index]);
				}
		} // func GetListData

		public override IDataRecord GetDetailedData(long objectId, string typ)
		{
			return null;
		} // func GetDetailedData
	} // class PpsLocalDataStore
}

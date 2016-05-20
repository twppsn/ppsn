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
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.DE.Data;

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

        #endregion

        private Dictionary<string, System.Data.DataTable> localData = new Dictionary<string, System.Data.DataTable>(StringComparer.OrdinalIgnoreCase);

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

			var fi = new FileInfo(Path.Combine(@"..\..\..\PPSnDesktop\Local", r.Request.Path.Substring(1).Replace('/', '\\')));
			if (!fi.Exists)
				throw new WebException("File not found.", WebExceptionStatus.ProtocolError);

			var contentType = String.Empty;
			if (fi.Extension == ".xaml")
				contentType = MimeTypes.Application.Xaml;
			else if (fi.Extension == ".lua")
				contentType = MimeTypes.Text.Plain;

			r.SetResponseData(fi.OpenRead(), contentType);
		} // func GetResponseDataStream

		/// <summary>Override to support a better stream of the locally stored data.</summary>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public override IEnumerable<IDataRow> GetListData(PpsShellGetList arguments)
		{
			// get the table
			string filterExpression = String.Empty;
			System.Data.DataTable dt = null;

			// get the datatable
			if (!localData.TryGetValue(arguments.ViewId, out dt))
			{
				LoadTestData(@"..\..\..\PPSnDesktop\Local\Data\" + arguments.ViewId + ".xml", ref dt);
				localData[arguments.ViewId] = dt;
			}

			#region Q&D
			var filterId = arguments.Filter;
			if (!String.IsNullOrEmpty(filterId))
			{
				switch (arguments.ViewId.ToLower())
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
								filterExpression = "DEBNR is null";
							else if (filterId == "kundonly")
								filterExpression = "KREDNR is null";
							else if (filterId == "intonly")
								filterExpression = "1 = 0";
						break;
				}
			}
			#endregion

			if (!String.IsNullOrEmpty(arguments.Filter))
			{
				if (filterExpression.Length > 0)
					filterExpression += " and OBJKMATCH like '%" + arguments.Filter + "%'";
				else
					filterExpression += "OBJKMATCH like '%" + arguments.Filter + "%'";
			}

			// filter data
			var orderDef = arguments.Order;
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

		public override IDataRow GetDetailedData(long objectId, string typ)
		{
			return null;
		} // func GetDetailedData
	} // class PpsLocalDataStore
}

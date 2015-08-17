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
		// Q&D
		private System.Data.DataTable dtKund;
		private System.Data.DataTable dtTeil;

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
		
		private void GetLocalList(PpsStoreResponse r)
		{
			var listId = r.Request.Arguments.Get("id").ToLower();
			var filterId = r.Request.Arguments.Get("filter");
			var orderDef = r.Request.Arguments.Get("order");
			var startAtString = r.Request.Arguments.Get("start");
			var countString = r.Request.Arguments.Get("count");

			// get the table
			string filterExpression = String.Empty;
			System.Data.DataTable dt = null;
			if (listId == "parts")
			{
				LoadTestData(@"..\..\Local\Data\Teil.xml", ref dtTeil);
				dt = dtTeil;

				if (!String.IsNullOrEmpty(filterId))
					if (filterId == "active")
						filterExpression = "TEILSTATUS = '10'";
					else if (filterId == "inactive")
						filterExpression = "TEILSTATUS = '90'";
			}
			else if (listId == "contacts")
			{
				LoadTestData(@"..\..\Local\Data\Kont.xml", ref dtKund);
				dt = dtKund;

				if (!String.IsNullOrEmpty(filterId))
					if (filterId == "liefonly")
						filterExpression = "KONTDEBNR is null";
					else if (filterId == "kundonly")
						filterExpression = "KONTKREDNR is null";
					else if (filterId == "intonly")
						filterExpression = "1 = 0";
			}

			// filter data
			if (orderDef != null)
				orderDef = orderDef.Replace("+", " asc").Replace("-", " desc");
			var startAt = Int32.Parse(startAtString);
			var count = Int32.Parse(countString);

			var xDataReader = new XElement("datareader");
			var xColumns = new XElement("columns");
			xDataReader.Add(xColumns);
			foreach (System.Data.DataColumn col in dt.Columns)
				xColumns.Add(new XElement("column", new XAttribute("name", col.ColumnName), new XAttribute("type", LuaType.GetType(col.DataType).AliasOrFullName)));

			var xItems = new XElement("items");
			xDataReader.Add(xItems);
			using (var dv = new System.Data.DataView(dt, filterExpression, orderDef, System.Data.DataViewRowState.CurrentRows))
				for (int i = 0; i < count; i++)
				{
					var index = startAt + i;
					if (index < dv.Count)
					{
						var xItem = new XElement("item");

						for (int j = 0; j < dt.Columns.Count; j++)
							xItem.Add(new XElement(dt.Columns[j].ColumnName, dv[index][j]));

						xItems.Add(xItem);								
					}
				}

			// create result
			var src = new MemoryStream();
			var b = Encoding.ASCII.GetBytes(xDataReader.ToString());
			src.Write(b, 0, b.Length);
			src.Position = 0;

			r.SetResponseData(src, MimeTypes.Xml);
		} // func GetLocalList

		protected override void GetResponseDataStream(PpsStoreResponse r)
		{
			var actionName = r.Request.Arguments.Get("action");
			if (actionName == "getlist")
			{
				GetLocalList(r);
				return;
			}

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
	} // class PpsLocalDataStore
}

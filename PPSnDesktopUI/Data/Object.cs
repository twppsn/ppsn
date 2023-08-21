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
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Neo.IronLua;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Data
{
	#region -- interface IPpsDataStream -----------------------------------------------

	/// <summary>Open data as an stream.</summary>
	public interface IPpsDataStream
	{
		/// <summary>Open an stream on the data.</summary>
		/// <param name="access"></param>
		/// <param name="expectedLength"></param>
		/// <returns></returns>
		Stream OpenStream(FileAccess access, long expectedLength = -1);
	} // interface IPpsDataStream

	#endregion

	#region -- interface IPpsDataObject -----------------------------------------------

	/// <summary>Defines a data access for UI-Controls.</summary>
	public interface IPpsDataObject : IPropertyReadOnlyDictionary, IDisposable
	{
		/// <summary>Gets call if the data was changed.</summary>
		event EventHandler DataChanged;
		/// <summary>Method to disable access to the data, from the data it self.</summary>
		Func<IDisposable> DisableUI { get; set; }

		/// <summary>Commit the data to the local changes.</summary>
		/// <returns></returns>
		Task CommitAsync();

		/// <summary>Is the data changable.</summary>
		bool IsReadOnly { get; }

		/// <summary>Access data</summary>
		object Data { get; }
	} // interface IPpsDataObject

	#endregion

	#region -- interface IPpsDataInfo -------------------------------------------------

	/// <summary></summary>
	public interface IPpsDataInfo
	{
		/// <summary></summary>
		/// <returns></returns>
		Task<IPpsDataObject> LoadAsync();

		/// <summary>Create a pane for the object.</summary>
		/// <param name="paneManager"></param>
		/// <param name="newPaneMode"></param>
		/// <param name="arguments"></param>
		/// <returns></returns>
		Task<IPpsWindowPane> OpenPaneAsync(IPpsWindowPaneManager paneManager = null, PpsOpenPaneMode newPaneMode = PpsOpenPaneMode.Default, LuaTable arguments = null);

		/// <summary>Name of the object.</summary>
		string Name { get; }
		/// <summary></summary>
		string MimeType { get; }
	} // interface IPpsDataInfo

	#endregion

	#region -- interface IPpsDataSetProvider ------------------------------------------

	/// <summary></summary>
	public interface IPpsDataSetProvider
	{
		/// <summary></summary>
		/// <param name="datasetName"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		PpsDataSetDefinition TryGetDataSetDefinition(string datasetName, bool throwException = false);
	} // interface IPpsDataSetProvider

	#endregion

	#region -- class PpsDataInfo ------------------------------------------------------

	public static class PpsDataInfo
	{
		#region -- class PpsGenericData ------------------------------------------------

		private sealed class PpsGenericData : IPpsDataStream
		{
			private readonly byte[] bytes;

			public PpsGenericData(byte[] bytes)
			{
				this.bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));
			} // ctor

			public Stream OpenStream(FileAccess access, long expectedLength = -1)
				=> new MemoryStream(bytes, false);
		} // class PpsGenericData

		#endregion

		#region -- class PpsFileData --------------------------------------------------

		private sealed class PpsFileData : IPpsDataStream
		{
			private readonly string fileName;

			public PpsFileData(string fileName)
			{
				this.fileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
			} // ctor

			public Stream OpenStream(FileAccess access, long expectedLength = -1)
				=> new FileStream(fileName, FileMode.Open);
		} // class PpsFileData

		#endregion

		#region -- class PpsGenericDataObject -----------------------------------------

		private sealed class PpsGenericDataObject : IPpsDataObject, IPpsDataInfo
		{
			public event EventHandler DataChanged { add { } remove { } }

			private readonly string name;
			private readonly string mimeType;
			private readonly IPropertyReadOnlyDictionary properties;
			private readonly IPpsDataStream data;

			public PpsGenericDataObject(string name, string mimeType, byte[] data, IPropertyReadOnlyDictionary properties)
			{
				this.name = name ?? "bytes.dat";
				this.mimeType = mimeType ?? MimeTypes.Application.OctetStream;
				this.properties = properties ?? PropertyDictionary.EmptyReadOnly;
				this.data = new PpsGenericData((byte[])data);
			} // ctor

			public PpsGenericDataObject(string fileName, IPropertyReadOnlyDictionary properties)
			{
				this.name = Path.GetFileName(fileName);
				this.mimeType = MimeTypeMapping.GetMimeTypeFromExtension(Path.GetExtension(fileName)) ?? MimeTypes.Application.OctetStream;
				this.properties = properties ?? PropertyDictionary.EmptyReadOnly;
				this.data = new PpsFileData(fileName);
			} // ctor

			public void Dispose()
			{
			} // proc Dispose

			public Task CommitAsync()
				=> Task.CompletedTask;

			public Task<IPpsDataObject> LoadAsync()
			{
				return Task.FromResult<IPpsDataObject>(this);
			} // func LoadAsync

			public Task<IPpsWindowPane> OpenPaneAsync(IPpsWindowPaneManager paneManager = null, PpsOpenPaneMode newPaneMode = PpsOpenPaneMode.Default, LuaTable arguments = null) 
				=> throw new NotImplementedException();

			bool IPropertyReadOnlyDictionary.TryGetProperty(string name, out object value)
				=> properties.TryGetProperty(name, out value);

			public Func<IDisposable> DisableUI { get => null; set { } }

			public string Name => name;
			public string MimeType => mimeType;

			public bool IsReadOnly => true;
			public object Data => data;
		} // class PpsGenericDataObject

		#endregion

		private static bool TryUnpackObjectInfo(string exInfo, out string objectTyp, out LuaTable t)
		{
			t = LuaTable.FromJson(Encoding.UTF8.GetString(Convert.FromBase64String(exInfo)));
			objectTyp = t.GetOptionalValue<string>("ObjectTyp", null);
			return objectTyp != null;
		} // func TryUnpackObjectInfo

		public static Task<IPpsDataInfo> ToPpsDataInfoAsync(string fileName)
		{
			return Task.FromResult<IPpsDataInfo>(new PpsGenericDataObject(fileName, PropertyDictionary.EmptyReadOnly));
		} // func ToDataInfoAsync

		public static async Task<IPpsDataInfo> ToPpsDataInfoAsync(this HttpResponseMessage http)
		{
			// in ex codiert
			if (http.Headers.TryGetValue("x-ppsn-object", out var exInfo)
				&& TryUnpackObjectInfo(exInfo, out var objectTyp, out var properties)) // object extension gefunden
			{
				properties.SetMemberValue("ObjectTyp", objectTyp);
			}
			else
				properties = new LuaTable();

			// in header codiert
			foreach (var kv in http.Headers)
			{
				if (kv.Key.StartsWith("x-ppsn-") && kv.Key != "x-ppsn-object")
				{
					var v = kv.Value.FirstOrDefault();
					if (v != null)
						properties.SetMemberValue(kv.Key.Substring(7), v);
				}
			}

			var bytes = await http.Content.ReadAsByteArrayAsync();
			return new PpsGenericDataObject(properties.GetOptionalValue<string>("Name", http.Content.Headers.ContentDisposition?.Name), http.Content.Headers.ContentType.MediaType, bytes, properties.ToProperties());
		} // func ToPpsDataInfoAsync
	} // class PpsDataInfo

	#endregion
}

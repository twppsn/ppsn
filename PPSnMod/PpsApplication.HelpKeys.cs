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
using System.Text;
using Neo.IronLua;
using TecWare.DE.Networking;

namespace TecWare.PPSn.Server
{
	/// <summary>Save and load help keys.</summary>
	public sealed class PpsHelpKeyItem : PpsObjectItem<string>
	{
		/// <summary></summary>
		/// <param name="sp"></param>
		/// <param name="name"></param>
		public PpsHelpKeyItem(IServiceProvider sp, string name) 
			: base(sp, name)
		{
		} // ctor

		/// <summary></summary>
		/// <param name="src"></param>
		/// <returns></returns>
		protected override string GetDataFromStream(Stream src)
		{
			using (var sr = new StreamReader(src))
				return sr.ReadToEnd();
		} // func GetDataFromStream

		/// <summary></summary>
		/// <param name="data"></param>
		/// <returns></returns>
		protected override Stream GetStreamFromData(string data)
			=> new MemoryStream(Encoding.UTF8.GetBytes(data));

		/// <summary>Always create revs.</summary>
		/// <param name="data"></param>
		/// <returns></returns>
		protected override bool IsDataRevision(string data)
			=> true;

		/// <summary>Creates a new attachment object.</summary>
		/// <param name="mimeType"></param>
		/// <returns></returns>
		[LuaMember]
		public PpsObjectAccess CreateNewObject(string mimeType)
		{
			var obj = Application.Objects.CreateNewObject(
				new LuaTable()
				{
					[nameof(PpsObjectAccess.Typ)] = ObjectType,
					[nameof(PpsObjectAccess.MimeType)] = MimeTypes.Text.Plain
				}
			);
			return obj;
		} // func CreateNewObject
	} // class PpsHelpKeyItem
}

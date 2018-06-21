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
using Neo.IronLua;
using TecWare.DE.Server;
using TecWare.DE.Stuff;
using TecWare.PPSn.Server.Wpf;

namespace TecWare.PPSn.Server
{
	/// <summary>Defines a generic package for a ppsn application.</summary>
	public class PpsPackage : DEConfigLogItem, IWpfClientApplicationFileProvider
	{
#pragma warning disable IDE1006 // Naming Styles
		private const string LuaApplicationFiles = "ApplicationFiles";
#pragma warning restore IDE1006 // Naming Styles

		/// <summary></summary>
		/// <param name="sp"></param>
		/// <param name="name"></param>
		public PpsPackage(IServiceProvider sp, string name)
			: base(sp, name)
		{
		} // ctor

		/// <summary></summary>
		/// <returns></returns>
		public virtual IEnumerable<PpsApplicationFileItem> GetApplicationFiles()
		{
			// check the LuaTable "ApplicationFiles"
			if (GetMemberValue(LuaApplicationFiles, rawGet: true) is LuaTable applicationFileProvider)
			{
				foreach (var c in applicationFileProvider)
				{
					if (c.Value is LuaTable item)
					{
						var path = item.GetOptionalValue<string>("path", String.Empty);
						if (!String.IsNullOrEmpty(path))
						{
							path = path[0] == '/'
								? path.Substring(1)
								: Name + "/" + path;

							yield return new PpsApplicationFileItem(path,
								item.ReturnOptionalValue<long>("length", -1),
								item.ReturnOptionalValue<DateTime>("lastModified", DateTime.MinValue)
							);
						}
					}
				}
			}
		} // func GetApplicationFiles

		/// <summary></summary>
		/// <param name="key"></param>
		/// <returns></returns>
		protected override object OnIndex(object key)
		{
			var value = base.OnIndex(key);
			if (value == null && key is string && (string)key == LuaApplicationFiles)
				value = SetMemberValue(LuaApplicationFiles, new LuaTable(), rawSet: true);
			return value;
		} // func OnIndex
	} // class PpsPackage
}

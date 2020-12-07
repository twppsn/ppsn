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
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace TecWare.PPSn.Themes
{
	public static class PpsTheme
	{
		#region -- class PpsThemeKey --------------------------------------------------

		private sealed class PpsThemeKey : ResourceKey
		{
			private readonly string name;

			public PpsThemeKey(string name)
				=> this.name = name ?? throw new ArgumentNullException(nameof(name));

			public override string ToString() 
				=> base.ToString();

			public override bool Equals(object obj)
				=> obj is PpsThemeKey k && k.name == name;

			public override int GetHashCode() 
				=> name.GetHashCode() ^ typeof(PpsTheme).GetHashCode();

			public string Name => name;
			public override Assembly Assembly => typeof(PpsTheme).Assembly;
		} // class PpsThemeKey

		#endregion

		/// <summary>Close geometry</summary>
		public static readonly ResourceKey WindowClosePathGeometry = new PpsThemeKey("windowClose");
	} // class PpsTheme
}

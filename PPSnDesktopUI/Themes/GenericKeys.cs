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
using System.Reflection;
using System.Windows;

namespace TecWare.PPSn.Themes
{
	public static class PpsResource
	{
		#region -- class PpsWpfResourceKey --------------------------------------------------

		private sealed class PpsWpfResourceKey : ResourceKey
		{
			private readonly string name;

			public PpsWpfResourceKey(string name)
			{
				this.name = name ?? throw new ArgumentNullException(nameof(name));
			} // ctor

			public override string ToString()
				=> "PPSn:" + name;

			public override bool Equals(object obj)
				=> obj is PpsWpfResourceKey k && k.name == name;

			public override int GetHashCode()
				=> name.GetHashCode() ^ typeof(PpsWpfResourceKey).GetHashCode();

			public string Name => name;
			public override Assembly Assembly => typeof(PpsWpfResourceKey).Assembly;
		} // class PpsWpfResourceKey

		#endregion

		/// <summary></summary>
		public static ResourceKey ExceptionControlStyle { get; } = new PpsWpfResourceKey(nameof(ExceptionControlStyle));
	} // class PpsResource
}

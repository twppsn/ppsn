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

namespace TecWare.PPSn.UI.Barcodes
{
	#region -- class GenericCode ------------------------------------------------------

	/// <summary>Fallback implementation for unknown codes.</summary>
	public sealed class GenericCode : PpsBarcode
	{
		private readonly string code;

		internal GenericCode(string code)
		{
			this.code = code;
		} // ctor

		public override string ToString()
			=> code;

		/// <summary>Test if there is a code.</summary>
		public override bool IsCodeValid => !String.IsNullOrEmpty(code);
		/// <summary>Name of the code.</summary>
		public override string CodeName => String.Empty;
		/// <summary>Plain code</summary>
		public override string Code => code;
	} // class PpsBarcode

	#endregion
}

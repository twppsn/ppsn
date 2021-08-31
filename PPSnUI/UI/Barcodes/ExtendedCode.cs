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
	#region -- class ExtendedCode -----------------------------------------------------

	/// <summary>Abstrakte Klasse die für die erweiterten Barcodes (HIBC und GS1) eine gemeinsame Verarbeitung ermöglicht</summary>
	public abstract class ExtendedCode : PpsBarcode
	{
		private readonly Lazy<string> encodeCode;

		/// <summary></summary>
		protected ExtendedCode()
		{
			encodeCode = new Lazy<string>(CreateRawCode);
		} // ctor

		/// <summary></summary>
		protected abstract string CreateRawCode();

		/// <summary>Return code representation.</summary>
		public sealed override string Code => encodeCode.Value;
		/// <summary>Serial number</summary>
		public abstract string ProductNr { get; }
		/// <summary>Lot</summary>
		public abstract string LOT { get; }
		/// <summary>Expiration date</summary>
		public abstract DateTime? UseByDate { get; }
		/// <summary>Production date</summary>
		public abstract DateTime? ProductionDate { get; }
	} // class ExtendedCode

	#endregion
}

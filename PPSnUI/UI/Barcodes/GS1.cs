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
	#region -- enum GS1CodeType -------------------------------------------------------

	/// <summary></summary>
	public enum GS1CodeType
	{
		/// <summary>EAN8</summary>
		EAN8,
		/// <summary>EAN12</summary>
		EAN12,
		/// <summary>EAN13</summary>
		EAN13,
		/// <summary>EAN14</summary>
		EAN14,
		/// <summary>GS1</summary>
		GS1,
		/// <summary>Unknown</summary>
		Unknown
	} // enum GS1CodeType

	#endregion

	#region -- class GS1 --------------------------------------------------------------

	/// <summary>GS1 implementation</summary>
	public sealed class GS1 : ExtendedCode
	{
		private readonly GS1CodeType type = GS1CodeType.Unknown;
		private readonly string prodNr = null;
		private readonly string lotNumber = null;
		private readonly string serialNumber = null;
		private readonly int? quantity = null;
		private readonly DateTime? useByDate = null;
		private readonly DateTime? productionDate = null;

		internal GS1(GS1CodeType type, string prodNr, string lotNumber, string serialNumber, int? quantity, DateTime? useByDate, DateTime? productionDate)
		{
			this.type = type;
			this.prodNr = prodNr ?? throw new ArgumentNullException(nameof(prodNr));
			this.lotNumber = lotNumber;
			this.serialNumber = serialNumber;
			this.quantity = quantity;
			this.useByDate = useByDate;
			this.productionDate = productionDate;
		} // ctor

		/// <inherited/>
		protected override string CreateRawCode() 
			=> throw new NotImplementedException();

		/// <inherited/>
		public override string CodeName => type.ToString();

		/// <summary>Global Trade Identification Number (EAN?)</summary>
		public string GTIN => throw new NotImplementedException();
		/// <inherited/>
		public override string ProductNr => prodNr;
		/// <inherited/>
		public override string LOT => lotNumber;
		/// <summary>Seriennummer</summary>
		public string Serialnumber => lotNumber != null && serialNumber != null ? serialNumber : null;
		/// <summary>Menge</summary>
		public int? Menge => quantity;
		/// <inherited/>
		public override DateTime? UseByDate => useByDate;
		/// <inherited/>
		public override DateTime? ProductionDate => productionDate;

		/// <inherited/>
		public override bool IsCodeValid => type != GS1CodeType.Unknown;
	} // class GS1

	#endregion
}

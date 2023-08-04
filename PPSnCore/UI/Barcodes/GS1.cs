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
using System.Text;

namespace TecWare.PPSn.Core.UI.Barcodes
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
		private readonly string gtin = null;
		private readonly string prodNr = null;
		private readonly string lotNumber = null;
		private readonly string serialNumber = null;
		private readonly int? quantity = null;
		private readonly DateTime? useByDate = null;
		private readonly DateTime? productionDate = null;

		private GS1(GS1CodeType type, string prodNr, string lotNumber, string serialNumber, int? quantity, DateTime? useByDate, DateTime? productionDate)
		{
			this.type = type;
			this.prodNr = prodNr ?? throw new ArgumentNullException(nameof(prodNr));
			this.lotNumber = String.IsNullOrEmpty(lotNumber) ? null : lotNumber;
			this.serialNumber = String.IsNullOrEmpty(serialNumber) ? null : serialNumber;
			this.quantity = quantity;
			this.useByDate = useByDate;
			this.productionDate = productionDate;

			gtin = CreateRaw(type == GS1CodeType.GS1 ? GS1CodeType.EAN14 : type, prodNr);
		} // ctor

		/// <summary></summary>
		/// <returns></returns>
		public override string ToString()
		{
			if (type == GS1CodeType.GS1)
			{
				const string rsNone = "<keine>";
				const string rsNone2 = "<keiner>";
				return String.Join(Environment.NewLine,
					new string[]
					{
						"Product\t" + prodNr,
						"Lot Number\t" + (lotNumber ?? rsNone),
						"Expiration Date\t" + (useByDate.HasValue ? useByDate.Value.ToString() : rsNone2),
						"Serial Number\t" + (serialNumber ?? rsNone),
						"Item Count\t" + (quantity.HasValue ? quantity.Value.ToString() : rsNone)
					}
				);
			}
			else
				return gtin;
		} // func ToString

		/// <inherited/>
		protected override string CreateRawCode()
		{
			return lotNumber != null || serialNumber != null || quantity.HasValue || useByDate.HasValue
				? CreateRaw(prodNr, lotNumber, useByDate, quantity)
				: CreateRaw(type, prodNr);
		} // func CreateRawCode

		/// <inherited/>
		public override string CodeName => type.ToString();

		/// <summary>Global Trade Identification Number (EAN?)</summary>
		public string GTIN => gtin;
		/// <inherited/>
		public override string ProductNr => prodNr;
		/// <inherited/>
		public override string LOT => lotNumber;
		/// <summary>Seriennummer</summary>
		public string Serialnumber => lotNumber != null && serialNumber != null ? serialNumber : null;
		/// <summary>Menge</summary>
		public override decimal? Menge => quantity;
		/// <inherited/>
		public override DateTime? UseByDate => useByDate;
		/// <inherited/>
		public override DateTime? ProductionDate => productionDate;

		/// <inherited/>
		public override bool IsCodeValid => type != GS1CodeType.Unknown;

		#region -- CalculateCheckSum --------------------------------------------------

		private static char CalculateCheckSum(string code, int offset, int length, bool throwException)
		{
			var checkSum = 0;
			var multiply = true;

			for (var i = offset + length - 1; i >= offset; i--)
			{
				var c = code[i];
				if (c < 48 || c > 57)
				{
					if (throwException)
						throw new ArgumentOutOfRangeException(nameof(code), (int)c, $"Numer expected, ({c} at {i}).");
					return '\0';
				}

				if (multiply)
				{
					checkSum += (c - 48) * 3;
					multiply = false;
				}
				else
				{
					checkSum += c - 48;
					multiply = true;
				}
			}

			var p = checkSum % 10;
			return (char)(p > 0 ? (10 + 48 - p) : '0');
		} // func CalculateCheckSum

		private static bool VerifyCheckDigit(string code, int offset, int length)
		{
			var cs = CalculateCheckSum(code, offset, length - 1, false);
			return cs != '\0' && code[offset + length - 1] == cs;
		} // func VerifyCheckDigit

		#endregion

		#region -- TryParse -----------------------------------------------------------

		private static void ParseApplicationIdentifier(string code, int offset, ref string lotNumber, ref string serialNumber, ref DateTime? useByDate, ref DateTime? productionDate, ref int? quantity)
		{
			while (code.Length - offset > 2)
			{
				switch (code.Substring(offset, 2))
				{
					case "10": // Charge
						offset += 2;

						var lotNumberStart = offset;
						while (offset < code.Length && code[offset] != '\u001d')
							offset++;

						lotNumber = code.Substring(lotNumberStart, offset - lotNumberStart);
						break;
					case "11": // Produktionsdatum
						offset += 2;

						if (TryDecodeDate(code, ref offset, out var dtTmp))
							productionDate = dtTmp;

						break;
					case "15": // UseBy
					case "16": // Best Before
					case "17": // Expiration (Health Risk)
						offset += 2;
						if (TryDecodeDate(code, ref offset, out var currentDate))
						{
							useByDate = (useByDate == null)
								? currentDate
								: ((useByDate < currentDate) ? useByDate : currentDate);
						}
						break;
					case "21": // Seriennummer, erweiterung der Charge
						offset += 2;
						var serialNumberStart = offset;

						while (offset < code.Length && code[offset] != '\u001d')
							offset++;

						serialNumber = code.Substring(serialNumberStart, offset - serialNumberStart);
						break;
					case "30": // Menge
						offset += 2;
						var maxWidth = offset + 8;
						var countStart = offset;

						while (offset < code.Length && code[offset] != '\u001d') // letzte Information/durch <FUNC> abgeschlossen/maximale Feldbreite ''8'' erreicht
						{
							offset++;
							if (offset >= maxWidth) // something went wrong, only 8 digits allowed, ignore rest
								break;
						}

						if (Int32.TryParse(code.Substring(countStart, offset - countStart), out var tmp) && tmp > 0)
							quantity = tmp;
						break;
					default: // ignore
						while (offset < code.Length && code[offset] != '\u001d')
							offset++;
						break;
				}

				// ignore group seperator
				if (offset < code.Length && code[offset] == '\u001d')
					offset++;
			}
		} // proc ParseApplicationIdentifier

		private static bool TryDecodeDate(string code, ref int offset, out DateTime date)
		{
			// wird nicht als DateTime geparsed, weil GS1-Standart die Bestimmung des Jahrhunderts vorschreibt
			if (offset + 6 > code.Length
				|| !Int32.TryParse(code.Substring(offset + 0, 2), out var year)
				|| !Int32.TryParse(code.Substring(offset + 2, 2), out var month)
				|| !Int32.TryParse(code.Substring(offset + 4, 2), out var day))
			{
				date = DateTime.MinValue;
				return false;
			}
			offset += 6;

			var currentYear = DateTime.Now.Year % 100;
			var yearDiff = year - currentYear;
			if ((50 < yearDiff) && (yearDiff < 100))
				year += (currentYear - 1) * 100;
			else if ((-49 > yearDiff) && (yearDiff > -100))
				year += (currentYear + 1) * 100;
			else
				year += DateTime.Now.Year - currentYear;

			// on dates, the day can be empty (0-padded), in that case the product expires the last day of the given month (ref. GS1spec - 3.4.2)
			date = day == 0
				? new DateTime(year, month + 1, 1).AddDays(-1)
				: new DateTime(year, month, day);

			return true;
		} // proc DecodeDate

		private static bool VerifyEan(string code, int offset, int length, out string prodNr)
		{
			if (VerifyCheckDigit(code, offset, length))
			{
				prodNr = code.Substring(offset, length - 1).TrimStart('0');
				return true;
			}
			else
			{
				prodNr = null;
				return false;
			}
		} // proc VerifyEan

		private static GS1 CreateEan(string code, int offset, int length, GS1CodeType type)
		{
			return VerifyEan(code, offset, length, out var prodNr)
				? new GS1(type, prodNr, null, null, null, null, null)
				: null;
		} // proc CreateEan

		private static bool IsGS1Core(string code)
			=> code.Length >= 16 && code[0] == '0' && code[1] == '1';

		/// <summary>Parse gs1</summary>
		/// <param name="rawCode"></param>
		/// <returns></returns>
		public static GS1 TryParse(string rawCode)
		{
			if (rawCode.Length == 8) // EAN8 mit Prüfsumme
				return CreateEan(rawCode, 0, 8, GS1CodeType.EAN8);
			else if (rawCode.Length == 12) // EAN12 mit Prüfsumme
				return CreateEan(rawCode, 0, 12, GS1CodeType.EAN12);
			else if (rawCode.Length == 13) // EAN13 mit Prüfsumme
				return CreateEan(rawCode, 0, 13, GS1CodeType.EAN13);
			else if (rawCode.Length == 14) // EAN14 mit Prüfsumme
				return CreateEan(rawCode, 0, 14, GS1CodeType.EAN14);

			else if (IsGS1Core(rawCode) && VerifyEan(rawCode, 2, 14, out var prodNr))  // Global Trade Item Number, grundsätzlich 13 (EAN14) stellig (ggf. vorangestellte 0en) plus Prüfziffer
			{
				string lotNumber = null;
				string serialNumber = null;
				int? quantity = null;
				DateTime? useByDate = null;
				DateTime? productionDate = null;

				if (rawCode.Length > 16)
					ParseApplicationIdentifier(rawCode, 16, ref lotNumber, ref serialNumber, ref useByDate, ref productionDate, ref quantity);

				return new GS1(GS1CodeType.GS1, prodNr, lotNumber, serialNumber, quantity, useByDate, productionDate);
			}
			else
				return null;
		} // func TryParse

		#endregion

		#region -- Create -------------------------------------------------------------

		private static string CreateEanCore(int maxLength, string prodNr)
		{
			if (maxLength < prodNr.Length)
				throw new ArgumentOutOfRangeException(nameof(prodNr), prodNr.Length, $"Product number is greater than {maxLength}");

			var code = prodNr.PadLeft(maxLength, '0');
			return code + CalculateCheckSum(code, 0, maxLength, true);
		} // func CreateEanCore

		/// <summary>Erstellt einen GS1-Barcode-String anhand der Eingabedaten.</summary>
		/// <param name="prodNr"></param>
		/// <param name="lotNumber"></param>
		/// <param name="useByDate"></param>
		/// <param name="menge"></param>
		/// <returns></returns>
		public static string CreateRaw(string prodNr, string lotNumber = null, DateTime? useByDate = null, int? menge = null)
		{
			if (String.IsNullOrEmpty(prodNr))
				throw new ArgumentNullException(nameof(prodNr));

			var sb = new StringBuilder("01", 256);

			// product number
			sb.Append(CreateEanCore(13, prodNr));

			if (useByDate.HasValue) // Verfallsdatum
				sb.Append("17").Append(useByDate.Value.ToString("yyMMdd"));

			if (String.IsNullOrEmpty(lotNumber))
			{
				if (menge.HasValue)
					sb.Append("30").Append(menge);
			}
			else // Charge
			{
				sb.Append("10").Append(lotNumber);
				if (menge.HasValue)
					sb.Append("\u001d30").Append(menge);

			}

			return sb.ToString();
		}  // func CreateRaw

		/// <summary>Erstellt einen GS1-Barcode-String anhand der Eingabedaten.</summary>
		/// <param name="type"></param>
		/// <param name="prodNr"></param>
		/// <returns></returns>
		public static string CreateRaw(GS1CodeType type, string prodNr)
		{
			if (String.IsNullOrEmpty(prodNr))
				throw new ArgumentNullException(nameof(prodNr));

			switch (type)
			{
				case GS1CodeType.EAN8:
					return CreateEanCore(7, prodNr);
				case GS1CodeType.EAN12:
					return CreateEanCore(11, prodNr);
				case GS1CodeType.EAN13:
					return CreateEanCore(12, prodNr);
				case GS1CodeType.EAN14:
					return CreateEanCore(13, prodNr);
				case GS1CodeType.GS1:
					return "01" + CreateEanCore(13, prodNr);
				default:
					throw new ArgumentException(nameof(type));
			}
		} // CreateRaw

		/// <summary>Erstellt einen GS1-Barcode-String anhand der Eingabedaten.</summary>
		/// <param name="prodNr"></param>
		/// <param name="lotNumber"></param>
		/// <param name="verfall"></param>
		/// <param name="menge"></param>
		/// <returns></returns>
		public static GS1 Create(string prodNr, string lotNumber = null, DateTime? verfall = null, int? menge = null)
			=> new GS1(GS1CodeType.GS1, prodNr, lotNumber, null, menge, verfall, null);

		/// <summary>Erstellt einen GS1-Barcode-String anhand der Eingabedaten.</summary>
		/// <param name="type"></param>
		/// <param name="prodNr"></param>
		/// <returns></returns>
		public static GS1 Create(GS1CodeType type, string prodNr)
			=> new GS1(type, prodNr, null, null, null, null, null);

		#endregion
	} // class GS1

	#endregion
}

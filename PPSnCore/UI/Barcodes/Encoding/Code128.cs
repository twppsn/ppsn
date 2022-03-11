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
using System.Globalization;
using System.Text;

namespace TecWare.PPSn.Core.UI.Barcodes.Encoding
{
	/// <summary>Code128 encoder</summary>
	public static class Code128
	{
		#region -- enum CodeSet -------------------------------------------------------

		[Flags]
		private enum CodeSet
		{
			None = 0,
			A = 1,
			B = 2,
			C = 4,
			AB = A | B
		} // enum CodeSet

		#endregion

		#region -- Code Pattern -------------------------------------------------------

		private static readonly byte[,] patterns =
		{
			{ 2, 1, 2, 2, 2, 2 },  // 0
            { 2, 2, 2, 1, 2, 2 },  // 1
            { 2, 2, 2, 2, 2, 1 },  // 2
            { 1, 2, 1, 2, 2, 3 },  // 3
            { 1, 2, 1, 3, 2, 2 },  // 4
            { 1, 3, 1, 2, 2, 2 },  // 5
            { 1, 2, 2, 2, 1, 3 },  // 6
            { 1, 2, 2, 3, 1, 2 },  // 7
            { 1, 3, 2, 2, 1, 2 },  // 8
            { 2, 2, 1, 2, 1, 3 },  // 9
            { 2, 2, 1, 3, 1, 2 },  // 10
            { 2, 3, 1, 2, 1, 2 },  // 11
            { 1, 1, 2, 2, 3, 2 },  // 12
            { 1, 2, 2, 1, 3, 2 },  // 13
            { 1, 2, 2, 2, 3, 1 },  // 14
            { 1, 1, 3, 2, 2, 2 },  // 15
            { 1, 2, 3, 1, 2, 2 },  // 16
            { 1, 2, 3, 2, 2, 1 },  // 17
            { 2, 2, 3, 2, 1, 1 },  // 18
            { 2, 2, 1, 1, 3, 2 },  // 19
            { 2, 2, 1, 2, 3, 1 },  // 20
            { 2, 1, 3, 2, 1, 2 },  // 21
            { 2, 2, 3, 1, 1, 2 },  // 22
            { 3, 1, 2, 1, 3, 1 },  // 23
            { 3, 1, 1, 2, 2, 2 },  // 24
            { 3, 2, 1, 1, 2, 2 },  // 25
            { 3, 2, 1, 2, 2, 1 },  // 26
            { 3, 1, 2, 2, 1, 2 },  // 27
            { 3, 2, 2, 1, 1, 2 },  // 28
            { 3, 2, 2, 2, 1, 1 },  // 29
            { 2, 1, 2, 1, 2, 3 },  // 30
            { 2, 1, 2, 3, 2, 1 },  // 31
            { 2, 3, 2, 1, 2, 1 },  // 32
            { 1, 1, 1, 3, 2, 3 },  // 33
            { 1, 3, 1, 1, 2, 3 },  // 34
            { 1, 3, 1, 3, 2, 1 },  // 35
            { 1, 1, 2, 3, 1, 3 },  // 36
            { 1, 3, 2, 1, 1, 3 },  // 37
            { 1, 3, 2, 3, 1, 1 },  // 38
            { 2, 1, 1, 3, 1, 3 },  // 39
            { 2, 3, 1, 1, 1, 3 },  // 40
            { 2, 3, 1, 3, 1, 1 },  // 41
            { 1, 1, 2, 1, 3, 3 },  // 42
            { 1, 1, 2, 3, 3, 1 },  // 43
            { 1, 3, 2, 1, 3, 1 },  // 44
            { 1, 1, 3, 1, 2, 3 },  // 45
            { 1, 1, 3, 3, 2, 1 },  // 46
            { 1, 3, 3, 1, 2, 1 },  // 47
            { 3, 1, 3, 1, 2, 1 },  // 48
            { 2, 1, 1, 3, 3, 1 },  // 49
            { 2, 3, 1, 1, 3, 1 },  // 50
            { 2, 1, 3, 1, 1, 3 },  // 51
            { 2, 1, 3, 3, 1, 1 },  // 52
            { 2, 1, 3, 1, 3, 1 },  // 53
            { 3, 1, 1, 1, 2, 3 },  // 54
            { 3, 1, 1, 3, 2, 1 },  // 55
            { 3, 3, 1, 1, 2, 1 },  // 56
            { 3, 1, 2, 1, 1, 3 },  // 57
            { 3, 1, 2, 3, 1, 1 },  // 58
            { 3, 3, 2, 1, 1, 1 },  // 59
            { 3, 1, 4, 1, 1, 1 },  // 60
            { 2, 2, 1, 4, 1, 1 },  // 61
            { 4, 3, 1, 1, 1, 1 },  // 62
            { 1, 1, 1, 2, 2, 4 },  // 63
            { 1, 1, 1, 4, 2, 2 },  // 64
            { 1, 2, 1, 1, 2, 4 },  // 65
            { 1, 2, 1, 4, 2, 1 },  // 66
            { 1, 4, 1, 1, 2, 2 },  // 67
            { 1, 4, 1, 2, 2, 1 },  // 68
            { 1, 1, 2, 2, 1, 4 },  // 69
            { 1, 1, 2, 4, 1, 2 },  // 70
            { 1, 2, 2, 1, 1, 4 },  // 71
            { 1, 2, 2, 4, 1, 1 },  // 72
            { 1, 4, 2, 1, 1, 2 },  // 73
            { 1, 4, 2, 2, 1, 1 },  // 74
            { 2, 4, 1, 2, 1, 1 },  // 75
            { 2, 2, 1, 1, 1, 4 },  // 76
            { 4, 1, 3, 1, 1, 1 },  // 77
            { 2, 4, 1, 1, 1, 2 },  // 78
            { 1, 3, 4, 1, 1, 1 },  // 79
            { 1, 1, 1, 2, 4, 2 },  // 80
            { 1, 2, 1, 1, 4, 2 },  // 81
            { 1, 2, 1, 2, 4, 1 },  // 82
            { 1, 1, 4, 2, 1, 2 },  // 83
            { 1, 2, 4, 1, 1, 2 },  // 84
            { 1, 2, 4, 2, 1, 1 },  // 85
            { 4, 1, 1, 2, 1, 2 },  // 86
            { 4, 2, 1, 1, 1, 2 },  // 87
            { 4, 2, 1, 2, 1, 1 },  // 88
            { 2, 1, 2, 1, 4, 1 },  // 89
            { 2, 1, 4, 1, 2, 1 },  // 90
            { 4, 1, 2, 1, 2, 1 },  // 91
            { 1, 1, 1, 1, 4, 3 },  // 92
            { 1, 1, 1, 3, 4, 1 },  // 93
            { 1, 3, 1, 1, 4, 1 },  // 94
            { 1, 1, 4, 1, 1, 3 },  // 95
            { 1, 1, 4, 3, 1, 1 },  // 96
            { 4, 1, 1, 1, 1, 3 },  // 97
            { 4, 1, 1, 3, 1, 1 },  // 98
            { 1, 1, 3, 1, 4, 1 },  // 99
            { 1, 1, 4, 1, 3, 1 },  // 100
            { 3, 1, 1, 1, 4, 1 },  // 101
            { 4, 1, 1, 1, 3, 1 },  // 102
            { 2, 1, 1, 4, 1, 2 },  // 103
            { 2, 1, 1, 2, 1, 4 },  // 104
            { 2, 1, 1, 2, 3, 2 },  // 105
            { 2, 3, 3, 1, 1, 1 }   // 106
        };

		#endregion

		#region -- class CodeArray ----------------------------------------------------

		private sealed class CodeArray
		{
			private readonly List<byte> codes;
			private int checkSum = 0;

			public CodeArray(int capacity)
			{
				codes = new List<byte>(capacity);
			} // ctor

			public void Add(byte code)
			{
				// calc check sum
				var count = codes.Count;
				if (count == 0)
					checkSum = code;
				else
					checkSum += count * code;

				codes.Add(code);
			} // proc Add

			public byte[] ToArray()
			{
				codes.Add((byte)(checkSum % 103));
				codes.Add(106);
				return codes.ToArray();
			} // proc AddStopCode
		} // class CodeArray

		#endregion

		#region -- Encode -------------------------------------------------------------

		private static CodeSet GetCodeSet(char c)
		{
			if (c < 32)
				return CodeSet.A;
			else if (c < 48) // 0
				return CodeSet.A | CodeSet.B;
			else if (c <= 58) // 9
				return CodeSet.A | CodeSet.B | CodeSet.C;
			else if (c <= 95) // 
				return CodeSet.A | CodeSet.B;
			else if (c <= 127)
				return CodeSet.B;
			else
				return CodeSet.None;
		} // func GetCodeSet

		private static void AnalyzeChars(string payload, int endAt, int offset, out int nextOnlyA, out int nextOnlyB, out int nextNumberAt, out int lastNumberAt)
		{
			var onlyNumbers = true;
			var onlyASet = false;
			var onlyBSet = false;

			nextOnlyA = Int32.MaxValue;
			nextOnlyB = Int32.MaxValue;
			nextNumberAt = Int32.MaxValue;
			lastNumberAt = offset;

			while (offset <= endAt)
			{
				var cs = GetCodeSet(payload[offset]);
				if (cs == CodeSet.None)
					throw new FormatException($"Invalid char at {offset}");
				else
				{
					if ((cs & CodeSet.C) != 0)
					{
						if (onlyNumbers)
							lastNumberAt = offset;
						else if (nextNumberAt == Int32.MaxValue)
							nextNumberAt = offset;
					}
					else
						onlyNumbers = false;

					if (!onlyASet && cs == CodeSet.A)
					{
						onlyNumbers = false;
						nextOnlyA = offset;
						onlyASet = true;
						if (onlyBSet)
							break;
					}
					else if (!onlyBSet && cs == CodeSet.B)
					{
						onlyNumbers = false;
						nextOnlyB = offset;
						onlyBSet = true;
						if (onlyASet)
							break;
					}
				}
				offset++;
			}
		} // func AnalyzeChars

		private static void GetNextCodeSet(bool isFirst, string payload, int offset, int endAt, out CodeSet codeSet, out int nextAt, out bool emitCurrentCodeSetBeforeC)
		{
			AnalyzeChars(payload, endAt, offset, out var nextOnlyA, out var nextOnlyB, out var nextNumberAt, out var lastNumberAt);

			// - Mindestens 4 Zahlen müssen für Type C gefunden werden
			// - Der Code besteht nur aus Zahlen
			// - Zahlen müssen ans Ende ausgerichtet werden
			var numberCount = lastNumberAt - offset + 1;
			if (((lastNumberAt == endAt || isFirst) && numberCount > 3) // code besteht komplett aus zahhlen, und hat mindestens 4 Zahlen
				|| numberCount > 5) // code hat mindesten 6 aufeinander folgende Zahlen 
			{
				var isOdd = (numberCount & 1) == 1; // ungerade
				codeSet = CodeSet.C;
				nextAt = lastNumberAt;

				if (isOdd)
				{
					if (isFirst)
					{
						nextAt--;
						emitCurrentCodeSetBeforeC = false;
					}
					else
						emitCurrentCodeSetBeforeC = isOdd;
				}
				else
					emitCurrentCodeSetBeforeC = false;
			}
			else if (nextOnlyA > nextOnlyB) // wenigsten codewechsel gewinnen, mit vorrang A
			{
				codeSet = CodeSet.B;
				nextAt = Math.Min(endAt, Math.Min(nextNumberAt, nextOnlyA) - 1);
				emitCurrentCodeSetBeforeC = false;
			}
			else
			{
				codeSet = CodeSet.A;
				nextAt = Math.Min(endAt, Math.Min(nextNumberAt, nextOnlyB) - 1);
				emitCurrentCodeSetBeforeC = false;
			}
		} // proc GetNextCodeSet

		private static byte GetStartCode(CodeSet codeSet)
		{
			switch (codeSet)
			{
				case CodeSet.A:
					return 103;
				case CodeSet.B:
					return 104;
				case CodeSet.C:
					return 105;
				default:
					throw new ArgumentOutOfRangeException();
			}
		} // func GetStartCode

		private static void EmitCode(CodeArray codes, CodeSet codeSet, string payload, int offset)
		{
			var c = payload[offset];
			var cs = GetCodeSet(c);

			if ((cs & codeSet) == CodeSet.None)
				throw new ArgumentOutOfRangeException(nameof(payload), payload, $"Char 0x{((byte)c):X2} does match CodeSet-{codeSet} at {offset}.");
			else if ((cs & CodeSet.AB) == CodeSet.AB) // 32 bis 95
				codes.Add((byte)(c - 32));
			else if (cs == CodeSet.A)
				codes.Add((byte)(c + 64));
			else if (cs == CodeSet.B)
				codes.Add((byte)(c - 32));
			else
				throw new InvalidOperationException($"EmitCode is not defined for CodeSet-{codeSet}."); // dürfte nicht passieren, außer C steht in codeSet
		} // func EmitCode

		private static void EmitCodes(CodeArray codes, CodeSet codeSet, string payload, int offset, int endAt)
		{
			if (codeSet == CodeSet.C)
			{
				while (offset <= endAt)
				{
					var c1 = payload[offset] - '0';
					var c2 = payload[offset + 1] - '0';
					if (c1 > 9 || c2 > 9)
						throw new ArgumentOutOfRangeException(nameof(payload), payload, $"Invalid CodeSet-C at {offset}");
					codes.Add((byte)(c1 * 10 + c2));
					offset += 2;
				}
			}
			else
			{
				while (offset <= endAt)
					EmitCode(codes, codeSet, payload, offset++);
			}
		} // proc EmitCodes

		/// <summary>Encode payload to code128 codes (0-127)</summary>
		/// <param name="payload"></param>
		/// <param name="offset"></param>
		/// <param name="count"></param>
		/// <returns>Raw codes.</returns>
		internal static byte[] Encode(string payload, int offset, int count)
		{
			if (String.IsNullOrEmpty(payload))
				throw new ArgumentNullException(nameof(payload));

			// result buffer
			var codes = new CodeArray(payload.Length * 12 / 10);
			var endAt = Math.Min(offset + count - 1, payload.Length - 1);

			// calc start codeset
			GetNextCodeSet(true, payload, offset, endAt, out var codeSet, out var nextAt, out var emitCurrentCodeSetBeforeC);
			if (emitCurrentCodeSetBeforeC) // StartCode Ermittlung
				throw new InvalidOperationException();

			// emit start sequence
			codes.Add(GetStartCode(codeSet));
			EmitCodes(codes, codeSet, payload, offset, nextAt);
			offset = nextAt + 1;

			// calc next codesets
			while (offset <= endAt)
			{
				GetNextCodeSet(false, payload, offset, endAt, out var nextCodeSet, out nextAt, out emitCurrentCodeSetBeforeC);

				// emit uneven char
				if (emitCurrentCodeSetBeforeC)
				{
					EmitCodes(codes, codeSet, payload, offset, offset);
					offset++;
				}

				if (codeSet != nextCodeSet
					&& (codeSet & CodeSet.AB) != 0 && (nextCodeSet & CodeSet.AB) != 0
					&& nextAt == offset) // only one char -> shift mode
				{
					codes.Add(98); // SHIFT
					EmitCode(codes, nextCodeSet, payload, offset);
					offset++;
				}
				else
				{
					// emit codes, change codeset
					if (codeSet != nextCodeSet)
					{
						if (nextCodeSet == CodeSet.C)
							codes.Add(99);
						else if (nextCodeSet == CodeSet.B)
							codes.Add(100);
						else if (nextCodeSet == CodeSet.A)
							codes.Add(101);
						else
							throw new InvalidOperationException($"Unknown CodeSet-Change {codeSet} -> {nextCodeSet}");
						codeSet = nextCodeSet;
					}
					EmitCodes(codes, codeSet, payload, offset, nextAt);
					offset = nextAt + 1;
				}
			}

			return codes.ToArray();
		} // func Encode

		/// <summary>Encode payload to code bars</summary>
		/// <param name="payload"></param>
		/// <param name="offset"></param>
		/// <param name="count"></param>
		/// <returns>Black, white pattern, with relative bar withs.</returns>
		public static (byte[] pattern, int totalWidth) ToBars(string payload, int offset = 0, int count = Int32.MaxValue)
		{
			var codes = Encode(payload, offset, count);
			var bars = new byte[codes.Length * 6 + 1];
			var totalWidth = 0;
			var k = 0;
			for (var i = 0; i < codes.Length; i++)
			{
				for (var j = 0; j < 6; j++)
				{
					var w = patterns[codes[i], j];
					bars[k++] = w;
					totalWidth += w;
				}
			}
			bars[k] = 2; // stop bars
			totalWidth += 2;

			return (bars, totalWidth);
		} // func ToBars

		#endregion

		#region -- Render -------------------------------------------------------------

		private static StringBuilder Num(this StringBuilder sb, float v)
			=> sb.Append(v.ToString(CultureInfo.InvariantCulture));

		private static StringBuilder M(this StringBuilder sb, float x, float y)
			=> sb.Append('M').Num(x).Append(',').Append(y);

		private static StringBuilder H(this StringBuilder sb, float x)
			=> sb.Append('H').Num(x);

		private static StringBuilder V(this StringBuilder sb, float y)
			=> sb.Append('V').Num(y);

		private static StringBuilder Z(this StringBuilder sb)
			=> sb.Append('Z');

		/// <summary>Convert the bars to an path.</summary>
		/// <param name="payload"></param>
		/// <param name="offset"></param>
		/// <param name="count"></param>
		/// <param name="left"></param>
		/// <param name="top"></param>
		/// <param name="width"></param>
		/// <param name="height"></param>
		/// <returns>A wpf/svg path.</returns>
		public static string ToPath(string payload, int offset = 0, int count = Int32.MaxValue, float left = 0.0f, float top = 0.0f, float width = Single.NaN, float height = Single.NaN)
		{
			var sb = new StringBuilder();

			// calc code128
			var (bars, totalWidth) = ToBars(payload, offset, count);

			var hasWidth = !Single.IsNaN(width);
			var barWidth = hasWidth ? width / totalWidth : 1.0f;
			var bottom = top + (Single.IsNaN(height)
				? (float)Math.Ceiling((hasWidth ? width : totalWidth) * 0.3f)
				: height);

			var isBlack = true;
			for (var i = 0; i < bars.Length; i++)
			{
				var startLeft = left;
				left += barWidth * bars[i];
				if (isBlack)
				{
					sb.M(startLeft, 0).H(left).V(bottom).H(startLeft).Z();
					isBlack = false;
				}
				else
					isBlack = true;
			}

			return sb.ToString();
		} // func EncodePath

		#endregion
	} // class Code128
}

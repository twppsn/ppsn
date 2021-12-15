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

namespace PPSnExcel.Data
{
	/// <summary>
	/// Caesar cipher is a simple substitution cipher used for text encryption/decryption.
	/// This impl. extends the algorithm to encrypt digits and use a enc-dec key
	/// </summary>
	internal static class CaesarCipher
	{
		private static string Transform(string text, int key)
		{
			var result = new char[text.Length];

			for (var i = 0; i < text.Length; i++)
				result[i] = TransformChar(text[i], key);

			return new string(result, 0, result.Length);
		} // func Transform

		private static char TransformChar(char c, char n, int m, int key)
			=> (char)(n + (c - n + key) % m);

		private static char TransformChar(char c, int key)
		{
			if (c >= '0' && c <= '9')
				return TransformChar(c, '0', 10, key);
			else if (c >= 'A' && c <= 'Z')
				return TransformChar(c, 'A', 26, key);
			else if (c >= 'a' && c <= 'z')
				return TransformChar(c, 'a', 26, key);
			else
			{
				var t = (c + key) % 62;
				return t < 26 ? (char)('A' + t) : (char)('a' + t - 26);
			}
		} // func TransformChar

		public static string Encrypt(string text, int key)
			=> Transform(text, key);
	} // class CaesarCipher
}

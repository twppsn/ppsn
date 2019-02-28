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
using System.Text;
using System.Text.RegularExpressions;

namespace TecWare.PPSn
{
	/// <summary></summary>
	public class ExcelException : Exception
	{
		/// <summary></summary>
		/// <param name="message"></param>
		public ExcelException(string message)
			: base(message)
		{
		} // ctor
	} // class ExcelException

	public static class XlProcs
	{
		private static readonly Regex lineProperties = new Regex(@"^\s*@@(?<k>\w+)\s*\=\s*(?<v>.*)\s*$", RegexOptions.Compiled);
		private static readonly Regex lineSplitter = new Regex(@"\r\n|\r|\n", RegexOptions.Compiled);

		public static string UpdateProperties(string comment, params KeyValuePair<string, string>[] args)
		{
			var sb = new StringBuilder();

			void AppendKeyValue(KeyValuePair<string, string> kv)
			{
				if (!String.IsNullOrEmpty(kv.Value))
					sb.Append("@@").Append(kv.Key).Append('=').AppendLine(kv.Value.Replace("\n", "\\n"));
			} // proc AppendKeyValue

			var updated = new bool[args.Length];
			for (var i = 0; i < updated.Length; i++)
				updated[i] = false;

			foreach (var l in lineSplitter.Split(comment ?? String.Empty))
			{
				var m = lineProperties.Match(l);
				if (m.Success)
				{
					var k = m.Groups["k"].Value;
					var i = Array.FindIndex(args, kv => kv.Key == k);
					if (i == -1)
						sb.AppendLine(l);
					else
					{
						updated[i] = true;
						AppendKeyValue(args[i]);
					}
				}
				else
					sb.AppendLine(l);
			}

			for (var i = 0; i < updated.Length; i++)
			{
				if (!updated[i])
					AppendKeyValue(args[i]);
			}

			return sb.ToString();
		} // func UpdateProperties

		public static IEnumerable<KeyValuePair<string, string>> GetLineProperties(string comment)
		{
			foreach (var l in lineSplitter.Split(comment ?? String.Empty))
			{
				var m = lineProperties.Match(l);
				if (m.Success)
					yield return new KeyValuePair<string, string>(m.Groups["k"].Value, m.Groups["v"].Value);
			}
		} // func GetLineProperties
	} // class XlProcs
}

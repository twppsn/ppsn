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
using System.Xml;
using System.Xml.Linq;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.Stuff
{
	#region -- class PpsXmlPosition ---------------------------------------------------

	/// <summary>Class to read source file hints from an xml-element.</summary>
	public sealed class PpsXmlPosition : IXmlLineInfo
	{
		/// <summary>Namespace that defines source file hints.</summary>
		public readonly static XNamespace XmlPositionNamespace = "http://tecware-gmbh.de/dev/ppsn/2014/source";

		/// <summary>Tag to write the line number.</summary>
		public readonly static XName xnLineNumber = XmlPositionNamespace + "l";
		/// <summary>Tag to write the line position (column).</summary>
		public readonly static XName xnLinePosition = XmlPositionNamespace + "p";
		/// <summary>Tag to write the file name.</summary>
		public readonly static XName xnFileName = XmlPositionNamespace + "f";

		private readonly int lineNumber;
		private readonly int linePosition;
		private readonly string fileName;

		private PpsXmlPosition(string fileName, int lineNumber, int linePosition)
		{
			this.fileName = fileName;
			this.lineNumber = lineNumber;
			this.linePosition = linePosition;
		} // ctor

		/// <summary>Format the line informations.</summary>
		/// <returns></returns>
		public override string ToString()
			=> String.Format("{0} ({1},{2})", FileName, lineNumber, linePosition);

		/// <summary>Copy the annotations to an <c>XElement</c> as attributes.</summary>
		/// <param name="x">Element to set the current source information.</param>
		public void WriteAttributes(XElement x)
		{
			// todo: recursiv absteigen, damit nicht daurnt der dateiname gesetzt wird
			x.SetAttributeValue(xnFileName, fileName);
			if (lineNumber >= 0)
				x.SetAttributeValue(xnLineNumber, lineNumber);
			if (linePosition >= 0)
				x.SetAttributeValue(xnLinePosition, linePosition);
		} // proc WriteAttribute

		/// <summary>Copy the annotations to an <c>XElement</c> as attributes.</summary>
		/// <param name="x">Element to set the current source information.</param>
		public void WriteLineInfo(XElement x)
		{
			// todo: Annotations setzen
			throw new NotImplementedException();
		} // proc WriteLineInfo

		/// <summary>Create a comment from the source file information.</summary>
		/// <returns></returns>
		public XComment GetComment()
			=> new XComment("L=" + LineInfo);

		/// <summary>Did the class find a line information.</summary>
		/// <returns><c>true</c>, the node has a line number.</returns>
		public bool HasLineInfo()
			=> lineNumber >= 0;

		/// <summary>Line number</summary>
		public int LineNumber => lineNumber;
		/// <summary>Line position (column)</summary>
		public int LinePosition => linePosition;
		/// <summary>Source file name</summary>
		public string FileName => fileName ?? "unknown.xml";

		/// <summary>Is the file name set.</summary>
		public bool HasFileName
			=> fileName != null;

		/// <summary>Get the source file information in a pretty format.</summary>
		public string LineInfo
			=> lineNumber >= 0 ? FileName + "," + lineNumber.ToString() : FileName;

		/// <summary>Is the source file information empty.</summary>
		public bool IsEmpty
			=> fileName == null && lineNumber == -1 && linePosition == -1;

		// -- Static --------------------------------------------------------------

		private static string GetFileNameFromUri(string baseUri)
		{
			if (String.IsNullOrEmpty(baseUri))
				return null;
			return Procs.GetFileName(new Uri(baseUri));
		} // func GetFileNameFromUri

		private static IXmlLineInfo GetXmlLineInfo(XNode x)
		{
			var c = x;
			while (c != null)
			{
				if (c is IXmlLineInfo r)
					return r;
				else if (c is XElement)
					return null;

				c = c.Parent;
			}
			return null;
		} // func GetXmlLineInfo

		/// <summary>Uses XmlLineInfo to retrieve the position.</summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static PpsXmlPosition GetXmlPositionFromXml(XNode x)
		{
			var fileName = GetFileNameFromUri(x.BaseUri);
			var position = GetXmlLineInfo(x);
			if (position != null)
				return new PpsXmlPosition(fileName, position.LineNumber, position.LinePosition);
			else
				return Empty;
		} // func GetXmlPositionFromXml

		private static void ReadInfo(XElement x, ref string fileName, ref int lineNumber, ref int linePosition, ref int f)
		{
			// get the line position
			if ((f & 1) == 0)
			{
				lineNumber = x.GetAttribute(xnLineNumber, -1);
				linePosition = x.GetAttribute(xnLinePosition, -1);

				if (linePosition >= 0)
					f = f | 1;
			}

			// read the filename
			if ((f & 2) == 0)
			{
				fileName = x.GetAttribute<string>(xnFileName, null);
				if (String.IsNullOrWhiteSpace(fileName))
					fileName = null;
				else if (fileName != null)
					f = f | 2;
			}
		} // func ReadInfo

		/// <summary>Uses the Xml Position attributes.</summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static PpsXmlPosition GetXmlPositionFromAttributes(XNode x)
		{
			var fileName = (string)null;
			var lineNumber = -1;
			var linePosition = -1;

			var xCur = x == null ? null : (x is XElement ? (XElement)x : x.Parent);

			// look the line position
			var f = 0;
			while (xCur != null && f < 3)
			{
				ReadInfo(xCur, ref fileName, ref lineNumber, ref linePosition, ref f);
				xCur = xCur.Parent;
			}
			if (fileName == null && lineNumber == -1 && linePosition == -1)
				return Empty;
			else
				return new PpsXmlPosition(fileName, lineNumber, linePosition);
		} // func GetXmlPositionFromAttributes

		/// <summary>Remove source file information attributes.</summary>
		/// <param name="x">Element to remove annotations.</param>
		public static void RemoveAttributes(XElement x)
		{
			var attr = x.Attribute(xnLineNumber);
			if (attr != null)
				attr.Remove();
			attr = x.Attribute(xnLinePosition);
			if (attr != null)
				attr.Remove();
			attr = x.Attribute(xnFileName);
			if (attr != null)
				attr.Remove();
		} // proc Remove

		/// <summary>Empty information</summary>
		public static PpsXmlPosition Empty { get; } = new PpsXmlPosition(null, -1, -1);
	} // class PpsXmlPosition

	#endregion
}

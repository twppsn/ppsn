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
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
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

		#region -- class PpsXmlPositionReader -----------------------------------------

		private sealed class PpsXmlPositionReader : XmlReader, IXmlNamespaceResolver, IXmlLineInfo
		{
			#region -- class StackLineInfo --------------------------------------------

			private sealed class StackLineInfo
			{
				public int globalStartLine;
				public int localStartLine;
				public int linePosition;
				public string baseUri;
				public int popLevel;

				public int GetLineNumber(IXmlLineInfo lineInfo)
				{
					if (lineInfo == null)
						return localStartLine;

					return localStartLine + lineInfo.LineNumber - globalStartLine;
				} // func GetLineNumber

				public int GetLinePosition(IXmlLineInfo lineInfo)
				{
					if (lineInfo == null)
						return linePosition;

					return lineInfo.LineNumber == globalStartLine ? linePosition : lineInfo.LinePosition;
				} // func GetLinePosition
			} // class StackLineInfo

			#endregion

			private readonly XmlReader xml;
			private readonly IXmlLineInfo lineInfo;
			private readonly Stack<StackLineInfo> positionStack = new Stack<StackLineInfo>();

			private List<int> attributeIndexMap = null;
			private int attributeIndex = 0;

			public PpsXmlPositionReader(XmlReader xml)
			{
				this.xml = xml ?? throw new ArgumentNullException(nameof(xml));

				lineInfo = xml as IXmlLineInfo;
			} // ctor

			protected override void Dispose(bool disposing)
			{
				base.Dispose(disposing);
				if (Settings.CloseInput)
					xml.Dispose();
			} // proc Dispose

			private void PopLineInfo()
			{
				while (positionStack.Count > 0)
				{
					var popLevel = positionStack.Peek().popLevel;
					if (popLevel >= xml.Depth)
						positionStack.Pop();
					else
						break;
				}
			} // proc PopLineInfo

			private void PushLineInfo(string newBaseUri, int newLineNumber, int newLinePosition)
			{
				PopLineInfo();
				positionStack.Push(
					new StackLineInfo()
					{
						baseUri = newBaseUri,
						globalStartLine = lineInfo?.LineNumber ?? 0,
						localStartLine = newLineNumber,
						linePosition = newLinePosition,
						popLevel = xml.Depth
					}
				);
			} // proc PushLineInfo

			private bool UpdateLineInfo()
			{
				if (xml.NodeType == XmlNodeType.Element)
				{
					var newLineNumber = xml.TryGetAttribute<string>(xnLineNumber, out var lineNumberString) && Int32.TryParse(lineNumberString, out var lineNumber) ? lineNumber : -1;
					var newLinePosition = newLineNumber >= 0 ? xml.TryGetAttribute<string>(xnLinePosition, out var linePositionString) && Int32.TryParse(lineNumberString, out var linePosition) ? linePosition : -1 : -1;
					var newBaseUri = xml.TryGetAttribute<string>(xnFileName, out var fileName) ? fileName : null;

					if (newBaseUri != null && newLinePosition < 0)
						PushLineInfo(newBaseUri, 0, 0);
					else if (newLinePosition >= 0)
						PushLineInfo(newBaseUri, newLineNumber, newLinePosition >= 0 ? newLinePosition : 0);
				}
				else if (xml.NodeType == XmlNodeType.EndElement)
					PopLineInfo();

				attributeIndexMap = null;
				return true;
			} // func UpdateLineInfo

			public override bool Read()
				=> xml.Read() && UpdateLineInfo();

			public async override Task<bool> ReadAsync()
				=> await xml.ReadAsync() && UpdateLineInfo();

			bool IXmlLineInfo.HasLineInfo() => true;
			int IXmlLineInfo.LineNumber => positionStack.Count > 0 ? positionStack.Peek().GetLineNumber(lineInfo) : lineInfo?.LineNumber ?? 0;
			int IXmlLineInfo.LinePosition => positionStack.Count > 0 ? positionStack.Peek().GetLinePosition(lineInfo) : lineInfo?.LinePosition ?? 0;

			#region -- overrides --

			IDictionary<string, string> IXmlNamespaceResolver.GetNamespacesInScope(XmlNamespaceScope scope)
				=> xml is IXmlNamespaceResolver resolver ? resolver.GetNamespacesInScope(scope) : null;
			string IXmlNamespaceResolver.LookupNamespace(string prefix)
				=> xml is IXmlNamespaceResolver resolver ? resolver.LookupNamespace(prefix) : null;
			string IXmlNamespaceResolver.LookupPrefix(string namespaceName)
				=> xml is IXmlNamespaceResolver resolver ? resolver.LookupPrefix(namespaceName) : null;

			public override object ReadContentAsObject() => xml.ReadContentAsObject();
			public override bool ReadContentAsBoolean() => xml.ReadContentAsBoolean();
			public override DateTime ReadContentAsDateTime() => xml.ReadContentAsDateTime();
			public override DateTimeOffset ReadContentAsDateTimeOffset() => xml.ReadContentAsDateTimeOffset();
			public override double ReadContentAsDouble() => xml.ReadContentAsDouble();
			public override float ReadContentAsFloat() => xml.ReadContentAsFloat();
			public override decimal ReadContentAsDecimal() => xml.ReadContentAsDecimal();
			public override int ReadContentAsInt() => xml.ReadContentAsInt();
			public override long ReadContentAsLong() => xml.ReadContentAsLong();
			public override string ReadContentAsString() => xml.ReadContentAsString();
			public override object ReadContentAs(Type returnType, IXmlNamespaceResolver namespaceResolver) => xml.ReadContentAs(returnType, namespaceResolver);
			public override object ReadElementContentAsObject() => xml.ReadElementContentAsObject();
			public override object ReadElementContentAsObject(string localName, string namespaceURI) => xml.ReadElementContentAsObject(localName, namespaceURI);
			public override bool ReadElementContentAsBoolean() => xml.ReadElementContentAsBoolean();
			public override bool ReadElementContentAsBoolean(string localName, string namespaceURI) => xml.ReadElementContentAsBoolean(localName, namespaceURI);
			public override DateTime ReadElementContentAsDateTime() => xml.ReadElementContentAsDateTime();
			public override DateTime ReadElementContentAsDateTime(string localName, string namespaceURI) => xml.ReadElementContentAsDateTime(localName, namespaceURI);
			public override double ReadElementContentAsDouble() => xml.ReadElementContentAsDouble();
			public override double ReadElementContentAsDouble(string localName, string namespaceURI) => xml.ReadElementContentAsDouble(localName, namespaceURI);
			public override float ReadElementContentAsFloat() => xml.ReadElementContentAsFloat();
			public override float ReadElementContentAsFloat(string localName, string namespaceURI) => xml.ReadElementContentAsFloat(localName, namespaceURI);
			public override decimal ReadElementContentAsDecimal() => xml.ReadElementContentAsDecimal();
			public override decimal ReadElementContentAsDecimal(string localName, string namespaceURI) => xml.ReadElementContentAsDecimal(localName, namespaceURI);
			public override int ReadElementContentAsInt() => xml.ReadElementContentAsInt();
			public override int ReadElementContentAsInt(string localName, string namespaceURI) => xml.ReadElementContentAsInt(localName, namespaceURI);
			public override long ReadElementContentAsLong() => xml.ReadElementContentAsLong();
			public override long ReadElementContentAsLong(string localName, string namespaceURI) => xml.ReadElementContentAsLong(localName, namespaceURI);
			public override string ReadElementContentAsString() => xml.ReadElementContentAsString();
			public override string ReadElementContentAsString(string localName, string namespaceURI) => xml.ReadElementContentAsString(localName, namespaceURI);
			public override object ReadElementContentAs(Type returnType, IXmlNamespaceResolver namespaceResolver) => xml.ReadElementContentAs(returnType, namespaceResolver);
			public override object ReadElementContentAs(Type returnType, IXmlNamespaceResolver namespaceResolver, string localName, string namespaceURI) => xml.ReadElementContentAs(returnType, namespaceResolver, localName, namespaceURI);
			public override void MoveToAttribute(int i) => xml.MoveToAttribute(i);
			public override void Skip() => xml.Skip();
			public override int ReadContentAsBase64(byte[] buffer, int index, int count) => xml.ReadContentAsBase64(buffer, index, count);
			public override int ReadElementContentAsBase64(byte[] buffer, int index, int count) => xml.ReadElementContentAsBase64(buffer, index, count);
			public override int ReadContentAsBinHex(byte[] buffer, int index, int count) => xml.ReadContentAsBinHex(buffer, index, count);
			public override int ReadElementContentAsBinHex(byte[] buffer, int index, int count) => xml.ReadElementContentAsBinHex(buffer, index, count);
			public override int ReadValueChunk(char[] buffer, int index, int count) => xml.ReadValueChunk(buffer, index, count);
			public override string ReadString() => xml.ReadString();
			public override XmlNodeType MoveToContent() => xml.MoveToContent();
			public override string ReadElementString() => xml.ReadElementString();
			public override string ReadElementString(string name) => xml.ReadElementString(name);
			public override string ReadElementString(string localname, string ns) => xml.ReadElementString(localname, ns);
			public override bool IsStartElement() => xml.IsStartElement();
			public override bool IsStartElement(string name) => xml.IsStartElement(name);
			public override bool IsStartElement(string localname, string ns) => xml.IsStartElement(localname, ns);
			public override bool ReadToFollowing(string name) => xml.ReadToFollowing(name);
			public override bool ReadToFollowing(string localName, string namespaceURI) => xml.ReadToFollowing(localName, namespaceURI);
			public override bool ReadToDescendant(string name) => xml.ReadToDescendant(name);
			public override bool ReadToDescendant(string localName, string namespaceURI) => xml.ReadToDescendant(localName, namespaceURI);
			public override bool ReadToNextSibling(string name) => xml.ReadToNextSibling(name);
			public override bool ReadToNextSibling(string localName, string namespaceURI) => xml.ReadToNextSibling(localName, namespaceURI);
			public override Task<string> GetValueAsync() => xml.GetValueAsync();
			public override Task<object> ReadContentAsObjectAsync() => xml.ReadContentAsObjectAsync();
			public override Task<string> ReadContentAsStringAsync() => xml.ReadContentAsStringAsync();
			public override Task<object> ReadContentAsAsync(Type returnType, IXmlNamespaceResolver namespaceResolver) => xml.ReadContentAsAsync(returnType, namespaceResolver);
			public override Task<object> ReadElementContentAsObjectAsync() => xml.ReadElementContentAsObjectAsync();
			public override Task<string> ReadElementContentAsStringAsync() => xml.ReadElementContentAsStringAsync();
			public override Task<object> ReadElementContentAsAsync(Type returnType, IXmlNamespaceResolver namespaceResolver) => xml.ReadElementContentAsAsync(returnType, namespaceResolver);
			public override Task SkipAsync() => xml.SkipAsync();
			public override Task<int> ReadContentAsBase64Async(byte[] buffer, int index, int count) => xml.ReadContentAsBase64Async(buffer, index, count);
			public override Task<int> ReadElementContentAsBase64Async(byte[] buffer, int index, int count) => xml.ReadElementContentAsBase64Async(buffer, index, count);
			public override Task<int> ReadContentAsBinHexAsync(byte[] buffer, int index, int count) => xml.ReadContentAsBinHexAsync(buffer, index, count);
			public override Task<int> ReadElementContentAsBinHexAsync(byte[] buffer, int index, int count) => xml.ReadElementContentAsBinHexAsync(buffer, index, count);
			public override Task<int> ReadValueChunkAsync(char[] buffer, int index, int count) => xml.ReadValueChunkAsync(buffer, index, count);
			public override Task<XmlNodeType> MoveToContentAsync() => xml.MoveToContentAsync();

			private bool IsPositionAttribute()
				=> xml.NamespaceURI == XmlPositionNamespace.NamespaceName;

			private void EnforceAttributeIndexMap()
			{
				if (attributeIndexMap != null)
					return;

				if (MoveToFirstAttribute())
				{
					while (MoveToNextAttribute()) { }
					MoveToElement();
				}
			} // func EnforceAttributeIndexMap

			public override bool MoveToFirstAttribute()
			{
				attributeIndex = 0;
				attributeIndexMap = new List<int>();

				if (xml.MoveToFirstAttribute())
				{
					if (IsPositionAttribute())
						return MoveToNextAttribute();
					else
					{
						attributeIndexMap.Add(attributeIndex);
						return true;
					}
				}
				else
					return false;
			} // func MoveToFirstAttribute

			public override bool MoveToNextAttribute()
			{
				if (xml.MoveToNextAttribute())
				{
					attributeIndex++;
					if (IsPositionAttribute())
						return MoveToNextAttribute();
					else
					{
						attributeIndexMap.Add(attributeIndex);
						return true;
					}
				}
				else
					return false;
			} // func MoveToNextAttribtue

			public override bool MoveToElement()
				=> xml.MoveToElement();

			public override string GetAttribute(int i)
			{
				EnforceAttributeIndexMap();
				return xml.GetAttribute(attributeIndexMap[i]);
			} // func GetAttribute

			public override string GetAttribute(string name)
				=> xml.GetAttribute(name);
			public override string GetAttribute(string name, string namespaceURI) 
				=> xml.GetAttribute(name, namespaceURI);
			public override bool MoveToAttribute(string name) 
				=> xml.MoveToAttribute(name);
			public override bool MoveToAttribute(string name, string ns)
				=> xml.MoveToAttribute(name, ns);

			public override int AttributeCount
			{
				get
				{
					EnforceAttributeIndexMap();
					return attributeIndexMap.Count;
				}
			} // prop AttributeCount
			
			public override string LookupNamespace(string prefix) => xml.LookupNamespace(prefix);
			public override bool ReadAttributeValue() => xml.ReadAttributeValue();
			public override void ResolveEntity() => xml.ResolveEntity();

			public override bool CanReadBinaryContent => xml.CanReadBinaryContent;
			public override bool CanReadValueChunk => xml.CanReadValueChunk;
			public override bool CanResolveEntity => xml.CanResolveEntity;

			public override string Name => xml.Name;
			public override bool HasValue => xml.HasValue;
			public override string Value => xml.Value;
			public override string NamespaceURI => xml.NamespaceURI;
			public override string LocalName => xml.LocalName;
			public override string Prefix => xml.Prefix;
			public override bool IsDefault => xml.IsDefault;
			public override char QuoteChar => xml.QuoteChar;
			public override Type ValueType => xml.ValueType;
			public override bool HasAttributes => xml.HasAttributes;

			public override int Depth => xml.Depth;
			public override string BaseURI => xml.BaseURI;

			public override bool IsEmptyElement => xml.IsEmptyElement;

			public override bool EOF => xml.EOF;
			public override XmlNodeType NodeType => xml.NodeType;
			public override ReadState ReadState => xml.ReadState;

			public override XmlSpace XmlSpace => xml.XmlSpace;
			public override string XmlLang => xml.XmlLang;
			public override XmlNameTable NameTable => xml.NameTable;
			public override XmlReaderSettings Settings => xml.Settings;
			public override IXmlSchemaInfo SchemaInfo => xml.SchemaInfo;

			public override string this[string name, string namespaceURI] => base[name, namespaceURI];
			public override string this[string name] => base[name];
			public override string this[int i] => base[i];

			#endregion
		} // class PpsXmlPositionReader

		#endregion

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
		[Obsolete("Todo: There is a XmlReader to filter the BaseUri")]
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

		/// <summary></summary>
		/// <param name="xml"></param>
		/// <returns></returns>
		public static XmlReader CreateLinePositionReader(XmlReader xml)
			=> new PpsXmlPositionReader(xml);

		/// <summary>Empty information</summary>
		public static PpsXmlPosition Empty { get; } = new PpsXmlPosition(null, -1, -1);
	} // class PpsXmlPosition

	#endregion
}

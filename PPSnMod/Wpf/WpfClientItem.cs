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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using TecWare.DE.Networking;
using TecWare.DE.Server;
using TecWare.DE.Server.Http;
using TecWare.DE.Stuff;
using TecWare.PPSn.Stuff;

namespace TecWare.PPSn.Server.Wpf
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class WpfClientItem : DEConfigItem
	{
		private static readonly XNamespace MarkupNamespace = "http://schemas.openxmlformats.org/markup-compatibility/2006";
		private static readonly XNamespace PresentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
		private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

		private static readonly XName xnXamlIgnorable = MarkupNamespace + "Ignorable";
		private static readonly XName xnXamlResourceDictionary = PresentationNamespace + "ResourceDictionary";
		private static readonly XName xnXamlMergedDictionaries = PresentationNamespace + "ResourceDictionary.MergedDictionaries";

		private static readonly XName xnXamlKey = XamlNamespace + "Key";
		private static readonly XName xnXamlCode = XamlNamespace + "Code";

		#region  -- class ParsedXamlFile --------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class ParsedXamlFile
		{
			private readonly object lockFile = new object();

			private readonly WpfClientItem owner;
			private readonly ParsedXamlFile parentFile;
			private readonly bool emitSourceInformationAsAttributes;
			private readonly XDocument cachedDocument;
			private readonly XElement xResources;

			private readonly List<string> collectedNamespaces;
			private readonly List<string> collectedFiles;
			private readonly List<string> collectedKeys;

			private DateTime lastChanged;

			#region -- Ctor/Dtor ------------------------------------------------------------

			public ParsedXamlFile(WpfClientItem owner, ParsedXamlFile parentFile, XDocument xBasicFile, XElement xResources, bool emitSourceInformationAsAttributes)
			{
				this.owner = owner;
				this.parentFile = parentFile;
				this.emitSourceInformationAsAttributes = emitSourceInformationAsAttributes;
				this.cachedDocument = xBasicFile;
				this.xResources = xResources;

				this.collectedNamespaces = new List<string>();
				this.collectedFiles = new List<string>();
				this.collectedKeys = new List<string>();

				this.lastChanged = DateTime.MinValue;
			} // ctor

			#endregion

			#region -- AddFile, AddKey, AddNamespace, AddResource ---------------------------

			private bool AddFile(string fileName)
			{
				if (parentFile != null && parentFile.ExistsFile(fileName))
					return false;

				var pos = collectedFiles.BinarySearch(fileName, StringComparer.OrdinalIgnoreCase);
				if (pos < 0)
				{
					collectedFiles.Insert(~pos, fileName);
					return true;
				}
				else
					return false;
			} // proc AddFile

			private bool ExistsFile(string fileName)
			{
				if (collectedFiles.BinarySearch(fileName) >= 0)
					return true;
				return parentFile != null && parentFile.ExistsFile(fileName);
			} // func ExistsFile

			private bool AddKey(XElement x)
			{
				var keyName = x.GetAttribute(xnXamlKey, String.Empty);
				if (String.IsNullOrEmpty(keyName))
				{
					// Eventuell gibt es einen TargetType
					keyName = x.GetAttribute("TargetType", String.Empty);
					if (String.IsNullOrEmpty(keyName))
						return false;
				}

				if (parentFile != null && parentFile.ExistsKey(keyName))
					return false;

				var pos = collectedKeys.BinarySearch(keyName);
				if (pos < 0)
				{
					collectedKeys.Insert(~pos, keyName);
					return true;
				}
				else
					return false;
			} // proc AddKey

			private bool ExistsKey(string key)
			{
				if (collectedKeys.BinarySearch(key) >= 0)
					return true;
				return parentFile != null && parentFile.ExistsKey(key);
			} // func ExistsFile

			private XNode AddNode(XNode xAddBefore, XNode xInsert)
			{
				if (xAddBefore == null)
				{
					xResources.Add(xInsert);
					return xResources.LastNode;
				}
				else
				{
#if DEBUG
					if (xResources != xAddBefore.Parent)
						throw new ArgumentException();
#endif
					xAddBefore.AddBeforeSelf(xInsert);
					return xAddBefore.PreviousNode;
				}
			} // func AddNode

			private void AddNamespace(string sPrefix, XNamespace ns)
			{
				var pos = collectedNamespaces.BinarySearch(ns.NamespaceName);
				if (pos < 0)
				{
					collectedNamespaces.Insert(~pos, ns.NamespaceName);
					if (!String.IsNullOrEmpty(sPrefix))
						xResources.SetAttributeValue(XNamespace.Xmlns + sPrefix, ns.NamespaceName);
				}
			} // proc AddNamespace

			private void AddNamespaces(XElement x)
			{
				AddNamespace(x.GetPrefixOfNamespace(x.Name.Namespace), x.Name.Namespace);
				foreach (var attr in x.Attributes())
				{
					if (attr.IsNamespaceDeclaration)
						AddNamespace(attr.Name.LocalName, attr.Value);
				}
			} // proc AddNamespaces

			private XNode AddResource(XElement xSource, XNode xAddBefore)
			{
				var position = PpsXmlPosition.GetXmlPositionFromXml(xSource); // get the source information

				XNode xFirstAdded = null;

				if (!position.IsEmpty)
				{
					if (emitSourceInformationAsAttributes)
						position.WriteAttributes(xSource);
					else
						xFirstAdded = AddNode(xAddBefore, position.GetComment());
				}

				// Combine namespaces
				AddNamespaces(xSource);

				// Resource
				var xTmp = AddNode(xAddBefore, xSource);
				return xFirstAdded ?? xTmp;
			} // proc AddResource

			#endregion

			#region -- Add, Load ------------------------------------------------------------

			public void AddResourcesFromFile(string basePath, string resourceFile, bool allowSubPaths)
			{
				var fullPath = GetDocumentPath(basePath, resourceFile, allowSubPaths);
				if (AddFile(fullPath))
				{
					var xDocument = LoadResourceDictionary(fullPath);
					CombineResourceDictionary(xDocument.Root, null, true);
				}
			} // proc AddResourcesFromFile

			private XDocument LoadResourceDictionary(string fileName)
			{
				// update last write date
				var dt = File.GetLastWriteTimeUtc(fileName);
				if (dt > lastChanged)
					lastChanged = dt;

				// Prüfe die Wurzel
				var xDoc = LoadDocument(fileName);
				if (xDoc.Root.Name != xnXamlResourceDictionary)
					throw new ArgumentException("invalid resource: ResourceDictionary expected");

				return xDoc;
			} // func LoadResourceDictionary

			private XDocument LoadResourceDictionary(XAttribute sourceAttribute, Predicate<string> canLoad)
			{
				try
				{
					// get the relative file name
					var relativePath = sourceAttribute?.Value;
					if (String.IsNullOrEmpty(relativePath))
						throw new DEConfigurationException(sourceAttribute.Parent, "Reference is missing.");

					// load file
					var fileName = ProcsDE.GetFileName(sourceAttribute, relativePath);
					if (canLoad != null && !canLoad(fileName))
						return null;

					return LoadDocument(fileName);
				}
				catch
				{
					throw new DEConfigurationException(sourceAttribute.Parent, String.Format("Failed to load reference ({0}).", sourceAttribute?.Value));
				}
			} // func LoadResourceDictionary

			#endregion

			#region -- Combine --------------------------------------------------------------

			private void CombineResourceDictionary(XElement xResourceDictionary, XNode xAddBefore, bool removeNodes)
			{
				if (xResourceDictionary == null)
					return;

				var source = xResourceDictionary.Attribute("Source");
				XNode xMergedAddBefore = null;
				if (source == null) // is this a embedded resource dictionary
				{
					// fetch resources
					foreach (var x in xResourceDictionary.Elements())
					{
						if (x.Name == xnXamlMergedDictionaries)
							continue;

						if (AddKey(x))
						{
							var xAdded = AddResource(x, xAddBefore);
							if (xMergedAddBefore == null)
								xMergedAddBefore = xAdded;
						}
					}

					// refrenced
					var xMerged = xResourceDictionary.Element(xnXamlMergedDictionaries);
					if (xMerged != null)
						CombineMergedResourceDictionaries(xMerged, xMergedAddBefore, removeNodes);
				}
				else // refrenced
				{
					CombineResourceDictionary(LoadResourceDictionary(source, AddFile)?.Root, xAddBefore, removeNodes);
				}
			} // proc CombineResourceDictionary

			private void CombineMergedResourceDictionaries(XElement xMerged, XNode xAddBefore, bool removeNodes)
			{
				// resolve only ResourceDictionaries
				var xResourceDictionaries = xMerged.Elements(xnXamlResourceDictionary).ToArray();
				for (int i = xResourceDictionaries.Length - 1; i >= 0; i--)
				{
					CombineResourceDictionary(xResourceDictionaries[i], xAddBefore, removeNodes);
					if (removeNodes)
						xResourceDictionaries[i].Remove();
				}

				// remove the element, if it is empty
				if (xMerged.IsEmpty && removeNodes)
					xMerged.Remove();
			} // proc CombineMergedResourceDictionaries

			#endregion

			public bool IsUpToDate()
			{
				lock (lockFile)
				{
					// check the file list
					var checkLastChanged = collectedFiles.Max(c => File.GetLastWriteTimeUtc(c));
					return lastChanged < checkLastChanged;
				}
			} // func IsUpToDate

			public XDocument Document => cachedDocument;

			public DateTime LastChanged => lastChanged;
		} // class ParsedXamlFile

		#endregion

		private readonly PpsApplication application;

		private ParsedXamlFile defaultTheme = null;

		public WpfClientItem(IServiceProvider sp, string name)
			: base(sp, name)
		{
			this.application = sp.GetService<PpsApplication>(true);
		} // ctor

		#region -- LoadDocument -----------------------------------------------------------

		private static XDocument LoadDocument(string fileName)
			=> RemoveXamlDebugElements(XDocument.Load(fileName, LoadOptions.SetBaseUri | LoadOptions.SetLineInfo));

		private static string GetDocumentPath(string basePath, string documentFile, bool allowSubPaths)
		{
			// check the filename
			if (allowSubPaths)
				documentFile = documentFile.Replace('/', '\\');
			else if (documentFile.IndexOfAny(new char[] { '/', '\\' }) >= 0)
				throw new ArgumentException(String.Format("Relative file names are not allowed ('{0}').", documentFile));

			// load the file
			return Path.Combine(basePath, documentFile);
		} // func GetDocumentPath

		#endregion

		#region -- RemoveXamlDebugElements ------------------------------------------------

		private static XDocument RemoveXamlDebugElements(XDocument xDocument)
		{
			// get the list of namespaces, to remove
			var ignorableNamespacesString = xDocument.Root.GetAttribute(xnXamlIgnorable, String.Empty).Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			if (ignorableNamespacesString.Length == 0)
				return xDocument;

			// remove the attribute
			var attr = xDocument.Root.Attribute(xnXamlIgnorable);
			if (attr != null)
				attr.Remove();

			// create a XName
			var ignorableNamespaces = new XName[ignorableNamespacesString.Length];
			for (var i = 0; i < ignorableNamespacesString.Length; i++)
			{
				attr = xDocument.Root.Attribute(XNamespace.Xmlns + ignorableNamespacesString[i]);
				ignorableNamespaces[i] = XName.Get(ignorableNamespacesString[i], attr.Value ?? String.Empty);
				attr.Remove();
			}

			// remove the namespace elements and attributes
			RemoveXamlDebugElements(xDocument.Root, ignorableNamespaces);

			return xDocument;
		} // proc RemoveXamlDebugElements

		private static void RemoveXamlDebugElements(XElement x, XName[] ignorableNamespaces)
		{
			// clear the current node attributes
			if (IsIgnorableName(ignorableNamespaces, x.Name))
			{
				x.Remove();
				return;
			}
			else
			{
				var attr = x.FirstAttribute;
				while (attr != null)
				{
					var n = attr.NextAttribute;
					if (IsIgnorableName(ignorableNamespaces, attr.Name))
						attr.Remove();
					attr = n;
				}
			}

			// check the child elements
			var xCur = x.FirstNode;
			while (xCur != null)
			{
				XNode xNext = xCur.NextNode;
				if (xCur.NodeType == XmlNodeType.Element) // clear the element
					RemoveXamlDebugElements((XElement)xCur, ignorableNamespaces);
				else if (xCur.NodeType == XmlNodeType.Comment) // remove comments too
					xCur.Remove();

				xCur = xNext;
			}
		} // proc RemoveXamlDebugElements

		private static bool IsIgnorableName(XName[] ignorableNamespaces, XName xnName)
			=> Array.FindIndex(ignorableNamespaces, xn => xn.Namespace == xnName.Namespace) >= 0;

		#endregion

		private void ClearXamlCache()
		{
		} // proc ClearXamlCache
		
		#region -- Wpf Xaml Parser --------------------------------------------------------

		private XDocument ParseXamlTheme(string themeFile)
		{
			var isDefaultTheme = String.Compare(themeFile, "default.xaml", StringComparison.OrdinalIgnoreCase) == 0;
			if (isDefaultTheme)
			{
				if (defaultTheme != null && !defaultTheme.IsUpToDate())
					return defaultTheme.Document;
				else
					ClearXamlCache();
			}
			else
			{
				// todo: cache
			}

			// load the document and parse the content
			var xTargetResources = CreateResourcesNode();
			var xTarget = new XDocument(
				new XElement("theme",
					new XAttribute(XNamespace.Xmlns + "s", PpsXmlPosition.XmlPositionNamespace.NamespaceName),
					new XAttribute(XNamespace.Xmlns + "x", XamlNamespace.NamespaceName),
					xTargetResources
				)
			);

			// create the resource file
			var resourceFile = new ParsedXamlFile(this, null, xTarget, xTargetResources, false);
			resourceFile.AddResourcesFromFile(ThemesDirectory, themeFile, false);

			// cache
			if (isDefaultTheme)
				defaultTheme = resourceFile;

			return resourceFile.Document;
		} // func ParseXamlTheme

		private static XElement CreateResourcesNode()
			=> new XElement(PresentationNamespace + "resources"); // change default namespace

		#endregion

		private void ParseViews(IDEContext r)
		{
			using (XmlWriter xml = XmlWriter.Create(r.GetOutputTextWriter(MimeTypes.Text.Xml), Procs.XmlWriterSettings))
			{
				xml.WriteStartElement("views");

				foreach (var v in application.GetViewDefinitions())
				{
					if (!v.IsVisible) // export only views with a name
						continue;

					// write the attributes
					xml.WriteStartElement("view");
					xml.WriteAttributeString("id", v.Name);
					xml.WriteProperty(v.Attributes, "shortcut");
					xml.WriteProperty(v.Attributes, "displayName");
					xml.WriteProperty(v.Attributes, "displayImage");

					// write filters and orders
					foreach (var f in v.Filter)
					{
						if (f.IsVisible)
							f.WriteElement(xml, "filter");
					}
					foreach (var o in v.Order)
					{
						if (o.IsVisible)
							o.WriteElement(xml, "order");
					}

					xml.WriteEndElement();
				} // foreach v
				xml.WriteEndElement();
			}
		} // func ParseViews

		protected override bool OnProcessRequest(IDEContext r)
		{
			if (r.RelativeSubPath == "default.xaml")
			{
				r.WriteXml(ParseXamlTheme(r.RelativeSubPath), MimeTypes.Application.Xaml);
				return true;
			}
			else if (r.RelativeSubPath == "views.xml")
			{
				ParseViews(r);
			}
			return base.OnProcessRequest(r);
		} // func OnProcessRequest

		public string ThemesDirectory => Config.GetAttribute("xamlSource", String.Empty);
	} // class WpfClientItem
}

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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using TecWare.DE.Data;
using TecWare.DE.Networking;
using TecWare.DE.Server;
using TecWare.DE.Server.Http;
using TecWare.DE.Stuff;
using TecWare.PPSn.Server.Data;
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

			public bool AddRootFile(string fileName)
			{
				if (AddFile(fileName))
				{
					UpdateLastChanged(fileName);
					return true;
				}
				else
					return false;
			} // proc AddRootFile

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

			public void AddResourcesFromFile(string resourceFile)
			{
				if (AddFile(resourceFile))
				{
					var xDocument = LoadResourceDictionary(resourceFile);
					CombineResourceDictionary(xDocument.Root, null, true);
				}
			} // proc AddResourcesFromFile

			public void AddTemplatesFromFile(string templateFile, ref int currentTemplatePriority)
			{
				if (AddFile(templateFile))
				{
					var xDocument = LoadResourceDictionary(templateFile);

					foreach (var x in xDocument.Root.Elements())
					{
						if (x.Name == xnXamlMergedDictionaries) // merge resources
							CombineMergedResourceDictionaries(x, null, false);
						else // parse template
						{
							string onlineViewId = null;

							// get the key
							var keyString = x.GetAttribute(xnXamlKey, String.Empty);
							if (String.IsNullOrEmpty(keyString))
								continue;

							// split the key
							var keyElements = keyString.Split(',',';');
							if (keyElements.Length >= 1)
								keyString = keyElements[0].Trim();
							if (keyElements.Length >= 2)
								onlineViewId = keyElements[1].Trim();
							if (keyElements.Length >= 3)
							{
								int t;
								if (Int32.TryParse(keyElements[2].Trim(), out t))
									currentTemplatePriority = t;
								else
									currentTemplatePriority++;
							}
							else
								currentTemplatePriority++;

							// create the new template
							var xTemplate = new XElement("template",
								new XAttribute("key", keyString),
								new XAttribute("priority", currentTemplatePriority),
								new XAttribute("viewId", onlineViewId)
							);

							// append the code item
							var xCode = x.Element(xnXamlCode);
							if (xCode != null)
							{
								AddLuaCodeItem(xTemplate, "condition", xCode);
								xCode.Remove();
							}

							// copy the content
							var xmlPosition = PpsXmlPosition.GetXmlPositionFromXml(x);
							x.Attribute(xnXamlKey).Remove();
							xTemplate.Add(x);
							xmlPosition.WriteAttributes(xTemplate);

							Document.Root.Add(xTemplate);
						}
					}
				}
			} // proc AddTemplatesFromFile

			private XDocument LoadDocumentIntern(string fileName)
			{
				// update last write date
				UpdateLastChanged(fileName);

				// Prüfe die Wurzel
				var xDoc = WpfClientItem.LoadDocument(fileName);
				return xDoc;
			} // func LoadDocument

			private void UpdateLastChanged(string fileName)
			{
				var dt = File.GetLastWriteTimeUtc(fileName);
				if (dt > lastChanged)
					lastChanged = dt;
			} // proc UpdateLastChanged

			private XDocument LoadResourceDictionary(string fileName)
			{
				var xDoc = LoadDocumentIntern(fileName);
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

					return LoadDocumentIntern(fileName);
				}
				catch
				{
					throw new DEConfigurationException(sourceAttribute.Parent, String.Format("Failed to load reference ({0}).", sourceAttribute?.Value));
				}
			} // func LoadResourceDictionary

			#endregion

			#region -- Combine --------------------------------------------------------------

			public void CombineResourceDictionary(XElement xResourceDictionary, XNode xAddBefore, bool removeNodes)
			{
				if (xResourceDictionary == null)
					return;
				if (xResources == null)
					throw new ArgumentException("no resource target defined.");

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
							var xAdded = xResourceDictionary == xResources ? // do we need to copy the resources
									x :
									AddResource(x, xAddBefore);
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

		#region -- Wpf Xaml Parser --------------------------------------------------------

		private void ClearXamlCache()
		{
		} // proc ClearXamlCache

		private ParsedXamlFile GetDefaultTheme()
			=> ParseXamlTheme("default");

		private ParsedXamlFile ParseXamlTheme(string theme)
		{
			var isDefaultTheme = String.Compare(theme, "default", StringComparison.OrdinalIgnoreCase) == 0;
			if (isDefaultTheme)
			{
				// is the default theme up to date
				if (defaultTheme != null && !defaultTheme.IsUpToDate())
					return defaultTheme;
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
			foreach (var x in Config.Elements(PpsStuff.xnWpfTheme).Where(x=> String.Compare(x.GetAttribute("id", String.Empty), theme, StringComparison.OrdinalIgnoreCase) == 0))
			{
				var fileName = x.GetAttribute("file", String.Empty);
				if (String.IsNullOrEmpty(fileName))
					throw new DEConfigurationException(x, "@file is missing.");
				resourceFile.AddResourcesFromFile(fileName);
			}

			// cache
			if (isDefaultTheme)
				defaultTheme = resourceFile;

			return resourceFile;
		} // func ParseXamlTheme
		
		private ParsedXamlFile ParseXamlTemplates()
		{
			// load the document and parse the content
			var xTargetResources = CreateResourcesNode();
			var xTarget = new XDocument(
				new XElement("templates",
					new XAttribute(XNamespace.Xmlns + "s", PpsXmlPosition.XmlPositionNamespace.NamespaceName),
					new XAttribute(XNamespace.Xmlns + "x", XamlNamespace.NamespaceName),
					xTargetResources
				)
			);

			// create the template
			var currentTemplatePriority = 0;
			var templateFile = new ParsedXamlFile(this, GetDefaultTheme(), xTarget, xTargetResources, false);
			foreach (var x in Config.Elements(PpsStuff.xnWpfTemplate))
			{
				var fileName = x.GetAttribute("file", String.Empty);
				if (String.IsNullOrEmpty(fileName))
					throw new DEConfigurationException(x, "@file is missing.");

				templateFile.AddTemplatesFromFile(fileName, ref currentTemplatePriority);
			}

			return templateFile;
		} // func ParseXamlTemplates

		private bool ResolveXamlPath(string relativePath, out string fullPath)
		{
			// check for '..' and trailing slashes
			if (relativePath.IndexOf("..") >= 0)
				throw new ArgumentException(String.Format("Relative file names are not allowed ('{0}').", relativePath));

			// find the file
			foreach (var x in Config.Elements(PpsStuff.xnWpfWpfSource))
			{
				var directoryPath = x.GetAttribute("directory", String.Empty);
				if (String.IsNullOrEmpty(directoryPath))
					continue;
				var virtualPath = x.GetAttribute("virtualPath", String.Empty) + "/";

				if (relativePath.StartsWith(virtualPath))
				{
					// replate the slashes
					relativePath = relativePath.Substring(virtualPath.Length).Replace('/', '\\');

					var tmp = Path.Combine(directoryPath, relativePath);
					if (File.Exists(tmp))
					{
						fullPath = tmp;
						return true;
					}
				}
			}

			fullPath = null;
			return false;
		} // func ResolveXamlPath

		private ParsedXamlFile ParseXaml(string relativePath)
		{
			// find the path
			string fullPath;
			if (!ResolveXamlPath(relativePath, out fullPath))
				return null;

			// load the plain xml-document
			var xTarget = LoadDocument(fullPath);

			// get the root element
			var nameRootElement = xTarget.Root.Name;
			var nameRootResources = XName.Get(nameRootElement.LocalName + ".Resources", nameRootElement.NamespaceName);

			// find code element
			var xCode = xTarget.Root.Element(xnXamlCode);
			
			// find root resource element and detach it
			var xSourceResources = xTarget.Root.Element(nameRootResources);
			if (xSourceResources != null)
				xSourceResources.Remove();

			// create the new dictionary
			var xTargetResourceDictionary = new XElement(nameRootResources.Namespace + "ResourceDictionary");
			var xTargetResources = new XElement(nameRootResources, xTargetResourceDictionary);

			// add the resources
			if (xCode != null)
				xCode.AddAfterSelf(xTargetResources);
			else
				xTarget.Root.AddFirst(xTargetResources);

			// parse the file
			var paneFile = new ParsedXamlFile(this, GetDefaultTheme(), xTarget, xTargetResourceDictionary, false);
			paneFile.AddRootFile(fullPath);

			// resolve merged resources
			paneFile.CombineResourceDictionary(xSourceResources, null, false);
			
			// Gibt die Quelle für den Code an
			if (xCode != null)
			{
				//xDoc.Root.Add(new XAttribute(XNamespace.Xmlns + "s", PpsXmlPosition.xnsNamespace.NamespaceName));

				//XNode first = xCode.FirstNode;

				//owner.ConvertElementSource(first, xCode);
			}

			return paneFile;
		} // func ParseXaml

		private static XElement CreateResourcesNode()
			=> new XElement(PresentationNamespace + "resources"); // change default namespace

		#endregion

		#region -- ParseNavigator ---------------------------------------------------------

		private void WriteSubItem(XmlWriter xml, string name, XElement x,ref int priority)
		{
			var displayName = x.GetAttribute("displayName", String.Empty);
			if (String.IsNullOrEmpty(displayName))
				return;

			xml.WriteStartElement(name);
			xml.WriteAttributeString("name", x.GetAttribute("name", displayName));
			xml.WriteAttributeString("displayName", displayName);

			var tmp = x.GetAttribute("priority", 0);
			if (tmp != 0)
				priority = tmp;
			xml.WriteAttributeString("priority", priority.ToString());

			xml.WriteCData(x.Value);
			xml.WriteEndElement();
		} // proc WriteSubItem

		private void ParseNavigator(IDEContext r)
		{
			using (XmlWriter xml = XmlWriter.Create(r.GetOutputTextWriter(MimeTypes.Text.Xml), Procs.XmlWriterSettings))
			{
				xml.WriteStartElement("navigator");

				foreach (var view in Config.Elements(PpsStuff.xnView))
				{
					var viewId = view.GetAttribute("view", String.Empty);
					var displayName = view.GetAttribute("displayName", String.Empty);

					if (String.IsNullOrEmpty(viewId) ||  // viewId exists
							String.IsNullOrEmpty(displayName) || // displayName exists
							!r.TryDemandToken(view.GetAttribute("securityToken", String.Empty))) // and is accessable
						continue;

					// write the attributes
					xml.WriteStartElement("view");
					xml.WriteAttributeString("name", view.GetAttribute("name", String.Empty));
					xml.WriteAttributeString("displayName", displayName);
					xml.WriteAttributeString("view", viewId);

					xml.WriteAttributeString(view, "filter");
					xml.WriteAttributeString(view, "displayGlyph");

					// write filters and orders
					var priority = 1;
					foreach (var f in view.Elements(PpsStuff.xnFilter))
						WriteSubItem(xml, "filter", f, ref priority);
					priority = 1;
					foreach (var f in view.Elements(PpsStuff.xnOrder))
						WriteSubItem(xml, "order", f, ref priority);

					xml.WriteEndElement();
				} // foreach v

				// parse all action attributes
				var posPriority = 1;

				foreach (var x in Config.Elements(PpsStuff.xnWpfAction))
				{
					var displayName = x.GetAttribute<string>("displayName", null);
					var securityToken = x.GetAttribute<string>("securityToken", null);

					if (String.IsNullOrEmpty(displayName) ||
							!r.TryDemandToken(securityToken))
						continue;

					xml.WriteStartElement("action");

					var priority = x.GetAttribute<int>("priority", posPriority);

					var code = x.Element(PpsStuff.xnWpfCode)?.Value;
					var condition = x.Element(PpsStuff.xnWpfCondition)?.Value;

					xml.WriteAttributeString("name", x.GetAttribute<string>("name", displayName));
					xml.WriteAttributeString("displayName", displayName);
					xml.WriteAttributeString(x, "displayGlyph");

					posPriority = priority + 1;
					xml.WriteAttributeString("priority", priority.ToString());

					if (!String.IsNullOrEmpty(code))
					{
						xml.WriteStartElement("code");
						xml.WriteCData(code);
						xml.WriteEndElement();
					}

					if (!String.IsNullOrEmpty(condition))
					{
						xml.WriteStartElement("condition");
						xml.WriteCData(condition);
						xml.WriteEndElement();
					}

					xml.WriteEndElement();
				}

				// Parse all documents
				foreach (var c in application.CollectChildren<PpsDocument>())
				{
					xml.WriteStartElement("document");
					xml.WriteAttributeString("name", c.Name);
					xml.WriteAttributeString("source", c.Name + "/schema.xml");
					xml.WriteEndElement();
				}

				xml.WriteEndElement();
			}
		} // func ParseNavigator

		#endregion

		#region -- Client synchronisation -------------------------------------------------

		private IEnumerable<PpsApplicationFileItem> GetApplicationFileList(IPpsPrivateDataContext privateUserData)
		{
			var basePath = this.Name;

			// navigator.xml
			yield return new PpsApplicationFileItem(basePath + "/navigator.xml", -1, DateTime.MinValue);

			// templates.xml
			yield return new PpsApplicationFileItem("/constants.xml", -1, DateTime.MinValue);

			// templates.xml
			yield return new PpsApplicationFileItem(basePath + "/templates.xaml", -1, DateTime.MinValue);

			// schemas from application/documents
			foreach (var c in application.CollectChildren<PpsDocument>())
			{
				foreach (var f in c.GetClientFiles(c.Name))
					yield return f;
			}

			// theme, wpfWpfSource
			foreach (var x in Config.Elements())
			{
				if (x.Name == PpsStuff.xnWpfWpfSource)
				{
					var directoryPath = x.GetAttribute("directory", String.Empty);
					var virtualPath = x.GetAttribute("virtualPath", String.Empty);

					foreach (var fi in new DirectoryInfo(directoryPath).GetFiles("*", SearchOption.TopDirectoryOnly))
						yield return new PpsApplicationFileItem(basePath + "/" + virtualPath + "/" + fi.Name, -1, DateTime.MinValue);
				}
				else if (x.Name == PpsStuff.xnWpfTheme)
				{
					var fileName = x.GetAttribute("file", String.Empty);
					var name = Path.GetFileName(fileName);
					yield return new PpsApplicationFileItem(basePath + "/" + name, -1, DateTime.MinValue);
				}
			}
		} // func GetApplicationFileList

		public PpsDataSelector GetApplicationFilesSelector(PpsSysDataSource dataSource, IPpsPrivateDataContext privateUserData)
			=> new PpsGenericSelector<PpsApplicationFileItem>(dataSource, "wpf.sync", GetApplicationFileList(privateUserData));

		#endregion

		private static string GetXamlContentType(IDEContext r)
		{
			switch (r.GetProperty("_debug", String.Empty))
			{
				case "txt":
					return MimeTypes.Text.Plain;
				case "xml":
					return MimeTypes.Text.Xml;
				default:
					return MimeTypes.Application.Xaml;
			}
		} // proc GetXamlContentType

		protected override bool OnProcessRequest(IDEContext r)
		{
			if (r.RelativeSubPath == "default.xaml")
			{
				r.WriteXml(ParseXamlTheme("default").Document, GetXamlContentType(r));
				return true;
			}
			else if (r.RelativeSubPath == "navigator.xml")
			{
				ParseNavigator(r);
				return true;
			}
			else if (r.RelativeSubPath == "templates.xaml")
			{
				r.WriteXml(ParseXamlTemplates().Document, GetXamlContentType(r));
				return true;
			}
			else if (r.RelativeSubPath.EndsWith(".xaml")) // parse wpf template file
			{
				var paneFile = ParseXaml(r.RelativeSubPath);
				if (paneFile != null)
				{
					r.WriteXml(paneFile.Document, GetXamlContentType(r));
					return true;
				}
			}
			else if (r.RelativeSubPath.EndsWith(".lua")) // sent a code snippet
			{
				string fullPath;
				if (ResolveXamlPath(r.RelativeSubPath, out fullPath))
				{
					r.WriteFile(fullPath, MimeTypes.Text.Plain);
					return true;
				}
			}
			return base.OnProcessRequest(r);
		} // func OnProcessRequest

		private static void AddLuaCodeItem(XElement destination, XName name, XElement source)
		{
			var xCode = new XElement(name);
			var sbLua = new StringBuilder();

			var isFirst = true;
			var x = source.FirstNode;
			while (x != null)
			{
				if (x is XText)
				{
					var isPrefix = x is XCData && !isFirst;
					if (isPrefix)
						sbLua.Append(' ', 9); // <![CDATA[
					sbLua.Append(((XText)x).Value);
					if (isPrefix)
						sbLua.Append(' ', 3); // ]]>
				}
				else
					sbLua.Append(x.ToString());

				if (isFirst)
					PpsXmlPosition.GetXmlPositionFromXml(x).WriteAttributes(xCode);

				x = x.NextNode;
			}
			xCode.Add(new XCData(sbLua.ToString()));
			destination.Add(xCode);
		} // proc AddLuaCodeItem
		
		public string ThemesDirectory => Config.GetAttribute("xamlSource", String.Empty);
	} // class WpfClientItem
}

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
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Networking;
using TecWare.DE.Server;
using TecWare.DE.Server.Http;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using TecWare.PPSn.Server.Data;
using TecWare.PPSn.Server.Sql;
using TecWare.PPSn.Stuff;

namespace TecWare.PPSn.Server.Wpf
{
	#region -- interface IWpfClientApplicationFileProvider ----------------------------

	/// <summary>Interface to register client site files for synchronization.</summary>
	public interface IWpfClientApplicationFileProvider
	{
		/// <summary>Get client site files, that should be synchronizated.</summary>
		/// <returns></returns>
		IEnumerable<PpsApplicationFileItem> GetApplicationFiles();
	} // IWpfClientApplicationFileProvider

	#endregion

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

		#region  -- class ParsedXamlFile ----------------------------------------------

		private sealed class ParsedXamlFile
		{
			private readonly object lockFile = new object();

			private readonly WpfClientItem owner;
			private readonly ParsedXamlFile parentFile;
			private readonly XDocument cachedDocument;
			private readonly XElement xResources;

			private readonly List<string> collectedNamespaces;
			private readonly List<string> collectedFiles;
			private readonly List<string> collectedKeys;

			private DateTime lastChanged;

			#region -- Ctor/Dtor ------------------------------------------------------------

			public ParsedXamlFile(WpfClientItem owner, ParsedXamlFile parentFile, XDocument xBasicFile, XElement xResources)
			{
				this.owner = owner;
				this.parentFile = parentFile;
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

			private bool AddKey(XElement x, bool allowRedefine)
			{
				var keyName = x.GetAttribute(xnXamlKey, String.Empty);
				if (String.IsNullOrEmpty(keyName))
				{
					// may we can use the target type -> it is not a 100% correct,
					// because the same type could have multiple string presentations
					keyName = x.GetAttribute("TargetType", String.Empty);
					if (String.IsNullOrEmpty(keyName))
						return true; // add all unknown content
				}

				if (parentFile != null && parentFile.ExistsKey(keyName))
					return allowRedefine;

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

			private void AddNamespace(string prefix, XNamespace ns)
			{
				var pos = collectedNamespaces.BinarySearch(ns.NamespaceName);
				if (pos < 0)
				{
					collectedNamespaces.Insert(~pos, ns.NamespaceName);
					if (!String.IsNullOrEmpty(prefix))
						cachedDocument.Root.SetAttributeValue(XNamespace.Xmlns + prefix, ns.NamespaceName);
				}
			} // proc AddNamespace

			private void AddNamespaces(XElement x)
			{
				// current node
				AddNamespace(x.GetPrefixOfNamespace(x.Name.Namespace), x.Name.Namespace);

				// name space declarations
				foreach (var attr in x.Attributes())
				{
					if (attr.IsNamespaceDeclaration)
						AddNamespace(attr.Name.LocalName, attr.Value);
				}

				// collect all namespaces, also from parent elements
				if (x.Parent != null)
					AddNamespaces(x.Parent);
			} // proc AddNamespaces

			private XNode AddResource(XElement xSource, XNode xAddBefore)
			{
				var position = PpsXmlPosition.GetXmlPositionFromXml(xSource); // get the source information

				XNode xFirstAdded = null;

				if (!position.IsEmpty)
					position.WriteAttributes(xSource);

				// Combine namespaces
				AddNamespaces(xSource);

				// Resource
				var xTmp = AddNode(xAddBefore, xSource);
				return xFirstAdded ?? xTmp;
			} // proc AddResource

			#endregion

			#region -- Add, Load ------------------------------------------------------------

			public void AddResourcesFromFile(string resourceFile, bool allowRedefine)
			{
				if (AddFile(resourceFile))
				{
					var xDocument = LoadResourceDictionary(resourceFile);
					CombineResourceDictionary(xDocument.Root, null, true, allowRedefine);
				}
			} // proc AddResourcesFromFile

			public void AddTemplatesFromFile(string templateFile, bool allowRedefine, ref int currentTemplatePriority)
			{
				if (AddFile(templateFile))
				{
					var xDocument = LoadResourceDictionary(templateFile);

					foreach (var x in xDocument.Root.Elements())
					{
						if (x.Name == xnXamlMergedDictionaries) // merge resources
							CombineMergedResourceDictionaries(x, null, false, allowRedefine);
						else // parse template
						{
							string onlineViewId = null;

							// get the key
							var keyString = x.GetAttribute(xnXamlKey, String.Empty);
							if (String.IsNullOrEmpty(keyString))
								continue;

							// split the key
							var keyElements = keyString.Split(',', ';');
							if (keyElements.Length >= 1)
								keyString = keyElements[0].Trim();
							if (keyElements.Length >= 2)
								onlineViewId = keyElements[1].Trim();
							if (keyElements.Length >= 3)
							{
								if (Int32.TryParse(keyElements[2].Trim(), out var t))
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
				var xDoc = LoadDocument(fileName);
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

			public void CombineResourceDictionary(XElement xResourceDictionary, XNode xAddBefore, bool removeNodes, bool allowRedefine)
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
						else if (x.Name == xnXamlResourceDictionary)
							CombineResourceDictionary(x, xAddBefore, removeNodes, allowRedefine);
						else if (AddKey(x, allowRedefine))
						{
							var xAdded = xResourceDictionary == xResources // do we need to copy the resources
								? x 
								: AddResource(x, xAddBefore);
							if (xMergedAddBefore == null)
								xMergedAddBefore = xAdded;
						}
					}

					// refrenced
					var xMerged = xResourceDictionary.Element(xnXamlMergedDictionaries);
					if (xMerged != null)
						CombineMergedResourceDictionaries(xMerged, xMergedAddBefore, removeNodes, allowRedefine);
				}
				else // refrenced
				{
					CombineResourceDictionary(LoadResourceDictionary(source, AddFile)?.Root, xAddBefore, removeNodes, allowRedefine);
				}
			} // proc CombineResourceDictionary

			private void CombineMergedResourceDictionaries(XElement xMerged, XNode xAddBefore, bool removeNodes, bool allowRedefine)
			{
				// resolve only ResourceDictionaries
				var xResourceDictionaries = xMerged.Elements(xnXamlResourceDictionary).ToArray();
				for (var i = xResourceDictionaries.Length - 1; i >= 0; i--)
				{
					CombineResourceDictionary(xResourceDictionaries[i], xAddBefore, removeNodes, allowRedefine);
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

		private PpsDataSetServerDefinition masterDataSetDefinition;
		private ParsedXamlFile defaultTheme = null;
		private List<PpsApplicationFileItem> scriptAddedFiles = new List<PpsApplicationFileItem>();

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="sp"></param>
		/// <param name="name"></param>
		public WpfClientItem(IServiceProvider sp, string name)
			: base(sp, name)
		{
			this.application = sp.GetService<PpsApplication>(true);

			sp.GetService<IServiceContainer>(true).AddService(typeof(WpfClientItem), this);
		} // ctor

		/// <summary></summary>
		/// <param name="disposing"></param>
		protected override void Dispose(bool disposing)
		{
			if (disposing)
				this.GetService<IServiceContainer>(true).RemoveService(typeof(WpfClientItem));
			base.Dispose(disposing);
		} // proc Dispose

		/// <summary></summary>
		/// <param name="config"></param>
		protected override void OnEndReadConfiguration(IDEConfigLoading config)
		{
			if (masterDataSetDefinition == null)
				application.RegisterInitializationTask(12000, "Bind documents", BindMasterDataSetDefinitionAsync);

			base.OnEndReadConfiguration(config);
		} // proc OnEndReadConfiguration

		private async Task BindMasterDataSetDefinitionAsync()
		{
			masterDataSetDefinition = application.GetDataSetDefinition("masterdata");
			if (!masterDataSetDefinition.IsInitialized) // initialize dataset functionality
				await masterDataSetDefinition.InitializeAsync();
		} // proc BindMasterDataSetDefinitionAsync

		#endregion

		#region -- LoadDocument -------------------------------------------------------

		private static XDocument LoadDocument(string fileName)
			=> RemoveXamlDebugElements(XDocument.Load(fileName, LoadOptions.SetBaseUri | LoadOptions.SetLineInfo));

		#endregion

		#region -- RemoveXamlDebugElements --------------------------------------------

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

		#region -- Wpf Xaml Parser ----------------------------------------------------

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
			var resourceFile = new ParsedXamlFile(this, isDefaultTheme ? null : GetDefaultTheme(), xTarget, xTargetResources);
			foreach (var x in Config.Elements(PpsStuff.xnWpfTheme).Where(x => String.Compare(x.GetAttribute("id", String.Empty), theme, StringComparison.OrdinalIgnoreCase) == 0))
			{
				var fileName = x.GetAttribute("file", String.Empty);
				if (String.IsNullOrEmpty(fileName))
					throw new DEConfigurationException(x, "@file is missing.");
				resourceFile.AddResourcesFromFile(fileName, true);
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
			var templateFile = new ParsedXamlFile(this, GetDefaultTheme(), xTarget, xTargetResources);
			foreach (var x in Config.Elements(PpsStuff.xnWpfTemplate))
			{
				var fileName = x.GetAttribute("file", String.Empty);
				if (String.IsNullOrEmpty(fileName))
					throw new DEConfigurationException(x, "@file is missing.");

				templateFile.AddTemplatesFromFile(fileName, false, ref currentTemplatePriority);
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

				if (virtualPath == "/" || relativePath.StartsWith(virtualPath))
				{
					// replate the slashes
					var relativeLocalPath = virtualPath == "/" ?
						relativePath.Replace('/', '\\') :
						relativePath.Substring(virtualPath.Length).Replace('/', '\\');

					var tmp = Path.Combine(directoryPath, relativeLocalPath);
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
			if (!ResolveXamlPath(relativePath, out var fullPath))
				return null;

			// load the plain xml-document
			var xTarget = LoadDocument(fullPath);

			// get the root element
			var nameRootElement = xTarget.Root.Name;
			var nameRootResources = XName.Get(nameRootElement.LocalName + ".Resources", nameRootElement.NamespaceName);

			// find root resource element and detach it
			var xNodeBeforeResources = (XNode)null;
			var xSourceResources = xTarget.Root.Element(nameRootResources);
			if (xSourceResources != null)
			{
				xNodeBeforeResources = xSourceResources.PreviousNode;
				Procs.XCopyAnnotations(xSourceResources, xSourceResources);
				xSourceResources.Remove();
			}

			// create the new dictionary
			var xTargetResourceDictionary = new XElement(PresentationNamespace + "ResourceDictionary");
			var xTargetResources = new XElement(nameRootResources, xTargetResourceDictionary);

			// add the resources
			if (xNodeBeforeResources != null)
				xNodeBeforeResources.AddAfterSelf(xTargetResources); // add old position
			else
			{
				// find code element
				var xCode = xTarget.Root.Element(xnXamlCode);
				if (xCode != null)
					xCode.AddAfterSelf(xTargetResources); // after the code directive
				else
					xTarget.Root.AddFirst(xTargetResources); // as first elemenet
			}

			// parse the file
			var paneFile = new ParsedXamlFile(this, GetDefaultTheme(), xTarget, xTargetResourceDictionary);
			paneFile.AddRootFile(fullPath);

			// resolve merged resources
			paneFile.CombineResourceDictionary(xSourceResources, null, false, false);

			return paneFile;
		} // func ParseXaml

		private static XElement CreateResourcesNode()
			=> new XElement(PresentationNamespace + "resources"); // change default namespace

		#endregion

		#region -- ParseNavigator -----------------------------------------------------

		private void EmitSubViewItem(XmlWriter xml, string name, XElement x, ref int priority)
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
		} // proc EmitSubViewItem

		private void EmitViewItem(IDEWebRequestScope r, XmlWriter xml, XElement view)
		{
			var viewId = view.GetAttribute("view", String.Empty);
			var displayName = view.GetAttribute("displayName", String.Empty);

			if (String.IsNullOrEmpty(viewId) ||  // viewId exists
					String.IsNullOrEmpty(displayName) || // displayName exists
					!r.TryDemandToken(view.GetAttribute("securityToken", String.Empty))) // and is accessable
				return;

			// write the attributes
			xml.WriteStartElement("view");
			xml.WriteAttributeString("name", view.GetAttribute("name", String.Empty));
			xml.WriteAttributeString("displayName", displayName);
			xml.WriteAttributeString("view", viewId);

			xml.WriteAttributeString(view, "filter");
			xml.WriteAttributeString(view, "displayImage");

			// write filters and orders
			var priority = 1;
			foreach (var f in view.Elements(PpsStuff.xnFilter))
				EmitSubViewItem(xml, "filter", f, ref priority);
			priority = 1;
			foreach (var f in view.Elements(PpsStuff.xnOrder))
				EmitSubViewItem(xml, "order", f, ref priority);

			xml.WriteEndElement();
		} // proc EmitViewItem

		#region -- class EnvironmentCodeComparer ------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class EnvironmentCodeComparer : IComparer<Tuple<int, string>>
		{
			private EnvironmentCodeComparer()
			{
			} // ctor

			public int Compare(Tuple<int, string> x, Tuple<int, string> y)
				=> x.Item1 - y.Item1;

			public static EnvironmentCodeComparer Instance { get; } = new EnvironmentCodeComparer();
		} // class EnvironmentCodeComparer

		#endregion

		private string CollectEnvironmentScripts(string hostName)
		{
			var priority = 1;
			var environmentLoader = new List<Tuple<int, string>>();
			foreach (var env in Config.Elements(PpsStuff.xnEnvironment))
			{
				priority = env.GetAttribute("priority", priority);

				var xCode = env.GetElement(PpsStuff.xnCode);
				if (xCode != null && !String.IsNullOrEmpty(xCode.Value))
				{
					var item = new Tuple<int, string>(priority,
						"--L=" + PpsXmlPosition.GetXmlPositionFromXml(xCode).LineInfo + Environment.NewLine + xCode.Value
					);
					var index = environmentLoader.BinarySearch(item, EnvironmentCodeComparer.Instance);
					environmentLoader.Insert(index < 0 ? ~index : index + 1, item);
				}

				priority = priority + 1;
			}

			var createdScript = CallMemberDirect("OnCollectEnvironmentScripts", new object[] { hostName }, ignoreNilFunction: true).ToString();
			if (!String.IsNullOrEmpty(createdScript))
				environmentLoader.Add(new Tuple<int, string>(Int32.MaxValue, createdScript));

			return String.Join(Environment.NewLine, from c in environmentLoader select c.Item2);
		} // func CollectEnvironmentScripts

		private void ParseEnvironment(IDEWebRequestScope r)
		{
			var user = r.GetUser<IPpsPrivateDataContext>();

			using (var xml = XmlWriter.Create(r.GetOutputTextWriter(MimeTypes.Text.Xml), Procs.XmlWriterSettings))
			{
				xml.WriteStartElement("environment");

				// create environment extentsions
				var environmentCode = CollectEnvironmentScripts(r.GetProperty("ppsn-hostname", String.Empty));
				if (environmentCode != null)
				{
					xml.WriteStartElement("code");
					xml.WriteCData(environmentCode);
					xml.WriteEndElement();
				}

				// write navigator specific
				xml.WriteStartElement("navigator");

				foreach (var view in Config.Elements(PpsStuff.xnView))
					EmitViewItem(r, xml, view);

				// parse all action attributes
				var posPriority = 1;

				foreach (var x in Config.Elements(PpsStuff.xnWpfAction))
				{
					var displayName = x.GetAttribute<string>("displayName", null);
					var displayImage = x.GetAttribute<string>("displayImage", "star");
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
					xml.WriteAttributeString("displayImage", displayImage);

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
				foreach (var c in application.CollectChildren<IPpsObjectItem>())
				{
					xml.WriteStartElement("document");
					xml.WriteAttributeString("name", c.ObjectType);
					if (c.ObjectSource != null)
						xml.WriteAttributeString("source", c.ObjectSource);
					if (c.DefaultPane != null)
						xml.WriteAttributeString("pane", c.DefaultPane);
					xml.WriteAttributeString("isRev", c.IsRevDefault.ChangeType<string>());
					xml.WriteEndElement();
				}

				xml.WriteEndElement();
				xml.WriteEndElement();
			}
		} // func ParseNavigator

		#endregion

		#region -- Client synchronisation ---------------------------------------------

		/// <summary>Add a new file, to synchronize to the client offline cache.</summary>
		/// <param name="path">Path to the file.</param>
		/// <param name="length">Length of the file.</param>
		/// <param name="lastWriteTime">Timestamp of the file.</param>
		public void AddApplicationFileItem(string path, long length, DateTime lastWriteTime)
		{
			lock (scriptAddedFiles)
			{
				var index = scriptAddedFiles.FindIndex(c => c.Path == path);
				if (index == -1)
					scriptAddedFiles.Add(new PpsApplicationFileItem(path, length, lastWriteTime));
				else
					scriptAddedFiles[index] = new PpsApplicationFileItem(path, length, lastWriteTime);
			}
		} // proc AddApplicationFileItem

		private IEnumerable<PpsApplicationFileItem> GetApplicationFileList()
		{
			var basePath = this.Name;

			yield return new PpsApplicationFileItem(basePath + "/styles.xaml", -1, Server.Configuration.ConfigurationStamp);

			// navigator.xml
			yield return new PpsApplicationFileItem(basePath + "/environment.xml", -1, Server.Configuration.ConfigurationStamp);

			// templates.xml
			yield return new PpsApplicationFileItem(basePath + "/templates.xaml", -1, Server.Configuration.ConfigurationStamp);

			// schemas from application/documents
			foreach (var c in application.CollectChildren<IWpfClientApplicationFileProvider>())
			{
				foreach (var f in c.GetApplicationFiles())
					yield return f;
			}

			// add script files
			lock (scriptAddedFiles)
			{
				foreach (var c in scriptAddedFiles)
					yield return c;
			}

			// theme, wpfWpfSource
			foreach (var x in Config.Elements())
			{
				if (x.Name == PpsStuff.xnWpfWpfSource)
				{
					var directoryPath = x.GetAttribute("directory", String.Empty);
					var virtualPath = x.GetAttribute("virtualPath", String.Empty);

					foreach (var fi in new DirectoryInfo(directoryPath).GetFiles("*", SearchOption.TopDirectoryOnly))
						yield return new PpsApplicationFileItem(basePath + "/" + virtualPath + "/" + fi.Name, fi.Length, fi.LastWriteTimeUtc);
				}
				else if (x.Name == PpsStuff.xnWpfTheme)
				{
					var id = x.GetAttribute("id", String.Empty);
					if (String.Compare(id, "default", StringComparison.OrdinalIgnoreCase) != 0)
						yield return new PpsApplicationFileItem(basePath + "/styles.xaml?id=" + id, -1, Server.Configuration.ConfigurationStamp);
				}
			}
		} // func GetApplicationFileList

		/// <summary>Return a view to all files that will be offline available on the client.</summary>
		/// <param name="dataSource"></param>
		/// <returns></returns>
		public PpsDataSelector GetApplicationFilesSelector(PpsSysDataSource dataSource)
			=> new PpsGenericSelector<PpsApplicationFileItem>(dataSource.SystemConnection, "wpf.sync", GetApplicationFileList());

		#endregion

		#region -- Master-Data Synchronisation ----------------------------------------
		
		private static void PrepareMasterDataSyncArguments(IDEWebRequestScope r, string tableName, long syncId, long lastSyncTimeStamp, out Dictionary<string, long> syncIds, out bool syncAllTables, out DateTime lastSynchronization, LuaTable updateTags)
		{
			syncIds = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
			if (r.HasInputData)
			{
				using (var xml = XmlReader.Create(r.GetInputTextReader(), Procs.XmlReaderSettings))
				{
					if (xml.MoveToContent() != XmlNodeType.Element)
						throw new ArgumentException();

					lastSyncTimeStamp = xml.GetAttribute<long>("lastSyncTimeStamp", -1);
					lastSynchronization = lastSyncTimeStamp >= 0 ? DateTime.FromFileTimeUtc(lastSyncTimeStamp) : DateTime.MinValue;

					if (!xml.IsEmptyElement)
					{
						xml.Read();

						while (xml.NodeType == XmlNodeType.Element)
						{
							if (xml.LocalName == "sync")
							{
								var table = xml.GetAttribute("table");
								syncIds[table] = Int64.Parse(xml.GetAttribute("syncId"));
								if (!xml.IsEmptyElement)
									xml.Skip();
								else
									xml.Read();
							}
							else if (xml.LocalName == "utag")
							{
								var tagData = new LuaTable
								{
									["Id"] = xml.GetAttribute("id", -1L),
									["ObjKId"] = xml.GetAttribute("objectId", -1L),
									["Key"] = xml.GetAttribute("name", String.Empty),
									["Class"] = xml.GetAttribute("tagClass", -1),
									["UserId"] = xml.GetAttribute("userId", -1),
									["CreateDate"] = xml.GetAttribute("createDate", DateTime.Now)
								};
								updateTags.ArrayList.Add(tagData);

								if (!xml.IsEmptyElement)
								{
									xml.Read();
									tagData["Value"] = xml.GetElementContent((string)null);
									if (xml.NodeType != XmlNodeType.EndElement)
										xml.Skip();
								}
								else
									xml.Read(); // read element
							}
							else
								xml.Skip();
						}

						xml.ReadEndElement();
					}
				}
				syncAllTables = true;
			}
			else if (!String.IsNullOrEmpty(tableName)) // create a single sync
			{
				lastSynchronization = lastSyncTimeStamp >= 0 ? DateTime.FromFileTimeUtc(lastSyncTimeStamp) : DateTime.MinValue;
				syncIds[tableName] = syncId;
				syncAllTables = false;
			}
			else // create a full batch
			{
				lastSynchronization = DateTime.MinValue;
				syncAllTables = true;
			}
		} // proc PrepareMasterDataSyncArguments

		private static void ExecuteMasterDataTableSync(LogMessageScopeProxy msg, IPpsPrivateDataContext user, Dictionary<PpsDataSource, PpsDataSynchronization> synchronisationSessions, XmlWriter xml, PpsDataTableDefinition table, long lastSyncId, DateTime lastSynchronization)
		{
			msg.WriteLine("Sync: {0}", table.Name);
			using (msg.Indent())
			{
				var syncType = table.Meta.GetProperty("syncType", String.Empty);
				if (String.IsNullOrEmpty(syncType) || String.Compare(syncType, "None", StringComparison.OrdinalIgnoreCase) == 0)
				{
					msg.WriteLine("Ignored.");
					return; // next table
				}

				var primaryKey = table.PrimaryKey as PpsDataColumnServerDefinition;
				if (primaryKey == null)
				{
					msg.WriteLine("Primary is null or has no field definition.");
					return;
				}

				var dataSource = primaryKey.FieldDescription.DataSource;

				// start a session
				if (!synchronisationSessions.TryGetValue(dataSource, out var session))
				{
					session = dataSource.CreateSynchronizationSession(user, lastSynchronization);
					synchronisationSessions[dataSource] = session;
				}

				// generate synchronization batch
				xml.WriteStartElement("batch");
				try
				{
					xml.WriteAttributeString("table", table.Name);

					using (var rows = session.GenerateBatch(table, syncType, lastSyncId))
					{
						if (rows.IsFullSync)
							xml.WriteAttributeString("isFull", rows.IsFullSync.ChangeType<string>());

						// create column names
						var columnNames = new string[table.Columns.Count];
						var columnIndex = new int[columnNames.Length];
						var columnConvert = new Func<object, string>[columnNames.Length];

						for (var i = 0; i < columnNames.Length; i++)
						{
							columnNames[i] = "c" + i.ToString();

							var targetColumn = table.Columns[i];
							var syncSourceColumnName = targetColumn.Meta.GetProperty(PpsDataColumnMetaData.SourceColumn, targetColumn.Name);
							var sourceColumnIndex = syncSourceColumnName == "#" ? -1 : ((IDataColumns)rows).FindColumnIndex(PpsSqlColumnInfo.GetColumnName(syncSourceColumnName));
							if (sourceColumnIndex == -1)
							{
								columnNames[i] = null;
								columnIndex[i] = -1;
								columnConvert[i] = null;
							}
							else
							{
								columnNames[i] = "c" + i.ToString();
								columnIndex[i] = sourceColumnIndex;
								if (rows.Columns[sourceColumnIndex].DataType == typeof(DateTime) && targetColumn.DataType == typeof(long))
								{
									columnConvert[i] = v =>
									{
										var dt = (DateTime)v;
										return dt == DateTime.MinValue ? null : dt.ToFileTimeUtc().ToString();
									};
								}
								else
									columnConvert[i] = v => v.ChangeType<string>();
							}
						}

						// export columns
						var newSyncId = rows.CurrentSyncId; // sync Id before first row
						while (rows.MoveNext())
						{
							var r = rows.Current;

							if (newSyncId < rows.CurrentSyncId)
								newSyncId = rows.CurrentSyncId;

							xml.WriteStartElement(rows.CurrentMode.ToString().ToLower());
							try
							{
								for (var i = 0; i < columnNames.Length; i++)
								{
									if (columnIndex[i] != -1)
									{
										var v = r[columnIndex[i]];
										if (v != null)
										{
											var s = columnConvert[i](v);
											if (s != null)
												xml.WriteElementString(columnNames[i], s);
										}
									}
								}
							}
							finally
							{
								xml.WriteEndElement();
							}
						}

						// write syncId
						xml.WriteStartElement("syncId");
						xml.WriteValue(newSyncId.ChangeType<string>());
						xml.WriteEndElement();
					}
				}
				finally
				{
					// end element
					xml.WriteEndElement();
				}
			}
		} // proc ExecuteMasterDataTableSync

		[DEConfigHttpAction("mdata", SecurityToken = SecurityUser, IsSafeCall = false)]
		private void HttpMasterDataSyncAction(IDEWebRequestScope r, string tableName = null, long syncId = -1, long syncStamp = -1)
		{
			var user = r.GetUser<IPpsPrivateDataContext>();

			if (masterDataSetDefinition == null || !masterDataSetDefinition.IsInitialized)
				throw new ArgumentException("Masterdata schema not initialized.");

			var updateTags = new LuaTable
			{
				["upsert"] = "dbo.ObjT",
				["columnList"] = new LuaTable { "Id", "ObjKId", "ObjRId", "Key", "UserId", "Class", "Value", "CreateDate" },
				["on"] = new LuaTable { "ObjKId", "ObjRId", "Key", "Class", "UserId" },
				["nmsrc"] = new LuaTable { ["delete"] = true, ["where"] = "DST.[Id] = @Id" }
			};

			// parse incomming sync id's
			PrepareMasterDataSyncArguments(r, tableName, syncId, syncStamp, out var syncIds, out var syncAllTables, out var lastSynchronization, updateTags);

			var synchronisationSessions = new Dictionary<PpsDataSource, PpsDataSynchronization>();
			var msg = Log.CreateScope(LogMsgType.Information, autoFlush: false, stopTime: true);

			// update tags
			if (updateTags.ArrayList.Count > 0)
			{
				using (var trans = user.CreateTransactionAsync(application.MainDataSource).AwaitTask())
				{
					trans.ExecuteNoneResult(updateTags);
					trans.Commit();
				}
			}

			// return changes
			var nextSyncStamp = DateTime.Now.ToFileTimeUtc();
			using (var xml = XmlWriter.Create(r.GetOutputTextWriter(MimeTypes.Text.Xml, Encoding.UTF8), Procs.XmlWriterSettings))
			{
				try
				{
					xml.WriteStartDocument();
					xml.WriteStartElement("mdata");

					if (syncAllTables)
					{
						foreach (var table in masterDataSetDefinition.TableDefinitions)
						{
							// get sync id
							if (!syncIds.TryGetValue(table.Name, out var currentSyncId))
								currentSyncId = -1;

							ExecuteMasterDataTableSync(msg, user, synchronisationSessions, xml, table, currentSyncId, lastSynchronization);
						}
					}
					else
					{
						foreach (var kv in syncIds)
						{
							var table = masterDataSetDefinition.FindTable(kv.Key);
							if (table != null)
								ExecuteMasterDataTableSync(msg, user, synchronisationSessions, xml, table, kv.Value, lastSynchronization);
							else
								msg.WriteLine($"Table not found: {kv.Value}");
						}
					}

					xml.WriteStartElement("syncStamp");
					xml.WriteValue(nextSyncStamp - 1);
					xml.WriteEndElement();

					xml.WriteEndElement();
					xml.WriteEndDocument();
				}
				catch (Exception e)
				{
					msg.AutoFlush(true);

					xml.WriteStartElement("error");
					xml.WriteValue(e.Message);
					xml.WriteEndElement();

					xml.Dispose();

					throw new HttpResponseException(HttpStatusCode.InternalServerError, "Can not create sync data.", e);
				}
				finally
				{
					// finish the sync sessions
					foreach (var c in synchronisationSessions)
						c.Value.Dispose();

					msg.Dispose();
				}
			}
		} // proc HttpMasterDataSyncAction

		#endregion

		private static string GetXamlContentType(IDEWebRequestScope r)
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

		/// <summary>Installs handlers for some virtual client files.</summary>
		/// <param name="r"></param>
		/// <returns></returns>
		protected override async Task<bool> OnProcessRequestAsync(IDEWebRequestScope r)
		{
			if (r.RelativeSubPath == "styles.xaml")
			{
				await Task.Run(() =>
				{
					var id = r.GetProperty("id", "default");
					r.WriteXml(ParseXamlTheme(id).Document, GetXamlContentType(r));
				});
				return true;
			}
			else if (r.RelativeSubPath == "environment.xml")
			{
				await Task.Run(() => ParseEnvironment(r));
				return true;
			}
			else if (r.RelativeSubPath == "templates.xaml")
			{
				await Task.Run(() => r.WriteXml(ParseXamlTemplates().Document, GetXamlContentType(r)));
				return true;
			}
			else if (r.RelativeSubPath == "masterdata.xml")
			{
				await Task.Run(() => masterDataSetDefinition.WriteToDEContext(r, ConfigPath + "/masterdata.xml"));
				return true;
			}
			else if (r.RelativeSubPath.EndsWith(".xaml")) // parse wpf template file
			{
				if (await Task.Run(() =>
				 {
					 var paneFile = ParseXaml(r.RelativeSubPath);
					 if (paneFile != null)
					 {
						 r.WriteXml(paneFile.Document, GetXamlContentType(r));
						 return true;
					 }
					 else
						 return false;
				 }))
					return true;
			}
			else if (r.RelativeSubPath.EndsWith(".lua")) // sent a code snippet
			{
				if (await Task.Run(() =>
				{
					if (ResolveXamlPath(r.RelativeSubPath, out var fullPath))
					{
						r.WriteFile(fullPath, MimeTypes.Text.Plain);
						return true;
					}
					else
						return false;
				}))
					return true;
			}
			return await base.OnProcessRequestAsync(r);
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

		/// <summary>Get the themes directory.</summary>
		public string ThemesDirectory => Config.GetAttribute("xamlSource", String.Empty);
		/// <summary>Masterdata view of the client.</summary>
		public PpsDataSetServerDefinition MasterDataSetDefinition => masterDataSetDefinition;
	} // class WpfClientItem
}

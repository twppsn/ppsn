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
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using Neo.IronLua;
using TecWare.DE.Networking;
using TecWare.DE.Server.Configuration;
using TecWare.DE.Server.Http;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.Server
{
	public partial class PpsApplication
	{
		#region -- class GeometryInfo -------------------------------------------------

		/// <summary>Description of a style geometry</summary>
		public sealed class GeometryInfo
		{
			private const string DefaultViewPort = "0,0,24,24";

			internal GeometryInfo(string name, string path, string viewport)
			{
				Name = name ?? throw new ArgumentNullException(nameof(name));
				Path = path ?? throw new ArgumentNullException(nameof(path));
				ViewPort = viewport ?? DefaultViewPort;
			} // ctor

			/// <summary>Name of the geometry</summary>
			public string Name { get; }
			/// <summary>View port of the geometry</summary>
			public string ViewPort { get; }
			/// <summary></summary>
			public bool IsDefaultViewPort => ViewPort == DefaultViewPort;
			/// <summary>Wpf path of the geometry.</summary>
			public string Path { get; }
		} // class GeometryInfo

		#endregion

		private bool TryGetStyleNode(out XConfigNode styles)
		{
			styles = ConfigNode.Element(PpsStuff.xnStyle, false);
			return styles != null;
		} // func TryGetStyleNode

		private static GeometryInfo CreateGeometryInfo(XConfigNode x)
		{
			return new GeometryInfo(
				x.GetAttribute<string>("name"),
				x.GetAttribute<string>("path"),
				x.GetAttribute<string>("rect")
			);
		} // func CreateGeometryInfo

		/// <summary>Get all geometries</summary>
		/// <returns></returns>
		[LuaMember]
		public IEnumerable<GeometryInfo> GetGeometries()
		{
			if (TryGetStyleNode(out var styles))
			{
				foreach (var x in styles.Elements(PpsStuff.xnStyleGeometry))
					yield return CreateGeometryInfo(x);
			}
		} // func GetGeometries

		/// <summary></summary>
		/// <param name="geometryName"></param>
		/// <returns></returns>
		[LuaMember]
		public GeometryInfo GetGeometry(string geometryName)
		{
			if (TryGetStyleNode(out var styles))
				return styles.Elements(PpsStuff.xnStyleGeometry)
					.Where(c => String.Compare(c.GetAttribute<string>("name"), geometryName, StringComparison.OrdinalIgnoreCase) == 0)
					.Select(c => CreateGeometryInfo(c))
					.FirstOrDefault();
			else
				return null;
		} // func GetGeometryPath

		private async Task<bool> WriteJsonGeometriesAsync(IDEWebRequestScope r)
		{
			// set time stamp
			r.SetLastModified(lastConfigurationTimeStamp);

			// write content
			if (r.InputMethod == HttpMethod.Head.Method)
				r.SetStatus(HttpStatusCode.OK, "Ok");
			else
			{
				await Task.Run(() =>
				{
					using (var sw = r.GetOutputTextWriter(MimeTypes.Text.Json))
					{
						sw.Write("{");
						var first = true;
						if (TryGetStyleNode(out var styles))
						{
							foreach (var g in GetGeometries())
							{
								if (first)
									first = false;
								else
									sw.Write(",");

								sw.Write($"\"{g.Name.Replace('"', '_')}\":");
								sw.Write(new LuaTable
								{
									["p"] = g.Path,
									["r"] = g.IsDefaultViewPort ? null : g.ViewPort
								}.ToJson(false));
							}
						}
						sw.Write("}");
						sw.Flush();
					}
				}
				);
			}

			return true;
		} // func WriteXmllGeometriesAsync

		private async Task<bool> WriteXmlGeometriesAsync(IDEWebRequestScope r)
		{
			// set time stamp
			r.SetLastModified(lastConfigurationTimeStamp);

			// write content
			if (r.InputMethod == HttpMethod.Head.Method)
				r.SetStatus(HttpStatusCode.OK, "Ok");
			else
			{
				await Task.Run(() =>
					{
						using (var xml = XmlWriter.Create(r.GetOutputTextWriter(MimeTypes.Text.Xml), Procs.XmlWriterSettings))
						{
							xml.WriteStartElement("geometries");
							if (TryGetStyleNode(out var styles))
							{
								foreach (var g in GetGeometries())
								{
									xml.WriteStartElement("g");
									xml.WriteAttributeString("n", g.Name);
									if (!g.IsDefaultViewPort)
										xml.WriteAttributeString("r", g.ViewPort);
									xml.WriteValue(g.Path);
									xml.WriteEndElement();
								}
							}
							xml.WriteEndElement();
							xml.Flush();
						}
					}
				);
				
			}

			return true;
		} // func WriteXmllGeometriesAsync

		private Task<bool> WriteSingleGeometry(IDEWebRequestScope r)
		{
			return Task.FromResult(false);
		} // func WriteSingleGeometry
	} // class PpsApplication
}

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
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Networking;
using TecWare.DE.Server.Configuration;
using TecWare.DE.Server.Http;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.Server
{
	#region -- class GeometryInfo -----------------------------------------------------

	/// <summary>Description of a style geometry</summary>
	public sealed class GeometryInfo
	{
		/// <summary>Default path view port.</summary>
		public const string DefaultViewPort = "0,0,24,24";

		internal GeometryInfo(string name, string path, string viewport)
		{
			Name = name ?? throw new ArgumentNullException(nameof(name));
			RawPath = path ?? throw new ArgumentNullException(nameof(path));
			ViewPort = viewport ?? DefaultViewPort;
		} // ctor

		public bool TryGetPath(out string path, out string pathFill)
		{
			var rawPath = RawPath;
			if (rawPath == null || rawPath.Length < 2)
			{
				path = null;
				pathFill = null;
				return false;
			}

			var ofs = 0;
			pathFill = "evenodd";

			// test for fill rule
			if (rawPath[0] == 'F' && rawPath[1] == '1')
			{
				ofs += 2;
				pathFill = "nonzero";
			}

			// create path
			path = rawPath.Substring(ofs);
			return true;
		} // func TryGetPath

		/// <summary>Name of the geometry</summary>
		public string Name { get; }
		/// <summary>View port of the geometry</summary>
		public string ViewPort { get; }
		/// <summary></summary>
		public bool IsDefaultViewPort => ViewPort == DefaultViewPort;
		/// <summary>Wpf path of the geometry.</summary>
		public string RawPath { get; }
	} // class GeometryInfo

	#endregion

	public partial class PpsApplication
	{
		#region -- enum ImageOuput ----------------------------------------------------

		private enum ImageOuput
		{
			None,
			Bmp,
			Png,
			Jpeg,
			Svg
		} // enum ImageOuput

		#endregion

		#region -- Core geometry ------------------------------------------------------

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

		#endregion

		#region -- Lua geometry access ------------------------------------------------

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

		#endregion

		#region -- Write all geometries -----------------------------------------------

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
									["p"] = g.RawPath,
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
									xml.WriteValue(g.RawPath);
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

		#endregion

		#region -- Write single geometry ----------------------------------------------

		[Obsolete]
		const string svgMime = "image/svg+xml";

		private ImageOuput GetImageOutputByMimeType(string mimeType, ImageOuput @default)
		{
			switch (mimeType)
			{
				case MimeTypes.Image.Bmp:
					return ImageOuput.Bmp;
				case MimeTypes.Image.Png:
					return ImageOuput.Png;
				case MimeTypes.Image.Jpeg:
					return ImageOuput.Jpeg;
				case svgMime: // MimeTypes.Image.Svg:
					return ImageOuput.Svg;
				default:
					return @default;
			}
		} // func GetImageOutputByMimeType

		private ImageOuput GetImageOutputByArgument(string typ, ImageOuput @default)
		{
			switch(typ)
			{
				case "bmp":
					return ImageOuput.Bmp;
				case "png":
					return ImageOuput.Png;
				case "jpg":
					return ImageOuput.Jpeg;
				case "svg":
					return ImageOuput.Svg;
				default:
					return @default;
			}
		} // func GetImageOutputByArgument

		private static readonly XNamespace svgNameSpace = "http://www.w3.org/2000/svg";
		private static readonly XName xSvg = svgNameSpace + "svg";
		private static readonly XName xPath = svgNameSpace + "path";

		private async Task<bool> ProcessSingleGeometryAsSvgAsync(IDEWebRequestScope r, GeometryInfo geometry)
		{
			if (!geometry.TryGetPath(out var path, out var pathFill))
			{
				r.SetStatus(HttpStatusCode.BadRequest, "Invalid geometry.");
				return true;
			}
			
			var svg = new XDocument(
				new XDocumentType("svg", "-//W3C//DTD SVG 1.1//EN", "http://www.w3.org/Graphics/SVG/1.1/DTD/svg11.dtd", null),
				new XElement(xSvg,
					new XAttribute("version", "1.1"),
					new XAttribute("width", r.GetProperty("w", 24)),
					new XAttribute("height", r.GetProperty("h", 24)),
					new XAttribute("viewBox", geometry.ViewPort.Replace(',', ' ')),
					new XElement(xPath,
						new XAttribute("fill", "#" + r.GetProperty("f", "000000")),
						new XAttribute("fill-rule", pathFill),
						new XAttribute("fill-opacity", r.GetProperty("fo", 1.0f)),
						new XAttribute("d", path)
					)
				)
			);

			await r.WriteXmlAsync(svg, svgMime);
			return true;
		} // func ProcessSingleGeometryAsSvgAsync

		private Task<bool> ProcessSingleGeometryAsync(IDEWebRequestScope r, GeometryInfo geometry)
		{
			var outputType = GetImageOutputByArgument(r.GetProperty("t", null), GetImageOutputByMimeType(r.AcceptedTypes.FirstOrDefault(), ImageOuput.Svg));
			switch(outputType)
			{
				case ImageOuput.Svg:
					return ProcessSingleGeometryAsSvgAsync(r, geometry);
				default:
					r.SetStatus(HttpStatusCode.BadRequest, "Invalid output type.");
					return Task.FromResult(true);
			}
		} // proc ProcessSingleGeometryAsync

		private Task<bool> WriteSingleGeometryAsync(IDEWebRequestScope r)
		{
			// image key
			var geometry = GetGeometry(r.RelativeSubPath);
			if (geometry == null)
				return Task.FromResult(false);
			else
				return ProcessSingleGeometryAsync(r, geometry);
		} // func WriteSingleGeometryAsync

		#endregion
	} // class PpsApplication
}

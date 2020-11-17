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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Neo.IronLua;
using Svg;
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

		internal GeometryInfo(string name, string path, string path2, string viewport)
		{
			Name = name ?? throw new ArgumentNullException(nameof(name));
			RawPath = path ?? throw new ArgumentNullException(nameof(path));
			RawPath2 = String.IsNullOrEmpty(path2) ? null : path2;
			ViewPort = viewport ?? DefaultViewPort;
		} // ctor

		private static bool TryGetPath(string rawPath, out string path, out string pathFill)
		{
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

		/// <summary>Split the raw path in fill type and path.</summary>
		/// <param name="path"></param>
		/// <param name="pathFill"></param>
		/// <returns></returns>
		public bool TryGetPath(out string path, out string pathFill)
			=> TryGetPath(RawPath, out path, out pathFill);

		/// <summary>Split the raw path2 in fill type and path.</summary>
		/// <param name="path"></param>
		/// <param name="pathFill"></param>
		/// <returns></returns>
		public bool TryGetPath2(out string path, out string pathFill)
			=> TryGetPath(RawPath2, out path, out pathFill);

		/// <summary>Name of the geometry</summary>
		public string Name { get; }
		/// <summary>View port of the geometry</summary>
		public string ViewPort { get; }
		/// <summary></summary>
		public bool IsDefaultViewPort => ViewPort == DefaultViewPort;
		/// <summary>Wpf path of the geometry.</summary>
		public string RawPath { get; }
		/// <summary>Wpf path of the geometry for the accent color.</summary>
		public string RawPath2 { get; }
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
				x.GetAttribute<string>("path2"),
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
								["p2"] = g.RawPath2,

								["r"] = g.IsDefaultViewPort ? null : g.ViewPort
							}.ToJson(false));
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

							foreach (var g in GetGeometries())
							{
								xml.WriteStartElement("g");
								xml.WriteAttributeString("n", g.Name);
								if (!g.IsDefaultViewPort)
									xml.WriteAttributeString("r", g.ViewPort);
								xml.WriteStartElement("path");
								xml.WriteValue(g.RawPath);
								xml.WriteEndElement();

								if (g.RawPath2 != null)
								{
									xml.WriteStartElement("path2");
									xml.WriteValue(g.RawPath);
									xml.WriteEndElement();
								}

								xml.WriteEndElement();
							}

							xml.WriteEndElement();
							xml.Flush();
						}
					}
				);
				
			}

			return true;
		} // func WriteXmllGeometriesAsync

		private async Task<bool> WriteXamlGeometriesAsync(IDEWebRequestScope r)
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
						using (var xml = XmlWriter.Create(r.GetOutputTextWriter(MimeTypes.Application.Xaml), Procs.XmlWriterSettings))
						{
							xml.WriteAttributeString("xmlns", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
							xml.WriteAttributeString("xmlns:x", "http://schemas.microsoft.com/winfx/2006/xaml");
							
							xml.WriteStartElement("ResourceDictionary");

							foreach (var g in GetGeometries())
							{
								if (g.RawPath2 == null)
								{
									xml.WriteStartElement("PathGeometry");
									xml.WriteAttributeString("Key", "x", g.Name + "PathGeometry");
									xml.WriteAttributeString("Figures", g.RawPath);
									xml.WriteEndElement();
								}
								else
								{
									xml.WriteStartElement("CombinedGeometry");

									xml.WriteStartElement("CombinedGeometry.Geometry1");
									xml.WriteStartElement("PathGeometry");
									xml.WriteAttributeString("Figures", g.RawPath);
									xml.WriteEndElement();
									xml.WriteEndElement();

									xml.WriteStartElement("CombinedGeometry.Geometry2");
									xml.WriteStartElement("PathGeometry");
									xml.WriteAttributeString("Figures", g.RawPath2);
									xml.WriteEndElement();
									xml.WriteEndElement();

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
		} // func WriteXamlGeometriesAsync

		#endregion

		#region -- Write single geometry ----------------------------------------------

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
				case MimeTypes.Image.Svg:
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

		private static XElement GetSvgPathElement(Color color, string pathFill, string path)
		{
			return new XElement(xPath,
				new XAttribute("fill", ColorTranslator.ToHtml(color)),
				new XAttribute("fill-rule", pathFill),
				new XAttribute("fill-opacity", (color.A / 255.0f).ChangeType<string>()),
				new XAttribute("d", path)
			);
		} // func GetSvgPathElement

		private static void DrawSvgPath(Graphics g, Brush brush, string path, string pathFill)
		{
			var graphicsPath = new GraphicsPath
			{
				FillMode = pathFill == "nonzero" ? FillMode.Winding : FillMode.Alternate
			};
			
			foreach (var seg in SvgPathBuilder.Parse(path))
				seg.AddToPath(graphicsPath);

			g.FillPath(brush, graphicsPath);
		} // func DrawSvgPath

		private bool TryGetSvgPath(IDEWebRequestScope r, GeometryInfo geometry, out string path, out string pathFill)
		{
			if (!geometry.TryGetPath(out path, out pathFill))
			{
				r.SetStatus(HttpStatusCode.BadRequest, "Invalid geometry.");
				return false;
			}

			r.SetLastModified(Server.Configuration.ConfigurationStamp);

			if (r.InputMethod == HttpMethod.Head.Method)
			{
				r.SetStatus(HttpStatusCode.OK, "Ok");
				return false;
			}
			else
				return true;
		} // func TryGetSvgPath

		private static void GetImagePropertiesFromRequest(IDEWebRequestScope r, out int width, out int height, out Color color, out Color color2)
		{
			width = r.GetProperty("w", 24);
			height = r.GetProperty("h", 24);

			var opacity = r.GetProperty("fo", 1.0f);

			color = PpsStuff.ChangeColorOpacity(PpsStuff.ParseColor(r.GetProperty("f", null), Color.Black), Convert.ToInt32(opacity * 255));
			color2 = PpsStuff.ChangeColorOpacity(PpsStuff.ParseColor(r.GetProperty("f2", null), color), Convert.ToInt32(opacity * 255));
		} // func GetImagePropertiesFromRequest

		private async Task<bool> ProcessSingleGeometryAsSvgAsync(IDEWebRequestScope r, GeometryInfo geometry)
		{
			if (TryGetSvgPath(r, geometry, out var path, out var pathFill))
			{
				GetImagePropertiesFromRequest(r, out var width, out var height, out var color, out var color2);

				var svg = new XDocument(
					new XDocumentType("svg", "-//W3C//DTD SVG 1.1//EN", "http://www.w3.org/Graphics/SVG/1.1/DTD/svg11.dtd", null),
					new XElement(xSvg,
						new XAttribute("version", "1.1"),
						new XAttribute("width", width),
						new XAttribute("height", height),
						new XAttribute("viewBox", geometry.ViewPort.Replace(',', ' ')),
						GetSvgPathElement(color, pathFill, path),
						geometry.TryGetPath2(out var path2, out var pathFill2)
							? GetSvgPathElement(color2, pathFill2, path2)
							: null
					)
				);

				await r.WriteXmlAsync(svg, MimeTypes.Image.Svg);
			}

			return true;
		} // func ProcessSingleGeometryAsSvgAsync

		private async Task<bool> ProcessSingleGeometryAsImageAsync(IDEWebRequestScope r, GeometryInfo geometry, string mimeType, ImageFormat imageFormat )
		{
			if (TryGetSvgPath(r, geometry, out var pathData, out var pathFill))
			{
				await Task.Run(() =>
					{
						GetImagePropertiesFromRequest(r, out var width, out var height, out var color, out var color2);

						using (var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb))
						{
							// render image
							using (var g = Graphics.FromImage(bmp))
							{
								g.Clear(Color.Transparent);

								// set scale matrix
								if (!geometry.IsDefaultViewPort || width != 24 || height != 24)
								{
									if (PpsStuff.TryParseRectangle(geometry.ViewPort, out var viewPort))
									{
										var xOffset = 0.0f;
										var yOffset = 0.0f;
										var xScale = width / viewPort.Width;
										var yScale = height / viewPort.Height;
										if (xScale < yScale) // width smaller
										{
											yScale = xScale;
											yOffset = (height - viewPort.Height) / 2;
										}
										else if (xScale > yScale)
										{
											xScale = yScale;
											xOffset = (width - viewPort.Width) / 2;
										}

										// move to center
										g.TranslateTransform(xOffset, yOffset);
										// scale image
										g.ScaleTransform(xScale, yScale);
									}
									else
										throw new ArgumentException("Invalid viewport");
								}

								// draw paths
								using (var br = new SolidBrush(color))
									DrawSvgPath(g, br, pathData, pathFill);

								if (geometry.TryGetPath2(out var path2, out var pathFill2))
								{
									using (var br2 = new SolidBrush(color2))
										DrawSvgPath(g, br2, path2, pathFill2);
								}
							}
							bmp.Save(r.GetOutputStream(mimeType), imageFormat);
						}
					}
				);
			}

			return true;
		} // proc ProcessSingleGeometryAsImageAsync

		private Task<bool> ProcessSingleGeometryAsync(IDEWebRequestScope r, GeometryInfo geometry)
		{
			var outputType = GetImageOutputByArgument(r.GetProperty("t", null), GetImageOutputByMimeType(r.AcceptedTypes?.FirstOrDefault(), ImageOuput.Svg));
			switch(outputType)
			{
				case ImageOuput.Svg:
					return ProcessSingleGeometryAsSvgAsync(r, geometry);
				case ImageOuput.Png:
					return ProcessSingleGeometryAsImageAsync(r, geometry, MimeTypes.Image.Png, ImageFormat.Png);
				case ImageOuput.Bmp:
					return ProcessSingleGeometryAsImageAsync(r, geometry, MimeTypes.Image.Jpeg, ImageFormat.Jpeg);
				case ImageOuput.Jpeg:
					return ProcessSingleGeometryAsImageAsync(r, geometry, MimeTypes.Image.Bmp, ImageFormat.Bmp);
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

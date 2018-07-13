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
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;
using Markdig;
using Markdig.Helpers;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace TecWare.PPSn.Reporting.Markdown
{
	internal class SpeeDataRenderer : RendererBase
	{
		#region -- class SpeeDataObjectRenderer ---------------------------------------

		private abstract class SpeeDataObjectRenderer<TObject> : MarkdownObjectRenderer<SpeeDataRenderer, TObject>
			where TObject : MarkdownObject
		{
		}

		#endregion

		#region -- class ThematicBreakRenderer ----------------------------------------

		private sealed class ThematicBreakRenderer : SpeeDataObjectRenderer<ThematicBreakBlock>
		{
			protected override void Write(SpeeDataRenderer renderer, ThematicBreakBlock obj)
			{
				renderer.WriteStartElement("Paragraph");
				renderer.WriteEndElement();
			}
		} // class ThematicBreakRenderer

		#endregion

		#region -- class QuoteBlockRenderer -------------------------------------------

		private sealed class QuoteBlockRenderer : SpeeDataObjectRenderer<QuoteBlock>
		{
			protected override void Write(SpeeDataRenderer renderer, QuoteBlock block)
			{
				renderer.WriteStartParagraph();
				renderer.WriteItems(block);
				renderer.WriteEndParagraph();
			}
		} // class QuoteBlockRenderer

		#endregion

		#region -- class ParagraphRenderer --------------------------------------------

		private sealed class ParagraphRenderer : SpeeDataObjectRenderer<ParagraphBlock>
		{
			protected override void Write(SpeeDataRenderer renderer, ParagraphBlock paragraph)
			{
				renderer.WriteStartParagraph();
				renderer.WriteItems(paragraph);
				renderer.WriteEndParagraph();
			}
		} // class ParagraphRenderer

		#endregion

		#region -- class CodeBlockRenderer --------------------------------------------

		private sealed class CodeBlockRenderer : SpeeDataObjectRenderer<CodeBlock>
		{
			protected override void Write(SpeeDataRenderer renderer, CodeBlock obj)
			{
				renderer.WriteStartParagraph();
				//if (obj is FencedCodeBlock f)
				//    f.Info;
				renderer.WriteItems(obj, true);
				renderer.WriteEndParagraph();
			}
		} // class CodeBlockRendere

		#endregion

		#region -- class HeadingRenderer ----------------------------------------------

		private sealed class HeadingRenderer : SpeeDataObjectRenderer<HeadingBlock>
		{
			protected override void Write(SpeeDataRenderer renderer, HeadingBlock headingBlock)
			{
				renderer.WriteStartParagraph();
				renderer.WriteStartElement("Fontface");
				renderer.WriteAttribute("fontfamily", headingBlock.Level <=1 ? "head1" : "head2");
				renderer.WriteStartElement("B");
				renderer.WriteItems(headingBlock);
				renderer.WriteEndElement();
				renderer.WriteEndElement();
				renderer.WriteEndParagraph();
			}
		} // class HeadingRenderer

		#endregion

		#region -- class ListRenderer -------------------------------------------------

		private sealed class ListRenderer : SpeeDataObjectRenderer<ListBlock>
		{
			protected override void Write(SpeeDataRenderer renderer, ListBlock listBlock)
			{
				if (listBlock.IsOrdered)
				{
					renderer.WriteStartElement("OL");

					//if (listBlock.OrderedStart != null && (listBlock.DefaultOrderedStart != listBlock.OrderedStart))
					//	renderer.WriteMember(List.StartIndexProperty, listBlock.OrderedStart);
				}
				else
					renderer.WriteStartElement("UL");


				foreach (var cur in listBlock)
				{
					renderer.WriteStartElement("LI");
					renderer.WriteItems((ContainerBlock)cur);
					renderer.WriteEndElement();
				}

				renderer.WriteEndElement();
			}
		} // class ListRenderer

		#endregion

		#region -- class LiteralInlineRenderer ----------------------------------------

		private sealed class LiteralInlineRenderer : SpeeDataObjectRenderer<LiteralInline>
		{
			protected override void Write(SpeeDataRenderer renderer, LiteralInline obj)
			{
				if (obj.Content.IsEmpty)
					return;

				renderer.WriteText(ref obj.Content);
			}
		} // class LiteralInlineRenderer

		#endregion

		#region -- class LinkInlineRenderer -------------------------------------------

		private sealed class LinkInlineRenderer : SpeeDataObjectRenderer<LinkInline>
		{
			protected override void Write(SpeeDataRenderer renderer, LinkInline link)
			{
				var url = link.GetDynamicUrl != null ? link.GetDynamicUrl() ?? link.Url : link.Url;

				if (link.IsImage)
				{
					//if (!Uri.IsWellFormedUriString(url, UriKind.RelativeOrAbsolute))
					//	url = "#";

					//renderer.WriteStartObject(typeof(Image));
					//renderer.WriteStaticResourceMember(null, "markdig:Styles.ImageStyleKey");
					//if (!String.IsNullOrEmpty(link.Title))
					//	renderer.WriteMember(ToolTipService.ToolTipProperty, link.Title);
					//renderer.WriteMember(Image.SourceProperty, new Uri(url, UriKind.RelativeOrAbsolute));
					//renderer.WriteEndObject();
				}
				else
				{
					renderer.WriteText(link.Title);
				}
			}
		} // class LinkInlineRenderer

		#endregion

		#region -- class LineBreakInlineRenderer --------------------------------------

		private sealed class LineBreakInlineRenderer : SpeeDataObjectRenderer<LineBreakInline>
		{
			protected override void Write(SpeeDataRenderer renderer, LineBreakInline obj)
			{
				if (obj.IsHard)
					renderer.WriteLineBreak();
				else // Soft line break.
					renderer.WriteText(" ");
			}
		} // class LineBreakInlineRenderer

		#endregion

		#region -- class EmphasisInlineRenderer ---------------------------------------

		private sealed class EmphasisInlineRenderer : SpeeDataObjectRenderer<EmphasisInline>
		{
			private static bool WriteSpan(SpeeDataRenderer renderer, EmphasisInline span)
			{
				// Links:
				// - https://github.com/lunet-io/markdig/blob/master/src/Markdig.Tests/Specs/EmphasisExtraSpecs.md
				// - http://commonmark.org/help/
				switch (span.DelimiterChar)
				{
					case '*':
					case '_':
						renderer.WriteStartElement(span.IsDouble ? "B" : "I");
						return true;
					case '~':
						if (span.IsDouble)
							return false; // StrikeThrough -> Durchgestrichen
						else
						{
							renderer.WriteStartElement("Sub");
							return true;
						}
					case '^':
						if (span.IsDouble)
							return false; // free
						else
						{
							renderer.WriteStartElement("Sup"); // Superscript -> Hochgestellt
							return true;
						}
					case '+':
						if (span.IsDouble)
						{
							renderer.WriteStartElement("U"); // Underlined -> Unterstrichen
							return true;
						}
						else
							return false; // free
					case '=': // Marked
						renderer.WriteStartElement("Color");
						renderer.WriteAttribute("name", "marked");
						return true;
					default:
						return false;
				}
			} // proc WriteSpan

			protected override void Write(SpeeDataRenderer renderer, EmphasisInline span)
			{
				if (WriteSpan(renderer, span))
				{
					renderer.WriteItems(span);
					renderer.WriteEndElement();
				}
				else
					renderer.WriteChildren(span);
			} // proc Write
		} // class EmphasisInlineRenderer

		#endregion

		#region -- class DelimiterInlineRenderer --------------------------------------

		private sealed class DelimiterInlineRenderer : SpeeDataObjectRenderer<DelimiterInline>
		{
			protected override void Write(SpeeDataRenderer renderer, DelimiterInline obj)
			{
				renderer.WriteText(obj.ToLiteral());
				renderer.WriteChildren(obj);
			}
		} // class DelimiterInlineRenderer

		#endregion

		#region -- class CodeInlineRenderer -------------------------------------------

		private sealed class CodeInlineRenderer : SpeeDataObjectRenderer<CodeInline>
		{
			protected override void Write(SpeeDataRenderer renderer, CodeInline code)
			{
				renderer.WriteStartElement("Span");
				renderer.WriteText(code.Content);
				renderer.WriteEndElement();
			}
		} // class CodeInlineRenderer

		#endregion

		#region -- class AutolinkInlineRenderer ---------------------------------------

		private sealed class AutolinkInlineRenderer : SpeeDataObjectRenderer<AutolinkInline>
		{
			protected override void Write(SpeeDataRenderer renderer, AutolinkInline link)
				=> renderer.WriteText(link.Url);
		} // class AutolinkInlineRenderer

		#endregion

		private readonly XmlWriter xml;

		private bool preserveWhitespace = false; // preserve current whitespaces
		private bool appendWhiteSpace = false;
		private bool firstCharOfBlock = true;
		private readonly StringBuilder textBuffer = new StringBuilder(); // current text buffer to collect all words

		#region -- Ctor/Dtor ----------------------------------------------------------

		public SpeeDataRenderer(XmlWriter xml)
		{
			this.xml = xml;

			// Block renderes
			ObjectRenderers.Add(new ListRenderer());
			ObjectRenderers.Add(new HeadingRenderer());
			ObjectRenderers.Add(new ParagraphRenderer());
			ObjectRenderers.Add(new QuoteBlockRenderer());
			ObjectRenderers.Add(new ThematicBreakRenderer());

			// Inline renderers
			ObjectRenderers.Add(new AutolinkInlineRenderer());
			ObjectRenderers.Add(new CodeInlineRenderer());
			ObjectRenderers.Add(new DelimiterInlineRenderer());
			ObjectRenderers.Add(new EmphasisInlineRenderer());
			ObjectRenderers.Add(new LineBreakInlineRenderer());
			ObjectRenderers.Add(new LinkInlineRenderer());
			ObjectRenderers.Add(new LiteralInlineRenderer());
		} // ctor

		#endregion

		public override object Render(MarkdownObject markdownObject)
		{
			Write(markdownObject);
			return xml;
		} // func Renderer

		#region -- Primitives ---------------------------------------------------------

		public void WriteStartParagraph()
		{
			WriteStartElement("Paragraph");
		} // proc WriteStartParagraph

		public void WriteEndParagraph()
		{
			WriteEndElement();
		} // proc WriteEndParagraph

		public void WriteStartElement(string elementName)
		{
			WritePendingText(true);
			xml.WriteStartElement(elementName);
		} // proc WriteStartElement

		public void WriteAttribute(string name, string value)
			=> xml.WriteAttributeString(name, value);

		public void WriteEndElement()
			=> xml.WriteEndElement();

		public void WriteStartText(bool preserveSpaces = false)
		{
			preserveWhitespace = preserveSpaces;
			appendWhiteSpace = false;
			firstCharOfBlock = true;
		}

		public void WriteEndText()
			=> WritePendingText(false);
		
		public void WriteLineBreak()
		{
			WriteStartElement("BR");
			WriteEndElement();
		} // proc WriteLineBreak

		public void WriteValue(string value)
			=> WriteText(value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void AppendChar(char c)
		{
			if (Char.IsWhiteSpace(c))
				appendWhiteSpace = true;
			else
			{
				if (appendWhiteSpace)
				{
					if (!firstCharOfBlock)
						textBuffer.Append(' ');
					appendWhiteSpace = false;
				}

				firstCharOfBlock = false;
				textBuffer.Append(c);
			}
		} // proc AppendChar

		/// <summary>Write normal text.</summary>
		/// <param name="slice"></param>
		public void WriteText(ref StringSlice slice)
		{
			if (slice.Start > slice.End)
				return;

			if (preserveWhitespace)
				textBuffer.Append(slice.Text, slice.Start, slice.Length);
			else
			{
				for (var i = slice.Start; i <= slice.End; i++)
				{
					var c = slice[i];
					AppendChar(c);
				}
			}
		} // proc WriteText

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal void WriteText(string text)
		{
			if (preserveWhitespace)
				textBuffer.Append(text);
			else
			{
				var l = text.Length;
				for (var i = 0; i < l; i++)
					AppendChar(text[i]);
			}
		} // proc WriteText

		private void WritePendingText(bool onStartObject)
		{
			if (IsPendingText)
			{
				if (preserveWhitespace)
				{
					var t = textBuffer.ToString();
					xml.WriteStartElement("Value");
					xml.WriteValue(textBuffer.ToString());
					xml.WriteEndElement();
					textBuffer.Length = 0;
				}
				else
				{
					if (appendWhiteSpace && onStartObject)
					{
						textBuffer.Append(' ');
						appendWhiteSpace = false;
					}

					xml.WriteStartElement("Value");
					xml.WriteValue(textBuffer.ToString());
					xml.WriteEndElement();
					textBuffer.Length = 0;
				}
			}
		} // proc WritePendingText

		public void WriteItems(LeafBlock leafBlock, bool preserveSpaces = false)
		{
			if (leafBlock == null)
				throw new ArgumentNullException(nameof(leafBlock));

			WriteStartText(preserveSpaces);

			if (leafBlock.Inline != null)
			{
				WriteChildren(leafBlock.Inline);
			}
			else
			{
				var lineCount = leafBlock.Lines.Count;
				var first = true;
				for (var i = 0; i < lineCount; i++)
				{
					if (first)
						first = false;
					else if (preserveSpaces)
						WriteLineBreak();
					else
						AppendChar(' ');

					WriteText(ref leafBlock.Lines.Lines[i].Slice);
				}
			}

			WriteEndText();
		} // proc WriteItems

		public void WriteItems(ContainerInline inlines, bool preserveSpaces = false)
		{
			if (inlines == null)
				throw new ArgumentNullException(nameof(inlines));

			WriteStartText(preserveSpaces);
			WriteChildren(inlines);
			WriteEndText();
		} // proc WriteItems

		public void WriteItems(ContainerBlock block, bool preserveSpaces = false)
		{
			if (block == null)
				throw new ArgumentNullException(nameof(block));

			WriteStartText(preserveSpaces);
			WriteChildren(block);
			WriteEndText();
		} // proc WriteItems

		private bool IsPendingText => textBuffer.Length > 0 || appendWhiteSpace;

		#endregion

		private static MarkdownPipeline CreatePipeline()
		{
			return new MarkdownPipelineBuilder()
				.UseEmphasisExtras()
				.Build();
		} // func CreatePipeline

		public static void ToXml(string markdown, XmlWriter xml, MarkdownPipeline pipeline)
		{
			if (markdown == null)
				return;
			if (xml == null)
				throw new ArgumentNullException(nameof(xml));

			pipeline = pipeline ?? DefaultPipeLine;

			var renderer = new SpeeDataRenderer(xml);
			pipeline.Setup(renderer);
			renderer.Render(Markdig.Markdown.Parse(markdown, pipeline));
		} // proc ToXaml

		public static MarkdownPipeline DefaultPipeLine { get; } = CreatePipeline();
	} // class SpeeDataRenderer
}

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
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.Data
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsDataListItemDefinition : PpsEnvironmentDefinition
	{
		private static readonly XName xnTemplates = "templates";
		private static readonly XName xnTemplate = "template";

		#region -- class TemplateItem -----------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class TemplateItem : IComparable<TemplateItem>
		{
			private readonly int priority;
			private readonly Func<object, bool> condition; // condition if the template is for this item
			private readonly string onlineViewId;	// view, that returns extended data for the row
			private DataTemplate template;				// template of the row

			public TemplateItem(PpsDataListItemDefinition owner, XElement xCur, ParserContext parserContext, ref int priority)
			{
				var xCondition = xCur.Element(XName.Get("condition"));
				if (xCondition != null)
				{
					this.condition = owner.Environment.CompileLambdaAsync<Func<object,bool>>(xCondition, true, "Item").AwaitTask();
					xCondition.Remove();
				}
				else
					this.condition = null;
				this.template = owner.Environment.Dispatcher.Invoke(() => (DataTemplate)owner.Environment.CreateResource(xCur.Elements().First().ToString(), parserContext));

				this.priority = priority = xCur.GetAttribute("priority", priority + 1);
				this.onlineViewId = xCur.GetAttribute("viewId", String.Empty);
			} // ctor

			public int CompareTo(TemplateItem other)
				=> Priority - other.Priority;

			/// <summary>Runs the condition, if the template is accepted.</summary>
			/// <param name="item"></param>
			/// <returns></returns>
			public bool SelectTemplate(dynamic item)
			{
				if (condition == null)
					return template != null;
				else
					return condition(item);
			} // func SelectTemplate

			public string OnlineViewId => onlineViewId;
			public int Priority => priority;
			public DataTemplate Template => template;
		} // class TemplateItem

		#endregion

		private readonly List<TemplateItem> templates = new List<TemplateItem>();

		internal PpsDataListItemDefinition(PpsEnvironment environment, string key)
			: base(environment, key)
		{
		} // ctor

		/// <summary></summary>
		/// <param name="xCur"></param>
		public void AppendTemplate(XElement xCur, ParserContext parserContext)
		{
			var priority = templates.Count;
			var template = new TemplateItem(this, xCur, parserContext, ref priority);

			// insert the item in order of the priority
			var index = templates.BinarySearch(template);
			if (index < 0)
				templates.Insert(~index, template);
			else
				templates.Insert(index, template);
		} // proc AppendTemplate

		/// <summary></summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public DataTemplate FindTemplate(dynamic item)
			=> templates.FirstOrDefault(c => c.SelectTemplate(item))?.Template;
	} // class PpsDataListItemType
}

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
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.Stuff;
using TecWare.PPSn.UI;
using LExpression = System.Linq.Expressions.Expression;

namespace TecWare.PPSn
{
	#region -- class PpsDataTemplateDefinition ----------------------------------------

	/// <summary>Template container.</summary>
	public sealed class PpsDataTemplateDefinition : PpsEnvironmentDefinition
	{
		#region -- class TemplateSelectScope ------------------------------------------

		private class TemplateSelectScope : IDynamicMetaObjectProvider
		{
			#region -- class TemplateSelectScopeMetaObject ----------------------------

			/// <summary></summary>
			private sealed class TemplateSelectScopeMetaObject : DynamicMetaObject
			{
				public TemplateSelectScopeMetaObject(LExpression expression, object value)
					: base(expression, BindingRestrictions.Empty, value)
				{
				} // ctor

				private DynamicMetaObject GetRawItemBinder()
				{
					if (Value is TemplateSelectScope scope)
					{
						var rawItemExpression = LExpression.Property(LExpression.Convert(Expression, typeof(TemplateSelectScope)), nameof(RawItem));
						var restriction = BindingRestrictions.GetTypeRestriction(Expression, typeof(TemplateSelectScope));
						return new DynamicMetaObject(rawItemExpression, restriction, scope.RawItem);
					}
					else
						throw new NotSupportedException();
				} // func GetRawItemBinder

				public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
				{
					if (String.Compare(binder.Name, nameof(RawItem), binder.IgnoreCase) == 0
						|| String.Compare(binder.Name, nameof(Container), binder.IgnoreCase) == 0)
						base.BindGetMember(binder);

					if (!HasValue)
						return binder.Defer(this);

					// redirect to the item
					return binder.FallbackGetMember(GetRawItemBinder());
				} // func BindGetMember

				public override DynamicMetaObject BindInvoke(InvokeBinder binder, DynamicMetaObject[] args)
				{
					if (!HasValue)
						return binder.Defer(this, args);

					// redirect to the item
					return binder.FallbackInvoke(GetRawItemBinder(), args);
				} // func BindInvoke

				public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
				{
					if (String.Compare(binder.Name, nameof(IsSmall), binder.IgnoreCase) == 0
						|| String.Compare(binder.Name, nameof(IsLarge), binder.IgnoreCase) == 0)
						return base.BindInvokeMember(binder, args);

					if (!HasValue)
						return binder.Defer(this, args);

					// redirect to the item
					return binder.FallbackInvokeMember(GetRawItemBinder(), args);
				} // func BindInvokeMember

				public override DynamicMetaObject BindGetIndex(GetIndexBinder binder, DynamicMetaObject[] indexes)
				{
					if (!HasValue)
						return binder.Defer(this, indexes);

					// redirect to the item
					return binder.FallbackGetIndex(GetRawItemBinder(), indexes);
				} // func BindGetIndex
			} // class TemplateSelectScopeMetaObject

			#endregion

			private readonly object item;
			private readonly DependencyObject container;

			public TemplateSelectScope(object item, DependencyObject container)
			{
				this.item = item ?? throw new ArgumentNullException(nameof(item));
				this.container = container ?? throw new ArgumentNullException(nameof(container));
			} // proc TemplateSelectScope

			DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(LExpression parameter)
				=> new TemplateSelectScopeMetaObject(parameter, this);

			public bool IsSmall()
				=> false;

			public bool IsLarge()
				=> true;

			public object Container => container;
			public object RawItem => item;
		} // class TemplateSelectScope

		#endregion

		#region -- class TemplateItem -------------------------------------------------

		private sealed class TemplateItem : IComparable<TemplateItem>
		{
			private readonly DataTemplate template;         // template of the row
			private readonly Func<object, bool> condition;  // condition if the template is for this item
			private readonly int priority;                  // select order

			public TemplateItem(int priority, Func<object, bool> condition, DataTemplate template)
			{
				this.priority = priority;
				this.condition = condition;
				this.template = template ?? throw new ArgumentNullException(nameof(template));
			} // ctor

			public int CompareTo(TemplateItem other)
				=> Priority - other.Priority;

			public bool SelectTemplate(TemplateSelectScope scope)
				=> condition?.Invoke(scope) ?? true;

			public int Priority => priority;
			public DataTemplate Template => template;
		} // class TemplateItem

		#endregion

		#region -- class DefaultDataTemplateSelector ----------------------------------

		private sealed class DefaultDataTemplateSelector : DataTemplateSelector
		{
			private readonly PpsDataTemplateDefinition templateDefinition;

			public DefaultDataTemplateSelector(PpsDataTemplateDefinition templateDefinition)
			{
				this.templateDefinition = templateDefinition ?? throw new ArgumentNullException(nameof(templateDefinition));
			}

			public override DataTemplate SelectTemplate(object item, DependencyObject container)
				=> templateDefinition.FindTemplate(item, container);
		} // class DefaultDataTemplateSelector

		#endregion

		#region -- class TemplateCode -------------------------------------------------

		private sealed class TemplateCode : LuaShellTable, IPpsXamlCode
		{
			public TemplateCode(PpsShellWpf shell) 
				: base(shell)
			{
			} // ctor

			#region -- IPpsXamlCode members -----------------------------------------------

			void IPpsXamlCode.CompileCode(Uri uri, string code)
				=> ((PpsShellWpf)Shell).CompileCodeForXaml(Shell, uri, code);
			
			#endregion
		} // class TemplateCode

		#endregion

		private readonly DataTemplateSelector templateSelector;
		private readonly TemplateCode templateCode;
		private readonly List<TemplateItem> templates = new List<TemplateItem>();

		internal PpsDataTemplateDefinition(PpsEnvironment environment, string key)
			: base(environment, key)
		{
			templateSelector = new DefaultDataTemplateSelector(this);
			templateCode = new TemplateCode(environment);
		} // ctor

		private async Task<Func<object, bool>> ReadConditionAsync(XmlReader xml)
		{
			var condition = await Environment.CompileLambdaAsync<Func<object, bool>>(xml, true, "Item");
			await xml.ReadEndElementAsync();
			return condition;
		} // func ReadConditionAsync

		/// <summary></summary>
		/// <param name="xml"></param>
		/// <param name="priority"></param>
		/// <returns></returns>
		public async Task<int> AppendTemplateAsync(XmlReader xml, int priority)
		{
			// get base attributes
			priority = xml.GetAttribute("priority", priority + 1);

			// read start element
			await xml.ReadAsync();

			// read optional condition
			var condition =
				await xml.ReadOptionalStartElementAsync(StuffUI.xnCondition)
					? await ReadConditionAsync(xml)
					: null;

			// read template
			var template = await PpsXamlParser.LoadAsync<DataTemplate>(xml.ReadElementAsSubTree(), new PpsXamlReaderSettings { ServiceProvider = Environment, Code = templateCode });

			var templateItem = new TemplateItem(priority, condition, template);

			// insert the item in order of the priority
			var index = templates.BinarySearch(templateItem);
			if (index < 0)
				templates.Insert(~index, templateItem);
			else
				templates.Insert(index, templateItem);

			return priority;
		} // proc AppendTemplate

		/// <summary>Find template for the specific item.</summary>
		/// <param name="item">Data item</param>
		/// <param name="container">Container for the data item</param>
		/// <returns></returns>
		public DataTemplate FindTemplate(object item, DependencyObject container)
		{
			var scope = new TemplateSelectScope(item, container);
			return templates.FirstOrDefault(c => c.SelectTemplate(scope))?.Template;
		} // func FindTemplate

		/// <summary>Returns the code fragment for the template.</summary>
		public LuaTable Code => templateCode;
		/// <summary>Return the template selector for this template class.</summary>
		public DataTemplateSelector Selector => templateSelector;
	} // class PpsDataListItemDefinition

	#endregion

	#region -- class PpsEnvironment ---------------------------------------------------

	public partial class PpsEnvironment : IPpsXamlCode
	{
		private async Task<Tuple<XDocument, DateTime>> GetXmlDocumentAsync(string path, bool isXaml, bool isOptional)
		{
			try
			{
				var acceptedMimeType = isXaml ? MimeTypes.Application.Xaml : MimeTypes.Text.Xml;
				using (var r = await Request.GetResponseAsync(path, acceptedMimeType))
				using (var xml = await r.GetXmlStreamAsync(acceptedMimeType))
				{
					var dt = DateTime.MinValue;
					return new Tuple<XDocument, DateTime>(await Task.Run(() => XDocument.Load(xml)), dt);
				}
			}
			catch (WebException e)
			{
				if (e.Status == WebExceptionStatus.ProtocolError && isOptional)
					return null;
				throw;
			}
		} // func GetXmlDocumentAsync

		private async Task<XmlReader> OpenXmlDocumentAsync(string path, bool isXaml, bool isOptional)
		{
			try
			{
				var acceptedMimeType = isXaml ? MimeTypes.Application.Xaml : MimeTypes.Text.Xml;
				var r = await Request.GetResponseAsync(path, acceptedMimeType);
				return await r.GetXmlStreamAsync(acceptedMimeType,
					new XmlReaderSettings()
					{
						Async = true,
						IgnoreComments = !isXaml,
						IgnoreWhitespace = !isXaml
					}
				);
			}
			catch (WebException e)
			{
				if (e.Status == WebExceptionStatus.ProtocolError && isOptional)
					return null;
				throw;
			}
		} // func OpenXmlDocumentAsync

		private async Task RefreshDefaultResourcesAsync()
		{
			// update the resources, load a server site resource dictionary
			using (var xml = PpsXmlPosition.CreateLinePositionReader(await OpenXmlDocumentAsync("wpf/styles.xaml", true, true)))
			{
				if (xml == null)
					return; // no styles found

				// move to "theme"
				await xml.ReadStartElementAsync(StuffUI.xnTheme);

				// parse resources
				await xml.ReadStartElementAsync(StuffUI.xnResources);

				await UpdateResourcesAsync(xml);

				await xml.ReadEndElementAsync(); // resource
				await xml.ReadEndElementAsync(); // theme
			}
		} // proc RefreshDefaultResourcesAsync

		private async Task RefreshTemplatesAsync()
		{
			var priority = 1;

			using (var xml = PpsXmlPosition.CreateLinePositionReader(await OpenXmlDocumentAsync("wpf/templates.xaml", true, true)))
			{
				if (xml == null)
					return; // no templates found

				// read root
				await xml.ReadStartElementAsync(StuffUI.xnTemplates);

				while (xml.NodeType != XmlNodeType.EndElement)
				{
					if (xml.NodeType == XmlNodeType.Element)
					{
						if (xml.IsName(StuffUI.xnResources))
						{
							if (!await xml.ReadAsync())
								break; // fetch element

							// check for a global resource dictionary, and update the main resources
							await UpdateResourcesAsync(xml);

							await xml.ReadEndElementAsync(); // resource
						}
						else if (xml.IsName(StuffUI.xnTemplate))
						{
							var key = xml.GetAttribute("key", String.Empty);
							if (String.IsNullOrEmpty(key))
							{
								xml.Skip();
								break;
							}

							var templateDefinition = templateDefinitions[key];
							if (templateDefinition == null)
							{
								templateDefinition = new PpsDataTemplateDefinition(this, key);
								templateDefinitions.AppendItem(templateDefinition);
							}
							priority = await templateDefinition.AppendTemplateAsync(xml, priority);
							await xml.ReadEndElementAsync();

							await xml.SkipAsync();
						}
						else
							await xml.SkipAsync();
					}
					else
						await xml.ReadAsync();
				}

				await xml.ReadEndElementAsync(); // templates
			} // using xml

			// remove unused templates
			// todo:
		} // proc RefreshTemplatesAsync

		private async Task UpdateResourcesAsync(XmlReader xml)
		{
			while (await xml.MoveToContentAsync() != XmlNodeType.EndElement)
				await UpdateResourceAsync(xml);
		} // proc UpdateResources

		#region -- UI - Helper --------------------------------------------------------

		///// <summary>Show Trace as window.</summary>
		///// <param name="owner"></param>
		//public Task ShowTraceAsync(Window owner)
		//	=> OpenPaneAsync(typeof(PpsTracePane), PpsOpenPaneMode.NewSingleDialog, new LuaTable() { ["DialogOwner"] = owner });

		///// <summary>Display the exception dialog.</summary>
		///// <param name="flags"></param>
		///// <param name="exception"></param>
		///// <param name="alternativeMessage"></param>
		//public void ShowException(PpsExceptionShowFlags flags, Exception exception, string alternativeMessage = null)
		//{
		//	var exceptionToShow = exception.UnpackException();

		//	// always add the exception to the list
		//	Log.Append(PpsLogType.Exception, exception, alternativeMessage ?? exceptionToShow.Message);

		//	// show the exception if it is not marked as background
		//	if ((flags & PpsExceptionShowFlags.Background) != PpsExceptionShowFlags.Background
		//		&& Application.Current != null)
		//	{
		//		var dialogOwner = Application.Current.Windows.OfType<Window>().FirstOrDefault(c => c.IsActive);
		//		if (ShowExceptionDialog(dialogOwner, flags, exceptionToShow, alternativeMessage)) // should follow a detailed dialog
		//			ShowTraceAsync(dialogOwner).AwaitTask();

		//		if ((flags & PpsExceptionShowFlags.Shutown) != 0) // close application
		//			Application.Current.Shutdown(1);
		//	}
		//} // proc ShowException

		/// <summary>Display the exception dialog.</summary>
		/// <param name="dialogOwner"></param>
		/// <param name="flags"></param>
		/// <param name="exception"></param>
		/// <param name="alternativeMessage"></param>
		/// <returns></returns>
		public bool ShowExceptionDialog(Window dialogOwner, PpsExceptionShowFlags flags, Exception exception, string alternativeMessage)
		{
			switch (exception)
			{
				case PpsEnvironmentOnlineFailedException ex:
					return MsgBox(ex.Message, MessageBoxButton.OK, MessageBoxImage.Information) != MessageBoxResult.OK;
				case ILuaUserRuntimeException urex:
					return MsgBox(urex.Message, MessageBoxButton.OK, MessageBoxImage.Information) != MessageBoxResult.OK;
				default:
					var shutDown = (flags & PpsExceptionShowFlags.Shutown) != 0;

					if (!shutDown && alternativeMessage != null)
					{
						MsgBox(alternativeMessage, MessageBoxButton.OK, MessageBoxImage.Information);
						return false;
					}
					else
					{
						MsgBox(alternativeMessage ?? exception.Message, MessageBoxButton.OK, MessageBoxImage.Error);
						return false;
						//var dialog = new PpsMessageDialog
						//{
						//	MessageType = shutDown ? PpsTraceItemType.Fail : PpsTraceItemType.Exception,
						//	MessageText = alternativeMessage ?? exception.Message,
						//	SkipVisible = !shutDown,

						//	Owner = dialogOwner
						//};
						//return dialog.ShowDialog() ?? false; // show the dialog
					}
			}
		} // func ShowExceptionDialog

		/// <summary></summary>
		/// <param name="paneType"></param>
		/// <returns></returns>
		public override Type GetPaneTypeFromString(string paneType)
		{
			switch (paneType)
			{
				case "mask":
					return typeof(PpsGenericMaskWindowPane);
				default:
					return base.GetPaneTypeFromString(paneType);
			}
		} // func GetPaneTypeFromString

		#endregion

		#region -- GetDataTemplate ----------------------------------------------------

		/// <summary>Get template for the specific object.</summary>
		/// <param name="data"></param>
		/// <param name="container"></param>
		/// <returns></returns>
		public override DataTemplate GetDataTemplate(object data, DependencyObject container)
		{
			string key = null;

			if (data is LuaTable t) // get type property from table as key
				key = t.GetMemberValue("Typ") as string;
			else if (data is PpsObject o) // is 
				key = o.Typ;
			else if (data is PpsMasterDataRow r)
				key = "Master." + r.Table.Definition.Name;
			else if (data is string k)
				key = k;

			if (key == null)
				return null;

			var templInfo = templateDefinitions[key];
			return templInfo?.FindTemplate(data, container);
		} // func GetDataTemplate

		#endregion
	} // class PpsEnvironment

	#endregion
}

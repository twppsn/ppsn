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
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.IronLua;
using TecWare.PPSn.UI;

namespace PPSnTests
{
	[TestClass]
	public class LuaUITests
	{
		private T CreateWpfType<T>(string snippet)
			where T : class
		{
			using (var lua = new Lua())
			{
				var g= lua.CreateEnvironment();
				return (T)g.DoChunk(snippet, "wpf.lua", new KeyValuePair<string, object>("UI", new LuaUI()))[0];
			}
		} // func CreateWpfType

		private string Lines(params string[] lines)
			=> String.Join(Environment.NewLine, lines);

		[TestMethod]
		public void UIMemberSet()
		{
			var l = CreateWpfType<Label>("return UI.Label { Content = 'Hello' };");
			Assert.AreEqual("Hello", l.Content);
		}

		[TestMethod]
		public void UIMemberSetMarkup()
		{
			var l = CreateWpfType<Label>("return UI.Label { Content = UI.Binding { Path = 'Test' } };");

			var b = l.GetBindingExpression(Label.ContentProperty);
			Assert.AreEqual("Test", b.ParentBinding.Path.Path);
		}

		[TestMethod]
		public void UIConstructWithArgument()
		{
			var b = CreateWpfType<Binding>("return UI.Binding('Test');");
			Assert.AreEqual("Test", b.Path.Path);
		}

		[TestMethod]
		public void UIDefaultMember()
		{
			var l = CreateWpfType<Label>("return UI.Label { 'Hello' };");
			Assert.AreEqual("Hello", l.Content);
		}

		[TestMethod]
		public void UIChildrenMember()
		{
			//TextBlock
			var p = CreateWpfType<StackPanel>("return UI.StackPanel { UI.Label { 'L1' }, UI.Label { 'L2' } };");
			Assert.AreEqual(2, p.Children.Count);
		}

		[TestMethod]
		public void UIChildrenMember02()
		{
			//TextBlock
			var t = CreateWpfType<TextBlock>("return UI.TextBlock { 'L1', UI.Run { 'L2' } };");
			Assert.AreEqual(2, t.Inlines.Count);
		}

	}
}

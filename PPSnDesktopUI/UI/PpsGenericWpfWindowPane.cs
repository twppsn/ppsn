using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using System.Xml.Linq;
using Neo.IronLua;
using System.Windows.Controls;

namespace TecWare.PPSn.UI
{
    ///////////////////////////////////////////////////////////////////////////////
    /// <summary>Inhalt, welche aus einem Xaml und einem Lua-Script besteht.</summary>
    public class PpsGenericWpfWindowPane : LuaTable, IPpsWindowPane
    {
        private static readonly XName xnCode = XName.Get("Code", "http://schemas.microsoft.com/winfx/2006/xaml");
        private string sXamlFile;
        private FrameworkElement control;

        private Lua lua = new Lua(); // todo: muss von außen kommen

        #region -- Ctor/Dtor --------------------------------------------------------------

        public PpsGenericWpfWindowPane(string sXamlFile)
        {
            this.sXamlFile = sXamlFile;
        } // ctor

        ~PpsGenericWpfWindowPane()
        {
            Dispose(false);
        } // ctor

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
        } // proc Dispose

        protected virtual void Dispose(bool lDisposing)
        {
        } // proc Dispose

        #endregion

        public virtual async Task LoadAsync()
        {
            await Task.Yield();

            var xaml = XDocument.Load(sXamlFile, LoadOptions.SetBaseUri | LoadOptions.SetLineInfo);

            // Lade den Inhalt Code zur Initialisierung des
            var xCode = xaml.Root.Element(xnCode);
            if (xCode != null)
            {
                var chunk = lua.CompileChunk(xCode.Value, Path.GetFileName(sXamlFile), null);

                // Führe den Code im UI-Thread aus
                Dispatcher.Invoke(() => chunk.Run(this));

                xCode.Remove();
            }

            // Parse das Xaml
            var xamlReader = new XamlReader();
            await Dispatcher.InvokeAsync(() =>
                {
                    control = xamlReader.LoadAsync(xaml.CreateReader()) as FrameworkElement;
                    control.DataContext = this;

                    control.Loaded += new RoutedEventHandler(PaneLoaded);
                });
        } // proc LoadAsync

        public Task<bool> UnloadAsync()
        {
            return Task.FromResult<bool>(true);
        } // func UnloadAsync

        public virtual string Title { get { return Path.GetFileName(sXamlFile); } }

        protected FrameworkElement Control { get { return control; } }
        protected string XamlFileName { get { return sXamlFile; } }
        public string BaseUri { get { return Path.GetDirectoryName(sXamlFile); } }

        object IPpsWindowPane.Control { get { return control; } }

        public Dispatcher Dispatcher { get { return Application.Current.Dispatcher; } }
        public Lua Lua { get { return lua; } }

        protected virtual void PaneLoaded(object sender, RoutedEventArgs e)
        {
        }

    } // class PpsGenericWpfWindowPane
}

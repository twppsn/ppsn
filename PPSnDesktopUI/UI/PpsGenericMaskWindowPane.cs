using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Xml.Linq;
using System.Windows.Controls;

using Neo.IronLua;
using TecWare.PPSn.Data;
using System.Windows;

namespace TecWare.PPSn.UI
{
    ///////////////////////////////////////////////////////////////////////////////
    /// <summary>Inhalt, welcher aus einem dynamisch geladenen Xaml besteht
    /// und einer Dataset</summary>
    public class PpsGenericMaskWindowPane : PpsGenericWpfWindowPane
    {
        private class LuaCommandImplementaion : ICommand
        {
            private Action command;

            public LuaCommandImplementaion(Action command)
            {
                this.command = command;
            } // ctor

            public bool CanExecute(object parameter)
            {
                return true;
            }

            public event EventHandler CanExecuteChanged;

            public void Execute(object parameter)
            {
                command();
            }
        } // class LuaCommandImplementaion

        private PpsDataSet dataSet;

        public PpsGenericMaskWindowPane(string sXamlFile)
            : base(sXamlFile)
        {
        } // ctor

        public override async Task LoadAsync()
        {
            await Task.Yield();

            try
            {
                // Lade die Definition
                string sSchema = Path.ChangeExtension(XamlFileName, ".sxml");
                var def = new PpsDataSetDefinitionClient(XDocument.Load(sSchema).Root);
                dataSet = def.CreateDataSet();

                string sData = Path.ChangeExtension(XamlFileName, ".dxml");
                dataSet.Read(XDocument.Load(sData).Root);

                #region install undo logic

                // fetch all data rows
                VisitorCollectObjectsOfType<PpsDataRow> visitor = new VisitorCollectObjectsOfType<PpsDataRow>();
                dataSet.Accept(visitor);

                // inject undo method
                foreach (PpsDataRow row in visitor.CollectedObjects)
                {
                    row.Current.actionUndo = ((PpsDataSetClient)dataSet).UndoRedo.AddItem;
                }

                #endregion
            }
            catch (Exception ex)
            {
                string stackTrace = ex.StackTrace; //~todo: show message box and/or write to log file

                throw ex; // stop execution
            }

            // Lade die Maske
            await base.LoadAsync();

            await Dispatcher.InvokeAsync(() => OnPropertyChanged("Data"));
        } // proc LoadAsync

        [LuaMember("print")]
        private void LuaPrint(string sText)
        {
            System.Windows.MessageBox.Show(sText);
        } // proc LuaPrint

        [LuaMember("command")]
        private object LuaCommand(Action command)
        {
            return new LuaCommandImplementaion(command);
        } // func LuaCommand

        [LuaMember("require")]
        private void LuaRequire(string sFileName)
        {
            Lua.CompileChunk(Path.Combine(BaseUri, sFileName), null).Run(this);
        } // proc LuaRequire

        [LuaMember("Data")]
        public PpsDataSet Data { get { return dataSet; } }

        public override string Title
        {
            get
            {
                if (dataSet == null)
                    return "Maske wird geladen...";
                else
                    return ((dynamic)dataSet).Caption;
            }
        } // prop Title

        protected override void PaneLoaded(object sender, RoutedEventArgs e)
        {
            AddStandardControls();
        }

        /// <summary>
        /// create, wire and show all standard controls in this window
        /// </summary>
        System.Windows.Controls.Button undoButton;
        System.Windows.Controls.Button redoButton;
        System.Windows.Controls.ListBox listBox;

        void AddStandardControls()
        {
            Panel panel = (Panel)((FrameworkElement)Control).FindName("StackPanelUndo");

            StackPanel stackPanel = new StackPanel();
            //stackPanel.Orientation = Orientation.Horizontal;

            undoButton = new Button();
            undoButton.Content = "Undo";
            undoButton.IsEnabled = false;
            undoButton.Click += new RoutedEventHandler(UndoButton_Click);
            stackPanel.Children.Add(undoButton);

            redoButton = new Button();
            redoButton.Content = "Redo";
            redoButton.IsEnabled = false;
            redoButton.Click += new RoutedEventHandler(RedoButton_Click);
            stackPanel.Children.Add(redoButton);

            System.Windows.Controls.Button beginGroupButton = new Button();
            beginGroupButton.Content = "Start Gruppe";
            beginGroupButton.Click += new RoutedEventHandler(BeginGroupButton_Click);
            stackPanel.Children.Add(beginGroupButton);

            System.Windows.Controls.Button endGroupButton = new Button();
            endGroupButton.Content = "Beende Gruppe";
            endGroupButton.Click += new RoutedEventHandler(EndGroupButton_Click);
            stackPanel.Children.Add(endGroupButton);

            listBox = new ListBox();
            listBox.FontSize = 9;
            stackPanel.Children.Add(listBox);


            panel.Children.Add(stackPanel);

            ((PpsDataSetClient)dataSet).UndoRedo.UndoRedoChanged += UndoRedo_UndoRedoChanged;
        }

        private void UndoRedo_UndoRedoChanged(object sender, EventArgs e)
        {
            undoButton.IsEnabled = ((PpsDataSetClient)dataSet).UndoRedo.CanUndo;
            redoButton.IsEnabled = ((PpsDataSetClient)dataSet).UndoRedo.CanRedo;

            // show undo/redo item descriptions
            listBox.Items.Clear();
            foreach (var item in ((UndoRedo)sender).Description)
            {
                listBox.Items.Add(item);
            }
        }

        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            ((PpsDataSetClient)dataSet).UndoRedo.Undo();
        }

        private void RedoButton_Click(object sender, RoutedEventArgs e)
        {
            ((PpsDataSetClient)dataSet).UndoRedo.Redo();
        }

        private void BeginGroupButton_Click(object sender, RoutedEventArgs e)
        {
            ((PpsDataSetClient)dataSet).UndoRedo.BeginGroup();
        }

        private void EndGroupButton_Click(object sender, RoutedEventArgs e)
        {
            ((PpsDataSetClient)dataSet).UndoRedo.EndGroup();
        }


    } // class PpsGenericMaskWindowPane
}

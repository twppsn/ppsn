using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace TecWare.PPSn.Data
{
    public static class GlobalExtensions //~ move to other file/class?
    {
        /// <summary>Gets an enumerable of all ancestors;
        /// useful for objects having some kind(s) of parents</summary>
        public static IEnumerable<T> Ancestors<T>(this T obj, Func<T, T> expression) where T : class
        {
            obj = expression.Invoke(obj);
            while (obj != null)
            {
                yield return obj;
                obj = expression.Invoke(obj);
            }
        }
    }

    public interface IUndoRedo
    {
        void Undo();
        void Redo();
        string Description { set; get; }// arbitrary clear text describing some object
    }

    public class ObjectDescription
    {
    }

    public class UndoRedo
    {
        private Stack<IUndoRedo> undoItems = new Stack<IUndoRedo>();
        private Stack<IUndoRedo> redoItems = new Stack<IUndoRedo>();
        private UndoRedoGroup currentUndoRedoGroup = null;
        private bool enableAddItem = true;
        private int automaticGroupNumber = 0;

        public event EventHandler UndoRedoChanged;
        public bool CanUndo { get { return undoItems.Count() > 0; } }
        public bool CanRedo { get { return redoItems.Count() > 0; } }
        public string[] Description 
        { 
            get
            {
                string[] descriptions = new string[undoItems.Count + redoItems.Count];
                int i = 0;
                foreach (var item in undoItems)
                {
                    descriptions[i++] = string.Format("Undo: {0}", item.Description);
                }

                foreach (var item in redoItems)
                {
                    descriptions[i++] = string.Format("Redo: {0}", item.Description);
                }

                return descriptions;
            }
        }


        public UndoRedo()
        {
        }

        public void AddItem(PpsDataRow dataRow, int columnIndex, object oldValue, object newValue)
        {
            if (enableAddItem) // avoid adding items on value changes caused by undo or redo itself
            {
                redoItems.Clear(); // discard "old" redo operations we don't need anymore henceforth

                IUndoRedo singleOperation = new UndoRedoSingleOperation(dataRow, columnIndex, oldValue, newValue);
                ((UndoRedoSingleOperation)singleOperation).Description = string.Format("{0}: old: '{1}' new: '{2}'", dataRow.Table.Columns[columnIndex].Name, oldValue, newValue);
                StoreItem(singleOperation);

                RaiseEvent();
            }
        }

        public void Undo()
        {
            if (undoItems.Count() > 0)
            {
                IUndoRedo itemForUndo = undoItems.Pop(); // pop current value
                redoItems.Push(itemForUndo); // ensure redo of this item

                DisableAddItem();
                itemForUndo.Undo();
                EnsableAddItem();

                RaiseEvent();
            }
        }

        public void Redo()
        {
            if (redoItems.Count() > 0)
            {
                IUndoRedo redoItem = redoItems.Pop();
                undoItems.Push(redoItem); // ensure undo of this item

                DisableAddItem();
                redoItem.Redo();
                EnsableAddItem();

                RaiseEvent();
            }
        }

        public void BeginGroup(string groupName)
        {
            if (currentUndoRedoGroup == null)
            {
                // append new group to main undo container
                currentUndoRedoGroup = new UndoRedoGroup(groupName);
                undoItems.Push(currentUndoRedoGroup);
                // from now on all undo items will go into this group
            }
            else
            {
                // append new group to current undo group (=nested group)
                var allParents = currentUndoRedoGroup.Ancestors(group => group.ParentGroup); // all parent groups (=enclosing groups) of current group

                if (currentUndoRedoGroup.GroupName != groupName // will become parent of new group
                    && !allParents.Any(group => group.GroupName == groupName))
                {
                    UndoRedoGroup newRedoGroup = new UndoRedoGroup(groupName);

                    newRedoGroup.ParentGroup = currentUndoRedoGroup;
                    currentUndoRedoGroup = newRedoGroup;
                }
                else
                {
                    throw new AppException(string.Format("Can't begin new undo group with name '{0}'; you must end the open group with the same name before."
                        , groupName));
                }
            }
        }

        public void EndGroup(string groupName)
        {
            if (currentUndoRedoGroup != null)
            {
                    if (groupName == currentUndoRedoGroup.GroupName)
                    {
                    //  OK: began last - end first

                    if (currentUndoRedoGroup.ItemCount == 0
                        && currentUndoRedoGroup.ParentGroup == null)
                    {
                        undoItems.Pop(); // remove useless empty group from main undo container
                    }
                    else if (currentUndoRedoGroup.ParentGroup != null)
                    {
                        // if undo group is enclosed by a parent undo group: remove current group by merging items of current group with its parent group 
                        int itemCountBeforeMerging = currentUndoRedoGroup.ParentGroup.ItemCount;
                        currentUndoRedoGroup.ParentGroup.AppendItemsOfGroup(currentUndoRedoGroup);
                        Checker.Assert(currentUndoRedoGroup.ParentGroup.ItemCount == itemCountBeforeMerging + currentUndoRedoGroup.ItemCount);

                        currentUndoRedoGroup = currentUndoRedoGroup.ParentGroup; // ...just merged before
                    }
                    else
                    {
                        currentUndoRedoGroup = null;
                        // from now on a new group may begin
                    }
                }
                else
                {
                    throw new AppException(string.Format("Tried to end undo group with name '{0}'; current group with name '{1}' is still open."
                         + " \nFix: Must end current group '{1}' first - to let groups overlap is not allowed."
                         , groupName, currentUndoRedoGroup.GroupName));
                }
            }
            else
            {
                throw new AppException(string.Format("Can't end undo group with name '{0}'. No such group is currently open."
                    + " \nFix: You must begin the group '{0}' before you can end it."
                    , groupName));
            }
        }

        public void BeginGroup()
        {
            BeginGroup(string.Format("Gruppe {0}", ++automaticGroupNumber));
            RaiseEvent();
        }

        public void EndGroup()
        {
            EndGroup(string.Format("Gruppe {0}", automaticGroupNumber));
            RaiseEvent();
        }

        void RaiseEvent()
        {
            if (UndoRedoChanged != null)
            {
                UndoRedoChanged(this, EventArgs.Empty);
            }
        }

        void StoreItem(IUndoRedo item)
        {
            if (currentUndoRedoGroup == null)
            {
                undoItems.Push(item); // append new item to main undo container
            }
            else
            {
                currentUndoRedoGroup.StoreItem(item); // append new item to current undo group
            }
        }

        void EnsableAddItem()
        {
            enableAddItem = true;
        }

        void DisableAddItem()
        {
            enableAddItem = false;
        }
    }

    public class UndoRedoSingleOperation : ObjectDescription, IUndoRedo
    {
        UndoRedoItem undoRedoItem;

        #region implement IUndoRedo

        public void Undo()
        {
            undoRedoItem.RowValue = undoRedoItem.NewValue;
        }

        public void Redo()
        {
            undoRedoItem.RowValue = undoRedoItem.OldValue;
        }

        public string Description // arbitrary clear text describing some object
        {
            set;
            get;
        }

        #endregion

        public UndoRedoSingleOperation(PpsDataRow dataRow, int columnIndex, object oldValue, object newValue)
        {
            undoRedoItem = new UndoRedoItem(dataRow, columnIndex, oldValue, newValue);
        }
    }

    public class UndoRedoGroup : IUndoRedo
    {
        private List<IUndoRedo> undoItems = new List<IUndoRedo>();

        public UndoRedoGroup ParentGroup { get; set; } // parent group contains this group
        public string GroupName { get; private set; }
        public int ItemCount { get { return undoItems.Count; } }
        public UndoRedoGroup()
        {
            ParentGroup = null;
        }

        public void AppendItemsOfGroup(UndoRedoGroup groupToAppend)
        {
            undoItems.AddRange(groupToAppend.undoItems);
        }

        #region implement IUndoRedo

        public void Undo()
        {
            foreach (var item in Enumerable.Reverse(undoItems)) // undo last entry first (like with stack)
            {
                item.Undo();
            }
        }

        public void Redo()
        {
            foreach (IUndoRedo item in undoItems)
            {
                item.Redo();
            }
        }

        public string Description 
        {
            set { Description = value; }
            get 
            {
                StringBuilder containingItemsDescription = new StringBuilder(1000);
                foreach (var item in undoItems)
                {
                    containingItemsDescription.AppendFormat(" # {0}", item.Description);
                }
                
                return string.Format("<{0}>{1}</{0}>",GroupName, containingItemsDescription.ToString());
            }
        }

        #endregion

        public UndoRedoGroup(string groupName)
        {
            Checker.Assert(!string.IsNullOrEmpty(groupName), "Undo group name required, name was null or empty.");

            GroupName = groupName;
        }

        public void StoreItem(IUndoRedo item)
        {
            undoItems.Add(item);
        }
    }


    public class UndoRedoItem
    {
        // PpsDataRow: editable object
        // int       : column index in data row
        // object 1  : old (previous) value
        // object 2  : new (current)  value
        private Tuple<PpsDataRow, int, object, object> item;

        public Tuple<PpsDataRow, int, object, object> Item { get { return item; } }
        public object RowValue { set { item.Item1.Current[item.Item2] = value; } }
        public object NewValue { get { return item.Item3; } }
        public object OldValue { get { return item.Item4; } }

        public UndoRedoItem(PpsDataRow dataRow, int columnIndex, object oldValue, object newValue)
        {
            item = new Tuple<PpsDataRow, int, object, object>(dataRow, columnIndex, oldValue, newValue);
        }
    }
}

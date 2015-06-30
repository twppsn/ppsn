using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TecWare.PPSn.Data
{
    public interface IVisitor
    {
        void Visit(PpsDataSet dataSet);
        void Visit(PpsDataTable dataTable);
        void Visit(PpsDataRow dataRow);
    }

    public class VisitorCollectObjectsOfType<T> : IVisitor where T : class
    {
        private List<T> collectedObjects = new List<T>();
        public ReadOnlyCollection<T> CollectedObjects;

        #region ctor

        public VisitorCollectObjectsOfType()
        {
            CollectedObjects = new ReadOnlyCollection<T>(collectedObjects);
        }

        #endregion

        #region implements IVisitor

        public void Visit(PpsDataSet dataSet)
        {
            AddIfTypeFits(dataSet);
        }

        public void Visit(PpsDataTable dataTable)
        {
            AddIfTypeFits(dataTable);
        }

        public void Visit(PpsDataRow dataRow)
        {
            AddIfTypeFits(dataRow);
        }

        #endregion

        void AddIfTypeFits(object o)
        {
            if (o.GetType() == (typeof(T))) //Ri: no idea for checking if o is derived from <T>
            {
                collectedObjects.Add(o as T);
            }
        }

    }
}

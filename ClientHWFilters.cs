using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System_Monitor_MQTT
{
    public class ClientHWFilters : List<string>
    {
        public bool isModified { get; private set; }

        public new void Add(string filter)
        {
            if (this.Contains(filter)) return;
            base.Add(filter);
            isModified = true;
        }

        public new void Remove(string filter)
        {
            if (!this.Contains(filter)) return;
            base.Remove(filter);
            isModified = true;
        }

        public new void Clear()
        {
            if (this.Count == 0) return;
            base.Clear();
            isModified = true;
        }

        public new void Insert(int index, string filter)
        {
            if (this.Contains(filter)) return;
            base.Insert(index, filter);
            isModified = true;
        }

        public new void RemoveAt(int index)
        {
            if (index < 0 || index >= this.Count) return;
            base.RemoveAt(index);
            isModified = true;
        }

        public new void RemoveAll(Predicate<string> match)
        {
            base.RemoveAll(match);
            isModified = true;
        }

        public new void RemoveRange(int index, int count)
        {
            base.RemoveRange(index, count);
            isModified = true;
        }

        public new void Reverse()
        {
            base.Reverse();
            isModified = true;
        }
        
        public IEnumerable<string> GetFilters()
        {
            isModified = false;
            return this;
        }

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System_Monitor_MQTT
{
    public class ClientHWFilters
    {
        private readonly List<string> _filters = new List<string>();
        public bool isModified { get; private set; }

        public void Add(string filter)
        {
            if (_filters.Contains(filter)) return;
            _filters.Add(filter);
            isModified = true;
        }

        public void Remove(string filter)
        {
            if (!_filters.Contains(filter)) return;
            _filters.Remove(filter);
            isModified = true;
        }

        public void Clear()
        {
            if (_filters.Count == 0) return;
            _filters.Clear();
            isModified = true;
        }

        public void Insert(int index, string filter)
        {
            if (_filters.Contains(filter)) return;
            _filters.Insert(index, filter);
            isModified = true;
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= _filters.Count) return;
            _filters.RemoveAt(index);
            isModified = true;
        }

        public void RemoveAll(Predicate<string> match)
        {
            _filters.RemoveAll(match);
            isModified = true;
        }

        public void RemoveRange(int index, int count)
        {
            _filters.RemoveRange(index, count);
            isModified = true;
        }

        public void Reverse()
        {
            _filters.Reverse();
            isModified = true;
        }
        
        public IEnumerable<string> GetFilters()
        {
            isModified = false;
            return _filters.AsReadOnly();
        }

    }
}

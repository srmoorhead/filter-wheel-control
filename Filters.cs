using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Filters
{
    /// <summary>
    /// An object representing a single filter
    /// </summary>
    public class Filter
    {
        private string type;
        private Filter next;
        private Filter prev;

        public Filter(string type)
        {
            this.type = type;
            this.next = null;
            this.prev = null;
        }

        public string GetFilterType() { return this.type; }

        public Filter GetNext() { return this.next; }

        public Filter GetPrev() { return this.prev; }

        public void SetNext(Filter f) { this.next = f; }

        public void SetPrev(Filter f) { this.prev = f; }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            Filter f = obj as Filter;
            if ((object)f == null)
                return false;

            return (type == f.GetFilterType() && next == f.GetNext() && prev == f.GetPrev());
        }

        public bool Equals(Filter f)
        {
            if ((object)f == null)
                return false;

            return (type == f.GetFilterType() && next == f.GetNext() && prev == f.GetPrev());
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override string ToString()
        {
            return this.type;
        }
    }

}

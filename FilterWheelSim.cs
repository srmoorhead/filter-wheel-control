using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Filters;
using System.Threading;

namespace FilterWheelSimulator
{
    /// <summary>
    /// An object representing a Filter Wheel, set up as a doubly linked list of Filter objects
    /// </summary>
    public class FilterWheel
    {
        private Filter current;
        private Filter first;
        private Filter last;
        private int size;

        public FilterWheel(Filter f)
        {
            this.current = this.first = this.last = f;
            f.SetNext(f);
            f.SetPrev(f);
            size = 1;
        }

        public void AddFilter(Filter f)
        {
            this.last.SetNext(f);
            f.SetPrev(this.last);
            f.SetNext(this.first);
            this.first.SetPrev(f);
            this.last = f;
            size++;
        }

        public Filter GetCurrent()
        {
            return this.current;
        }

        public Filter MoveCW()
        {
            current = current.GetPrev();
            Thread.Sleep(1000);
            return this.current;
        }

        public Filter MoveCCW()
        {
            current = current.GetNext();
            Thread.Sleep(1000);
            return this.current;
        }

        private Tuple<string, int> DistanceTo(string t)
        {
            if (t == current.GetFilterType())
                return Tuple.Create("cw", 0);

            Filter ccw = current.GetNext();
            Filter cw = current.GetPrev();
            int dist = 1;
            while (ccw != current && cw != current && ccw.GetFilterType() != t && cw.GetFilterType() != t)
            {
                ccw = ccw.GetNext();
                cw = cw.GetPrev();
                dist++;
            }

            if (cw.GetFilterType() == t)
                return Tuple.Create("cw", dist);
            else if (ccw.GetFilterType() == t)
                return Tuple.Create("ccw", dist);
            else
                return Tuple.Create("void", -1);
        }

        public Filter MoveTo(string t)
        {
            Tuple<string, int> measure = DistanceTo(t);
            if (measure.Item2 == -1)
                return null;

            if (measure.Item1 == "cw")
            {
                for (int i = 0; i < measure.Item2; i++)
                    current = current.GetPrev();
            }
            else
            {
                for (int i = 0; i < measure.Item2; i++)
                    current = current.GetNext();
            }
            Thread.Sleep(1000 * measure.Item2);
            return this.current;
        }

        public override string ToString()
        {
            Filter f = current.GetNext();
            string result = current.ToString();
            while (f != current)
            {
                result += ", " + f.ToString();
                f = f.GetNext();
            }
            return result;
        }

        public List<Filter> GetOrderedFilterSet()
        {
            List<Filter> set = new List<Filter>();
            Filter f = current;
            set.Add(f);
            while ((f = f.GetNext()) != current)
                set.Add(f);
            return set;
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Threading;

namespace FilterWheelControl.HardwareInterface
{
    public class WheelInteraction
    {
        private static volatile string CURRENT_FILTER = "u";
        private static readonly List<string> LOADED_FILTERS = new List<string>{ "u", "g", "r", "i", "z", "BG40", "DARK", "NOFI" }; // any changes to the physical filters must be reflected here and listed IN ORDER moving clockwise
        private static readonly object ROTATE_LOCK = new object();

        /// <summary>
        /// Sends a trigger to the Filter Wheel to move the wheel to the specified filter
        /// </summary>
        /// <param name="f">An object of type string.  Caution:  Does not check for type correctness before casting.</param>
        public static void RotateWheelToFilter(object f)
        {
            string rotateTo = (string)f;
            lock (ROTATE_LOCK)
            {
                CURRENT_FILTER = rotateTo;
                Thread.Sleep(3000);
            }
        }

        /// <summary>
        /// Tells you if the filter wheel needs to be rotated
        /// </summary>
        /// <param name="f">The desired filter</param>
        /// <returns>true if the desired filter is different from the current filter, false otherwise</returns>
        public static bool MustRotate(string f)
        {
            lock (ROTATE_LOCK)
            {
                return f != CURRENT_FILTER;
            }
        }

        /// <summary>
        /// Gets the filter currently in front of the camera.
        /// If the filter wheel is rotating, it will return the previous filter value until the wheel has finished rotating.
        /// </summary>
        /// <returns>string of the filter type</returns>
        public static string getCurrentFilter() { lock (ROTATE_LOCK) { return CURRENT_FILTER; } }

        /// <summary>
        /// Starting with the current filter, returns filters in order moving clockwise around the wheel w.r.t. the camera
        /// </summary>
        /// <returns>string[] holding the ordered filter types</returns>
        public static string[] getCurrentOrder()
        {
            lock (ROTATE_LOCK)
            {
                string current = getCurrentFilter();
                string[] order = new string[LOADED_FILTERS.Count()];
                order[0] = current;
                int order_loc = 0;

                int i = LOADED_FILTERS.IndexOf(current);
                do
                {
                    order[order_loc] = LOADED_FILTERS[i];
                    if (i == LOADED_FILTERS.Count() - 1)
                        i = 0;
                    else
                        i++;
                    order_loc++;
                }
                while (LOADED_FILTERS[i] != current);

                return order;
            }
        }
    }
}

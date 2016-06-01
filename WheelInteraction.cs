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
        private static string CURRENT_FILTER = "u";

        /// <summary>
        /// Sends a trigger to the Filter Wheel to move the wheel to the specified filter
        /// </summary>
        /// <param name="f">An object of type string.  Caution:  Does not check for type correctness before casting.</param>
        public static void RotateWheelToFilter(object f)
        {
            string rotateTo = (string)f;
            if (rotateTo != CURRENT_FILTER)
            {
                CURRENT_FILTER = rotateTo;
                Thread.Sleep(3000);
            }
        }
    }
}

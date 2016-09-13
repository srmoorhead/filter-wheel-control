# filter-wheel-control
The files for the Filter Wheel Control add-in in LightField.

**********
NOTE:  The main algorithm being implemented in this application to handle the filter wheel rotation and data acquisition has changed dramatically from the first iteration of this program.  Please see previous commits, including one where we thought the project had to be scrapped, for further information on the early versions.  The project is still under development and will be changing on a weekly basis until complete (hopefully in a month or so).
**********

The Filter Wheel Control project is an add-in for the software LightField (by Princeton Instruments).  It hooks up to the Run and Acquire functionality of LightField and provides custom event handling that allows for a sequence of filters to be iterated through while observing.  The program alters expsoure times in real time to change the behavior of hardware componenets to account for the rotation of a filter wheel, provides a system for manual control of the filter wheel without these timing alterations, offers timing feedback to the observer, displays thumbnail images of the captured frames, and offers a visual representation of the filter wheel in its present state.  It is an interfacing system between the observer, the camera, the timing unit, and the filter wheel.

Future work includes:  completing the custom event handling, providing an option to inject a filter sequence into an otherwise constant-filter observation run, and more!

The Filter Wheel Control project implements a significant portion of the LightField AddIn and Automation SDK, which can be found on the Princeton Instruments website.  It also utilizes a set of classes developed by Josh Smith to allow drag-drop reording of a list view element.

Development of this application is led primarily by Sean Moorhead, and all good and bad design decisions should be attributed solely to him.  His work would not have been possible without the consultations and contributions of Zach Vanderbosch, Keaton Bell, Don Winget, Mike Montgomery, and numerous others in the University of Texas Astronomy Department.  Many thanks to these folks for their support and honest feedback.

A list of included files:

MouseUtilities.cs, DragAdorner.cs, and ListViewDragDropManager.cs are copyright (2007) Josh Smith.  See:  http://www.codeproject.com/Articles/17266/Drag-and-Drop-Items-in-a-WPF-ListView

ControlPanel.xaml.cs - Primary interaction logic for the control panel.
ControlPanel.xaml - Layout and design of control panel.
FilterWheelControl.cs - Launch file that loads add-in into LightField.
CaptureSession.cs - Handles capturing images in a synchronous manner with the filter wheel rotation.  Controls most parts of image acquisition.
CurrentSettingsList.cs - Handles all functionality involving the current settings list, including file IO.
FileHandler.cs - Handles all image file IO for .spe and .fits files.
Filters.cs - An object representing a filter in a filter wheel.  Closely mirrors a singly-linked list.
FilterWheelSim.cs - A simulator for a filter wheel.  Used while the physical wheel was not present for development.
WheelInterface.cs - Handles all interactions with the filter wheel hardware device.

All LightField methods and libraries are copyright protected by Princeton Instruments.  For information on LightField, see:  http://www.princetoninstruments.com/products/LightField

For questions on this add-in, contact Sean Moorhead at sean[dot]moorhead[at]utexas[dot]edu .

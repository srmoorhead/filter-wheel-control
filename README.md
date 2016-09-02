# filter-wheel-control
The files for the Filter Wheel Control add-in in LightField.

**********
NOTE:  As of 02 September 2016, this project is no longer under development.  The Princeton Instrument SDK was not satisfactory for our work and our communication with Princeton Instruments yielded no solutions.  While we attempted to finalize this work, we found that the API (specifically, the output of the Capture method) did not provide absolute time stamps for our data, so we could not compare between frames or use them for time-series observations - the majority of our work.  It is kept here as both an example of this programmer's (Sean Moorhead's) work and a small tribute to the many hours spent developing this application.
**********

The Filter Wheel Control project is an add-in for the software LightField (by Princeton Instruments).  It overrides the typical Run and Acquire functionality of LightField with alternative methods that allow for a sequence of filters to be iterated through while observing.  The functions interrupt image capturing during filter wheel rotation, provide a system for manual control of the filter wheel, offer timing feedback to the observer, display thumbnail images of the captured frames, and offer a visual representation of the filter wheel in its present state.  It is an interfacing system between the observer, the camera, the timing unit, and the filter wheel.

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

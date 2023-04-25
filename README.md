# FS2020Control: Export Control Settings in Microsoft FS2020 to SQLite, Excel and Pdf

[![stability-release-candidate](https://img.shields.io/badge/stability-pre--release-48c9b0.svg)](https://github.com/mkenney/software-guides/blob/master/STABILITY-BADGES.md#release-candidate)

[dieter.menne\@menne-biomed.de](mailto:dieter.menne@menne-biomed.de)

Thanks to @pmb2019 for testing on the Steam platform.

Getting an overview of the assigned keys for keyboard, mouse, flight stick in Microsoft 
Flight Simulator 2020 is not easy. For the installations from the Microsoft Store, 
the local copies of the control settings can be found in a file with a path like:

    C:\Users\MyName\AppData\Local\Packages\
    Microsoft.FlightSimulator_8wekyb3d8bbwe\
    SystemAppData\wgs\
    000901F9BE5269E8_00000000000000000000000069F80140\
    9D40C7EC95724C16A62F4153D13B14B9\
    C6A6EACA3129423E8E4E42B33DE3BFCF

*the above is ONE LINE*

or, much simpler for Steam installations

  C:\Program Files (x86)\Steam\userdata\123456789\1250410

where 1250410 seems to be fixed, while the first numeric string is different in 
all installations.

Whenever changes are made in the control settings of FS2020, the device's settings file 
is deleted and replaced by another file with cryptic name.

In short: Microsoft does not want us to mess with it, and may change everything without notice.

## What the app does

-   Tries to find the settings files of the controls and scans all - I am not sure if this will work in all installations;
-   Writes all settings into an SQLite Database, which can be used by advanced users to generate custom reports;
-   Displays all control settings, allows for a subset selection and sort order for reporting;
-   Generates a PDF reference sheet;
-   Generates an Excel file with sortable and selectable entries.

Database, Excel and PDF file are stored in directory `Documents/FS2020Key` 
with names such as `FS2020Controls_Mouse_A300.pdf`. The directory is created when it does not exist.

## Installation

Releases are on [flightsim.to](https://flightsim.to/d/dmenne/fs2020-control).

- Unzip SetupFS2020Control.zip into a temporary directory
- Double-click `setup.exe`
- You can delete all temporary files after installation

## Steam

Steam installations are tricky, and I had to rely on @pmb2019 for debugging. If someone has both Store and 
Steam installation, or swapped one for the other, nasty things might happen. Please report to github.

## Main Window

**Do not forget to click *Reload* after any changes you made to the control settings in FS2020.**

![FS2020 Main Window. By default, debug items are hidden.](png/FS2020App.png)

## Generated output

### PDF

![Pdf Summary of selected controls. Re-build of this report fails when a 
previous version is open in a reader.](png/FS2020Pdf.png)

Example file name:

`C:\Users\<Username>\Documents\FS2020Keys\FS2020Controls_Mouse_A300.pdf`

### Excel

![Excel table of selected controls. Re-build of the workbook fails when a 
previous version is open in Excel.](png/FS2020Excel.png)

Example file name: 

`C:\Users\<Username>\Documents\FS2020Keys\FS2020Controls_Keyboard_A310.xlsx`

### Database

![SQLite database for advanced users. The database is recreated on explicit 
reload request, but not on restart of the program. It contains settings for all 
control items, independent of the selection.](png/FS2020SQLite.png)

Example file name: 

`C:\Users\<Username>\Documents\FS2020Keys\FS2020.sqlite`

## Advanced 

When you double-click an item in the upper left data grid, you can select to open the file 
with the control settings with Notepad, or copy the gargantuan file name to the clipboard 
for processing in other editors. 


![After a double click, you can decide if you want to open the file or copy the path to the clipboard. 
The message turns up only once per session, 
you have to restart the program when you changed your decision.](png/FS2020DoubleClick.png)

Any changes using the external editor are at your own risk. Edits may or may not 
do what you expect, worst case scenario is that all settings for the device are lost, 
so better work on a clone made in FS2020.

Do not be surprised when you XML-editor (recommended: NotePad++ with XML plugin) complains 
that the file is not valid XML - 
while the header insists that the content is XML, there is no root level element as 
required by the norm. For internal processing, the app temporarily adds a root 
level element - see function [MakeValidHtml](https://github.com/dmenne/FS2020Control/blob/76a26385f2830040c2a71d4f0432f3f2f18848a5/FS2020Control/XmlToSqlite.cs#L147-L159) in file `XmlToSqlite`.


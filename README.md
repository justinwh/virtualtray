Virtualtray
===========

Virtualtray is a companion program for the VirtualBox VM ([http://www.virtualbox.org/](http://www.virtualbox.org/)) that lets you start/stop/save VMs from the Windows system tray.  I wrote it so I could easily keep an eye on headless VMs without using the command line.

The executable is self-contained and has no external dependencies other than VirtualBox and the .NET runtime.  It's Windows-only, and tested on Windows 7.

To try it, download Virtualtray.exe from:

>  [http://github.com/downloads/justinwh/virtualtray/Virtualtray.exe/qr_code](http://github.com/downloads/justinwh/virtualtray/Virtualtray.exe/qr_code)



Building
--------

To build Virtualtray, you need Microsoft's .NET Framework (v3.5 or greater, ships with Windows 7) and the ILMerge tool, which is available from Microsoft at:

>  [http://www.microsoft.com/downloads/details.aspx?FamilyID=22914587-b4ad-4eae-87cf-b14ae6a939b0&displaylang=en](http://www.microsoft.com/downloads/details.aspx?FamilyID=22914587-b4ad-4eae-87cf-b14ae6a939b0&displaylang=en)


1. Compile the primary EXE file.

   `%NET_FRAMEWORK_PATH%\csc.exe /reference:VirtualBox.dll /win32icon:icon/icon.ico /resource:icon/icon-16.ico /resource:icon/icon-gray-16.ico /target:winexe /out:Virtualtray0.exe Virtualtray.cs`

   Replace `%NET_FRAMEWORK_PATH%` with the path to your .NET Framework installation.  Mine is "C:\Windows\Microsoft.NET\Framework\v3.5".


2. Run the ILMerge tool to merge the DLL and primary EXE into a single, final EXE file.

   `ILMerge.exe /target:winexe /out:Virtualtray.exe Virtualtray0.exe VirtualBox.dll`


3. Run Virtualtray.exe to start Virtualtray.  This is a standalone EXE file that can be copied to another folder (or distributed to other machines) and run without any dependencies (other than the .NET runtime).



Rebuilding VirtualBox.dll
-------------------------

The VirtualBox.dll library provides a .NET interface to the VirtualBox COM library.  You shouldn't need to rebuild this since it's included in the Virtualtray repository, but here are the instructions to do so if you're curious.

VirtualBox.dll is generated by running the TlbImp2 tool on the VirtualBox.tlb file included in the VirtualBox SDK.  To get a copy of VirtualBox.tlb, download the VirtualBox SDK and look in the "bindings\mscom\lib" folder.  The TlbImp2 tool is available from Microsoft at:

>  [http://clrinterop.codeplex.com/releases/view/17579#DownloadId=44381](http://clrinterop.codeplex.com/releases/view/17579#DownloadId=44381)


1. Run the TlbImp2 tool.

   `TlbImp2.exe VirtualBox.tlb /out:VirtualBox.dll`


2. The generated file is VirtualBox.dll, and it can be used to build a new Virtualtray.exe or used in any .NET project.



The Virtualtray icon
--------------------

The icon for Virtualtray was converted from an SVG source file into an ICO file using Apache Batik and Greenfish Icon Editor Pro.  You won't need to do this since the finished icons are included in the repository.

Batik is an open source library and toolset for handling SVG files, and was used to rasterize the SVG at various sizes (16x16, 24x24, 32x32, 48x48, 256x256).  Batik is available at:

>  [http://xmlgraphics.apache.org/batik/](http://xmlgraphics.apache.org/batik/)


Icon Editor Pro is a freeware ICO file editor, and was used to write out the final ICO files.  Icon Editor Pro is available at:

>  [http://greenfish.xtreemhost.com/](http://greenfish.xtreemhost.com/)



License
-------

Copyright (c) 2010 Justin Huntington  
This software is licensed under the MIT license.  See the LICENSE file.

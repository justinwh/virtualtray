// Copyright (c) 2010 Justin Huntington
// This file is licensed under the MIT license.  See the LICENSE file.

//$/reference:VirtualBox.dll
using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using VirtualBox;

[assembly: AssemblyTitle("Virtualtray")]
[assembly: AssemblyProduct("Virtualtray")]
[assembly: AssemblyCopyright("© 2010 Justin Huntington, MIT license, http://github.com/justinwh")]
[assembly: AssemblyVersion("1.0.*")]

/* A running Virtualtray has one instance of this class to manage the icon & 
 * global menu items.  It creates one MachineMenu per VM, to manage each 
 * submenu.
 */
public class Virtualtray : ApplicationContext, IVirtualBoxCallback {
	
	private NotifyIcon _notifyIcon;
	private IVirtualBox _virtualbox;
	private Hashtable _vms;
	private Icon _icon;
	private Icon _idleIcon;
	
	private Virtualtray() {
		_virtualbox = (IVirtualBox) new VirtualBox.VirtualBox();
		
		/* the generated VirtualBox.dll COM library doesn't work with the C# event 
		 * system, so we handle events by registering callbacks
		 */
		_virtualbox.RegisterCallback(this);
		
		_icon = GetIcon("icon/icon-16.ico");
		_idleIcon = GetIcon("icon/icon-gray-16.ico");
		
		_notifyIcon = new NotifyIcon();
		_notifyIcon.Icon = _idleIcon;
		_notifyIcon.Text = "Virtualtray";
		_notifyIcon.Visible = true;
		
		_vms = new Hashtable();
		
		ContextMenu menu = new ContextMenu();
		foreach (IMachine machine in _virtualbox.Machines) {
			MachineMenu mm = new MachineMenu(machine, menu);
			mm.StateChange += new EventHandler(MachineStateChangeEventHandler);
			_vms.Add(machine.Id, mm);
		}
		menu.MenuItems.Add(new MenuItem("-") {Name = "-"});
		menu.MenuItems.Add(new MenuItem("Open VirtualBox...", new EventHandler(
			MenuLaunchVirtualBoxEventHandler)));
		menu.MenuItems.Add(new MenuItem("Exit", new EventHandler(MenuExitEventHandler)));
		_notifyIcon.ContextMenu = menu;
		
		ToggleIcon();
		_notifyIcon.MouseClick += new MouseEventHandler(IconClickEventHandler);
		Application.ApplicationExit += new EventHandler(ApplicationExitEventHandler);
	}
	
	// get the icon embedded in the EXE, fall back to looking for the file
	private Icon GetIcon(string path) {
		System.IO.Stream s = Assembly.GetExecutingAssembly()
			.GetManifestResourceStream(path);
		if (s != null) {
			return new Icon(s);
		}
		return new Icon(path);
	}
	
	private void IconClickEventHandler(object sender, MouseEventArgs e) {
		if (e.Button != MouseButtons.Left) {
			return;
		}
		// there's no public interface to get NotifyIcon to show its ContextMenu
		MethodInfo showContextMenu = typeof(NotifyIcon).GetMethod("ShowContextMenu", 
			BindingFlags.Instance | BindingFlags.NonPublic);
		if (showContextMenu != null) {
			showContextMenu.Invoke(_notifyIcon, null);
		}
	}
	
	private void MenuLaunchVirtualBoxEventHandler(object sender, EventArgs e) {
		LaunchVirtualBox();
	}
	
	private void MenuExitEventHandler(object sender, EventArgs e) {
		Application.Exit();
	}
	
	private void MachineStateChangeEventHandler(object sender, EventArgs e) {
		ToggleIcon();
	}
	
	/* toggle between the grayscale (idle) icon and the normal icon when any machine 
	 * becomes active
	 */
	private void ToggleIcon() {
		foreach (MachineMenu mm in _vms.Values) {
			if (!mm.Stopped) {
				_notifyIcon.Icon = _icon;
				return;
			}
		}
		_notifyIcon.Icon = _idleIcon;
	}
	
	// add/remove MachineMenus when VMs are created/destroyed in the main Virtualbox UI
	public void OnMachineRegistered(string machineId, int registered) {
		if (registered > 0) {
			IMachine machine = Array.Find<IMachine>(_virtualbox.Machines, delegate(IMachine m) {
				return m.Id == machineId;
			});
			MachineMenu mm = new MachineMenu(machine, _notifyIcon.ContextMenu);
			_vms.Add(machineId, mm);
		}
		else {
			MachineMenu m = (MachineMenu) _vms[machineId];
			m.Remove();
			_vms.Remove(machineId);
		}
	}
	
	private void ApplicationExitEventHandler(object sender, EventArgs e) {
		_notifyIcon.Icon = null;
	}
	
	// unused callbacks
	public void OnMachineStateChange(string machineId, MachineState state) {}
	public void OnMachineDataChange(string machineId) {}
	public void OnExtraDataChange(string machineId, string key, string value) {}
	public void OnMediumRegistered(string mediumId, VirtualBox.DeviceType type, int registered) {}
	public void OnSessionStateChange(string sessionId, VirtualBox.SessionState state) {}
	public void OnSnapshotTaken(string machineId, string snapshotId) {}
	public void OnSnapshotDeleted(string machineId, string snapshotId) {}
	public void OnSnapshotChange(string machineId, string snapshotId) {}
	public void OnGuestPropertyChange(string machineId, string name, string value, string flags) {}
	public int OnExtraDataCanChange(string machineId, string key, string value, out string error) {
		error = null;
		return 1;
	}
	
	// launch the main Virtualbox UI (or bring it to the front if it's running)
	private static void LaunchVirtualBox() {
		Process virtualbox = null;
		foreach (Process p in Process.GetProcesses()) {
			/* since VMs have the same ProcessName as the config UI, guess which is 
			 * which based on Window title
			 */
			if (p.ProcessName == "VirtualBox" && p.MainWindowTitle != null && 
				!p.MainWindowTitle.Contains("-"))
			{
				virtualbox = p;
				break;
			}
		}
		if (virtualbox != null) {
			ShowWindow(virtualbox.MainWindowHandle.ToInt32(), 5);
			SetForegroundWindow(virtualbox.MainWindowHandle.ToInt32());
		}
		else {
			string path = Environment.GetEnvironmentVariable("VBOX_INSTALL_PATH");
			Process.Start(path + "VirtualBox.exe");
		}
	}
	
	[DllImport("User32")]
	private static extern int ShowWindow(int hWnd, int nCmdShow);
	
	[DllImport("User32")]
	private static extern int SetForegroundWindow(int hWnd);
	
	
	// Each instance of MachineMenu manage one VM and its submenu
	private class MachineMenu : IVirtualBoxCallback {
		
		private enum Submenu {Start = 0, StartHeadless, Save, Stop}
		
		private IVirtualBox _virtualbox;
		private ISession _session;
		private string _id;
		private string _name;
		private Menu _parentMenu;
		private MenuItem _menu;
		private MenuItem[] _submenu = new MenuItem[Enum.GetValues(typeof(Submenu)).Length];
		
		public MachineMenu(IMachine machine, Menu parentMenu) {
			_virtualbox = machine.Parent;
			_virtualbox.RegisterCallback(this);
			_session = null;
			_id = machine.Id;
			_name = machine.Name;
			_parentMenu = parentMenu;
			_menu = new MenuItem(machine.Name);
			
			/* initially create MenuItems for all possible VM actions, then show/hide 
			 * them as needed
			 */
			object[][] submenus = new object [][] {
				new object[] {Submenu.Start, "Start Machine", new EventHandler(MenuStartEventHandler)},
				new object[] {Submenu.StartHeadless, "Start Machine Headless", new EventHandler(MenuStartHeadlessEventHandler)},
				new object[] {Submenu.Save, "Save Machine", new EventHandler(MenuSaveEventHandler)},
				new object[] {Submenu.Stop, "Stop Machine", new EventHandler(MenuStopEventHandler)}
			};
			
			Array.ForEach<object[]>(submenus, delegate(object[] s) {
				MenuItem m = new MenuItem((string) s[1], (EventHandler) s[2]);
				m.Visible = false;
				_submenu[(int) s[0]] = m;
				_menu.MenuItems.Add(m);
			});
			
			State = machine.State;
			
			int last = _parentMenu.MenuItems.IndexOfKey("-");
			last = last < 0 ? _parentMenu.MenuItems.Count : last;
			_parentMenu.MenuItems.Add(last, _menu);
		}
		
		public event EventHandler StateChange;
		
		public void Remove() {
			_parentMenu.MenuItems.Remove(_menu);
		}
		
		private MachineState _state;
		
		// toggle label and MenuItem visibility based on VM state
		public MachineState State {
			get {
				return _state;
			}
			set {
				_state = value;
				
				string stateLabel = null;
				switch (_state) {
					case MachineState.MachineState_Running:
					case MachineState.MachineState_Paused:
					case MachineState.MachineState_Teleporting:
					case MachineState.MachineState_LiveSnapshotting:
					case MachineState.MachineState_TeleportingPausedVM:
					case MachineState.MachineState_TeleportingIn:
					case MachineState.MachineState_DeletingSnapshotOnline:
					case MachineState.MachineState_DeletingSnapshotPaused:
						stateLabel = "Running";
						onlyShowSubmenus(Submenu.Save, Submenu.Stop);
						break;
					
					case MachineState.MachineState_Starting:
					case MachineState.MachineState_Restoring:
						stateLabel = "Starting";
						onlyShowSubmenus();
						break;
					
					case MachineState.MachineState_Stopping:
						stateLabel = "Stopping";
						onlyShowSubmenus();
						break;
						
					case MachineState.MachineState_Saving:
						stateLabel = "Saving";
						onlyShowSubmenus();
						break;
					
					case MachineState.MachineState_Stuck:
						stateLabel = "Crashed";
						onlyShowSubmenus(Submenu.Stop);
						break;
					
					case MachineState.MachineState_PoweredOff:
					case MachineState.MachineState_Saved:
					case MachineState.MachineState_Teleported:
					case MachineState.MachineState_Aborted:
					case MachineState.MachineState_RestoringSnapshot:
					case MachineState.MachineState_DeletingSnapshot:
					case MachineState.MachineState_SettingUp:
					default:
						stateLabel = "Stopped";
						onlyShowSubmenus(Submenu.Start, Submenu.StartHeadless);
						break;
				}
				
				_menu.Text = _name + " (" + stateLabel + ")";
				
				if (StateChange != null) {
					StateChange(this, EventArgs.Empty);
				}
			}
		}
		
		public bool Stopped {
			get {
				switch (_state) {
					case MachineState.MachineState_Running:
					case MachineState.MachineState_Paused:
					case MachineState.MachineState_Teleporting:
					case MachineState.MachineState_LiveSnapshotting:
					case MachineState.MachineState_TeleportingPausedVM:
					case MachineState.MachineState_TeleportingIn:
					case MachineState.MachineState_DeletingSnapshotOnline:
					case MachineState.MachineState_DeletingSnapshotPaused:
					
					case MachineState.MachineState_Starting:
					case MachineState.MachineState_Restoring:
					
					case MachineState.MachineState_Stopping:
						
					case MachineState.MachineState_Saving:
					
					case MachineState.MachineState_Stuck:
						return false;
					
					case MachineState.MachineState_PoweredOff:
					case MachineState.MachineState_Saved:
					case MachineState.MachineState_Teleported:
					case MachineState.MachineState_Aborted:
					case MachineState.MachineState_RestoringSnapshot:
					case MachineState.MachineState_DeletingSnapshot:
					case MachineState.MachineState_SettingUp:
					default:
						return true;
				}
			}
		}
		
		/* convenience method to toggle submenus, call like:
		 *     onlyShowSubmenus(Submenu.Start, Submenu.StartHeadless)
		 */
		private void onlyShowSubmenus(params Submenu[] toshow) {
			foreach (MenuItem m in _submenu) {
				m.Visible = false;
			}
			foreach (Submenu i in toshow) {
				_submenu[(int) i].Visible = true;
			}
		}
		
		/* disable submenus while an action is in progress & re-enable 
		 * once complete
		 */
		private void enableSubmenus(bool enable) {
			foreach (MenuItem m in _submenu) {
				m.Enabled = enable;
			}
		}
		
		// start the VM using the VirtualBox.dll COM interface
		private void MenuStartEventHandler(object sender, EventArgs e) {
			if (_session != null) {
				return;
			}
			enableSubmenus(false);
			_session = new Session();
			_virtualbox.OpenRemoteSession((Session) _session, _id, "gui", "");
		}
		
		// start the VM in headless mode
		private void MenuStartHeadlessEventHandler(object sender, EventArgs e) {
			if (_session != null) {
				return;
			}
			enableSubmenus(false);
			
			/* Using OpenRemoteSession() to initiate 'headless' VM execution causes the 
			 * VM to execute in a cmd window.  Virtualbox has a 'DETACHED' flag internally 
			 * that allows execution without a cmd window, but doesn't expose it via the 
			 * COM API.  To execute a VM headless without a cmd window, we instead execute 
			 * the VBoxHeadless.exe executable directly.
			 */
			
			/*
			_session = new Session();
			_virtualbox.OpenRemoteSession((Session) _session, _id, "headless", "");
			return;
			*/
			
			string path = Environment.GetEnvironmentVariable("VBOX_INSTALL_PATH");
			Process p = new Process();
			p.StartInfo.FileName = path + "VBoxHeadless.exe";
			p.StartInfo.Arguments = "--startvm " + _id + " --vrdp=config";
			p.StartInfo.UseShellExecute = false;
			p.Start();
			
			enableSubmenus(true);
		}
		
		// stop the VM
		private void MenuStopEventHandler(object sender, EventArgs e) {
			if (_session != null) {
				return;
			}
			enableSubmenus(false);
			_session = new Session();
			_virtualbox.OpenExistingSession((Session) _session, _id);
			_session.Console.PowerDown();
			_session.Close();
			_session = null;
			enableSubmenus(true);
		}
		
		// stop the VM & save its state
		private void MenuSaveEventHandler(object sender, EventArgs e) {
			if (_session != null) {
				return;
			}
			enableSubmenus(false);
			_session = new Session();
			_virtualbox.OpenExistingSession((Session) _session, _id);
			_session.Console.SaveState();
			_session.Close();
			_session = null;
			enableSubmenus(true);
		}
		
		private IMachine GetMachine() {
			foreach (IMachine m in _virtualbox.Machines) {
				if (m.Id == _id) {
					return m;
				}
			}
			throw new Exception("Machine '" + _id + "' doesn't exist");
		}
		
		public void OnMachineStateChange(string machineId, MachineState state) {
			if (machineId != _id) {
				return;
			}
			this.State = state;
		}
		
		// listen for VM startup and immediately close our remote session
		public void OnSessionStateChange(string machineId, SessionState state) {
			if (machineId != _id) {
				return;
			}
			if (state == SessionState.SessionState_Open) {
				_session.Close();
				_session = null;
				enableSubmenus(true);
			}
		}
		
		// unused callbacks
		public void OnMachineDataChange(string machineId) {}
		public void OnExtraDataChange(string machineId, string key, string value) {}
		public void OnMediumRegistered(string mediumId, DeviceType type, int registered) {}
		public void OnMachineRegistered(string machineId, int registered) {}
		public void OnSnapshotTaken(string machineId, string snapshotId) {}
		public void OnSnapshotDeleted(string machineId, string snapshotId) {}
		public void OnSnapshotChange(string machineId, string snapshotId) {}
		public void OnGuestPropertyChange(string machineId, string name, string value, string flags) {}
		public int OnExtraDataCanChange(string machineId, string key, string value, out string error) {
			error = null;
			return 1;
		}
	}
	
	[STAThread]
	static void Main(string[] args) {
		// find Virtualbox's install path so we can run executables
		string path = Environment.GetEnvironmentVariable("VBOX_INSTALL_PATH");
		if (path == null) {
			// fall back to looking in the registry
			path = (string) Microsoft.Win32.Registry.GetValue(
				"HKEY_LOCAL_MACHINE\\SOFTWARE\\Oracle\\VirtualBox", "InstallDir", null);
			
			if (path != null) {
				Environment.SetEnvironmentVariable("VBOX_INSTALL_PATH", path);
			}
			else {
				Console.WriteLine("VBOX_INSTALL_PATH environment variable is not set");
				return;
			}
		}
		
		Virtualtray v = new Virtualtray();
		Application.Run(v);
	}
}

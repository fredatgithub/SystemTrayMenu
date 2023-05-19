﻿// <copyright file="SingleAppInstance.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace SystemTrayMenu.Utilities
{
    using System;
    using System.Diagnostics;
    using System.Linq;
#if TODO //HOTKEY
    using SystemTrayMenu.UserInterface.HotkeyTextboxControl;
#endif

    internal static class SingleAppInstance
    {
        internal static bool Initialize()
        {
            bool success = true;

            try
            {
                foreach (Process p in Process.GetProcessesByName(
                       Process.GetCurrentProcess().ProcessName).
                       Where(s => s.Id != Environment.ProcessId))
                {
                    if (Properties.Settings.Default.SendHotkeyInsteadKillOtherInstances)
                    {
#if TODO // HOTKEY
                        Key modifiers = HotkeyControl.HotkeyModifiersFromString(Properties.Settings.Default.HotKey);
                        Key hotkey = HotkeyControl.HotkeyFromString(Properties.Settings.Default.HotKey);

                        try
                        {
                            List<VirtualKeyCode> virtualKeyCodesModifiers = new();
                            foreach (string key in modifiers.ToString().ToUpperInvariant().Split(", "))
                            {
                                if (key == "NONE")
                                {
                                    continue;
                                }

                                VirtualKeyCode virtualKeyCode = VirtualKeyCode.LWIN;
                                virtualKeyCode = key switch
                                {
                                    "ALT" => VirtualKeyCode.MENU,
                                    _ => (VirtualKeyCode)Enum.Parse(
                                                 typeof(VirtualKeyCode), key.ToUpperInvariant()),
                                };
                                virtualKeyCodesModifiers.Add(virtualKeyCode);
                            }

                            VirtualKeyCode virtualKeyCodeHotkey = 0;
                            if (Enum.IsDefined(typeof(VirtualKeyCode), (int)hotkey))
                            {
                                virtualKeyCodeHotkey = (VirtualKeyCode)(int)hotkey;
                            }

                            new InputSimulator().Keyboard.ModifiedKeyStroke(virtualKeyCodesModifiers, virtualKeyCodeHotkey);

                            success = false;
                        }
                        catch (Exception ex)
                        {
                            Log.Warn($"Send hoktey {Properties.Settings.Default.HotKey} to other instance failed", ex);
                        }
#endif
                    }

                    if (!Properties.Settings.Default.SendHotkeyInsteadKillOtherInstances)
                    {
                        try
                        {
                            if (!p.CloseMainWindow())
                            {
                                p.Kill();
                            }

                            p.WaitForExit();
                            p.Close();
                        }
                        catch (Exception ex)
                        {
                            Log.Error("Run as single instance failed", ex);
                            success = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Run as single instance failed", ex);
            }

            return success;
        }
    }
}

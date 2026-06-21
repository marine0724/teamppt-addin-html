using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Office.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace TeampptAddin
{
    /// <summary>
    /// 패널 생명주기 전담. "창마다 CTP 1개"의 모든 진실을 소유한다.
    /// 키 = DocumentWindow.HWND(int). 해제는 항상 3종 세트:
    /// Delete() + Marshal.ReleaseComObject + dict.Remove.
    /// </summary>
    public sealed class TaskPaneManager
    {
        private const string ToggleControlId = "teampptToggle";

        private readonly Dictionary<int, _CustomTaskPane> _panes
            = new Dictionary<int, _CustomTaskPane>();

        private ICTPFactory _factory;
        private IRibbonUI _ribbon;

        public void SetFactory(ICTPFactory factory)
        {
            _factory = factory;
            Logger.Log("Manager.SetFactory");
        }

        public void SetRibbon(IRibbonUI ribbon)
        {
            _ribbon = ribbon;
            Logger.Log("Manager.SetRibbon");
        }

        public bool IsVisible(int hwnd)
        {
            if (hwnd != 0 && _panes.TryGetValue(hwnd, out var pane))
            {
                try { return pane.Visible; } catch { return false; }
            }
            return false;
        }

        public void Toggle(int hwnd, bool pressed)
        {
            if (hwnd == 0) { Logger.Log("Manager.Toggle ignored: hwnd=0"); return; }

            if (_panes.TryGetValue(hwnd, out var existing))
            {
                try { existing.Visible = pressed; } catch (Exception ex) { Logger.Log($"Toggle visible failed: {ex.Message}"); }
                Logger.Log($"Manager.Toggle existing hwnd={hwnd} pressed={pressed}");
                return;
            }

            if (!pressed) { Logger.Log($"Manager.Toggle no-op hwnd={hwnd} (none, pressed=false)"); return; }
            if (_factory == null) { Logger.Log("Manager.Toggle abort: factory null"); return; }

            try
            {
                var pane = _factory.CreateCTP("TeampptAddin.TaskPaneHost", "TEAMPPT");
                pane.Width = 660;
                pane.DockPosition = MsoCTPDockPosition.msoCTPDockPositionRight;
                _panes[hwnd] = pane;
                SubscribeVisibleStateChange(pane);
                pane.Visible = true;
                Logger.Log($"Manager.Toggle created hwnd={hwnd}. count={_panes.Count}");
            }
            catch (Exception ex)
            {
                Logger.Log($"Manager.Toggle create FAILED hwnd={hwnd}: {ex}");
            }
        }

        public void SweepClosedWindows()
        {
            var live = new List<int>();
            try
            {
                var windows = Globals.Application?.Windows;
                if (windows != null)
                {
                    foreach (PowerPoint.DocumentWindow w in windows)
                    {
                        try { live.Add(w.HWND); } catch { }
                    }
                }
            }
            catch (Exception ex) { Logger.Log($"Sweep enum failed: {ex.Message}"); }

            var reclaim = WindowSweep.ToReclaim(new List<int>(_panes.Keys), live);
            foreach (var hwnd in reclaim)
            {
                ReleaseOne(hwnd);
                Logger.Log($"Manager.Sweep reclaimed hwnd={hwnd}. count={_panes.Count}");
            }
            if (reclaim.Count > 0) InvalidateButton();
        }

        public void ReleaseAll()
        {
            foreach (var hwnd in new List<int>(_panes.Keys))
                ReleaseOne(hwnd);
            _panes.Clear();
            Logger.Log("Manager.ReleaseAll done");
        }

        // 해제 3종 세트: Delete() + ReleaseComObject + dict.Remove
        private void ReleaseOne(int hwnd)
        {
            if (!_panes.TryGetValue(hwnd, out var pane)) return;
            try { pane.Delete(); } catch (Exception ex) { Logger.Log($"ReleaseOne Delete failed hwnd={hwnd}: {ex.Message}"); }
            try { Marshal.ReleaseComObject(pane); } catch { }
            _panes.Remove(hwnd);
        }

        private void SubscribeVisibleStateChange(_CustomTaskPane pane)
        {
            try
            {
                ((_CustomTaskPaneEvents_Event)pane).VisibleStateChange +=
                    _ => InvalidateButton();
            }
            catch (Exception ex) { Logger.Log($"SubscribeVisibleStateChange failed: {ex.Message}"); }
        }

        private void InvalidateButton()
        {
            try { _ribbon?.InvalidateControl(ToggleControlId); }
            catch (Exception ex) { Logger.Log($"InvalidateButton failed: {ex.Message}"); }
        }
    }
}

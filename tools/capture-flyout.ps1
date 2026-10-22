# Captures a Kitty flyout (e.g. the settings panel) without stealing focus or
# moving the cursor: the flyout is opened through UI Automation when closed,
# optionally scrolled with posted wheel messages, then captured with PrintWindow.
#
# Usage:
#   powershell -NoProfile -File capture-flyout.ps1 -AutomationId SettingsButton -OutDir ..\.impeccable\review -Prefix settings
#   powershell -NoProfile -File capture-flyout.ps1 -AutomationId SettingsButton -OutDir ..\.impeccable\review -Prefix settings -ScrollSteps 8
param(
    [Parameter(Mandatory=$true)][string]$AutomationId,
    [Parameter(Mandatory=$true)][string]$OutDir,
    [Parameter(Mandatory=$true)][string]$Prefix,
    [string]$ProcessName = 'Kitty',
    [int]$ScrollSteps = 0,
    [int]$DelayMs = 350
)

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class Win32Flyout {
    public delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern int GetClassName(IntPtr hwnd, StringBuilder sb, int max);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@

[Win32Flyout]::SetProcessDPIAware() | Out-Null
if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }

$proc = Get-Process -Name $ProcessName -ErrorAction Stop | Select-Object -First 1
$pidWanted = [uint32]$proc.Id
$main = $proc.MainWindowHandle

# Every visible window of the process other than the main one is a popup/flyout.
function Find-Popup {
    $list = New-Object System.Collections.Generic.List[IntPtr]
    $cb = [Win32Flyout+EnumProc]{
        param($hwnd, $lparam)
        $owner = [uint32]0
        [Win32Flyout]::GetWindowThreadProcessId($hwnd, [ref]$owner) | Out-Null
        if ($owner -ne $pidWanted -or $hwnd -eq $main -or -not [Win32Flyout]::IsWindowVisible($hwnd)) { return $true }
        # tooltips are popups too; a flyout is never this small
        $r = New-Object Win32Flyout+RECT
        [Win32Flyout]::GetWindowRect($hwnd, [ref]$r) | Out-Null
        if (($r.Right - $r.Left) -lt 200 -or ($r.Bottom - $r.Top) -lt 200) { return $true }
        $list.Add($hwnd)
        return $true
    }
    [Win32Flyout]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
    if ($list.Count -eq 0) { return $null }

    # Largest visible popup wins: tooltips and other small popups can be up at the
    # same time as the flyout, and EnumWindows order is not reliable.
    $best = $list[0]
    $bestArea = 0
    foreach ($hwnd in $list) {
        $rect = New-Object Win32Flyout+RECT
        [Win32Flyout]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
        $area = ($rect.Right - $rect.Left) * ($rect.Bottom - $rect.Top)
        if ($area -gt $bestArea) { $bestArea = $area; $best = $hwnd }
    }
    return $best
}

$popup = Find-Popup
if ($null -eq $popup) {
    # Re-invoking an open flyout closes it, so only click when it is closed.
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)
    $win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
    if ($null -eq $win) { Write-Error 'no automation window for the process'; exit 1 }
    $idCond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $AutomationId)
    $el = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $idCond)
    if ($null -eq $el) { Write-Error "AutomationId '$AutomationId' not found"; exit 1 }
    $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Write-Output "invoked $AutomationId"
    for ($i = 0; $i -lt 20 -and $null -eq $popup; $i++) {
        Start-Sleep -Milliseconds $DelayMs
        $popup = Find-Popup
    }
} else { Write-Output 'flyout already open' }
if ($null -eq $popup) { Write-Error 'flyout did not open'; exit 1 }

$pr = New-Object Win32Flyout+RECT
[Win32Flyout]::GetWindowRect($popup, [ref]$pr) | Out-Null
Write-Output ("popup {0}x{1} at {2},{3}" -f ($pr.Right - $pr.Left), ($pr.Bottom - $pr.Top), $pr.Left, $pr.Top)

if ($ScrollSteps -gt 0) {
    # WM_MOUSEWHEEL (0x020A): wParam hi-word = delta, lParam = screen coordinates.
    $cx = [int](($pr.Left + $pr.Right) / 2)
    $cy = [int](($pr.Top + $pr.Bottom) / 2)
    for ($i = 0; $i -lt $ScrollSteps; $i++) {
        $wp = [IntPtr]([int](-120) -shl 16)
        $lp = [IntPtr]((($cy -band 0xFFFF) -shl 16) -bor ($cx -band 0xFFFF))
        [Win32Flyout]::PostMessage($popup, 0x020A, $wp, $lp) | Out-Null
        Start-Sleep -Milliseconds 80
    }
    Start-Sleep -Milliseconds 250
}

$w = $pr.Right - $pr.Left; $h = $pr.Bottom - $pr.Top
$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
[Win32Flyout]::PrintWindow($popup, $hdc, 2) | Out-Null
$g.ReleaseHdc($hdc); $g.Dispose()
$out = Join-Path $OutDir ("{0}.png" -f $Prefix)
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Output "saved $out"

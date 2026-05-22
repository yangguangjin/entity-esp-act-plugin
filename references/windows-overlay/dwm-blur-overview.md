# Extended Window Styles (Winuser.h) - Win32 apps | Microsoft Learn

Source: https://learn.microsoft.com/en-us/windows/win32/dwm/blur-ovw

This browser is no longer supported.

Upgrade to Microsoft Edge to take advantage of the latest features, security updates, and technical support.

Note

Access to this page requires authorization. You can try [signing in](#) or changing directories.

Access to this page requires authorization. You can try changing directories.

# Extended Window Styles

## In this article

The following are the extended window styles, these can be used along with the [**CreateWindowExA**](/en-us/windows/win32/api/winuser/nf-winuser-createwindowexa)/[**CreateWindowExW**](/en-us/windows/win32/api/winuser/nf-winuser-createwindowexw) functions.

| Constant/value | Description |
| --- | --- |
| **WS\_EX\_ACCEPTFILES**  0x00000010L | The window accepts drag-drop files. |
| **WS\_EX\_APPWINDOW**  0x00040000L | Forces a top-level window onto the taskbar when the window is visible. |
| **WS\_EX\_CLIENTEDGE**  0x00000200L | The window has a border with a sunken edge. |
| **WS\_EX\_COMPOSITED**  0x02000000L | Paints all descendants of a window in bottom-to-top painting order using double-buffering. Bottom-to-top painting order allows a descendent window to have translucency (alpha) and transparency (color-key) effects, but only if the descendent window also has the WS\_EX\_TRANSPARENT bit set. Double-buffering allows the window and its descendents to be painted without flicker. This cannot be used if the window has a [class style](about-window-classes) of **CS\_OWNDC**, **CS\_CLASSDC**, or **CS\_PARENTDC**.   **Windows 2000:** This style is not supported. |
| **WS\_EX\_CONTEXTHELP**  0x00000400L | The title bar of the window includes a question mark. When the user clicks the question mark, the cursor changes to a question mark with a pointer. If the user then clicks a child window, the child receives a [**WM\_HELP**](../shell/wm-help) message. The child window should pass the message to the parent window procedure, which should call the [**WinHelp**](/en-us/windows/desktop/api/winuser/nf-winuser-winhelpa) function using the **HELP\_WM\_HELP** command. The Help application displays a pop-up window that typically contains help for the child window.  **WS\_EX\_CONTEXTHELP** cannot be used with the **WS\_MAXIMIZEBOX** or **WS\_MINIMIZEBOX** styles. |
| **WS\_EX\_CONTROLPARENT**  0x00010000L | The window itself contains child windows that should take part in dialog box navigation. If this style is specified, the dialog manager recurses into children of this window when performing navigation operations such as handling the TAB key, an arrow key, or a keyboard mnemonic. |
| **WS\_EX\_DLGMODALFRAME**  0x00000001L | The window has a double border; the window can, optionally, be created with a title bar by specifying the **WS\_CAPTION** style in the *dwStyle* parameter. |
| **WS\_EX\_LAYERED**  0x00080000L | The window is a [layered window](window-features). This style cannot be used if the window has a [class style](about-window-classes) of either **CS\_OWNDC** or **CS\_CLASSDC**.  **Windows 8:** The **WS\_EX\_LAYERED** style is supported for top-level windows and child windows. Previous Windows versions support **WS\_EX\_LAYERED** only for top-level windows. |
| **WS\_EX\_LAYOUTRTL**  0x00400000L | If the shell language is Hebrew, Arabic, or another language that supports reading order alignment, the horizontal origin of the window is on the right edge. Increasing horizontal values advance to the left. |
| **WS\_EX\_LEFT**  0x00000000L | The window has generic left-aligned properties. This is the default. |
| **WS\_EX\_LEFTSCROLLBAR**  0x00004000L | If the shell language is Hebrew, Arabic, or another language that supports reading order alignment, the vertical scroll bar (if present) is to the left of the client area. For other languages, the style is ignored. |
| **WS\_EX\_LTRREADING**  0x00000000L | The window text is displayed using left-to-right reading-order properties. This is the default. |
| **WS\_EX\_MDICHILD**  0x00000040L | The window is a MDI child window. |
| **WS\_EX\_NOACTIVATE**  0x08000000L | A top-level window created with this style does not become the foreground window when the user clicks it. The system does not bring this window to the foreground when the user minimizes or closes the foreground window.  The window should not be activated through programmatic access or via keyboard navigation by accessible technology, such as Narrator.  To activate the window, use the [**SetActiveWindow**](/en-us/windows/desktop/api/winuser/nf-winuser-setactivewindow) or [**SetForegroundWindow**](/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow) function.  The window does not appear on the taskbar by default. To force the window to appear on the taskbar, use the **WS\_EX\_APPWINDOW** style. |
| **WS\_EX\_NOINHERITLAYOUT**  0x00100000L | The window does not pass its window layout to its child windows. |
| **WS\_EX\_NOPARENTNOTIFY**  0x00000004L | The child window created with this style does not send the [**WM\_PARENTNOTIFY

[Content truncated — showing first 5,000 of 8,602 chars. LLM summarization timed out. To fix: increase auxiliary.web_extract.timeout in config.yaml, or use a faster auxiliary model. Use browser_navigate for the full page.]
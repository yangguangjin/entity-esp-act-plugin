# SetWindowLongPtrA function (winuser.h) - Win32 apps | Microsoft Learn

Source: https://overlayplugin.github.io/docs/setup/

[Skip to Ask Learn chat experience](#) 

This browser is no longer supported.

Upgrade to Microsoft Edge to take advantage of the latest features, security updates, and technical support.

[Download Microsoft Edge](https://go.microsoft.com/fwlink/p/?LinkID=2092881 )   [More info about Internet Explorer and Microsoft Edge](https://learn.microsoft.com/en-us/lifecycle/faq/internet-explorer-microsoft-edge)

[Read in English](#)   [Edit](https://github.com/MicrosoftDocs/sdk-api/blob/docs/sdk-api-src/content/winuser/nf-winuser-setwindowlongptra.md)  

---

---

Note

Access to this page requires authorization. You can try [signing in](#) or changing directories.

Access to this page requires authorization. You can try changing directories.

# SetWindowLongPtrA function (winuser.h)

Changes an attribute of the specified window. The function also sets a value at the specified offset in the extra window memory.

**Note**  To write code that is compatible with both 32-bit and 64-bit versions of Windows, use **SetWindowLongPtr**. When compiling for 32-bit Windows, **SetWindowLongPtr** is defined as a call to the [SetWindowLong](/en-us/windows/desktop/api/winuser/nf-winuser-setwindowlonga) function.

## Syntax

```
LONG_PTR SetWindowLongPtrA( [in] HWND hWnd, [in] int nIndex, [in] LONG_PTR dwNewLong ); 
```

## Parameters

`[in] hWnd`

Type: **HWND**

A handle to the window and, indirectly, the class to which the window belongs. The **SetWindowLongPtr** function fails if the process that owns the window specified by the *hWnd* parameter is at a higher process privilege in the UIPI hierarchy than the process the calling thread resides in.

**Windows XP/2000:** The **SetWindowLongPtr** function fails if the window specified by the *hWnd* parameter does not belong to the same process as the calling thread.

`[in] nIndex`

Type: **int**

The zero-based offset to the value to be set. Valid values are in the range zero through the number of bytes of extra window memory, minus the size of a **LONG\_PTR**. To set any other value, specify one of the following values.

| Value | Meaning |
| --- | --- |
| **GWL\_EXSTYLE**  -20 | Sets a new [extended window style](/en-us/windows/desktop/winmsg/extended-window-styles). |
| **GWLP\_HINSTANCE**  -6 | Sets a new application instance handle. |
| **GWLP\_HWNDPARENT**  -8 | Sets a new owner for a top-level window. |
| **GWLP\_ID**  -12 | Sets a new identifier of the child window. The window cannot be a top-level window. |
| **GWL\_STYLE**  -16 | Sets a new [window style](/en-us/windows/desktop/winmsg/window-styles). |
| **GWLP\_USERDATA**  -21 | Sets the user data associated with the window. This data is intended for use by the application that created the window. Its value is initially zero. |
| **GWLP\_WNDPROC**  -4 | Sets a new address for the window procedure. |

The following values are also available when the *hWnd* parameter identifies a dialog box.

| Value | Meaning |
| --- | --- |
| **DWLP\_DLGPROC**  DWLP\_MSGRESULT + sizeof(LRESULT) | Sets the new pointer to the dialog box procedure. |
| **DWLP\_MSGRESULT**  0 | Sets the return value of a message processed in the dialog box procedure. |
| **DWLP\_USER**  DWLP\_DLGPROC + sizeof(DLGPROC) | Sets new extra information that is private to the application, such as handles or pointers. |

`[in] dwNewLong`

Type: **LONG\_PTR**

The replacement value.

## Return value

Type: **LONG\_PTR**

If the function succeeds, the return value is the previous value of the specified offset.

If the function fails, the return value is zero. To get extended error information, call [GetLastError](/en-us/windows/desktop/api/errhandlingapi/nf-errhandlingapi-getlasterror).

If the previous value is zero and the function succeeds, the return value is zero, but the function does not clear the last error information. To determine success or failure, clear the last error information by calling [SetLastError](/en-us/windows/desktop/api/errhandlingapi/nf-errhandlingapi-setlasterror) with 0, then call **SetWindowLongPtr**. Function failure will be indicated by a return value of zero and a [GetLastError](/en-us/windows/desktop/api/errhandlingapi/nf-errhandlingapi-getlasterror) result that is nonzero.

## Remarks

Certain window data is cached, so changes you make using **SetWindowLongPtr** will not take effect until you call the [SetWindowPos](/en-us/windows/desktop/api/winuser/nf-winuser-setwindowpos) function.

If you use **SetWindowLongPtr** with the **GWLP\_WNDPROC** index to replace the window procedure, the window procedure must conform to the guidelines specified in the description of the [WindowProc](/en-us/windows/win32/api/winuser/nc-winuser-wndproc) callback function.

If you use **SetWindowLongPtr** with the **DWLP\_MSGRESULT** index to set the return value for a message processed by a dialog box procedure, the dialog box procedure should return **TRUE** directly afterward. Otherwise, if you call any function that results in your dialog box procedure 

[Content truncated — showing first 5,000 of 8,480 chars. LLM summarization timed out. To fix: increase auxiliary.web_extract.timeout in config.yaml, or use a faster auxiliary model. Use browser_navigate for the full page.]
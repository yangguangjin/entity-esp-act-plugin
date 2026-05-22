# FFXIV ACT Setup Guide | docs

Source: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowlongptra

# [docs](https://overlayplugin.github.io/docs/)

# FFXIV ACT Setup Guide

This guide is intended to get a FFXIV player setup with ACT and an overlay for parsing purposes and be able to upload logs to the FFLogs website.

*Last updated: 2026-04-09*

![act_logo](resources/act_logo.png)

![act_logo](resources/act_logo.png)

## Contents

## Installing ACT

Navigate to the [ACT website](https://advancedcombattracker.com/), click on the **Download** page tab, then click on the `Advanced Combat Tracker - Setup` link to download the ACT installation program.

`Advanced Combat Tracker - Setup`

![Downloading ACT](/docs/setup/resources/act_download.png)

![Downloading ACT](/docs/setup/resources/act_download.png)

Find the `ACTv3-Setup` executable in your downloads and run it to begin the installation (If you get a User Account Control prompt, click yes).

`ACTv3-Setup`

![Open the ACTv3-Setup.exe File](/docs/setup/resources/actv3_setup.png)

![Open the ACTv3-Setup.exe File](/docs/setup/resources/actv3_setup.png)

The setup program will ask you for the installation location and start menu folder (You can leave the default options). Click **Install** then **Close** to complete the installation.

![ACT Installation Wizard](/docs/setup/resources/act_installation.png)

![ACT Installation Wizard](/docs/setup/resources/act_installation.png)

## FFXIV ACT Plugin

Upon first running ACT, it will prompt you with the Startup Wizard. If you forget to download a parsing plugin, ACT will prompt you again the next time you run it, or you can manually open the wizard by going to **Options** > **Miscellaneous** > **Show Startup Wizard**.

![Where to Find the Startup Wizard in ACT](/docs/setup/resources/startup_wizard.png)

![Where to Find the Startup Wizard in ACT](/docs/setup/resources/startup_wizard.png)

In the **Parsing Plugin** section of the startup wizard, ensure `FFXIV Parsing Plugin` is selected from the dropdown, then click the `Download/Enable Plugin` button. You will receive an alert when the plugin has been added to ACT. Click **Ok** to dismiss it.

`FFXIV Parsing Plugin`
`Download/Enable Plugin`

![Installing the Parsing Plugin in ACT](/docs/setup/resources/parsing_plugin.png)

![Installing the Parsing Plugin in ACT](/docs/setup/resources/parsing_plugin.png)

Click **Next** to move to the log file section. ACT will ask if it will be used for Final Fantasy XIV. Select **Yes** to configure ACT logs for FFXIV.

![Log File Screen in the Wizard](/docs/setup/resources/log_file.png)

![Log File Screen in the Wizard](/docs/setup/resources/log_file.png)

Click **Next** to move to Startup Settings, then **Close** to accept the default settings and finish the startup wizard.

At this point `FFXIV_ACT_Plugin.dll` should be enabled in **Plugins** > **Plugin Listing**.

`FFXIV_ACT_Plugin.dll`

![Plugin Listing Tab](/docs/setup/resources/ffxiv_act_plugin.png)

![Plugin Listing Tab](/docs/setup/resources/ffxiv_act_plugin.png)

### Configuring FFXIV ACT Plugin

Previously, there were options to use other methods to capture network data, but as of patch 7.2 the only remaining option is Deucalion. Everything is included in the default plugin install, and no additional configuration steps are required.

##### Running as Admin

Most users will no longer need to run ACT as Admin. If you need to do so for some reason, you’ll need to run FFXIV as Admin as well.

##### Adding Firewall Exception

Due to Deucalion being the only valid option as of patch 7.2, firewall exceptions are no longer needed.

## OverlayPlugin

From the **Plugin Listing** tab, click on the `Get Plugins...` button near the upper right corner. This will open a window that will populate with available plugins for ACT.

`Get Plugins...`

![Get Plugins Button](/docs/setup/resources/get_plugins.png)

![Get Plugins Button](/docs/setup/resources/get_plugins.png)

In the **Get Plugins** window, select the `Overlay Plugin` option and click on `Download and Enable`. This will add the latest **OverlayPlugin** to ACT (the OverlayPlugin auto-updater may also run during this step).

`Overlay Plugin`
`Download and Enable`

![Get Plugins Window](/docs/setup/resources/get_plugins_window.png)

![Get Plugins Window](/docs/setup/resources/get_plugins_window.png)

The OverlayPlugin should now be setup. Click on the `X` to close the **Get Plugins** window.

`X`

**At this point it is recommended to restart ACT before continuing on.**

### Using OverlayPlugin to End Encounters

It is recommended to use OverlayPlugin’s in/out-of-combat detection to split encounters, rather than ACT’s less accurate behavior.

To do this, first find ACT’s encounter split timeout (Options > Main Table/Encounters > General).
It is recommended to disable it entirely by un-checking both `Number of seconds to wait` checkboxes:

`Number of seconds to wait`

![ACT Timeout Settings](/docs/setup/resources/act_timeout_settings.png)

![ACT Timeout Settings](/docs/setup/resources/act_timeout_settings.png)

Then, under Plugins >

[Content truncated — showing first 5,000 of 10,953 chars. LLM summarization timed out. To fix: increase auxiliary.web_extract.timeout in config.yaml, or use a faster auxiliary model. Use browser_navigate for the full page.]
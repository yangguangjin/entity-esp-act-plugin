# FFXIV ACT FAQ | docs

Source: https://overlayplugin.github.io/OverlayPlugin/devs/

# [docs](https://overlayplugin.github.io/docs/)

# FFXIV ACT FAQ

## TOC

## Important - Read These First

### Which OverlayPlugin fork am I supposed to use?

The correct version of OverlayPlugin is the [one in the OverlayPlugin org](https://github.com/OverlayPlugin/OverlayPlugin).
Neither the hibiyasleep nor ngld versions are currently maintained.

## Troubleshooting stuff

### My ACT isn’t showing any numbers. What can I do?

Go to Plugins > FFXIV Settings and click `Test game connection`.

`Test game connection`

**It complains about the firewall. What should I do?**  
Make sure you have a firewall exception for ACT. Also make sure that the
`.exe` file (visible under Details) and network type are correct.

`.exe`

If you’re sure the firewall exception is correct, disable the firewall. If that fixes your
problem, your exception is not correct. If that doesn’t fix your problem,
come to [the Discord](https://discord.gg/ahFKcmx) and mention that your parser doesn’t work
even when your firewall is disabled.

**It complains about memory signatures/addresses. What should I do?**  
This usually means your parser doesn’t work with your game version. Check the
[plugin page](https://github.com/ravahn/FFXIV_ACT_Plugin/releases)
for updates. If you already have the latest version, you’ll probably see a notice at the top
of this FAQ that an update is being worked on.

If you got this error with the latest parser and the game hasn’t been updated in the last several days,
go to [the Discord](https://discord.gg/ahFKcmx) and ask for help.

**It complains that it can’t find the game process.**  
Make sure you’re running the correct ACT `.exe`. `Advanced Combat Tracker.exe` for the game in DirectX 11
mode and not `ACTx86.exe`. FFXIV no longer supports DirectX 9 mode.

`.exe`
`Advanced Combat Tracker.exe`
`ACTx86.exe`

**It complains that no recent network traffic has been received.**  
Check if your firewall is interfering with the parser or if your VPN is causing problems.

If you use a VPN, try disabling the “High performance network parser”. Disabling it means ACT will cause
more CPU load but it might fix your problem.

### My overlay isn’t updating / not showing

First, check if your FFXIV plugin is the first entry in your plugins list (you can find that on ACT’s Plugins tab). If it isn’t move it to the top using the arrows and restart ACT. If that didn’t fix your issue, continue reading.

Make sure that ACT is showing DPS info on the Main tab. If that doesn’t work, you’ll have to fix
ACT / the parser first. See the [above section](#my-act-isnt-showing-any-numbers-what-can-i-do) for more information
about that.

Next, check the overlay log if it contains any of the following messages. The overlay log is the area below
your overlay settings.

`System.NullReferenceException - Object reference not set to an instance of an object.`
`Get Plugins`

`Could not load type 'RainbowMage.OverlayPlugin.EventSourseBase' from assembly 'OverlayPlugin.Core, ...'`  
You tried to use an addon or plugin which requires a newer version of OverlayPlugin, but you are using an obsolete fork.

`Could not load type 'RainbowMage.OverlayPlugin.EventSourseBase' from assembly 'OverlayPlugin.Core, ...'`

You either have to look for a different download / build of the addon/plugin you’re trying to use or
switch to the newest OverlayPlugin.

`System.TypeLoadException - Could not load type 'System.ValueTuple`
`3' from assembly 'System.ValueTuple, ...`

Finally, here are some typical issues which can lead to an overlay showing up but not updating:

`New`
`FFXIV_ACT_Plugin.dll`

### My overlay only shows if I’m out of the game / alt-tabbed

Set your game to run in Borderless mode. Fullscreen means the game has exclusive control of your screen and other
programs can’t draw over it.

Discord, Steam, etc. get around this by hooking into the game. We’re trying to avoid this which is why we use windows
to draw over the game.

### My overlay shows over the game but stops updating

This is usually an issue with AMD’s graphics driver. Make sure you disable AMD Chill or come to
[the Discord](https://discord.gg/ahFKcmx) and ask for help there.

### My overlay doesn’t sort by DPS

You have to enable sorting. Go to `Plugins` > `MiniParseEventSource` and set `Sort By` to `DPS`.

`Plugins`
`MiniParseEventSource`
`Sort By`
`DPS`

### OverlayPlugin failed because it couldn’t download something

Go to the OverlayPlugin tab. It’ll tell you more about the file it tried to download. You’ll be able to retry the download or manually download the required file there.

### OverlayPlugin complains that Newtonsoft.Json is outdated

Go to your ACT folder (that’s where your `Advanced Combat Tracker.exe` is) and delete `Newtonsoft.Json.dll`. That file isn’t part of ACT and most likely ended up there as part of a plugin. Please install plugins in `%AppData%\Advanced Combat Tracker\Plugins` or sub folders to avoid issues like this.

`Advanced Combat Tracker.exe`
`Newtonsoft.Json.dll`
`%

[Content truncated — showing first 5,000 of 11,394 chars. LLM summarization timed out. To fix: increase auxiliary.web_extract.timeout in config.yaml, or use a faster auxiliary model. Use browser_navigate for the full page.]
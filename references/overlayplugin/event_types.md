# GitHub - ravahn/FFXIV_ACT_Plugin: FFXIV Plugin for Advanced Combat Tracker · GitHub

Source: https://overlayplugin.github.io/OverlayPlugin/devs/event_types.html

## Navigation Menu

# Search code, repositories, users, issues, pull requests...

# Provide feedback

We read every piece of feedback, and take your input very seriously.

# Saved searches

## Use saved searches to filter your results more quickly

To see all available qualifiers, see our [documentation](https://docs.github.com/search-github/github-code-search/understanding-github-code-search-syntax).

# ravahn/FFXIV\_ACT\_Plugin

## Folders and files

| Name | | Name | Last commit message | Last commit date |
| --- | --- | --- | --- | --- |
| Latest commit   History[331 Commits](/ravahn/FFXIV_ACT_Plugin/commits/master/)   331 Commits | | |
| [Definitions](/ravahn/FFXIV_ACT_Plugin/tree/master/Definitions "Definitions") | | [Definitions](/ravahn/FFXIV_ACT_Plugin/tree/master/Definitions "Definitions") |  |  |
| [Overrides](/ravahn/FFXIV_ACT_Plugin/tree/master/Overrides "Overrides") | | [Overrides](/ravahn/FFXIV_ACT_Plugin/tree/master/Overrides "Overrides") |  |  |
| [Releases](/ravahn/FFXIV_ACT_Plugin/tree/master/Releases "Releases") | | [Releases](/ravahn/FFXIV_ACT_Plugin/tree/master/Releases "Releases") |  |  |
| [Releases\_KR](/ravahn/FFXIV_ACT_Plugin/tree/master/Releases_KR "Releases_KR") | | [Releases\_KR](/ravahn/FFXIV_ACT_Plugin/tree/master/Releases_KR "Releases_KR") |  |  |
| [README.md](/ravahn/FFXIV_ACT_Plugin/blob/master/README.md "README.md") | | [README.md](/ravahn/FFXIV_ACT_Plugin/blob/master/README.md "README.md") |  |  |
| View all files | | |

## Latest commit

## History

## Repository files navigation

# FFXIV\_ACT\_Plugin

The ACT Parsing Plugin for Final Fantasy XIV

This project is to track releases and issues for the ACT FFXIV Plugin. The source code is not currently public.

The DLL file included in this project enables the multi-game parser Advanced Combat Tracker (ACT) to process and display combat information from Final Fantasy XIV patch 6.0.

DISCLAIMER: Use of this program is at your own risk. Square Enix does not permit the use of any third party tools. They have stated in interviews that they did not view parsers as a significant problem unless players use them to harass other players, so the consensus is to not discuss parsers or DPS in-game at all.

I have started a Discord server for discussions regarding this plugin. All are welcome to join, but keep in mind this is for the purpose of developing and improving the plugin, and so may be moderated if discussions go wild.
<https://discord.gg/9tHJ7s2P3r>

Installation Instructions:

Download & install ACT. If you have an existing ACT installation, please remove any other plugins, to ensure there are not any conflicts to start with. ACT can be downloaded here:  
<http://advancedcombattracker.com/download.php>

Launch the ACT Startup Wizard. On the Parsing Plugin tab, click the 'Get Available parsing plugins' button. Choose #73 "FFXIV Parsing Plugin", and click the "Use this plugin" button. Continue the wizard or close it as desired.

The FFXIV\_ACT\_Plugin reads a combination of memory and network data from your local pc. It has three different ways of accessing the network data:

a) Default mode - by default, it will use a windows raw socket for the game's network data. This requires running ACT as a local administrator and adding a firewall rule to permit it to do so. For Windows Defender it will prompt you to add this rule, but for other firewalls you will need to configure it youtself.

b) Use Npcap kernel driver - If you prefer, you can install Npcap separately from ACT and the FFXIV plugin. This will allow the plugin to read network data and bypass unique firewall or vpn configurations, and does not require running ACT as an Administrator. After installing Npcap, the feature can be enabled by going to the ACT Plugins tab, then to the FFXIV ACT Plugin tab, and enabling the "Use Winpcap-compatible library" feature.

c) Use Deucalion Injection library - Selecting this option will inject a small DLL into the FFXIV game process, which hooks the game functions that receive and process network data, and makes it available to the FFXIV\_ACT\_Plugin over a named pipe. The Deucalion library is open source here: <https://github.com/ff14wed/deucalion>

## About

FFXIV Plugin for Advanced Combat Tracker

### Resources

### Uh oh!

There was an error while loading. Please reload this page.

There was an error while loading. Please reload this page.

### Stars

### Watchers

### Forks

## [Releases 184](/ravahn/FFXIV_ACT_Plugin/releases)

## [Packages 0](/users/ravahn/packages?repo_name=FFXIV_ACT_Plugin)

### Uh oh!

There was an error while loading. Please reload this page.

There was an error while loading. Please reload this page.

## [Contributors](/ravahn/FFXIV_ACT_Plugin/graphs/contributors)

### Uh oh!

There was an error while loading. Please reload this page.

There was an error while loading. Please reload this page.

## Footer

### Footer navigation
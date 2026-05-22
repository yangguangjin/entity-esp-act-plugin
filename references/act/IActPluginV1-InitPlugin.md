# Plugin Creation Tips · EQAditu/AdvancedCombatTracker Wiki · GitHub

Source: https://advancedcombattracker.com/apidoc/html/M_Advanced_Combat_Tracker_IActPluginV1_InitPlugin.htm

## Navigation Menu

# Search code, repositories, users, issues, pull requests...

# Provide feedback

We read every piece of feedback, and take your input very seriously.

# Saved searches

## Use saved searches to filter your results more quickly

To see all available qualifiers, see our [documentation](https://docs.github.com/search-github/github-code-search/understanding-github-code-search-syntax).

# Plugin Creation Tips

# Plugin Creation Tips

Plugins for ACT are normal .NET Framework(4.x) assemblies. They can either be pre-compiled (\*.dll / \*.exe), or ACT can compile plugin source code on-the-fly (\*.cs / \*.vb).

To be loaded by ACT, they must implement the plugin interface `Advanced_Combat_Tracker.`[`IActPluginV1`](https://advancedcombattracker.com/apidoc/html/T_Advanced_Combat_Tracker_IActPluginV1.htm). This interface consists of a main entry point, and a exit method to dispose resources in.

`Advanced_Combat_Tracker.`
`IActPluginV1`

To create plugins I suggest you use an IDE like Visual Studio.
Microsoft supplies, free of charge, Community versions of their Visual Studio suites. The only caveat is that you must link it to a Microsoft account so that it can activate/maintain the free license.

Documentation on ACT's API can be found [here](https://advancedcombattracker.com/apidoc/)([archive](https://advancedcombattracker.com/includes/page-download.php?id=8)). This contains the API in HTML format, and XML format. The HTML format should be obvious, the XML format can be specially used inside of IDEs to describe objects as you use them. Place the `Advanced Combat Tracker.XML` file with the ACT EXE file reference and Visual Studio will use it automatically. In other words, put the XML file in ACT's install folder for Intellisense to function.

`Advanced Combat Tracker.XML`

## Creating a new plugin

Once in the IDE, create a new project. A class library template is probably your best bet. Name the project whatever you want... it probably won't show up anywhere within ACT, but something identifiable to yourself would be good. Although when the plugin causes exceptions within ACT the Namespace and Class name will be shown. The new project creates `ClassLibrary1.Class1`, so you can go ahead and rename those things now if you wish. VS comes with handy abilities to rename objects without breaking references if you right-click them.

`ClassLibrary1.Class1`

The first thing you will want to do is add ACT as an assembly reference. In the **Solution Explorer**, right-click **References** and **Add Reference...** browse to where ACT, and hopefully the documentation XML, and select "*Advanced Combat Tracker.exe*". This will allow you access to the `Advanced_Combat_Tracker` namespace. You can add a `using Advanced_Combat_Tracker;` line to the top of the file to make typing easier.

`Advanced_Combat_Tracker`
`using Advanced_Combat_Tracker;`

As said previously, you must implement the interface `IActPluginV1`. This is done by adding a colon after your class's name and typing out the interface name. When the IDE recognizes the interface it will make a box appear to allow you to automatically create the interface member stubs within your class. (`Shift-Alt-F10` *or* `Ctrl-.` will open the option box)

`IActPluginV1`
`Shift-Alt-F10`
`Ctrl-.`

Once these methods are created, the plugin is technically ready for use, however it won't do anything. Creating plugin contents should be another post.

Each plugin should have only one class that implements this interface. When ACT scans the plugin, it will use one implementing class it finds and ignores the rest.

## Adding references not included by ACT

As previously mentioned, ACT plugins can come in two forms: DLLs and source files. If you make a \*.dll plugin, no special considerations have to be made. If you make a source file plugin, you may find yourself needing to put everything in one file, or make special assembly references. For the case of assembly references, ACT can parse special comment tags at the beginning of the source file to add as references.

This will add `System.dll` to the references from the Global Assembly Cache(GAC). If your reference is not in the GAC, you may need to supply a relative(from ACT) or absolute path. Assembly attributes will also be parsed from the source file to be shown in the plugin info panel when ACT loads them. You can see examples of them in the **AssemblyInfo.cs** file created in your project's **Properties** folder.

`System.dll`

If your referenced assembly is not in the GAC, you may have to do some extra work. You may find it easy to just put your referenced assembly in ACT's program folder but this is *strongly* discouraged as it can break other plugins looking for newer versions of common assemblies. The cleaner method is to subscribe to the `AppDomain.`[`AssemblyResolve`](https://docs.microsoft.com/en-us/dotnet/api/system.appdomain.assemblyresolve?view=netframework-4.8) event. This event will fire any time a plugin ref

[Content truncated — showing first 5,000 of 9,795 chars. LLM summarization timed out. To fix: increase auxiliary.web_extract.timeout in config.yaml, or use a faster auxiliary model. Use browser_navigate for the full page.]
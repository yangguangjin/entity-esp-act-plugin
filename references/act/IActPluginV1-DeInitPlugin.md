# IActPluginV1.InitPlugin Method

Source: https://advancedcombattracker.com/apidoc/html/M_Advanced_Combat_Tracker_IActPluginV1_DeInitPlugin.htm

Advanced Combat Tracker Plugin API

[Advanced Combat Tracker Plugin API](../html/N_Advanced_Combat_Tracker.htm "Advanced Combat Tracker Plugin API")

[Advanced\_Combat\_Tracker](../html/N_Advanced_Combat_Tracker.htm "Advanced_Combat_Tracker")

[IActPluginV1 Interface](../html/T_Advanced_Combat_Tracker_IActPluginV1.htm "IActPluginV1 Interface")

[IActPluginV1 Methods](../html/Methods_T_Advanced_Combat_Tracker_IActPluginV1.htm "IActPluginV1 Methods")

[DeInitPlugin Method](../html/M_Advanced_Combat_Tracker_IActPluginV1_DeInitPlugin.htm "DeInitPlugin Method")

[InitPlugin Method](../html/M_Advanced_Combat_Tracker_IActPluginV1_InitPlugin.htm "InitPlugin Method")

|  |
| --- |
| IActPluginV1InitPlugin Method |

Will be called when ACT starts the plugin

  
**Namespace:** [Advanced\_Combat\_Tracker](N_Advanced_Combat_Tracker.htm)  
**Assembly:** Advanced Combat Tracker (in Advanced Combat Tracker.exe) Version: 3.8.5.288

Syntax

C#

[Copy](# "Copy")

```
void InitPlugin TabPage pluginScreenSpace Label pluginStatusText
```

#### Parameters

pluginScreenSpace  [TabPage](https://learn.microsoft.com/dotnet/api/system.windows.forms.tabpage)
:   Provides the plugin with a default screen space in which to draw controls

pluginStatusText  [Label](https://learn.microsoft.com/dotnet/api/system.windows.forms.label)
:   Provides the plugin with a default label on the main plugins page to show status

See Also

#### Reference

[IActPluginV1 Interface](T_Advanced_Combat_Tracker_IActPluginV1.htm)

[Advanced\_Combat\_Tracker Namespace](N_Advanced_Combat_Tracker.htm)
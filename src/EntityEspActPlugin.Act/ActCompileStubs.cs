#if ENTITY_ESP_ACT_STUBS
using System;
using System.Windows.Forms;

namespace Advanced_Combat_Tracker
{
    public interface IActPluginV1
    {
        void InitPlugin(TabPage pluginScreenSpace, Label pluginStatusText);
        void DeInitPlugin();
    }

    public sealed class LogLineEventArgs : EventArgs
    {
        public string? logLine { get; set; }
    }

    public static class ActGlobals
    {
        public static readonly FormActMain oFormActMain = new FormActMain();
    }

    public sealed class FormActMain
    {
        public event Action<bool, LogLineEventArgs>? OnLogLineRead;

        public void RaiseLogLineRead(bool isImport, LogLineEventArgs logInfo)
        {
            OnLogLineRead?.Invoke(isImport, logInfo);
        }
    }
}
#endif

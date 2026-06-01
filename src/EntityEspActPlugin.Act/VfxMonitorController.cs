using System;
using EntityEspActPlugin.Core.Models;
using EntityEspActPlugin.Core.Services;

namespace EntityEspActPlugin.Act;

/// <summary>功能：按插件配置管理 VFX 实时内存服务和独立面板的生命周期。</summary>
public sealed class VfxMonitorController : IDisposable
{
    private readonly ActiveVfxMemoryService _service = new ActiveVfxMemoryService();
    private readonly Action<bool> _panelStateChanged;
    private readonly Action<string> _statusChanged;
    private VfxMonitorForm? _form;
    private bool _closingFromController;

    public VfxMonitorController(Action<bool> panelStateChanged, Action<string> statusChanged)
    {
        _panelStateChanged = panelStateChanged;
        _statusChanged = statusChanged;
    }

    public string StatusText => _service.StatusText;

    /// <summary>功能：根据配置开关启动或停止 active VFX 面板。</summary>
    public void ApplyConfig(EspConfig config)
    {
        if (config.ShowVfxMonitorPanel)
        {
            EnsureStarted(config);
            return;
        }

        Stop();
    }

    /// <summary>功能：关闭面板并停止 active VFX 读取服务。</summary>
    public void Stop()
    {
        CloseFormFromController();
        if (_service.IsRunning)
        {
            _service.Stop();
        }
    }

    public void Dispose()
    {
        Stop();
        _service.Dispose();
    }

    /// <summary>功能：确保 active VFX 服务已启用且面板已显示。</summary>
    private void EnsureStarted(EspConfig config)
    {
        if (!_service.IsRunning)
        {
            _service.EnsureRunning();
        }

        if (_form == null || _form.IsDisposed)
        {
            _form = new VfxMonitorForm(_service, config);
            _form.FormClosed += delegate
            {
                _form = null;
                if (!_closingFromController)
                {
                    _service.Stop();
                    _panelStateChanged(false);
                    _statusChanged("VFX monitor panel closed; " + _service.StatusText);
                }
            };
            _form.Show();
        }
        else
        {
            _form.ApplyConfig(config);
            if (!_form.Visible)
            {
                _form.Show();
            }
        }
    }

    /// <summary>功能：由配置/卸载路径关闭面板，避免把用户手动关闭误判成配置关闭。</summary>
    private void CloseFormFromController()
    {
        if (_form == null || _form.IsDisposed)
        {
            _form = null;
            return;
        }

        try
        {
            _closingFromController = true;
            _form.Close();
        }
        finally
        {
            _closingFromController = false;
            _form = null;
        }
    }
}

using System;

namespace EntityEspActPlugin.Core.Services;

public static class RelatedActLogPanelLayout
{
    public static float CalculatePanelHeight(int wrappedLineCount, float lineHeight, float clientHeight)
    {
        if (wrappedLineCount <= 0 || lineHeight <= 0f || clientHeight <= 84f)
        {
            return 0f;
        }

        var contentHeight = wrappedLineCount * lineHeight;
        return Math.Min(clientHeight - 84f, contentHeight + 12f);
    }
}

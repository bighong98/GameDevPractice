using System.Collections.Generic;

namespace TH.UI
{
    public interface ITooltipTokenProvider
    {
        void CollectTokens(in TooltipTokenContext context, IDictionary<string, TooltipTokenValue> tokens);
    }
}

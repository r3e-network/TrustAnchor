using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services;

namespace TrustAnchor
{
#pragma warning disable CS8604 // NEO Storage.Get returns nullable, handled by contract logic
#pragma warning disable CS8602 // NEO framework nullable dereferences are safe
#pragma warning disable CS8625 // Null literals for NEO framework compatibility
#pragma warning disable CS8603 // NEO framework returns are handled

    public partial class TrustAnchor : SmartContract
    {
        private static bool IsPaused()
        {
            return Storage.Get(Storage.CurrentContext, new byte[] { PREFIXPAUSED }) is not null;
        }
    }
}

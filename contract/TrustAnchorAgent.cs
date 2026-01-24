using System.Numerics;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;
using Neo.SmartContract.Framework.Attributes;

namespace TrustAnchor
{
#pragma warning disable CS8625 // Null literals for NEO framework compatibility
    [ManifestExtra("Author", "R3E Network")]
    [ManifestExtra("Email", "developer@r3e.network")]
    [ManifestExtra("Description", "TrustAnchor Agent Contract")]
    [ContractPermission("0xef4073a0f2b305a38ec4050e4d3d28bc40ea63f5", "transfer", "vote")]
        [ContractPermission("0xd2a4cff31913016155e38e474a2c06d08be276cf", "transfer")]
        [ContractPermission("0xfffdc93764dbaddd97c48f252a53ea4643faa3fd", "update")]
    public class TrustAnchorAgent : SmartContract
    {
        [InitialValue("[TODO]: ARGS", Neo.SmartContract.Framework.ContractParameterType.Hash160)]
        private static readonly UInt160 CORE = default;

        public static void Transfer(UInt160 to, BigInteger amount)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(CORE));
            ExecutionEngine.Assert(NEO.Transfer(Runtime.ExecutingScriptHash, to, amount));
        }

        public static void Sync()
        {
            ExecutionEngine.Assert(NEO.Transfer(Runtime.ExecutingScriptHash, Runtime.ExecutingScriptHash, 0));
        }

        public static void Claim()
        {
            ExecutionEngine.Assert(GAS.Transfer(Runtime.ExecutingScriptHash, CORE, GAS.BalanceOf(Runtime.ExecutingScriptHash), true));
        }

        public static void Vote(ECPoint target)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(CORE));
            ExecutionEngine.Assert(NEO.Vote(Runtime.ExecutingScriptHash, target));
        }

        public static void OnNEP17Payment(UInt160 from, BigInteger amount, object data)
        {
        }

        public static void Update(ByteString nefFile, string manifest)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness((UInt160)Contract.Call(CORE, "owner", CallFlags.All)));
            // Check if CORE contract is paused
            var isPaused = Contract.Call(CORE, "isPaused", CallFlags.ReadStates);
            ExecutionEngine.Assert(isPaused is null || isPaused.Equals(false));
            ContractManagement.Update(nefFile, manifest, null);
        }
    }
}

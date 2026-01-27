using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Numerics;
using System.Runtime.Loader;
using Neo;
using Neo.Cryptography.ECC;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.Extensions;
using Neo.SmartContract.Testing;
using Neo.SmartContract.Testing.Extensions;
using Neo.SmartContract.Testing.Native;
using Neo.VM;
using Neo.VM.Types;
using Neo.Wallets;

namespace TrustAnchor.Tests;

#pragma warning disable CS8600 // NEO test framework conversion
#pragma warning disable CS8603 // Test framework return handled

public sealed class TrustAnchorFixture
{
    private static readonly string CompilerDir = FindCompilerDirectory();

    private static string FindCompilerDirectory()
    {
        // Try multiple possible locations for the Neo compiler
        var possiblePaths = new[]
        {
            // Standard dotnet tool location
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".dotnet", "tools", ".store", "neo.compiler.csharp", "3.8.1",
                "neo.compiler.csharp", "3.8.1", "tools", "net9.0", "any"),
            // Alternative net8.0 location
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".dotnet", "tools", ".store", "neo.compiler.csharp", "3.8.1",
                "neo.compiler.csharp", "3.8.1", "tools", "net8.0", "any"),
            // GitHub Actions runner location
            Path.Combine("/home", "runner", ".dotnet", "tools", ".store",
                "neo.compiler.csharp", "3.8.1", "neo.compiler.csharp", "3.8.1",
                "tools", "net9.0", "any"),
            Path.Combine("/home", "runner", ".dotnet", "tools", ".store",
                "neo.compiler.csharp", "3.8.1", "neo.compiler.csharp", "3.8.1",
                "tools", "net8.0", "any"),
        };

        foreach (var path in possiblePaths)
        {
            var nccsPath = Path.Combine(path, "nccs.dll");
            if (File.Exists(nccsPath))
                return path;
        }

        // Fallback to first path (will fail with clear error message)
        return possiblePaths[0];
    }

    private static bool _resolverInitialized;

    private readonly TestEngine _engine;
    private readonly Signer _ownerSigner;
    private readonly Signer _userSigner;
    private readonly Signer _committeeSigner;
    private readonly Signer _validatorsSigner;
    private readonly Signer _otherSigner;
    private readonly Signer _strangerSigner;

    public UInt160 OwnerHash { get; }
    public ECPoint OwnerPubKey { get; }
    public UInt160 TrustHash { get; }
    public UInt160 UserHash { get; }
    public UInt160 OtherHash { get; }
    public UInt160 StrangerHash { get; }
    private readonly UInt160[] _agentHashes;

    public UInt160 AgentHash { get; }
    public IReadOnlyList<UInt160> AgentHashes => _agentHashes;
    public UInt160 AuthAgentHash { get; private set; } = UInt160.Zero;

    public TrustAnchorFixture()
    {
        _engine = new TestEngine();

        var ownerKey = new KeyPair(Enumerable.Repeat((byte)0x01, 32).ToArray());
        var userKey = new KeyPair(Enumerable.Repeat((byte)0x02, 32).ToArray());
        var otherKey = new KeyPair(Enumerable.Repeat((byte)0x03, 32).ToArray());
        var strangerKey = new KeyPair(Enumerable.Repeat((byte)0x05, 32).ToArray());
        OwnerPubKey = ownerKey.PublicKey;
        OwnerHash = ownerKey.PublicKeyHash;
        UserHash = userKey.PublicKeyHash;
        OtherHash = otherKey.PublicKeyHash;
        StrangerHash = strangerKey.PublicKeyHash;

        _ownerSigner = new Signer { Account = OwnerHash, Scopes = WitnessScope.CalledByEntry };
        _userSigner = new Signer { Account = UserHash, Scopes = WitnessScope.CalledByEntry };
        _committeeSigner = new Signer { Account = _engine.CommitteeAddress, Scopes = WitnessScope.CalledByEntry };
        _validatorsSigner = new Signer { Account = _engine.ValidatorsAddress, Scopes = WitnessScope.CalledByEntry };
        _otherSigner = new Signer { Account = OtherHash, Scopes = WitnessScope.CalledByEntry };
        _strangerSigner = new Signer { Account = StrangerHash, Scopes = WitnessScope.CalledByEntry };

        var trustSource = PatchTrustAnchorSource(OwnerHash);
        var (nef, manifest) = CompileSource(trustSource);
        var contract = _engine.Deploy<Neo.SmartContract.Testing.SmartContract>(nef, manifest, null, null);
        TrustHash = contract.Hash;

        var agentSource = TestAgentSource();
        var (agentNef, agentManifest) = CompileSource(agentSource);
        _agentHashes = new UInt160[21];
        for (int i = 0; i < _agentHashes.Length; i++)
        {
            var deployer = new UInt160(Enumerable.Repeat((byte)(i + 1), 20).ToArray());
            _engine.SetTransactionSigners(new[] { new Signer { Account = deployer, Scopes = WitnessScope.CalledByEntry } });
            var agentContract = _engine.Deploy<Neo.SmartContract.Testing.SmartContract>(agentNef, agentManifest, null, null);
            _agentHashes[i] = agentContract.Hash;
        }
        AgentHash = _agentHashes[0];
    }

    public T Call<T>(string operation, params object[] args)
    {
        return CallContract<T>(TrustHash, operation, args);
    }

    public void CallFrom(UInt160 signer, string operation, params object[] args)
    {
        _engine.SetTransactionSigners(new[] { ResolveSigner(signer) });
        Invoke(TrustHash, operation, args);
    }

    public T CallFrom<T>(UInt160 signer, string operation, params object[] args)
    {
        _engine.SetTransactionSigners(new[] { ResolveSigner(signer) });
        return CallContract<T>(TrustHash, operation, args);
    }

    public void RegisterAgent(UInt160 agent, ECPoint target, string name)
    {
        _engine.SetTransactionSigners(new[] { _ownerSigner });
        Invoke(TrustHash, "registerAgent", agent, target, name);
    }

    public void UpdateAgentNameById(int index, string name)
    {
        _engine.SetTransactionSigners(new[] { _ownerSigner });
        Invoke(TrustHash, "updateAgentNameById", new BigInteger(index), name);
    }

    public void UpdateAgentTargetById(int index, ECPoint target)
    {
        _engine.SetTransactionSigners(new[] { _ownerSigner });
        Invoke(TrustHash, "updateAgentTargetById", new BigInteger(index), target);
    }

    public void SetAgentVotingById(int index, BigInteger amount)
    {
        _engine.SetTransactionSigners(new[] { _ownerSigner });
        Invoke(TrustHash, "setAgentVotingById", new BigInteger(index), amount);
    }

    public void RegisterSingleAgentWithVoting(int index, BigInteger votingAmount)
    {
        RegisterAgent(_agentHashes[index], AgentCandidate(index), $"agent-{index}");
        SetAgentVotingById(index, votingAmount);
    }

    public void MintNeo(UInt160 to, BigInteger amount)
    {
        var funding = SelectNeoFundingAccount();
        var signer = funding == _engine.CommitteeAddress ? _committeeSigner : _validatorsSigner;
        _engine.SetTransactionSigners(new[] { signer });
        var result = _engine.Native.NEO.Transfer(funding, to, amount, null);
        if (result != true) throw new InvalidOperationException("NEO mint failed");
    }

    public void NeoTransfer(UInt160 from, UInt160 to, BigInteger amount)
    {
        _engine.SetTransactionSigners(new[] { ResolveSigner(from) });
        var result = _engine.Native.NEO.Transfer(from, to, amount, null);
        if (result != true) throw new InvalidOperationException("NEO transfer failed");
    }

    public void MintGas(UInt160 to, BigInteger amount)
    {
        var funding = SelectGasFundingAccount();
        var signer = funding == _engine.CommitteeAddress ? _committeeSigner : _validatorsSigner;
        _engine.SetTransactionSigners(new[] { signer });
        var result = _engine.Native.GAS.Transfer(funding, to, amount, null);
        if (result != true) throw new InvalidOperationException("GAS mint failed");
    }

    public void GasTransfer(UInt160 from, UInt160 to, BigInteger amount)
    {
        _engine.SetTransactionSigners(new[] { ResolveSigner(from) });
        var result = _engine.Native.GAS.Transfer(from, to, amount, null);
        if (result != true) throw new InvalidOperationException("GAS transfer failed");
    }

    public void InvokeNeoPayment(UInt160 from, BigInteger amount)
    {
        var property = _engine.GetType().GetProperty("OnGetCallingScriptHash",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property is null)
            throw new InvalidOperationException("OnGetCallingScriptHash not found on TestEngine");

        var original = property.GetValue(_engine);
        var handler = BuildCallingScriptHashHandler(property.PropertyType, GetNeoHash());

        property.SetValue(_engine, handler);
        try
        {
            Invoke(TrustHash, "onNEP17Payment", from, amount, new byte[0]);
        }
        finally
        {
            property.SetValue(_engine, original);
        }
    }

    public BigInteger GasBalance(UInt160 account)
    {
        return _engine.Native.GAS.BalanceOf(account) ?? BigInteger.Zero;
    }

    public BigInteger NeoBalance(UInt160 account)
    {
        return _engine.Native.NEO.BalanceOf(account) ?? BigInteger.Zero;
    }

    public UInt160 DeployAuthAgent()
    {
        var agentSource = AuthAgentSource().Replace("[TODO]: ARGS", TrustHash.ToString());
        var (agentNef, agentManifest) = CompileSource(agentSource);
        var deployer = new UInt160(Enumerable.Repeat((byte)0x42, 20).ToArray());
        _engine.SetTransactionSigners(new[] { new Signer { Account = deployer, Scopes = WitnessScope.CalledByEntry } });
        var agentContract = _engine.Deploy<Neo.SmartContract.Testing.SmartContract>(agentNef, agentManifest, null, null);
        AuthAgentHash = agentContract.Hash;
        return AuthAgentHash;
    }

    public UInt160 AuthAgentLastTransferTo()
    {
        return CallContract<UInt160>(AuthAgentHash, "lastTransferTo");
    }

    public BigInteger AuthAgentLastTransferAmount()
    {
        return CallContract<BigInteger>(AuthAgentHash, "lastTransferAmount");
    }

    public void DrainAgentTo(UInt160 agent, UInt160 to, BigInteger amount)
    {
        var signer = new Signer { Account = agent, Scopes = WitnessScope.CalledByEntry };
        _engine.SetTransactionSigners(new[] { signer });
        Invoke(agent, "transfer", to, amount);
    }

    public UInt160 AgentLastTransferTo(int index = 0)
    {
        return CallContract<UInt160>(_agentHashes[index], "lastTransferTo");
    }

    public BigInteger AgentLastTransferAmount(int index = 0)
    {
        return CallContract<BigInteger>(_agentHashes[index], "lastTransferAmount");
    }

    public ECPoint AgentLastVote(int index = 0)
    {
        return CallContract<ECPoint>(_agentHashes[index], "lastVote");
    }

    public ECPoint AgentCandidate(int index)
    {
        var keyBytes = new byte[32];
        keyBytes[^1] = (byte)(index + 1);
        return new KeyPair(keyBytes).PublicKey;
    }

    private UInt160 GetNeoHash()
    {
        var property = _engine.Native.NEO.GetType().GetProperty("Hash",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property is null)
            throw new InvalidOperationException("Hash not found on NEO native contract");
        return (UInt160)property.GetValue(_engine.Native.NEO)!;
    }

    private static Delegate BuildCallingScriptHashHandler(Type delegateType, UInt160 hash)
    {
        var invoke = delegateType.GetMethod("Invoke") ??
                     throw new InvalidOperationException("Calling script hash delegate missing Invoke");
        var parameters = invoke.GetParameters()
            .Select(p => Expression.Parameter(p.ParameterType, p.Name))
            .ToArray();
        var body = Expression.Convert(Expression.Constant(hash), invoke.ReturnType);
        return Expression.Lambda(delegateType, body, parameters).Compile();
    }

    private UInt160 SelectNeoFundingAccount()
    {
        var committeeBalance = _engine.Native.NEO.BalanceOf(_engine.CommitteeAddress) ?? BigInteger.Zero;
        var validatorsBalance = _engine.Native.NEO.BalanceOf(_engine.ValidatorsAddress) ?? BigInteger.Zero;
        if (committeeBalance > BigInteger.Zero) return _engine.CommitteeAddress;
        if (validatorsBalance > BigInteger.Zero) return _engine.ValidatorsAddress;
        throw new InvalidOperationException("No funding account with NEO balance found");
    }

    private UInt160 SelectGasFundingAccount()
    {
        var committeeBalance = _engine.Native.GAS.BalanceOf(_engine.CommitteeAddress) ?? BigInteger.Zero;
        var validatorsBalance = _engine.Native.GAS.BalanceOf(_engine.ValidatorsAddress) ?? BigInteger.Zero;
        if (committeeBalance > BigInteger.Zero) return _engine.CommitteeAddress;
        if (validatorsBalance > BigInteger.Zero) return _engine.ValidatorsAddress;
        throw new InvalidOperationException("No funding account with GAS balance found");
    }

    private Signer ResolveSigner(UInt160 account)
    {
        if (account == OwnerHash) return _ownerSigner;
        if (account == UserHash) return _userSigner;
        if (account == OtherHash) return _otherSigner;
        if (account == StrangerHash) return _strangerSigner;
        if (account == _engine.CommitteeAddress) return _committeeSigner;
        if (account == _engine.ValidatorsAddress) return _validatorsSigner;
        return new Signer { Account = account, Scopes = WitnessScope.CalledByEntry };
    }

    private T CallContract<T>(UInt160 contract, string operation, params object[] args)
    {
        var result = Invoke(contract, operation, args);
        return (T)TestExtensions.ConvertTo(result, typeof(T));
    }

    private static string TestAgentSource()
    {
        return @"
using System.Numerics;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;

public class TestAgent : SmartContract
{
    private const byte PrefixTransferTo = 0x01;
    private const byte PrefixTransferAmount = 0x02;
    private const byte PrefixVote = 0x03;

    public static void Transfer(UInt160 to, BigInteger amount)
    {
        Storage.Put(Storage.CurrentContext, new byte[] { PrefixTransferTo }, to);
        Storage.Put(Storage.CurrentContext, new byte[] { PrefixTransferAmount }, amount);
        ExecutionEngine.Assert(NEO.Transfer(Runtime.ExecutingScriptHash, to, amount));
    }

    public static void Vote(ECPoint target)
    {
        Storage.Put(Storage.CurrentContext, new byte[] { PrefixVote }, target);
    }

    public static UInt160 LastTransferTo()
    {
        var value = Storage.Get(Storage.CurrentContext, new byte[] { PrefixTransferTo });
        return value is null ? UInt160.Zero : (UInt160)(byte[])value;
    }

    public static BigInteger LastTransferAmount()
    {
        var value = Storage.Get(Storage.CurrentContext, new byte[] { PrefixTransferAmount });
        return value is null ? BigInteger.Zero : (BigInteger)value;
    }

    public static ECPoint LastVote()
    {
        var value = Storage.Get(Storage.CurrentContext, new byte[] { PrefixVote });
        return value is null ? null : (ECPoint)(byte[])value;
    }

    public static void OnNEP17Payment(UInt160 from, BigInteger amount, object data)
    {
    }
}
";
    }

    private static string AuthAgentSource()
    {
        return @"
using System.Numerics;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;

public class AuthAgent : SmartContract
{
    private const byte PrefixTransferTo = 0x01;
    private const byte PrefixTransferAmount = 0x02;

    [InitialValue(""[TODO]: ARGS"", ContractParameterType.Hash160)]
    private static readonly UInt160 CORE = default;

    public static void Transfer(UInt160 to, BigInteger amount)
    {
        Storage.Put(Storage.CurrentContext, new byte[] { PrefixTransferTo }, to);
        Storage.Put(Storage.CurrentContext, new byte[] { PrefixTransferAmount }, amount);
        ExecutionEngine.Assert(Runtime.CallingScriptHash == CORE);
        ExecutionEngine.Assert(NEO.Transfer(Runtime.ExecutingScriptHash, to, amount));
    }

    public static UInt160 LastTransferTo()
    {
        var value = Storage.Get(Storage.CurrentContext, new byte[] { PrefixTransferTo });
        return value is null ? UInt160.Zero : (UInt160)(byte[])value;
    }

    public static BigInteger LastTransferAmount()
    {
        var value = Storage.Get(Storage.CurrentContext, new byte[] { PrefixTransferAmount });
        return value is null ? BigInteger.Zero : (BigInteger)value;
    }

    public static void OnNEP17Payment(UInt160 from, BigInteger amount, object data)
    {
    }
}
";
    }

    private StackItem Invoke(UInt160 contract, string operation, params object[] args)
    {
        using var builder = new ScriptBuilder();
        builder.EmitDynamicCall(contract, operation, args);
        var script = new Script(builder.ToArray());
        return _engine.Execute(script, 0, null);
    }

    private static string PatchTrustAnchorSource(UInt160 ownerHash)
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var sourcePath = Path.Combine(repoRoot, "contract", "TrustAnchor.cs");
        var source = File.ReadAllText(sourcePath);
        return source.Replace("[TODO]: ARGS", ownerHash.ToString());
    }

    private static (NefFile nef, ContractManifest manifest) CompileSource(string source)
    {
        EnsureCompilerResolver();
        var compilerPath = Path.Combine(CompilerDir, "nccs.dll");
        if (!File.Exists(compilerPath))
            throw new InvalidOperationException($"nccs tool not found at {compilerPath}");

        var assembly = Assembly.LoadFrom(compilerPath);
        var optionsType = assembly.GetType("Neo.Compiler.CompilationOptions") ??
            throw new InvalidOperationException("CompilationOptions type not found");
        var engineType = assembly.GetType("Neo.Compiler.CompilationEngine") ??
            throw new InvalidOperationException("CompilationEngine type not found");

        var options = Activator.CreateInstance(optionsType);
        var engine = Activator.CreateInstance(engineType, options) ??
            throw new InvalidOperationException("Unable to create CompilationEngine");

        var tempFile = Path.Combine(Path.GetTempPath(), $"TrustAnchor.{Guid.NewGuid():N}.cs");
        File.WriteAllText(tempFile, source);

        var compileMethod = engineType.GetMethod("CompileSources", new[] { typeof(string[]) }) ??
            throw new InvalidOperationException("CompileSources method not found");
        var contexts = (IEnumerable)compileMethod.Invoke(engine, new object[] { new[] { tempFile } })!;
        var context = contexts.Cast<object>().First();

        var success = (bool)(context.GetType().GetProperty("Success")?.GetValue(context) ?? false);
        if (!success)
        {
            var diagnostics = context.GetType().GetProperty("Diagnostics")?.GetValue(context) as IEnumerable;
            var details = diagnostics is null
                ? "Compilation failed"
                : string.Join(Environment.NewLine, diagnostics.Cast<object>().Select(d => d.ToString()));
            throw new InvalidOperationException(details);
        }

        var nef = (NefFile)context.GetType().GetMethod("CreateExecutable")!.Invoke(context, null)!;
        var manifest = (ContractManifest)context.GetType().GetMethod("CreateManifest")!.Invoke(context, null)!;
        return (nef, manifest);
    }

    private static void EnsureCompilerResolver()
    {
        if (_resolverInitialized) return;
        _resolverInitialized = true;
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            var candidate = Path.Combine(CompilerDir, $"{name.Name}.dll");
            return File.Exists(candidate) ? Assembly.LoadFrom(candidate) : null;
        };
    }
}

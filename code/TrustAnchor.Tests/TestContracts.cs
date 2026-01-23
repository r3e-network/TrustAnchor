using System;
using System.Collections;
using System.IO;
using System.Linq;
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

public sealed class TrustAnchorFixture
{
    private static readonly string CompilerDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".dotnet",
        "tools",
        ".store",
        "neo.compiler.csharp",
        "3.8.1",
        "neo.compiler.csharp",
        "3.8.1",
        "tools",
        "net9.0",
        "any");

    private static bool _resolverInitialized;

    private readonly TestEngine _engine;
    private readonly Signer _ownerSigner;
    private readonly Signer _userSigner;
    private readonly Signer _committeeSigner;
    private readonly Signer _validatorsSigner;

    public UInt160 OwnerHash { get; }
    public ECPoint OwnerPubKey { get; }
    public UInt160 TrustHash { get; }
    public UInt160 UserHash { get; }

    public TrustAnchorFixture()
    {
        _engine = new TestEngine();

        var ownerKey = new KeyPair(Enumerable.Repeat((byte)0x01, 32).ToArray());
        var userKey = new KeyPair(Enumerable.Repeat((byte)0x02, 32).ToArray());
        OwnerPubKey = ownerKey.PublicKey;
        OwnerHash = ownerKey.PublicKeyHash;
        UserHash = userKey.PublicKeyHash;

        _ownerSigner = new Signer { Account = OwnerHash, Scopes = WitnessScope.CalledByEntry };
        _userSigner = new Signer { Account = UserHash, Scopes = WitnessScope.CalledByEntry };
        _committeeSigner = new Signer { Account = _engine.CommitteeAddress, Scopes = WitnessScope.CalledByEntry };
        _validatorsSigner = new Signer { Account = _engine.ValidatorsAddress, Scopes = WitnessScope.CalledByEntry };

        var trustSource = PatchTrustAnchorSource(OwnerHash);
        var (nef, manifest) = CompileSource(trustSource);
        var contract = _engine.Deploy<Neo.SmartContract.Testing.SmartContract>(nef, manifest, null, null);
        TrustHash = contract.Hash;
    }

    public T Call<T>(string operation, params object[] args)
    {
        var result = Invoke(TrustHash, operation, args);
        return (T)TestExtensions.ConvertTo(result, typeof(T));
    }

    public void SetAgent(int index, UInt160 agent)
    {
        _engine.SetTransactionSigners(new[] { _ownerSigner });
        Invoke(TrustHash, "setAgent", new BigInteger(index), agent);
    }

    public void MintNeo(UInt160 to, int amount)
    {
        var funding = SelectNeoFundingAccount();
        var signer = funding == _engine.CommitteeAddress ? _committeeSigner : _validatorsSigner;
        _engine.SetTransactionSigners(new[] { signer });
        var result = _engine.Native.NEO.Transfer(funding, to, amount, null);
        if (result != true) throw new InvalidOperationException("NEO mint failed");
    }

    public void NeoTransfer(UInt160 from, UInt160 to, int amount)
    {
        _engine.SetTransactionSigners(new[] { from == UserHash ? _userSigner : new Signer { Account = from, Scopes = WitnessScope.CalledByEntry } });
        var result = _engine.Native.NEO.Transfer(from, to, amount, null);
        if (result != true) throw new InvalidOperationException("NEO transfer failed");
    }

    private UInt160 SelectNeoFundingAccount()
    {
        var committeeBalance = _engine.Native.NEO.BalanceOf(_engine.CommitteeAddress) ?? BigInteger.Zero;
        var validatorsBalance = _engine.Native.NEO.BalanceOf(_engine.ValidatorsAddress) ?? BigInteger.Zero;
        if (committeeBalance > BigInteger.Zero) return _engine.CommitteeAddress;
        if (validatorsBalance > BigInteger.Zero) return _engine.ValidatorsAddress;
        throw new InvalidOperationException("No funding account with NEO balance found");
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
        var sourcePath = Path.Combine(repoRoot, "code", "TrustAnchor.cs");
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

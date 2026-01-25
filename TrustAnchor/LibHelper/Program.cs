using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Neo;
using Neo.VM;
using Neo.VM.Types;

namespace LibHelper
{
    public static class Program
    {
        static void Main(string[] args) => "OK!".Log();
        public static (List<S>, List<T>) Map2<R, S, T>(this IEnumerable<R> list, Func<R, S> fx, Func<R, T> fy) => (list.Select(v => fx(v)).ToList(), list.Select(v => fy(v)).ToList());
        public static bool HasBytes(this IEnumerable<byte[]> list, byte[] val) => list.Where(v => v.SequenceEqual(val)).Any();
        public static T FindBy<T>(this IEnumerable<(byte[], T)> list, byte[] val) => list.Where(v => v.Item1.SequenceEqual(val)).Single().Item2;
        public static T FindByOrDefault<T>(this IEnumerable<(byte[], T)> list, byte[] val) => list.Where(v => v.Item1.SequenceEqual(val)).SingleOrDefault().Item2;
        public static IEnumerable<T> Merge<T>(this IEnumerable<T> list, IEnumerable<T> other) => list.Merge(new Queue<T>(other), v => v is null);
        public static IEnumerable<T> Merge<T>(this IEnumerable<T> list, IEnumerable<T> other, Func<T, bool> f) => list.Merge(new Queue<T>(other), f);
        public static IEnumerable<T> Merge<T>(this IEnumerable<T> list, Queue<T> other, Func<T, bool> f) => list.Select(v => f(v) ? other.Dequeue() : v);

        public static void Log<T>(this T val) => Console.Error.WriteLine(val);
        public static void Out<T>(this T val) => Console.WriteLine(val);
        public static UInt160 ToU160(this StackItem val) => new UInt160(val.GetSpan());
        public static byte[] ToBytes(this StackItem val) => val.GetSpan().ToArray();
        public static IEnumerable<T> Nullize<T>(this IEnumerable<T> val) => val.Any() ? val : null;
        public static Neo.VM.Types.Array ToVMArray(this StackItem val) => (Neo.VM.Types.Array)val;
        public static Neo.VM.Types.Struct ToVMStruct(this StackItem val) => (Neo.VM.Types.Struct)val;
        public static void Assert(this bool val, string message = "Assertion failed")
        {
            if (val == false)
            {
                throw new InvalidOperationException(message);
            }
        }

        public static long ToTimestamp(this DateTime dt)
        {
            return new DateTimeOffset(dt).ToUnixTimeSeconds();
        }

        public static string ToHexString(this byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;
            var hex = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
            {
                hex.Append(b.ToString("x2"));
            }
            return hex.ToString();
        }

        public static byte[] MakeScript(this UInt160 contract, string method, params object[] args)
        {
            var sb = new ScriptBuilder();
            
            // Push arguments in reverse order
            foreach (var arg in args.Reverse())
            {
                switch (arg)
                {
                    case BigInteger bi:
                        sb.EmitPush(bi);
                        break;
                    case int i:
                        sb.EmitPush(i);
                        break;
                    case uint ui:
                        sb.EmitPush(ui);
                        break;
                    case long l:
                        sb.EmitPush(l);
                        break;
                    case byte[] bytes:
                        sb.EmitPush(bytes);
                        break;
                    case string s:
                        sb.EmitPush(Encoding.UTF8.GetBytes(s));
                        break;
                    case UInt160 u:
                        sb.EmitPush(u.GetSpan());
                        break;
                    case Neo.Cryptography.ECC.ECPoint ep:
                        sb.EmitPush(ep.EncodePoint(true));
                        break;
                    default:
                        throw new NotSupportedException($"Unsupported argument type: {arg.GetType()}");
                }
            }
            
            // Push method name
            sb.EmitPush(Encoding.UTF8.GetBytes(method));
            
            // Emit SYSCALL for contract call
            sb.EmitSysCall(0xf8236d1); // System.Contract.Call for neo-go
            
            return sb.ToArray();
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace CapriDesktop
{
    internal sealed class Embed : IDisposable
    {
        internal const int Size = 768;
        private const uint ApiVersion = 16;
        private const int LongTensor = 7;
        private const int ErrorsOnly = 3;
        private static readonly object gate = new object();
        private static IntPtr api, env;
        private static string bound;
        private static CreateStatusFn CreateStatus;
        private static GetErrorCodeFn GetErrorCode;
        private static GetErrorMessageFn GetErrorMessage;
        private static CreateEnvFn CreateEnv;
        private static CreateSessionFn CreateSession;
        private static RunFn Run;
        private static OutFn CreateSessionOptions, GetAllocatorWithDefaultOptions;
        private static HandleFn DisableCpuMemArena;
        private static ThreadsFn SetIntraOpNumThreads, SetInterOpNumThreads;
        private static CountFn SessionGetInputCount;
        private static NameFn SessionGetInputName, SessionGetOutputName;
        private static TensorFn CreateTensorWithDataAsOrtValue;
        private static HandleOutFn GetTensorMutableData, GetTensorTypeAndShape;
        private static CountFn GetDimensionsCount;
        private static DimensionsFn GetDimensions;
        private static MemoryFn CreateCpuMemoryInfo;
        private static FreeFn AllocatorFree;
        private static ReleaseFn ReleaseStatus, ReleaseMemoryInfo, ReleaseSession, ReleaseValue, ReleaseTensorTypeAndShapeInfo, ReleaseSessionOptions;

        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr ApiBaseFn();
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr GetApiFn(uint version);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr CreateStatusFn(int code, [MarshalAs(UnmanagedType.LPStr)] string message);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int GetErrorCodeFn(IntPtr status);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr GetErrorMessageFn(IntPtr status);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr CreateEnvFn(int level, [MarshalAs(UnmanagedType.LPStr)] string name, out IntPtr made);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr CreateSessionFn(IntPtr environment, [MarshalAs(UnmanagedType.LPWStr)] string path, IntPtr options, out IntPtr made);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr RunFn(IntPtr session, IntPtr options, IntPtr[] inputNames, IntPtr[] inputs, UIntPtr inputCount, IntPtr[] outputNames, UIntPtr outputCount, [In, Out] IntPtr[] outputs);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr OutFn(out IntPtr made);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr HandleFn(IntPtr handle);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr ThreadsFn(IntPtr options, int count);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr CountFn(IntPtr handle, out UIntPtr count);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr NameFn(IntPtr session, UIntPtr index, IntPtr allocator, out IntPtr name);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr TensorFn(IntPtr memory, IntPtr data, UIntPtr bytes, long[] shape, UIntPtr rank, int type, out IntPtr made);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr HandleOutFn(IntPtr handle, out IntPtr made);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr DimensionsFn(IntPtr info, [In, Out] long[] dimensions, UIntPtr count);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr MemoryFn(int allocator, int kind, out IntPtr made);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr FreeFn(IntPtr allocator, IntPtr pointer);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void ReleaseFn(IntPtr handle);

        private IntPtr session, memory;
        private readonly List<string> inputs = new List<string>();
        private IntPtr[] inputNames;
        private IntPtr[] outputNames;

        internal WordPiece Words { get; private set; }

        private Embed() { }

        internal static string Probe(string dll)
        {
            try
            {
                lock (gate)
                {
                    Bind(dll);
                    IntPtr status = CreateStatus(2, "probe");
                    if (status == IntPtr.Zero) return "no status came back";
                    int code = GetErrorCode(status);
                    string message = Marshal.PtrToStringAnsi(GetErrorMessage(status));
                    ReleaseStatus(status);
                    return code == 2 && message == "probe" ? null : "status " + code + " \"" + message + "\"";
                }
            }
            catch (Exception error) { return error.Message; }
        }

        internal static Embed Open(string dll, string model, string vocab, int threads)
        {
            Embed made = new Embed();
            made.Words = WordPiece.Load(vocab);
            lock (gate)
            {
                Bind(dll);
                if (env == IntPtr.Zero) Check(CreateEnv(ErrorsOnly, "capri-meaning", out env));
                IntPtr options;
                Check(CreateSessionOptions(out options));
                try
                {
                    Check(DisableCpuMemArena(options));
                    Check(SetIntraOpNumThreads(options, Math.Max(1, threads)));
                    Check(SetInterOpNumThreads(options, 1));
                    Check(CreateSession(env, model, options, out made.session));
                }
                finally { ReleaseSessionOptions(options); }
            }
            try
            {
                Check(CreateCpuMemoryInfo(0, 0, out made.memory));
                IntPtr allocator;
                Check(GetAllocatorWithDefaultOptions(out allocator));
                UIntPtr count;
                Check(SessionGetInputCount(made.session, out count));
                for (uint i = 0; i < count.ToUInt32(); i++) made.inputs.Add(made.Name(SessionGetInputName, i, allocator));
                made.inputNames = new IntPtr[made.inputs.Count];
                for (int i = 0; i < made.inputs.Count; i++)
                {
                    if (made.inputs[i] != "input_ids" && made.inputs[i] != "attention_mask" && made.inputs[i] != "token_type_ids") throw new InvalidDataException("The meaning model wants an input called " + made.inputs[i] + ".");
                    made.inputNames[i] = Utf8(made.inputs[i]);
                }
                made.outputNames = new[] { Utf8(made.Name(SessionGetOutputName, 0, allocator)) };
            }
            catch (Exception)
            {
                made.Dispose();
                throw;
            }
            return made;
        }

        private string Name(NameFn read, uint index, IntPtr allocator)
        {
            IntPtr name;
            Check(read(session, new UIntPtr(index), allocator, out name));
            try { return Marshal.PtrToStringAnsi(name); }
            finally { Check(AllocatorFree(allocator, name)); }
        }

        internal float[] Vector(string text, out float norm)
        {
            long[] ids = Words.Ids(text);
            long[] mask = new long[ids.Length];
            long[] kinds = new long[ids.Length];
            for (int i = 0; i < mask.Length; i++) mask[i] = 1;
            long[] shape = { 1, ids.Length };
            List<GCHandle> pinned = new List<GCHandle>();
            IntPtr[] values = new IntPtr[inputs.Count];
            IntPtr[] outputs = new IntPtr[1];
            try
            {
                for (int i = 0; i < inputs.Count; i++)
                {
                    long[] data = inputs[i] == "input_ids" ? ids : inputs[i] == "attention_mask" ? mask : kinds;
                    GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
                    pinned.Add(handle);
                    Check(CreateTensorWithDataAsOrtValue(memory, handle.AddrOfPinnedObject(), new UIntPtr((ulong)data.Length * 8), shape, new UIntPtr(2), LongTensor, out values[i]));
                }
                Check(Run(session, IntPtr.Zero, inputNames, values, new UIntPtr((uint)values.Length), outputNames, new UIntPtr(1), outputs));
                IntPtr info;
                Check(GetTensorTypeAndShape(outputs[0], out info));
                long[] dimensions;
                try
                {
                    UIntPtr rank;
                    Check(GetDimensionsCount(info, out rank));
                    dimensions = new long[rank.ToUInt32()];
                    Check(GetDimensions(info, dimensions, rank));
                }
                finally { ReleaseTensorTypeAndShapeInfo(info); }
                if (dimensions.Length != 3 || dimensions[0] != 1 || dimensions[1] != ids.Length || dimensions[2] != Size) throw new InvalidDataException("The meaning model answered in an unexpected shape.");
                IntPtr numbers;
                Check(GetTensorMutableData(outputs[0], out numbers));
                float[] vector = new float[Size];
                Marshal.Copy(numbers, vector, 0, Size);
                double total = 0;
                foreach (float value in vector) total += value * value;
                norm = (float)Math.Sqrt(total);
                if (norm > 0) for (int i = 0; i < vector.Length; i++) vector[i] /= norm;
                return vector;
            }
            finally
            {
                if (outputs[0] != IntPtr.Zero) ReleaseValue(outputs[0]);
                foreach (IntPtr value in values) if (value != IntPtr.Zero) ReleaseValue(value);
                foreach (GCHandle handle in pinned) handle.Free();
            }
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (session != IntPtr.Zero) { ReleaseSession(session); session = IntPtr.Zero; }
                if (memory != IntPtr.Zero) { ReleaseMemoryInfo(memory); memory = IntPtr.Zero; }
            }
            foreach (IntPtr[] names in new[] { inputNames, outputNames })
                if (names != null) foreach (IntPtr name in names) if (name != IntPtr.Zero) Marshal.FreeHGlobal(name);
            inputNames = null;
            outputNames = null;
        }

        private static void Bind(string dll)
        {
            if (api != IntPtr.Zero)
            {
                if (!string.Equals(bound, dll, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("onnxruntime is already loaded from " + bound + ".");
                return;
            }
            if (IntPtr.Size != 8) throw new PlatformNotSupportedException("Meaning search needs 64-bit Windows.");
            IntPtr module = LoadLibraryEx(dll, IntPtr.Zero, 8);
            if (module == IntPtr.Zero) throw new IOException("onnxruntime wouldn't load (error " + Marshal.GetLastWin32Error() + ").");
            IntPtr entry = GetProcAddress(module, "OrtGetApiBase");
            if (entry == IntPtr.Zero) throw new IOException("onnxruntime has no OrtGetApiBase.");
            IntPtr baseTable = ((ApiBaseFn)Marshal.GetDelegateForFunctionPointer(entry, typeof(ApiBaseFn)))();
            IntPtr table = ((GetApiFn)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(baseTable), typeof(GetApiFn)))(ApiVersion);
            if (table == IntPtr.Zero) throw new IOException("onnxruntime does not offer API version " + ApiVersion + ".");
            CreateStatus = Slot<CreateStatusFn>(table, 0);
            GetErrorCode = Slot<GetErrorCodeFn>(table, 1);
            GetErrorMessage = Slot<GetErrorMessageFn>(table, 2);
            CreateEnv = Slot<CreateEnvFn>(table, 3);
            CreateSession = Slot<CreateSessionFn>(table, 7);
            Run = Slot<RunFn>(table, 9);
            CreateSessionOptions = Slot<OutFn>(table, 10);
            DisableCpuMemArena = Slot<HandleFn>(table, 19);
            SetIntraOpNumThreads = Slot<ThreadsFn>(table, 24);
            SetInterOpNumThreads = Slot<ThreadsFn>(table, 25);
            SessionGetInputCount = Slot<CountFn>(table, 30);
            SessionGetInputName = Slot<NameFn>(table, 36);
            SessionGetOutputName = Slot<NameFn>(table, 37);
            CreateTensorWithDataAsOrtValue = Slot<TensorFn>(table, 49);
            GetTensorMutableData = Slot<HandleOutFn>(table, 51);
            GetDimensionsCount = Slot<CountFn>(table, 61);
            GetDimensions = Slot<DimensionsFn>(table, 62);
            GetTensorTypeAndShape = Slot<HandleOutFn>(table, 65);
            CreateCpuMemoryInfo = Slot<MemoryFn>(table, 69);
            AllocatorFree = Slot<FreeFn>(table, 76);
            GetAllocatorWithDefaultOptions = Slot<OutFn>(table, 78);
            ReleaseStatus = Slot<ReleaseFn>(table, 93);
            ReleaseMemoryInfo = Slot<ReleaseFn>(table, 94);
            ReleaseSession = Slot<ReleaseFn>(table, 95);
            ReleaseValue = Slot<ReleaseFn>(table, 96);
            ReleaseTensorTypeAndShapeInfo = Slot<ReleaseFn>(table, 99);
            ReleaseSessionOptions = Slot<ReleaseFn>(table, 100);
            bound = dll;
            api = table;
        }

        private static T Slot<T>(IntPtr table, int index) where T : class
        {
            IntPtr pointer = Marshal.ReadIntPtr(table, index * IntPtr.Size);
            if (pointer == IntPtr.Zero) throw new IOException("onnxruntime is missing function " + index + ".");
            return (T)(object)Marshal.GetDelegateForFunctionPointer(pointer, typeof(T));
        }

        private static void Check(IntPtr status)
        {
            if (status == IntPtr.Zero) return;
            string message = Marshal.PtrToStringAnsi(GetErrorMessage(status));
            ReleaseStatus(status);
            throw new IOException("onnxruntime: " + message);
        }

        private static IntPtr Utf8(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? "");
            IntPtr pointer = Marshal.AllocHGlobal(bytes.Length + 1);
            Marshal.Copy(bytes, 0, pointer, bytes.Length);
            Marshal.WriteByte(pointer, bytes.Length, 0);
            return pointer;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr LoadLibraryEx(string path, IntPtr file, int flags);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)] private static extern IntPtr GetProcAddress(IntPtr module, string name);
    }
}

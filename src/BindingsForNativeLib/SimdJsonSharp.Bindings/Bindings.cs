﻿using System;
using System.Runtime.InteropServices;
using System.Text;

#region stdint types and friends
using size_t = System.IntPtr; // IntPtr here!!
using uint8_t = System.Byte;
using uint64_t = System.UInt64;
using uint32_t = System.UInt32;
using int64_t = System.Int64;
using bytechar = System.SByte;
#endregion

namespace SimdJsonSharp
{
    public static unsafe class SimdJsonN // 'N' stands for Native
    {
        public const string NativeLib = @"SimdJsonNative";

        public static uint MinifyJson(byte* jsonDataPtr, int jsonDataLength, uint8_t* output) => 
            (uint)Global_jsonminify(jsonDataPtr, (size_t)jsonDataLength, output);

        public static string MinifyJson(byte[] inputBytes)
        {
            byte[] outputBytes = new byte[inputBytes.Length]; // no Span<T> and ArrayPool in ns2.0

            fixed (byte* inputBytesPtr = inputBytes)
            fixed (byte* outputBytesPtr = outputBytes)
            {
                uint bytesWritten = MinifyJson(inputBytesPtr, inputBytes.Length, outputBytesPtr);
                return Encoding.UTF8.GetString(outputBytes, 0, (int)bytesWritten);
            }
        }

        public static string MinifyJson(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            return MinifyJson(inputBytes);
        }

        public static ParsedJsonN ParseJson(byte[] jsonData)
        {
            fixed (byte* jsonDataPtr = jsonData)
                return ParseJson(jsonDataPtr, jsonData.Length);
        }

        public static ParsedJsonN ParseJson(byte* jsonDataPtr, int jsonDataLength, bool reallocifneeded = true)
        {
            ParsedJsonN pj = new ParsedJsonN();
            bool ok = pj.AllocateCapacity((uint32_t)jsonDataLength);
            if (ok)
            {
                Global_json_parse(jsonDataPtr, (size_t)jsonDataLength, pj.Handle, reallocifneeded);
            }
            else
            {
                throw new InvalidOperationException("failure during memory allocation");
            }
            return pj;
        }


        #region pinvokes
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern bytechar* Global_allocate_padded_buffer(size_t length);
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern size_t Global_jsonminify(uint8_t* buf, size_t len, uint8_t* output);
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern int Global_json_parse(uint8_t* buf, size_t len, void* pj, bool reallocifneeded = true);
        #endregion
    }

    public unsafe class ParsedJsonN : IDisposable // 'N' stands for Native
    {
        public void* Handle { get; private set; }
        public ParsedJsonN(void* handle) => this.Handle = handle;
        public ParsedJsonN() => Handle = ParsedJson_ParsedJson();
        public ParsedJsonIteratorN CreateIterator() => new ParsedJsonIteratorN(this);
        public bool AllocateCapacity(uint len, uint maxdepth = 1024) => ParsedJson_allocateCapacity(Handle, len, maxdepth) > 0;
        public bool IsValid => ParsedJson_isValid(Handle) > 0;
        public void Deallocate() => ParsedJson_deallocate(Handle);
        public void Init() => ParsedJson_init(Handle);
        public void WriteTape(uint64_t val, uint8_t c) => ParsedJson_write_tape(Handle, val, c);
        public void WriteTapeS64(int64_t i) => ParsedJson_write_tape_s64(Handle, i);
        public void WriteTapeDouble(double d) => ParsedJson_write_tape_double(Handle, d);
        public uint32_t GetCurrentLoc() => ParsedJson_get_current_loc(Handle);
        public void AnnotatePreviousLoc(uint32_t savedLoc, uint64_t val) => ParsedJson_annotate_previousloc(Handle, savedLoc, val);


        #region pinvokes
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern void* ParsedJson_ParsedJson();
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern uint8_t ParsedJson_allocateCapacity(void* target, uint len, uint maxdepth);
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern uint8_t ParsedJson_isValid(void* target);
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern void ParsedJson_deallocate(void* target);
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern void ParsedJson_init(void* target);
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern void ParsedJson_write_tape(void* target, uint64_t val, uint8_t c);
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern void ParsedJson_write_tape_s64(void* target, int64_t i);
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern void ParsedJson_write_tape_double(void* target, double d);
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern uint32_t ParsedJson_get_current_loc(void* target);
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern void ParsedJson_annotate_previousloc(void* target, uint32_t saved_loc, uint64_t val);
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern void ParsedJson_dispose(void* target);
        #endregion

        public void Dispose()
        {
            if (Handle != (void*)IntPtr.Zero)
            {
                ParsedJson_dispose(Handle);
                Handle = (void*)IntPtr.Zero;
            }
        }

        ~ParsedJsonN() => Dispose();
    }

    public unsafe class ParsedJsonIteratorN : IDisposable
    {
        internal static readonly UTF8Encoding _utf8Encoding = new UTF8Encoding(false, true);

        public void* Handle { get; private set; }
        public ParsedJsonIteratorN(void* handle) => this.Handle = handle;
        public ParsedJsonIteratorN(ParsedJsonN pj) => Handle = iterator_iterator(pj.Handle);
        public bool IsOk => iterator_isOk(Handle) > 0;
        public uint TapeLocation => (uint)iterator_get_tape_location(Handle);
        public uint TapeLength => (uint)iterator_get_tape_length(Handle);
        public uint Depth => (uint)iterator_get_depth(Handle);
        public uint8_t ScopeType => iterator_get_scope_type(Handle);
        public bool MoveForward() => iterator_move_forward(Handle) > 0;
        public uint8_t CurrentType => iterator_get_type(Handle);
        public int64_t GetInteger() => iterator_get_integer(Handle);
        public bytechar* GetUtf8String() => iterator_get_string(Handle);
        public uint32_t GetUtf8StringLength() => iterator_get_string_length(Handle);
        public string GetUtf16String() => _utf8Encoding.GetString((byte*)iterator_get_string(Handle), (int)iterator_get_string_length(Handle));
        public double GetDouble() => iterator_get_double(Handle);
        public bool IsObjectOrArray => iterator_is_object_or_array(Handle) > 0;
        public bool IsObject => iterator_is_object(Handle) > 0;
        public bool IsArray => iterator_is_array(Handle) > 0;
        public bool IsString => iterator_is_string(Handle) > 0;
        public bool IsInteger => iterator_is_integer(Handle) > 0;
        public bool IsDouble => iterator_is_double(Handle) > 0;
        public bool MoveToKey(bytechar* key) => iterator_move_to_key(Handle, key) > 0;
        public bool Next() => iterator_next(Handle) > 0;
        public bool Prev() => iterator_prev(Handle) > 0;
        public bool Up() => iterator_up(Handle) > 0;
        public bool Down() => iterator_down(Handle) > 0;
        public void ToStartScope() => iterator_to_start_scope(Handle);

        #region pinvokes
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern void* iterator_iterator(void* pj);
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern uint8_t iterator_isOk(void* target);
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern size_t iterator_get_tape_location(void* target);
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern size_t iterator_get_tape_length(void* target);
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern size_t iterator_get_depth(void* target);
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern uint8_t iterator_get_scope_type(void* target);
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern uint8_t iterator_move_forward(void* target);
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern uint8_t iterator_get_type(void* target);
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern int64_t iterator_get_integer(void* target);
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern bytechar* iterator_get_string(void* target);
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern uint32_t iterator_get_string_length(void* target);
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern double iterator_get_double(void* target);
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern uint8_t iterator_is_object_or_array(void* target);
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern uint8_t iterator_is_object(void* target);
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern uint8_t iterator_is_array(void* target);
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern uint8_t iterator_is_string(void* target);
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern uint8_t iterator_is_integer(void* target);
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern uint8_t iterator_is_double(void* target);
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern uint8_t iterator_move_to_key(void* target, bytechar* key);
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern uint8_t iterator_next(void* target);
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern uint8_t iterator_prev(void* target);
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern uint8_t iterator_up(void* target);
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern uint8_t iterator_down(void* target);
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern void iterator_to_start_scope(void* target);
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern void iterator_is_object_or_array_static(uint8_t type);
        [DllImport(SimdJsonN.NativeLib, CallingConvention = CallingConvention.Cdecl)] private static extern void iterator_dispose(void* target);
        #endregion

        public void Dispose()
        {
            if (Handle != (void*) IntPtr.Zero)
            {
                iterator_dispose(Handle);
                Handle = (void*) IntPtr.Zero;
            }
        }

        ~ParsedJsonIteratorN() => Dispose();
    }
}

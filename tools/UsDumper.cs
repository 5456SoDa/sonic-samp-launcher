using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// Extracts the #US (user strings) heap from a .NET PE file.
// Usage: UsDumper <assembly.exe> [out.txt]
// Built with the legacy C# 5 compiler (csc.exe from .NET Framework 4.0).
static class UsDumper
{
    static byte[] b;
    struct Sec { public uint Va, VSize, Raw, RawSize; }
    static List<Sec> secs;

    static int RvaToFile(int rva)
    {
        foreach (Sec s in secs)
        {
            uint span = Math.Max(s.VSize, s.RawSize);
            if ((uint)rva >= s.Va && (uint)rva < s.Va + span)
                return (int)(s.Raw + ((uint)rva - s.Va));
        }
        throw new Exception("rva out of range: " + rva);
    }

    static int Main(string[] args)
    {
        if (args.Length < 1) { Console.Error.WriteLine("usage: UsDumper <file> [out.txt]"); return 2; }
        b = File.ReadAllBytes(args[0]);
        if (b[0] != 'M' || b[1] != 'Z') throw new Exception("not MZ");
        int pe = BitConverter.ToInt32(b, 0x3C);
        if (b[pe] != 'P' || b[pe + 1] != 'E') throw new Exception("not PE");
        int coff = pe + 4;
        ushort numSections = BitConverter.ToUInt16(b, coff + 2);
        ushort optSize = BitConverter.ToUInt16(b, coff + 16);
        int opt = coff + 20;
        ushort magic = BitConverter.ToUInt16(b, opt);
        int ddOff = magic == 0x10B ? 96 : (magic == 0x20B ? 112 : -1);
        if (ddOff < 0) throw new Exception("bad optional header magic " + magic.ToString("X"));
        int cliRva = BitConverter.ToInt32(b, opt + ddOff + 14 * 8);
        if (cliRva == 0) throw new Exception("not a managed PE (no CLI header)");

        int secOff = opt + optSize;
        secs = new List<Sec>();
        for (int i = 0; i < numSections; i++)
        {
            int s = secOff + i * 40;
            Sec sc;
            sc.Va = BitConverter.ToUInt32(b, s + 12);
            sc.VSize = BitConverter.ToUInt32(b, s + 8);
            sc.Raw = BitConverter.ToUInt32(b, s + 20);
            sc.RawSize = BitConverter.ToUInt32(b, s + 16);
            secs.Add(sc);
        }

        int mdRva = BitConverter.ToInt32(b, RvaToFile(cliRva) + 8);
        int md = RvaToFile(mdRva);
        if (BitConverter.ToUInt32(b, md) != 0x424A5342) throw new Exception("no BSJB metadata root");
        int verLen = BitConverter.ToInt32(b, md + 12);
        int p = md + 16 + verLen; // skip version string
        p += 2; // flags
        ushort streams = BitConverter.ToUInt16(b, p); p += 2;
        bool haveUs = false; int usOff = 0, usSize = 0;
        for (int i = 0; i < streams; i++)
        {
            int o = BitConverter.ToInt32(b, p);
            int sz = BitConverter.ToInt32(b, p + 4);
            int q = p + 8; int nameStart = q;
            while (b[q] != 0) q++;
            string name = Encoding.ASCII.GetString(b, nameStart, q - nameStart);
            int len = q - nameStart + 1;                 // name + terminating NUL
            q = nameStart + ((len + 3) & ~3);            // pad to 4-byte boundary
            p = q;
            if (name == "#US") { haveUs = true; usOff = o; usSize = sz; }
        }
        if (!haveUs) throw new Exception("no #US stream");

        int h = md + usOff;
        int end = h + usSize;
        int i2 = h + 1; // first byte of heap is reserved (0)
        TextWriter w = args.Length > 1
            ? (TextWriter)new StreamWriter(args[1], false, new UTF8Encoding(false))
            : Console.Out;
        int count = 0;
        try
        {
            while (i2 < end)
            {
                // ECMA-335 II.23.2 compressed uint, then blob = v bytes total:
                // (v-1) bytes of UTF-16 data + 1 trailing flag byte.
                uint v; int l;
                if ((b[i2] & 0x80) == 0) { v = b[i2]; l = 1; }
                else if ((b[i2] & 0xC0) == 0x80) { v = (uint)((b[i2] & 0x3F) << 8 | b[i2 + 1]); l = 2; }
                else { v = (uint)((b[i2] & 0x1F) << 24 | b[i2 + 1] << 16 | b[i2 + 2] << 8 | b[i2 + 3]); l = 4; }
                i2 += l;
                if (v == 0) continue;
                int dataLen = (int)v - 1;           // exclude flag byte
                int chars = dataLen / 2;
                if (chars <= 0 || i2 + (int)v > end) { i2 += (int)v; continue; }
                string s = Encoding.Unicode.GetString(b, i2, chars * 2);
                i2 += (int)v;
                count++;
                w.WriteLine("[" + count.ToString("D4") + "] " + s.Replace("\r", "\\r").Replace("\n", "\\n"));
            }
        }
        finally { w.Dispose(); }
        Console.Error.WriteLine("dumped " + count + " strings");
        return 0;
    }
}

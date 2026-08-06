// ---------------------------------------------------------------------------
// ScsArchiveReader.cs — HashFS (.scs) archive reader for SCS Software games
// ---------------------------------------------------------------------------
// The HashFS parsing logic in this file is adapted from the ts-map project
// by Dario Wouters, licensed under the MIT License.
//
// Original source: https://github.com/dariowouters/ts-map
// Copyright (c) Dario Wouters
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
// ---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace ETSOverlay.ScsArchive
{
    /// <summary>
    /// Reads SCS HashFS (.scs) archive files used by Euro Truck Simulator 2 / American Truck Simulator.
    /// Adapted from the ts-map project (MIT License): https://github.com/dariowouters/ts-map
    /// </summary>
    public sealed class ScsArchiveReader : IDisposable
    {
        private readonly string _path;
        private readonly Dictionary<ulong, ScsEntry> _entries = new();
        private ushort _version;
        private ushort _salt;

        /// <summary>Magic marker for SCS HashFS archives: "SCS#"</summary>
        private const uint Magic = 0x23534353;

        public bool IsValid { get; private set; } = true;

        public ScsArchiveReader(string path)
        {
            _path = path;
            if (!File.Exists(path))
            {
                IsValid = false;
                return;
            }

            try
            {
                using var br = new BinaryReader(File.OpenRead(path));
                if (br.BaseStream.Length < 32)
                {
                    IsValid = false;
                    return;
                }

                var magic = br.ReadUInt32();
                if (magic != Magic)
                {
                    IsValid = false;
                    return;
                }

                _version = br.ReadUInt16();
                _salt = br.ReadUInt16();
                var hashMethod = br.ReadUInt32();

                if (hashMethod != 0x59544943 && hashMethod != 0) // 'CITY' or 0
                {
                    IsValid = false;
                    return;
                }

                if (_version == 1)
                {
                    var entryCount = br.ReadUInt32();
                    var startOffset = br.ReadInt64();

                    if (startOffset < 0 || entryCount == 0)
                    {
                        IsValid = false;
                        return;
                    }

                    br.BaseStream.Seek(startOffset, SeekOrigin.Begin);

                    for (var i = 0; i < entryCount; i++)
                    {
                        var entry = new ScsEntry
                        {
                            Hash = br.ReadUInt64(),
                            Offset = br.ReadInt64(),
                            Flags = br.ReadUInt32(),
                            Crc = br.ReadUInt32(),
                            Size = br.ReadInt32(),
                            CompressedSize = br.ReadInt32()
                        };

                        entry.IsDirectory = (entry.Flags & 0x04) != 0;
                        entry.IsCompressed = (entry.Flags & 0x02) != 0;

                        _entries[entry.Hash] = entry;
                    }
                }
                else if (_version == 2)
                {
                    var numEntries = br.ReadUInt32();
                    var entryTableLength = br.ReadUInt32();
                    var numMetadataEntries = br.ReadUInt32();
                    var metadataTableLength = br.ReadUInt32();
                    var entryTableStart = br.ReadUInt64();
                    var metadataTableStart = br.ReadUInt64();

                    br.BaseStream.Seek((long)entryTableStart, SeekOrigin.Begin);
                    var entryTableCompressed = br.ReadBytes((int)entryTableLength);
                    var entryTableBytes = DecompressZlib(entryTableCompressed);

                    br.BaseStream.Seek((long)metadataTableStart, SeekOrigin.Begin);
                    var metadataTableCompressed = br.ReadBytes((int)metadataTableLength);
                    var metadataTableBytes = DecompressZlib(metadataTableCompressed);

                    using var entryReader = new BinaryReader(new MemoryStream(entryTableBytes));
                    using var metaReader = new BinaryReader(new MemoryStream(metadataTableBytes));

                    for (int i = 0; i < numEntries; i++)
                    {
                        ulong hash = entryReader.ReadUInt64();
                        uint metaIndex = entryReader.ReadUInt32();
                        ushort metaCount = entryReader.ReadUInt16();
                        ushort flags = entryReader.ReadUInt16();

                        if (metaCount == 0) continue;

                        metaReader.BaseStream.Seek(metaIndex * 4L, SeekOrigin.Begin);
                        
                        // Read first chunk type
                        var idxBytes = metaReader.ReadBytes(3);
                        var chunkType = metaReader.ReadByte();
                        
                        // Plain=128, Directory=129
                        if (chunkType == 128 || chunkType == 129)
                        {
                            // Skip remaining chunk definitions
                            metaReader.BaseStream.Seek(metaIndex * 4L + (metaCount * 4L), SeekOrigin.Begin);
                            
                            var cSizeBuf = metaReader.ReadBytes(3);
                            var cSizeFlags = metaReader.ReadByte();
                            uint cSize = (uint)(cSizeBuf[0] | (cSizeBuf[1] << 8) | (cSizeBuf[2] << 16) | ((cSizeFlags & 0x0F) << 24));
                            bool isCompressed = (cSizeFlags & 0x10) != 0;

                            var sizeBuf = metaReader.ReadBytes(3);
                            var sizeFlags = metaReader.ReadByte();
                            uint size = (uint)(sizeBuf[0] | (sizeBuf[1] << 8) | (sizeBuf[2] << 16) | ((sizeFlags & 0x0F) << 24));

                            var unknown = metaReader.ReadUInt32();
                            uint offsetBlock = metaReader.ReadUInt32();

                            var entry = new ScsEntry
                            {
                                Hash = hash,
                                Offset = offsetBlock * 16L,
                                Size = (int)size,
                                CompressedSize = (int)cSize,
                                IsCompressed = isCompressed,
                                IsDirectory = chunkType == 129
                            };

                            _entries[entry.Hash] = entry;
                        }
                    }
                }
                else
                {
                    IsValid = false;
                    return;
                }

                IsValid = true;
            }
            catch
            {
                IsValid = false;
            }
        }

        /// <summary>Check if a file exists in the archive.</summary>
        public bool FileExists(string path)
        {
            var hash = HashPath(path);
            return _entries.ContainsKey(hash);
        }

        /// <summary>Extract a file from the archive as a byte array. Returns null if not found.</summary>
        public byte[]? ExtractFile(string path)
        {
            var hash = HashPath(path);
            if (!_entries.TryGetValue(hash, out var entry) || entry.IsDirectory)
                return null;

            return ExtractEntry(entry);
        }

        /// <summary>Extract a file from the archive as a UTF-8 string. Returns null if not found.</summary>
        public string? ExtractFileAsString(string path)
        {
            var bytes = ExtractFile(path);
            return bytes != null ? Encoding.UTF8.GetString(bytes) : null;
        }

        /// <summary>Get a directory listing (child paths) for the given directory path.</summary>
        public string[]? GetDirectoryListing(string path)
        {
            var hash = HashPath(path);
            if (!_entries.TryGetValue(hash, out var entry) || !entry.IsDirectory)
                return null;

            var bytes = ExtractEntry(entry);
            if (bytes == null) return null;

            if (_version == 2)
            {
                using var ms = new MemoryStream(bytes);
                using var br = new BinaryReader(ms);
                
                var count = br.ReadUInt32();
                var lengths = br.ReadBytes((int)count);
                var list = new List<string>((int)count);
                
                for (var i = 0; i < count; i++)
                {
                    var chars = br.ReadBytes(lengths[i]);
                    var str = Encoding.UTF8.GetString(chars);
                    if (!str.StartsWith('/')) // File
                    {
                        list.Add(str);
                    }
                    else // Directory
                    {
                        list.Add(str.Substring(1));
                    }
                }
                return list.ToArray();
            }
            else // V1
            {
                var content = Encoding.UTF8.GetString(bytes);
                return content.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                              .Select(e => e.Trim())
                              .Where(e => !string.IsNullOrEmpty(e))
                              .ToArray();
            }
        }

        private static byte[] DecompressZlib(byte[] compressedData)
        {
            using var ms = new MemoryStream(compressedData);
            ms.ReadByte(); // Skip zlib header
            ms.ReadByte();
            using var deflateStream = new DeflateStream(ms, CompressionMode.Decompress);
            using var resultMs = new MemoryStream();
            deflateStream.CopyTo(resultMs);
            return resultMs.ToArray();
        }

        private byte[]? ExtractEntry(ScsEntry entry)
        {
            try
            {
                using var br = new BinaryReader(File.OpenRead(_path));
                br.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);

                if (!entry.IsCompressed)
                {
                    return br.ReadBytes(entry.Size);
                }

                var compressedData = br.ReadBytes(entry.CompressedSize);
                using var ms = new MemoryStream(compressedData);

                // Skip zlib header (2 bytes)
                ms.ReadByte();
                ms.ReadByte();

                using var deflateStream = new DeflateStream(ms, CompressionMode.Decompress);
                using var resultMs = new MemoryStream();
                deflateStream.CopyTo(resultMs);
                return resultMs.ToArray();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Calculate CityHash64 of a normalized path.</summary>
        public ulong HashPath(string path)
        {
            if (path != "" && path.StartsWith('/'))
                path = path.Substring(1);
            if (_salt != 0)
                path = _salt + path;

            var bytes = Encoding.UTF8.GetBytes(path);
            return CityHash.CityHash64(bytes, (ulong)bytes.Length);
        }

        public void Dispose() { /* No unmanaged resources */ }
    }

    internal sealed class ScsEntry
    {
        public ulong Hash { get; set; }
        public long Offset { get; set; }
        public uint Flags { get; set; }
        public uint Crc { get; set; }
        public int Size { get; set; }
        public int CompressedSize { get; set; }
        public bool IsDirectory { get; set; }
        public bool IsCompressed { get; set; }
    }
}

using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Unpack_Pak_Gui
{
    public class PakParser
    {
        public class Block
        {
            public ulong Start { get; set; }
            public ulong Size { get; set; }

            public Block(ulong start, ulong size)
            {
                Start = start;
                Size = size;
            }
        }

        public class Record
        {
            public string FileName { get; set; }
            public ulong Offset { get; set; }
            public ulong FileSize { get; set; }
            public ulong SizeDecompressed { get; set; }
            public uint CompressionMethod { get; set; }
            public bool IsEncrypted { get; set; }
            public List<byte> Data { get; set; } = new List<byte>();
            public List<Block> CompressionBlocks { get; set; } = new List<Block>();

            public void Read(BinaryReader reader, uint fileVersion, bool includesHeader, bool quickread = false)
            {
                if (includesHeader)
                {
                    uint strLen = reader.ReadUInt32();
                    FileName = new string(reader.ReadChars((int)strLen));
                    Debug.WriteLine($"FileName: {FileName}");
                }

                Offset = reader.ReadUInt64();
                Debug.WriteLine($"Offset: {Offset}");

                if (quickread)
                {
                    reader.BaseStream.Seek(16, SeekOrigin.Current);
                }
                else
                {
                    FileSize = reader.ReadUInt64();
                    SizeDecompressed = reader.ReadUInt64();
                    Debug.WriteLine($"FileSize: {FileSize}, SizeDecompressed: {SizeDecompressed}");
                }

                CompressionMethod = reader.ReadUInt32();

                Debug.WriteLine($"CompressionMethod (Hex): {CompressionMethod:X2}");

                if (fileVersion <= 1)
                {
                    ulong timestamp = reader.ReadUInt64();
                    Debug.WriteLine($"Timestamp: {timestamp}");
                }

                if (quickread)
                {
                    reader.BaseStream.Seek(20, SeekOrigin.Current);
                }
                else
                {
                    byte[] sha1hash = reader.ReadBytes(20);
                }

                if (fileVersion >= 3)
                {
                    if (CompressionMethod != 0)
                    {
                        uint blockCount = reader.ReadUInt32();
                        Debug.WriteLine($"BlockCount: {blockCount}");
                        if (quickread)
                        {
                            reader.BaseStream.Seek(blockCount * 16, SeekOrigin.Current);
                        }
                        else
                        {
                            for (uint i = 0; i < blockCount; ++i)
                            {
                                ulong startOffset = reader.ReadUInt64();
                                ulong endOffset = reader.ReadUInt64();
                                CompressionBlocks.Add(new Block(startOffset, endOffset - startOffset));
                                Debug.WriteLine($"Block: StartOffset={startOffset}, Size={endOffset - startOffset}");
                            }
                        }
                    }

                    if (quickread)
                    {
                        reader.BaseStream.Seek(5, SeekOrigin.Current);
                    }
                    else
                    {
                        byte isEncryptedByte = reader.ReadByte();
                        IsEncrypted = isEncryptedByte > 0;
                        uint compressionBlockSize = reader.ReadUInt32();
                        Debug.WriteLine($"IsEncrypted: {IsEncrypted}, CompressionBlockSize: {compressionBlockSize}");
                    }
                }
            }
        }

        private const uint UE4_PAK_MAGIC = 0x5a6f12e1;
        private BinaryReader _reader;
        private uint _fileVersion;
        private ulong _indexOffset;
        private ulong _indexSize;
        private uint _recordCount;
        private ulong _seekStop = 0;
        private uint _countStop = 0;
        private string _mountPoint;
        private Dictionary<string, ulong> _headers = new Dictionary<string, ulong>();

        Dictionary<uint, string> CompressionMethod = new Dictionary<uint, string>
        {
            { 0, "NONE" },
            { 1, "ZLIB" },
            { 2, "BIAS_MEMORY" },
            { 3, "BIAS_SPEED" },
            { 4, "Oodle" }
        };

        public PakParser(Stream fileStream)
        {
            _reader = new BinaryReader(fileStream);
        }

        private bool ValidateMagicNumber()
        {
            long fileLength = _reader.BaseStream.Length;
            Debug.WriteLine($"File length: {fileLength}");

            _reader.BaseStream.Seek(-44, SeekOrigin.End);
            uint magic = _reader.ReadUInt32();
            long position = _reader.BaseStream.Position - sizeof(uint);
            Debug.WriteLine($"Checking for magic number at position: {position}");
            Debug.WriteLine($"Magic number read: {magic:X}");

            if (magic != UE4_PAK_MAGIC)
            {
                _reader.BaseStream.Seek(-204, SeekOrigin.End);
                magic = _reader.ReadUInt32();
                position = _reader.BaseStream.Position - sizeof(uint);
                Debug.WriteLine($"Checking for magic number at position: {position}");
                Debug.WriteLine($"Magic number read: {magic:X}");
            }

            if (magic != UE4_PAK_MAGIC)
            {
                Debug.WriteLine("Error: Invalid magic number!");
                throw new InvalidDataException("Invalid magic number: The file is not a valid PAK file.");
            }

            Debug.WriteLine($"Magic number found at position: {position}");
            return true;
        }

        public List<string> List(string recordName = "")
        {
            List<string> fileNames = new List<string>();

            if (_seekStop == 0)
            {
                if (!ValidateMagicNumber()) return fileNames;
                _fileVersion = _reader.ReadUInt32();
                _indexOffset = _reader.ReadUInt32();
                _indexSize = _reader.ReadUInt32();

                Debug.WriteLine($"PAK Index Offset: {_indexOffset:X}");
                _reader.BaseStream.Seek((long)_indexOffset, SeekOrigin.Begin);
                uint strLen = _reader.ReadUInt32();
                _mountPoint = new string(_reader.ReadChars((int)strLen));
                _recordCount = _reader.ReadUInt32();
            }
            else
            {
                _reader.BaseStream.Seek((long)_seekStop, SeekOrigin.Begin);
            }

            for (uint i = 0; i < (_recordCount != 0 ? _recordCount : _countStop); ++i)
            {
                Record rec = new Record();
                rec.Read(_reader, _fileVersion, true, true);

                string sanitizedFileName = rec.FileName.Replace('\0', '_').TrimEnd('_');
                _headers[sanitizedFileName] = rec.Offset;

                Debug.WriteLine($"Added key: {sanitizedFileName} with offset: {_headers[sanitizedFileName]}");
            }

            foreach (var header in _headers)
            {
                fileNames.Add(header.Key);
            }

            return fileNames;
        }

        public Record Unpack(string recordName, bool decode = false)
        {
            if (!_headers.ContainsKey(recordName))
            {
                Debug.WriteLine($"Key '{recordName}' not found, calling List to update headers.");
                List(recordName);

                if (!_headers.ContainsKey(recordName))
                {
                    throw new KeyNotFoundException($"The given key '{recordName}' could not be added by List function.");
                }
            }

            Debug.WriteLine($"Attempting to unpack file: '{recordName}'");
            foreach (var key in _headers.Keys)
            {
                Debug.WriteLine($"Header key: '{key}'");
            }

            if (!_headers.TryGetValue(recordName, out ulong offset))
            {
                throw new KeyNotFoundException($"The given key '{recordName}' was not present in the dictionary.");
            }

            _reader.BaseStream.Seek((long)offset, SeekOrigin.Begin);

            Record rec = new Record();
            rec.Read(_reader, _fileVersion, false);

            Debug.WriteLine($"File: {recordName}, Compression Method: {rec.CompressionMethod}");

            if (!CompressionMethod.ContainsKey(rec.CompressionMethod))
            {
                throw new InvalidOperationException($"Unimplemented compression method {rec.CompressionMethod}");
            }

            switch (CompressionMethod[rec.CompressionMethod])
            {
                case "NONE":
                    rec.Data = _reader.ReadBytes((int)rec.FileSize).ToList();
                    if (decode)
                    {
                        string decoded = Encoding.UTF8.GetString(rec.Data.ToArray());
                        rec.Data = decoded.Select(c => (byte)c).ToList();
                    }
                    break;

                case "ZLIB":
                    List<byte> dataDecompressed = new List<byte>();
                    foreach (var block in rec.CompressionBlocks)
                    {
                        ulong blockOffset = block.Start;
                        if (_fileVersion == 8)
                        {
                            blockOffset += offset;
                        }
                        ulong blockSize = block.Size;
                        _reader.BaseStream.Seek((long)blockOffset, SeekOrigin.Begin);
                        byte[] memStream = _reader.ReadBytes((int)blockSize);

                        byte[] decompressedData = DecompressZlib(memStream);
                        dataDecompressed.AddRange(decompressedData);
                    }
                    rec.Data = dataDecompressed;

                    if (decode)
                    {
                        string decoded = Encoding.UTF8.GetString(rec.Data.ToArray());
                        rec.Data = decoded.Select(c => (byte)c).ToList();
                    }
                    break;

                default:
                    throw new InvalidOperationException($"Unimplemented compression method {CompressionMethod}");
            }

            rec.FileName = recordName;
            return rec;
        }
        public static byte[] DecompressZlib(byte[] compressedData)
        {
            using (var inputStream = new MemoryStream(compressedData, 2, compressedData.Length - 2))
            using (var deflateStream = new DeflateStream(inputStream, CompressionMode.Decompress))
            using (var outputStream = new MemoryStream())
            {
                deflateStream.CopyTo(outputStream);
                return outputStream.ToArray();
            }
        }

    }
}

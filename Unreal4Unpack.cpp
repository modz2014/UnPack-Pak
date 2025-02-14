#include <iostream>
#include <sstream>
#include <vector>
#include <map>
#include "zlib.h"
#include <cstring>
#include <cassert>
#include <fstream>

#define UE4_PAK_MAGIC 0x5a6f12e1

class PakParser {
public:
    struct Block {
        uint64_t Start;
        uint64_t Size;

        Block(uint64_t start, uint64_t size) : Start(start), Size(size) {}
    };

    struct Record {
        std::string fileName;
        uint64_t offset;
        uint64_t fileSize;
        uint64_t sizeDecompressed;
        uint32_t compressionMethod;
        bool isEncrypted;
        std::vector<Block> compressionBlocks;
        std::vector<uint8_t> Data;

        void Read(std::istream& file, uint32_t fileVersion, bool includesHeader, bool quickread = false) {
            if (includesHeader) {
                uint32_t strLen;
                file.read(reinterpret_cast<char*>(&strLen), sizeof(strLen));
                fileName.resize(strLen);
                file.read(&fileName[0], strLen);
            }

            file.read(reinterpret_cast<char*>(&offset), sizeof(offset));

            if (quickread) {
                file.seekg(16, std::ios::cur);
            }
            else {
                file.read(reinterpret_cast<char*>(&fileSize), sizeof(fileSize));
                file.read(reinterpret_cast<char*>(&sizeDecompressed), sizeof(sizeDecompressed));
            }

            file.read(reinterpret_cast<char*>(&compressionMethod), sizeof(compressionMethod));

            if (fileVersion <= 1) {
                uint64_t timestamp;
                file.read(reinterpret_cast<char*>(&timestamp), sizeof(timestamp));
            }

            if (quickread) {
                file.seekg(20, std::ios::cur);
            }
            else {
                char sha1hash[20];
                file.read(sha1hash, 20);
            }

            if (fileVersion >= 3) {
                if (compressionMethod != 0) {
                    uint32_t blockCount;
                    file.read(reinterpret_cast<char*>(&blockCount), sizeof(blockCount));
                    if (quickread) {
                        file.seekg(blockCount * 16, std::ios::cur);
                    }
                    else {
                        for (uint32_t i = 0; i < blockCount; ++i) {
                            uint64_t startOffset, endOffset;
                            file.read(reinterpret_cast<char*>(&startOffset), sizeof(startOffset));
                            file.read(reinterpret_cast<char*>(&endOffset), sizeof(endOffset));
                            compressionBlocks.emplace_back(startOffset, endOffset - startOffset);
                        }
                    }
                }
                if (quickread) {
                    file.seekg(5, std::ios::cur);
                }
                else {
                    uint8_t isEncrypted;
                    file.read(reinterpret_cast<char*>(&isEncrypted), sizeof(isEncrypted));
                    this->isEncrypted = isEncrypted > 0;
                    uint32_t compressionBlockSize;
                    file.read(reinterpret_cast<char*>(&compressionBlockSize), sizeof(compressionBlockSize));
                }
            }
        }
    };

    PakParser(std::istream& fileStream) : file(fileStream) {}

    std::vector<std::string> List(const std::string& recordName = "") {
        if (seekStop == 0) {
            // Check magic number at the end of the file
            file.seekg(-44, std::ios::end);
            uint32_t magic;
            file.read(reinterpret_cast<char*>(&magic), sizeof(magic));
            std::cout << "Checking for magic number at position: "
                << std::hex << (file.tellg() - static_cast<std::streamoff>(sizeof(magic)))
                << std::dec << std::endl;

            if (magic != UE4_PAK_MAGIC) {
                file.seekg(-204, std::ios::end);
                file.read(reinterpret_cast<char*>(&magic), sizeof(magic));
                std::cout << "Checking for magic number at position: "
                    << std::hex << (file.tellg() - static_cast<std::streamoff>(sizeof(magic)))
                    << std::dec << std::endl;
            }

            // Assert magic number is valid
            assert(magic == UE4_PAK_MAGIC);
            std::cout << "Magic number found at position: "
                << std::hex << (file.tellg() - static_cast<std::streamoff>(sizeof(magic)))
                << std::dec << std::endl;

            // Read version, index offset, and index size
            file.read(reinterpret_cast<char*>(&fileVersion), sizeof(fileVersion));
            file.read(reinterpret_cast<char*>(&indexOffset), sizeof(indexOffset));
            file.read(reinterpret_cast<char*>(&indexSize), sizeof(indexSize));

            // Output index offset
            std::cout << "PAK Index Offset: "
                << std::hex << indexOffset
                << std::dec << std::endl;

            // Seek to the index offset and read the mount point and record count
            file.seekg(indexOffset, std::ios::beg);
            uint32_t strLen;
            file.read(reinterpret_cast<char*>(&strLen), sizeof(strLen));
            mountPoint.resize(strLen);
            file.read(&mountPoint[0], strLen);
            file.read(reinterpret_cast<char*>(&recordCount), sizeof(recordCount));
        }
        else {
            file.seekg(seekStop, std::ios::beg);
        }

        if (headers.find(recordName) == headers.end()) {
            for (uint32_t i = 0; i < (recordCount ? recordCount : countStop); ++i) {
                Record rec;
                rec.Read(file, fileVersion, true, true);
                headers[rec.fileName] = rec.offset;
                if (recordName == rec.fileName) {
                    seekStop = file.tellg();
                    countStop = i;
                    break;
                }
            }
        }

        // Collect all header file names
        std::vector<std::string> headerKeys;
        for (const auto& header : headers) {
            headerKeys.push_back(header.first);
        }
        return headerKeys;
    }

    Record Unpack(const std::string& recordName, bool decode = false) {
        if (headers.find(recordName) == headers.end()) {
            List(recordName);
        }

        uint64_t offset = headers[recordName];
        file.seekg(offset, std::ios::beg);

        Record rec2;
        rec2.Read(file, fileVersion, false);
        if (CompressionMethod.at(rec2.compressionMethod) == "NONE") {
            rec2.Data.resize(rec2.fileSize);
            file.read(reinterpret_cast<char*>(rec2.Data.data()), rec2.fileSize);
            if (decode) {
                std::string decoded(rec2.Data.begin(), rec2.Data.end());
                std::copy(decoded.begin(), decoded.end(), rec2.Data.begin());
            }
        }
        else if (CompressionMethod.at(rec2.compressionMethod) == "ZLIB") {
            std::vector<uint8_t> dataDecompressed;
            for (const auto& block : rec2.compressionBlocks) {
                uint64_t blockOffset = block.Start;
                if (fileVersion == 8) {
                    blockOffset += offset;
                }
                uint64_t blockSize = block.Size;
                file.seekg(blockOffset, std::ios::beg);
                std::vector<uint8_t> memstream(blockSize);
                file.read(reinterpret_cast<char*>(memstream.data()), blockSize);
                std::vector<uint8_t> decompressedData(rec2.sizeDecompressed);
                zlib_decompress(memstream.data(), blockSize, decompressedData.data(), rec2.sizeDecompressed);
                dataDecompressed.insert(dataDecompressed.end(), decompressedData.begin(), decompressedData.end());
            }
            rec2.Data = std::move(dataDecompressed);
            if (decode) {
                std::string decoded(rec2.Data.begin(), rec2.Data.end());
                std::copy(decoded.begin(), decoded.end(), rec2.Data.begin());
            }
        }
        else {
            throw std::runtime_error("Unimplemented compression method " + CompressionMethod.at(rec2.compressionMethod));
        }
        rec2.fileName = recordName;
        return rec2;
    }

private:
    std::istream& file;
    uint32_t fileVersion;
    uint64_t indexOffset;
    uint64_t indexSize;
    uint32_t recordCount;
    uint64_t seekStop = 0;
    uint32_t countStop = 0;
    std::string mountPoint;
    std::map<std::string, uint64_t> headers;

    const std::map<uint32_t, std::string> CompressionMethod = { {0, "NONE"}, {1, "ZLIB"}, {2, "BIAS_MEMORY"}, {3, "BIAS_SPEED"} };

    void zlib_decompress(uint8_t* in, size_t in_size, uint8_t* out, size_t out_size) {
        z_stream zs;
        memset(&zs, 0, sizeof(zs));
        if (inflateInit(&zs) != Z_OK) {
            throw std::runtime_error("inflateInit failed");
        }

        zs.next_in = in;
        zs.avail_in = in_size;
        zs.next_out = out;
        zs.avail_out = out_size;

        int ret = inflate(&zs, Z_FINISH);
        if (ret != Z_STREAM_END) {
            inflateEnd(&zs);
            throw std::runtime_error("inflate failed");
        }

        inflateEnd(&zs);
    }
};





int main(int argc, char* argv[]) {
    try {
        // Check if a file path is provided
        std::string filePath;
        if (argc > 1) {
            filePath = argv[1];
        }
        else {
            std::cout << "No file path provided. Please drag and drop a .pak file or enter the path manually: ";
            std::getline(std::cin, filePath);
        }

        std::ifstream file(filePath, std::ios::binary);
        if (!file) {
            throw std::runtime_error("Failed to open file: " + filePath);
        }

        PakParser parser(file);

        // List all records in the PAK file
        std::vector<std::string> records = parser.List();
        std::cout << "Number of records in PAK file: " << records.size() << std::endl;
        std::cout << "Records in PAK file:" << std::endl;
        for (const auto& record : records) {
            std::cout << record << std::endl;
        }

        // Unpack a specific record (example: first record if available)
        if (!records.empty()) {
            PakParser::Record unpackedRecord = parser.Unpack(records[0]);

            // Process the unpacked data using a stream
            std::ostringstream dataStream;
            dataStream.write(reinterpret_cast<char*>(unpackedRecord.Data.data()), unpackedRecord.Data.size());
        }
    }
    catch (const std::exception& ex) {
        std::cerr << "Error: " << ex.what() << std::endl;
        return 1;
    }

    return 0;
}


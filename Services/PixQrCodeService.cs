using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GameLauncher.Services;

/// <summary>
/// Generates PIX BR Code payload and renders QR Code as WPF ImageSource using SkiaSharp-free bit matrix.
/// </summary>
public static class PixQrCodeService
{
    /// <summary>
    /// Builds the PIX BR Code (EMV) payload string for a static PIX key.
    /// </summary>
    public static string BuildPixPayload(string pixKey, string merchantName, string merchantCity, string description = "")
    {
        // EMV format: TLV (Tag-Length-Value)
        var payload = new StringBuilder();

        // Payload Format Indicator
        payload.Append(Tlv("00", "01"));

        // Merchant Account Information (GUI = br.gov.bcb.pix + key)
        var mai = new StringBuilder();
        mai.Append(Tlv("00", "br.gov.bcb.pix"));
        mai.Append(Tlv("01", pixKey));
        if (!string.IsNullOrEmpty(description))
            mai.Append(Tlv("02", Truncate(description, 25)));
        payload.Append(Tlv("26", mai.ToString()));

        // Merchant Category Code
        payload.Append(Tlv("52", "0000"));

        // Transaction Currency (986 = BRL)
        payload.Append(Tlv("53", "986"));

        // Country Code
        payload.Append(Tlv("58", "BR"));

        // Merchant Name
        payload.Append(Tlv("59", Truncate(merchantName, 25)));

        // Merchant City
        payload.Append(Tlv("60", Truncate(merchantCity, 15)));

        // Additional Data Field (txid)
        var additional = Tlv("05", "***");
        payload.Append(Tlv("62", additional));

        // CRC16 placeholder
        payload.Append("6304");

        // Calculate CRC16-CCITT
        var crc = Crc16Ccitt(payload.ToString());
        payload.Append(crc.ToString("X4"));

        return payload.ToString();
    }

    /// <summary>
    /// Generates a QR Code as a WPF ImageSource from a text payload.
    /// </summary>
    public static ImageSource GenerateQrCode(string text, int moduleSize = 8, int quietZone = 4)
    {
        var matrix = QrEncoder.Encode(text);
        int size = matrix.GetLength(0);
        int imgSize = (size + quietZone * 2) * moduleSize;

        var wb = new WriteableBitmap(imgSize, imgSize, 96, 96, PixelFormats.Bgra32, null);
        int stride = imgSize * 4;
        var pixels = new byte[stride * imgSize];

        // Fill white
        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = 255;     // B
            pixels[i + 1] = 255; // G
            pixels[i + 2] = 255; // R
            pixels[i + 3] = 255; // A
        }

        // Draw modules
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (!matrix[y, x]) continue;

                int px = (x + quietZone) * moduleSize;
                int py = (y + quietZone) * moduleSize;

                for (int dy = 0; dy < moduleSize; dy++)
                {
                    for (int dx = 0; dx < moduleSize; dx++)
                    {
                        int offset = ((py + dy) * imgSize + (px + dx)) * 4;
                        pixels[offset] = 0;     // B
                        pixels[offset + 1] = 0; // G
                        pixels[offset + 2] = 0; // R
                        pixels[offset + 3] = 255; // A
                    }
                }
            }
        }

        wb.WritePixels(new Int32Rect(0, 0, imgSize, imgSize), pixels, stride, 0);
        wb.Freeze();
        return wb;
    }

    private static string Tlv(string tag, string value)
    {
        return $"{tag}{value.Length:D2}{value}";
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static ushort Crc16Ccitt(string data)
    {
        var bytes = Encoding.UTF8.GetBytes(data);
        ushort crc = 0xFFFF;
        ushort polynomial = 0x1021;

        foreach (byte b in bytes)
        {
            crc ^= (ushort)(b << 8);
            for (int i = 0; i < 8; i++)
            {
                if ((crc & 0x8000) != 0)
                    crc = (ushort)((crc << 1) ^ polynomial);
                else
                    crc <<= 1;
            }
        }

        return crc;
    }
}

/// <summary>
/// Minimal QR Code encoder (Version 1-10, Error Correction Level M, Byte mode).
/// No external dependencies.
/// </summary>
internal static class QrEncoder
{
    public static bool[,] Encode(string text)
    {
        var data = Encoding.UTF8.GetBytes(text);
        int version = SelectVersion(data.Length);
        int size = 17 + version * 4;

        var modules = new bool[size, size];
        var isFunction = new bool[size, size];

        // Draw function patterns
        DrawFinderPattern(modules, isFunction, 0, 0);
        DrawFinderPattern(modules, isFunction, size - 7, 0);
        DrawFinderPattern(modules, isFunction, 0, size - 7);
        DrawAlignmentPatterns(modules, isFunction, version);
        DrawTimingPatterns(modules, isFunction, size);
        DrawFormatBits(modules, isFunction, size, 0); // placeholder

        // Encode data
        var codewords = EncodeData(data, version);
        var bits = AddErrorCorrection(codewords, version);

        // Place data bits
        PlaceDataBits(modules, isFunction, size, bits);

        // Apply best mask
        int bestMask = 0;
        int bestPenalty = int.MaxValue;
        for (int mask = 0; mask < 8; mask++)
        {
            var temp = (bool[,])modules.Clone();
            ApplyMask(temp, isFunction, size, mask);
            DrawFormatBits(temp, null!, size, mask);
            int penalty = CalculatePenalty(temp, size);
            if (penalty < bestPenalty)
            {
                bestPenalty = penalty;
                bestMask = mask;
            }
        }

        ApplyMask(modules, isFunction, size, bestMask);
        DrawFormatBits(modules, new bool[size, size], size, bestMask);

        return modules;
    }

    private static int SelectVersion(int dataLength)
    {
        // Byte mode capacities at ECC level M
        int[] capacities = [0, 14, 26, 42, 62, 84, 106, 122, 152, 180, 213];
        for (int v = 1; v < capacities.Length; v++)
        {
            if (dataLength <= capacities[v])
                return v;
        }
        throw new ArgumentException("Data too long for QR Code version 1-10");
    }

    private static byte[] EncodeData(byte[] data, int version)
    {
        // Total data codewords for version at ECC M
        int[] totalCodewords = [0, 16, 28, 44, 64, 86, 108, 124, 154, 182, 216];
        int capacity = totalCodewords[version];

        var bits = new List<bool>();

        // Mode indicator: Byte = 0100
        bits.AddRange([false, true, false, false]);

        // Character count indicator
        int ccBits = version <= 9 ? 8 : 16;
        for (int i = ccBits - 1; i >= 0; i--)
            bits.Add(((data.Length >> i) & 1) == 1);

        // Data
        foreach (byte b in data)
            for (int i = 7; i >= 0; i--)
                bits.Add(((b >> i) & 1) == 1);

        // Terminator
        int totalBits = capacity * 8;
        int terminatorLen = Math.Min(4, totalBits - bits.Count);
        for (int i = 0; i < terminatorLen; i++)
            bits.Add(false);

        // Pad to byte boundary
        while (bits.Count % 8 != 0)
            bits.Add(false);

        // Pad codewords
        byte[] padBytes = [0xEC, 0x11];
        int padIdx = 0;
        while (bits.Count < totalBits)
        {
            for (int i = 7; i >= 0; i--)
                bits.Add(((padBytes[padIdx] >> i) & 1) == 1);
            padIdx = (padIdx + 1) % 2;
        }

        var result = new byte[capacity];
        for (int i = 0; i < capacity; i++)
        {
            int val = 0;
            for (int b = 0; b < 8; b++)
                val = (val << 1) | (bits[i * 8 + b] ? 1 : 0);
            result[i] = (byte)val;
        }

        return result;
    }

    private static bool[] AddErrorCorrection(byte[] data, int version)
    {
        // ECC M: (numBlocks, dataPerBlock, eccPerBlock)
        var eccTable = new (int blocks, int dataPerBlock, int eccPerBlock)[]
        {
            (0,0,0), (1,16,10), (1,28,16), (1,44,26), (2,32,18), (2,43,24),
            (2,54,16), (2,62,18), (2,78,22), (2,91,22), (2,108,26)
        };

        var (blocks, dataPerBlock, eccPerBlock) = eccTable[version];
        var allBits = new List<bool>();

        // For simplicity with 1-2 blocks, handle the interleaving
        var dataBlocks = new byte[blocks][];
        var eccBlocks = new byte[blocks][];
        int offset = 0;

        for (int b = 0; b < blocks; b++)
        {
            int blockDataLen = b < blocks - 1 ? dataPerBlock : data.Length - offset;
            dataBlocks[b] = new byte[blockDataLen];
            Array.Copy(data, offset, dataBlocks[b], 0, blockDataLen);
            offset += blockDataLen;
            eccBlocks[b] = ReedSolomonEncode(dataBlocks[b], eccPerBlock);
        }

        // Interleave data
        int maxDataLen = dataBlocks.Max(b => b.Length);
        for (int i = 0; i < maxDataLen; i++)
            foreach (var block in dataBlocks)
                if (i < block.Length)
                    for (int bit = 7; bit >= 0; bit--)
                        allBits.Add(((block[i] >> bit) & 1) == 1);

        // Interleave ECC
        for (int i = 0; i < eccPerBlock; i++)
            foreach (var block in eccBlocks)
                if (i < block.Length)
                    for (int bit = 7; bit >= 0; bit--)
                        allBits.Add(((block[i] >> bit) & 1) == 1);

        return allBits.ToArray();
    }

    private static byte[] ReedSolomonEncode(byte[] data, int eccLen)
    {
        var gen = ReedSolomonGenerator(eccLen);
        var result = new byte[eccLen];

        foreach (byte b in data)
        {
            byte factor = (byte)(b ^ result[0]);
            Array.Copy(result, 1, result, 0, eccLen - 1);
            result[eccLen - 1] = 0;
            for (int i = 0; i < eccLen; i++)
                result[i] ^= GfMul(gen[i], factor);
        }

        return result;
    }

    private static byte[] ReedSolomonGenerator(int degree)
    {
        var result = new byte[degree];
        result[degree - 1] = 1;

        byte root = 1;
        for (int i = 0; i < degree; i++)
        {
            for (int j = 0; j < degree; j++)
            {
                result[j] = GfMul(result[j], root);
                if (j + 1 < degree)
                    result[j] ^= result[j + 1];
            }
            root = GfMul(root, 2);
        }

        return result;
    }

    private static byte GfMul(byte x, byte y)
    {
        int z = 0;
        for (int i = 7; i >= 0; i--)
        {
            z = (z << 1) ^ ((z >> 7) * 0x11D);
            z ^= ((y >> i) & 1) * x;
        }
        return (byte)z;
    }

    private static void DrawFinderPattern(bool[,] modules, bool[,] isFunc, int row, int col)
    {
        int size = modules.GetLength(0);
        for (int dy = -1; dy <= 7; dy++)
        {
            for (int dx = -1; dx <= 7; dx++)
            {
                int y = row + dy, x = col + dx;
                if (y < 0 || y >= size || x < 0 || x >= size) continue;

                bool dark = (dy >= 0 && dy <= 6 && (dx == 0 || dx == 6)) ||
                            (dx >= 0 && dx <= 6 && (dy == 0 || dy == 6)) ||
                            (dy >= 2 && dy <= 4 && dx >= 2 && dx <= 4);

                modules[y, x] = dark;
                isFunc[y, x] = true;
            }
        }
    }

    private static void DrawAlignmentPatterns(bool[,] modules, bool[,] isFunc, int version)
    {
        if (version < 2) return;
        int[][] positions =
        [
            [], [], [6, 18], [6, 22], [6, 26], [6, 30], [6, 34],
            [6, 22, 38], [6, 24, 42], [6, 26, 46], [6, 28, 50]
        ];

        var pos = positions[version];
        int size = modules.GetLength(0);

        foreach (int cy in pos)
        {
            foreach (int cx in pos)
            {
                if (isFunc[cy, cx]) continue;
                for (int dy = -2; dy <= 2; dy++)
                {
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        int y = cy + dy, x = cx + dx;
                        if (y < 0 || y >= size || x < 0 || x >= size) continue;
                        modules[y, x] = Math.Abs(dy) == 2 || Math.Abs(dx) == 2 || (dy == 0 && dx == 0);
                        isFunc[y, x] = true;
                    }
                }
            }
        }
    }

    private static void DrawTimingPatterns(bool[,] modules, bool[,] isFunc, int size)
    {
        for (int i = 8; i < size - 8; i++)
        {
            if (!isFunc[6, i])
            {
                modules[6, i] = i % 2 == 0;
                isFunc[6, i] = true;
            }
            if (!isFunc[i, 6])
            {
                modules[i, 6] = i % 2 == 0;
                isFunc[i, 6] = true;
            }
        }
    }

    private static void DrawFormatBits(bool[,] modules, bool[,]? isFunc, int size, int mask)
    {
        // ECC Level M = 0, mask
        int data = (0 << 3) | mask;
        int rem = data;
        for (int i = 0; i < 10; i++)
            rem = (rem << 1) ^ ((rem >> 9) * 0x537);
        int bits = ((data << 10) | rem) ^ 0x5412;

        for (int i = 0; i <= 5; i++)
        {
            SetModule(modules, isFunc, 8, i, ((bits >> i) & 1) == 1);
            SetModule(modules, isFunc, size - 1 - i, 8, ((bits >> i) & 1) == 1);
        }
        SetModule(modules, isFunc, 8, 7, ((bits >> 6) & 1) == 1);
        SetModule(modules, isFunc, 8, 8, ((bits >> 7) & 1) == 1);
        SetModule(modules, isFunc, 7, 8, ((bits >> 8) & 1) == 1);

        SetModule(modules, isFunc, size - 7, 8, ((bits >> 6) & 1) == 1);

        // Bits 7-14 of the second copy go along row 8 from top-right
        for (int i = 7; i < 15; i++)
        {
            SetModule(modules, isFunc, 14 - i, 8, ((bits >> i) & 1) == 1);
            SetModule(modules, isFunc, 8, size - 15 + i, ((bits >> i) & 1) == 1);
        }

        // Dark module
        modules[size - 8, 8] = true;
        if (isFunc != null) isFunc[size - 8, 8] = true;
    }

    private static void SetModule(bool[,] modules, bool[,]? isFunc, int y, int x, bool dark)
    {
        modules[y, x] = dark;
        if (isFunc != null) isFunc[y, x] = true;
    }

    private static void PlaceDataBits(bool[,] modules, bool[,] isFunc, int size, bool[] bits)
    {
        int bitIdx = 0;
        for (int right = size - 1; right >= 1; right -= 2)
        {
            if (right == 6) right = 5;
            for (int vert = 0; vert < size; vert++)
            {
                for (int j = 0; j < 2; j++)
                {
                    int x = right - j;
                    bool upward = ((right + 1) & 2) == 0;
                    int y = upward ? size - 1 - vert : vert;

                    if (y < 0 || y >= size || x < 0 || x >= size) continue;
                    if (isFunc[y, x]) continue;
                    if (bitIdx < bits.Length)
                        modules[y, x] = bits[bitIdx++];
                }
            }
        }
    }

    private static void ApplyMask(bool[,] modules, bool[,] isFunc, int size, int mask)
    {
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (isFunc[y, x]) continue;
                bool invert = mask switch
                {
                    0 => (y + x) % 2 == 0,
                    1 => y % 2 == 0,
                    2 => x % 3 == 0,
                    3 => (y + x) % 3 == 0,
                    4 => (y / 2 + x / 3) % 2 == 0,
                    5 => y * x % 2 + y * x % 3 == 0,
                    6 => (y * x % 2 + y * x % 3) % 2 == 0,
                    7 => ((y + x) % 2 + y * x % 3) % 2 == 0,
                    _ => false
                };
                if (invert) modules[y, x] = !modules[y, x];
            }
        }
    }

    private static int CalculatePenalty(bool[,] modules, int size)
    {
        int penalty = 0;

        // Rule 1: consecutive same-color modules in row/col
        for (int y = 0; y < size; y++)
        {
            int run = 1;
            for (int x = 1; x < size; x++)
            {
                if (modules[y, x] == modules[y, x - 1]) run++;
                else run = 1;
                if (run == 5) penalty += 3;
                else if (run > 5) penalty++;
            }
        }
        for (int x = 0; x < size; x++)
        {
            int run = 1;
            for (int y = 1; y < size; y++)
            {
                if (modules[y, x] == modules[y - 1, x]) run++;
                else run = 1;
                if (run == 5) penalty += 3;
                else if (run > 5) penalty++;
            }
        }

        // Rule 2: 2x2 blocks
        for (int y = 0; y < size - 1; y++)
            for (int x = 0; x < size - 1; x++)
                if (modules[y, x] == modules[y, x + 1] &&
                    modules[y, x] == modules[y + 1, x] &&
                    modules[y, x] == modules[y + 1, x + 1])
                    penalty += 3;

        return penalty;
    }
}

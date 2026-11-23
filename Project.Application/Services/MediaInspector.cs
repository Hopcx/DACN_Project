using System.Buffers.Binary;

namespace Project.Application.Services;

public sealed record MediaKindInfo(string Kind, string ContentType, string Extension);

public sealed record MediaInspection(MediaKindInfo? Type, int? Width, int? Height, string? Error);

/// <summary>
/// To-be T14 (Q11b/Q12a): nhận dạng media bằng magic bytes — KHÔNG tin đuôi/MIME do client gửi.
/// Ảnh: JPEG, PNG, WebP (đọc kích thước từ header để chặn ảnh quá lớn trước khi trình duyệt giải mã).
/// Âm thanh: MP3 (ID3 hoặc frame sync), M4A/AAC (ftyp brand trong danh sách), OGG. Từ chối mọi loại khác (SVG, GIF, HTML…).
/// </summary>
public static class MediaInspector
{
    public static readonly MediaKindInfo Jpeg = new("image", "image/jpeg", ".jpg");
    public static readonly MediaKindInfo Png = new("image", "image/png", ".png");
    public static readonly MediaKindInfo WebP = new("image", "image/webp", ".webp");
    public static readonly MediaKindInfo Mp3 = new("audio", "audio/mpeg", ".mp3");
    public static readonly MediaKindInfo M4a = new("audio", "audio/mp4", ".m4a");
    public static readonly MediaKindInfo Ogg = new("audio", "audio/ogg", ".ogg");
    private static readonly string[] M4aBrands = ["M4A ", "M4B ", "mp42", "mp41", "isom", "iso2"];

    public static MediaKindInfo? Detect(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return Jpeg;
        if (b.Length >= 8 && b[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return Png;
        if (b.Length >= 12 && Ascii(b, 0, "RIFF") && Ascii(b, 8, "WEBP")) return WebP;
        if (b.Length >= 4 && Ascii(b, 0, "OggS")) return Ogg;
        if (b.Length >= 12 && Ascii(b, 4, "ftyp"))
            foreach (var brand in M4aBrands) if (Ascii(b, 8, brand)) return M4a;
        if (b.Length >= 10 && Ascii(b, 0, "ID3")) return Mp3;
        if (b.Length >= 2 && b[0] == 0xFF && (b[1] & 0xE0) == 0xE0 && (b[1] & 0x06) != 0) return Mp3; // MPEG audio frame sync, layer ≠ 0
        return null;
    }

    /// <summary>Kiểm loại, dung lượng và kích thước ảnh theo giới hạn (Q12a).</summary>
    public static MediaInspection Inspect(ReadOnlySpan<byte> bytes, MediaLimits limits)
    {
        if (bytes.Length == 0) return new(null, null, null, "MEDIA_EMPTY");
        var type = Detect(bytes);
        if (type == null) return new(null, null, null, "MEDIA_TYPE_NOT_ALLOWED");
        if (type.Kind == "image" && bytes.Length > limits.MaxImageBytes) return new(type, null, null, "MEDIA_TOO_LARGE");
        if (type.Kind == "audio" && bytes.Length > limits.MaxAudioBytes) return new(type, null, null, "MEDIA_TOO_LARGE");
        // Review T14: chỉ nhận ảnh tĩnh và âm thanh thuần — chặn APNG, WebP động, MP4 có track video
        if (type == M4a && !AudioOnlyMp4(bytes)) return new(type, null, null, "MEDIA_TYPE_NOT_ALLOWED");
        if (type == Png && PngAnimated(bytes)) return new(type, null, null, "MEDIA_TYPE_NOT_ALLOWED");
        if (type == WebP && WebPAnimated(bytes)) return new(type, null, null, "MEDIA_TYPE_NOT_ALLOWED");
        if (type.Kind != "image") return new(type, null, null, null);
        var size = type == Png ? PngSize(bytes) : type == Jpeg ? JpegSize(bytes) : WebPSize(bytes);
        if (size == null) return new(type, null, null, "MEDIA_CORRUPT");
        var (w, h) = size.Value;
        if (w <= 0 || h <= 0) return new(type, w, h, "MEDIA_CORRUPT");
        if (w > limits.MaxImageDimension || h > limits.MaxImageDimension) return new(type, w, h, "MEDIA_DIMENSIONS_TOO_LARGE");
        return new(type, w, h, null);
    }

    private static bool Ascii(ReadOnlySpan<byte> b, int offset, string text)
    {
        if (b.Length < offset + text.Length) return false;
        for (var i = 0; i < text.Length; i++) if (b[offset + i] != (byte)text[i]) return false;
        return true;
    }

    // MP4/M4A: hộp 'hdlr' có handler_type ở byte +12 (sau size/type ngoài, version/flags, pre_defined). Không được có 'vide';
    // cần ít nhất một 'soun'. Quét cả file (moov có thể nằm cuối, sau mdat).
    private static bool AudioOnlyMp4(ReadOnlySpan<byte> b)
    {
        var sound = false;
        for (var i = 4; i + 16 <= b.Length; i++)
        {
            if (b[i] != (byte)'h' || !Ascii(b, i, "hdlr")) continue;
            if (Ascii(b, i + 12, "vide")) return false;
            if (Ascii(b, i + 12, "soun")) sound = true;
        }
        return sound;
    }

    // APNG: có chunk 'acTL' trước 'IDAT'
    private static bool PngAnimated(ReadOnlySpan<byte> b)
    {
        var i = 8;
        while (i + 8 <= b.Length)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(b.Slice(i, 4));
            if (Ascii(b, i + 4, "acTL")) return true;
            if (Ascii(b, i + 4, "IDAT") || Ascii(b, i + 4, "IEND") || length > int.MaxValue - 12) return false;
            i += 12 + (int)length;
        }
        return false;
    }

    // WebP động: VP8X có cờ ANIMATION (0x02) hoặc có chunk 'ANIM'/'ANMF'
    private static bool WebPAnimated(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 21 && Ascii(b, 12, "VP8X") && (b[20] & 0x02) != 0) return true;
        var i = 12;
        while (i + 8 <= b.Length)
        {
            if (Ascii(b, i, "ANIM") || Ascii(b, i, "ANMF")) return true;
            var size = BinaryPrimitives.ReadUInt32LittleEndian(b.Slice(i + 4, 4));
            if (size > int.MaxValue - 9) return false;
            i += 8 + (int)size + (int)(size & 1);
        }
        return false;
    }

    private static (int, int)? PngSize(ReadOnlySpan<byte> b) =>
        b.Length >= 24 && Ascii(b, 12, "IHDR")
            ? ((int)BinaryPrimitives.ReadUInt32BigEndian(b.Slice(16, 4)), (int)BinaryPrimitives.ReadUInt32BigEndian(b.Slice(20, 4)))
            : null;

    // Duyệt các marker JPEG tới SOFn (C0–CF trừ C4, C8, CC)
    private static (int, int)? JpegSize(ReadOnlySpan<byte> b)
    {
        var i = 2;
        while (i + 9 < b.Length)
        {
            if (b[i] != 0xFF) return null;
            var marker = b[i + 1];
            if (marker == 0xFF) { i++; continue; }
            if (marker is 0xD8 or 0x01 || marker is >= 0xD0 and <= 0xD7) { i += 2; continue; }
            var length = BinaryPrimitives.ReadUInt16BigEndian(b.Slice(i + 2, 2));
            if (length < 2) return null;
            if (marker is >= 0xC0 and <= 0xCF && marker is not (0xC4 or 0xC8 or 0xCC))
                return (BinaryPrimitives.ReadUInt16BigEndian(b.Slice(i + 7, 2)), BinaryPrimitives.ReadUInt16BigEndian(b.Slice(i + 5, 2)));
            i += 2 + length;
        }
        return null;
    }

    private static (int, int)? WebPSize(ReadOnlySpan<byte> b)
    {
        if (b.Length < 30) return null;
        if (Ascii(b, 12, "VP8X"))
            return (1 + (b[24] | b[25] << 8 | b[26] << 16), 1 + (b[27] | b[28] << 8 | b[29] << 16));
        if (Ascii(b, 12, "VP8L") && b[20] == 0x2F)
        {
            var bits = BinaryPrimitives.ReadUInt32LittleEndian(b.Slice(21, 4));
            return ((int)(bits & 0x3FFF) + 1, (int)((bits >> 14) & 0x3FFF) + 1);
        }
        if (Ascii(b, 12, "VP8 ") && b[23] == 0x9D && b[24] == 0x01 && b[25] == 0x2A)
            return (BinaryPrimitives.ReadUInt16LittleEndian(b.Slice(26, 2)) & 0x3FFF, BinaryPrimitives.ReadUInt16LittleEndian(b.Slice(28, 2)) & 0x3FFF);
        return null;
    }
}

/// <summary>To-be T14 (Q12a): giới hạn media, cấu hình ở <c>Media:*</c>.</summary>
public sealed record MediaLimits(long MaxImageBytes = 2 * 1024 * 1024, long MaxAudioBytes = 10 * 1024 * 1024, int MaxImageDimension = 4096,
    int MaxPerQuestion = 5, int MaxAudioPerQuestion = 1, int UrlMinutes = 10)
{
    public MediaLimits Validate()
    {
        if (MaxImageBytes is < 1024 or > 50L * 1024 * 1024) throw new InvalidOperationException("Media:MaxImageBytes must be 1 KB..50 MB.");
        if (MaxAudioBytes is < 1024 or > 100L * 1024 * 1024) throw new InvalidOperationException("Media:MaxAudioBytes must be 1 KB..100 MB.");
        if (MaxImageDimension is < 16 or > 20000) throw new InvalidOperationException("Media:MaxImageDimension must be 16..20000.");
        if (MaxPerQuestion is < 1 or > 20 || MaxAudioPerQuestion < 0 || MaxAudioPerQuestion > MaxPerQuestion)
            throw new InvalidOperationException("Media:MaxPerQuestion/MaxAudioPerQuestion invalid.");
        if (UrlMinutes is < 1 or > 60) throw new InvalidOperationException("Media:UrlMinutes must be 1..60.");
        return this;
    }

    public long MaxUploadBytes => Math.Max(MaxImageBytes, MaxAudioBytes);
}

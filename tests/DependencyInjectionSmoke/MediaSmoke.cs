using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Project.Api.Services;
using Project.Application.Services;

// To-be T14: nhận dạng media bằng magic bytes, kích thước ảnh, token URL ký tạm, cấu hình, lưu trữ cục bộ.
internal static class MediaSmoke
{
    public static byte[] Png(int w, int h)
    {
        var b = new byte[33];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R' }.CopyTo(b, 0);
        b[16] = (byte)(w >> 24); b[17] = (byte)(w >> 16); b[18] = (byte)(w >> 8); b[19] = (byte)w;
        b[20] = (byte)(h >> 24); b[21] = (byte)(h >> 16); b[22] = (byte)(h >> 8); b[23] = (byte)h;
        return b;
    }

    public static byte[] Jpeg(int w, int h) =>
    [
        0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x04, 0x00, 0x00,                                // SOI + APP0 (độ dài 4)
        0xFF, 0xC0, 0x00, 0x11, 0x08, (byte)(h >> 8), (byte)h, (byte)(w >> 8), (byte)w, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0xFF, 0xD9
    ];

    public static void Run()
    {
        var limits = new MediaLimits().Validate();
        MediaInspection I(byte[] bytes) => MediaInspector.Inspect(bytes, limits);
        var png = I(Png(640, 480));
        Check(png.Type == MediaInspector.Png && png.Width == 640 && png.Height == 480 && png.Error == null, "PNG + size.");
        var jpg = I(Jpeg(800, 600));
        Check(jpg.Type == MediaInspector.Jpeg && jpg.Width == 800 && jpg.Height == 600 && jpg.Error == null, "JPEG SOF0 size.");
        var webpX = new byte[30]; Encoding.ASCII.GetBytes("RIFF").CopyTo(webpX, 0); Encoding.ASCII.GetBytes("WEBPVP8X").CopyTo(webpX, 8);
        webpX[24] = 99; webpX[27] = 49;   // (99+1) × (49+1)
        Check(I(webpX) is { Type: var t1, Width: 100, Height: 50, Error: null } && t1 == MediaInspector.WebP, "WebP VP8X size.");
        var webpL = new byte[30]; Encoding.ASCII.GetBytes("RIFF").CopyTo(webpL, 0); Encoding.ASCII.GetBytes("WEBPVP8L").CopyTo(webpL, 8);
        webpL[20] = 0x2F; var bits = (uint)(199 | (99 << 14)); BitConverter.GetBytes(bits).CopyTo(webpL, 21);
        Check(I(webpL) is { Width: 200, Height: 100, Error: null }, "WebP VP8L size.");
        Check(I(Png(4097, 10)).Error == "MEDIA_DIMENSIONS_TOO_LARGE" && I(Png(0, 10)).Error == "MEDIA_CORRUPT", "Image dimension limits.");
        Check(I([0xFF, 0xD8, 0xFF, 0xE0]).Error == "MEDIA_CORRUPT", "Truncated JPEG rejected.");
        var mp3 = I(Encoding.ASCII.GetBytes("ID3\x04\0\0\0\0\0\0rest"));
        Check(mp3.Type == MediaInspector.Mp3 && mp3.Error == null, "MP3 ID3.");
        Check(MediaInspector.Detect(new byte[] { 0xFF, 0xFB, 0x90, 0x00 }) == MediaInspector.Mp3, "MP3 frame sync.");
        Check(MediaInspector.Detect(Encoding.ASCII.GetBytes("\0\0\0 ftypM4A \0\0\0\0")) == MediaInspector.M4a, "M4A ftyp.");
        Check(MediaInspector.Detect(Encoding.ASCII.GetBytes("\0\0\0 ftypqt  \0\0\0\0")) == null, "QuickTime (mov) brand rejected.");
        Check(MediaInspector.Detect(Encoding.ASCII.GetBytes("OggS\0\x02")) == MediaInspector.Ogg, "OGG.");
        // Review T14: chỉ âm thanh thuần / ảnh tĩnh; file cắt cụt, segment JPEG sai độ dài
        byte[] Mp4(string brand, params string[] handlers)
        {
            var bytes = new List<byte>(Encoding.ASCII.GetBytes("\0\0\0 ftyp" + brand + "\0\0\0\0"));
            foreach (var h in handlers) bytes.AddRange(Encoding.ASCII.GetBytes("\0\0\0!hdlr\0\0\0\0\0\0\0\0" + h + "\0\0\0\0"));
            return bytes.ToArray();
        }
        Check(I(Mp4("M4A ", "soun")).Error == null && I(Mp4("isom", "soun")).Error == null, "Audio-only MP4/M4A accepted.");
        Check(I(Mp4("isom", "vide", "soun")).Error == "MEDIA_TYPE_NOT_ALLOWED" && I(Mp4("mp42")).Error == "MEDIA_TYPE_NOT_ALLOWED",
            "MP4 with video track or without audio track rejected.");
        var apng = Png(10, 10).Take(33).ToList();
        apng.AddRange(new byte[] { 0, 0, 0, 8 }); apng.AddRange(Encoding.ASCII.GetBytes("acTL")); apng.AddRange(new byte[12]);
        var pngWithIhdrCrc = Png(10, 10); // IHDR (13 byte dữ liệu) + CRC kết thúc ở byte 33
        Check(I(pngWithIhdrCrc).Error == null && I(apng.ToArray()).Error == "MEDIA_TYPE_NOT_ALLOWED", "APNG rejected, static PNG accepted.");
        var animWebp = (byte[])webpX.Clone(); animWebp[20] = 0x02;
        Check(I(animWebp).Error == "MEDIA_TYPE_NOT_ALLOWED", "Animated WebP rejected.");
        Check(I(Png(10, 10)[..20]).Error == "MEDIA_CORRUPT" && I(webpX[..20]).Error == "MEDIA_CORRUPT", "Truncated PNG/WebP rejected.");
        Check(I([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x01, 0, 0, 0, 0, 0, 0, 0]).Error == "MEDIA_CORRUPT" &&
            I([0xFF, 0xD8, 0xFF, 0xE0, 0xFF, 0xFF, 0, 0, 0, 0, 0, 0, 0]).Error == "MEDIA_CORRUPT", "JPEG with bad segment length rejected.");
        foreach (var bad in new[] { "<svg xmlns='http://www.w3.org/2000/svg'/>", "GIF89a....", "<html><script>", "MZ\x90\0" })
            Check(I(Encoding.ASCII.GetBytes(bad)).Error == "MEDIA_TYPE_NOT_ALLOWED", $"Rejected: {bad[..3]}");
        Check(I([]).Error == "MEDIA_EMPTY", "Empty rejected.");
        var bigImage = new byte[limits.MaxImageBytes + 1]; Png(10, 10).CopyTo(bigImage, 0);
        Check(I(bigImage).Error == "MEDIA_TOO_LARGE", "Image over 2 MB rejected.");
        var bad2 = 0;
        foreach (var cfg in new[] { new MediaLimits(MaxImageBytes: 10), new MediaLimits(UrlMinutes: 0), new MediaLimits(MaxPerQuestion: 1, MaxAudioPerQuestion: 2) })
            try { cfg.Validate(); } catch (InvalidOperationException) { bad2++; }
        Check(bad2 == 3, "Invalid limits rejected.");

        // Token: đúng media + chưa hết hạn; sai media/khóa/chữ ký/hết hạn → null
        var key = Encoding.UTF8.GetBytes(new string('k', 32)); var media = Guid.NewGuid(); var user = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow; var token = MediaToken.Sign(key, media, user, now.AddMinutes(10));
        Check(MediaToken.Verify(key, media, token, now) == user, "Token valid.");
        Check(MediaToken.Verify(key, Guid.NewGuid(), token, now) == null, "Token bound to media.");
        Check(MediaToken.Verify(key, media, token, now.AddMinutes(11)) == null, "Token expires.");
        Check(MediaToken.Verify(Encoding.UTF8.GetBytes(new string('x', 32)), media, token, now) == null, "Token bound to key.");
        var forged = token[..^2] + (token[^2] == 'A' ? "BB" : "AA");
        var otherUser = $"{Guid.NewGuid():N}" + token[32..];
        Check(MediaToken.Verify(key, media, forged, now) == null && MediaToken.Verify(key, media, otherUser, now) == null &&
            MediaToken.Verify(key, media, "a.b.c", now) == null && MediaToken.Verify(key, media, null, now) == null, "Forged/edited tokens rejected.");

        // Cấu hình (QT14-4)
        var root = Path.Combine(Path.GetTempPath(), "dacn-media-smoke-" + Guid.NewGuid().ToString("N"));
        var content = Path.Combine(root, "api");
        Directory.CreateDirectory(content);
        IConfiguration Cfg(params (string, string)[] values) => new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(x => x.Item1, x => (string?)x.Item2)).Build();
        var dev = MediaSettings.From(Cfg(), new Env("Development", content), null);
        Check(dev.RootPath == Path.GetFullPath(Path.Combine(content, "..", "dacn-media-dev")) && dev.SigningKey.Length == 32, "Development defaults.");
        var failures = 0;
        foreach (var (env, cfg, web) in new[]
        {
            ("Production", Cfg(("Media:SigningKey", new string('s', 32))), (string?)null),
            ("Production", Cfg(("Media:RootPath", root)), null),
            ("Production", Cfg(("Media:RootPath", root), ("Media:SigningKey", "short")), null),
            ("Development", Cfg(("Media:RootPath", Path.Combine(content, "wwwroot", "media"))), Path.Combine(content, "wwwroot")),
        })
            try { MediaSettings.From(cfg, new Env(env, content), web); } catch (InvalidOperationException) { failures++; }
        Check(failures == 4, "Missing root/key outside Development, short key, root inside wwwroot → startup error.");
        var prod = MediaSettings.From(Cfg(("Media:RootPath", root), ("Media:SigningKey", new string('s', 32))), new Env("Production", content), null);

        // Lưu cục bộ: <root>/<2 ký tự>/<guid>, ghi lại cùng id không lỗi
        var storage = new LocalMediaStorage(prod);
        var id = Guid.NewGuid();
        storage.SaveAsync(id, [1, 2, 3]).GetAwaiter().GetResult();
        storage.SaveAsync(id, [9]).GetAwaiter().GetResult();
        var path = storage.PhysicalPath(id);
        Check(path == Path.Combine(root, id.ToString("N")[..2], id.ToString("N")) && File.ReadAllBytes(path!).Length == 3 &&
            storage.PhysicalPath(Guid.NewGuid()) == null && !Directory.GetFiles(root, "*.tmp", SearchOption.AllDirectories).Any(), "Local storage.");
        Directory.Delete(root, true);
        // To-be T15: snapshot v4 chụp media (đúng thứ tự, bỏ media đã tắt); URL học viên ký cho đúng user + media
        var q = new Project.Domain.Entities.Question { Id = 5, QuestionTypeId = 2, Content = "Q", Status = 1 };
        q.Answers.Add(new Project.Domain.Entities.Answer { Id = 50, QuestionId = 5, Content = "A", IsCorrect = true, Status = 1 });
        q.Answers.Add(new Project.Domain.Entities.Answer { Id = 51, QuestionId = 5, Content = "B", IsCorrect = false, Status = 1 });
        Guid m1 = Guid.NewGuid(), m2 = Guid.NewGuid(), m3 = Guid.NewGuid();
        q.QuestionMedia.Add(new Project.Domain.Entities.QuestionMedia { QuestionId = 5, MediaId = m2, SortOrder = 2,
            Media = new Project.Domain.Entities.MediaFile { Id = m2, Kind = "audio", Status = 1 } });
        q.QuestionMedia.Add(new Project.Domain.Entities.QuestionMedia { QuestionId = 5, MediaId = m1, SortOrder = 1,
            Media = new Project.Domain.Entities.MediaFile { Id = m1, Kind = "image", Width = 640, Height = 480, Status = 1 } });
        q.QuestionMedia.Add(new Project.Domain.Entities.QuestionMedia { QuestionId = 5, MediaId = m3, SortOrder = 3,
            Media = new Project.Domain.Entities.MediaFile { Id = m3, Kind = "image", Status = 0 } });
        var built = AttemptSnapshotFactory.BuildQuestion(q, 1);
        Check(built.Media!.Select(x => x.Id).SequenceEqual(new[] { m1, m2 }) && built.Media[0].Width == 640 && built.Media[1].Kind == "audio",
            "v4 captures active media in order.");
        var json = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        var snap = new Project.Domain.ValueObjects.AttemptSnapshot(Project.Domain.ValueObjects.AttemptSnapshot.CurrentVersion, 1, 1, "E", "V", [built]);
        var back = System.Text.Json.JsonSerializer.Deserialize<Project.Domain.ValueObjects.AttemptSnapshot>(System.Text.Json.JsonSerializer.Serialize(snap, json), json)!;
        Check(back.Version == Project.Domain.ValueObjects.AttemptSnapshot.CurrentVersion && back.Questions[0].Media!.Count == 2, "v4+ JSON round-trips media.");
        var v3 = System.Text.Json.JsonSerializer.Deserialize<Project.Domain.ValueObjects.AttemptSnapshot>(
            """{"version":3,"examId":1,"examDetailId":1,"examName":"E","variantCode":"V","questions":[{"id":"00000000-0000-0000-0000-000000000001","sourceQuestionId":5,"questionTypeId":2,"content":"Q","point":1,"options":[],"chapterId":2,"tagIds":[1]}]}""", json)!;
        Check(v3.Questions[0].Media == null && AttemptSnapshotFactory.ForStudent(v3, _ => "x")[0].Media!.Count == 0, "v3 (no media) still reads; no media for students.");
        var student = Guid.NewGuid();
        var urls = MediaUrlFactory.For(prod, student);
        var dto = AttemptSnapshotFactory.ForStudent(snap, urls)[0];
        var t = Uri.UnescapeDataString(dto.Media![0].Url.Split("?t=")[1]);
        Check(dto.Media.Count == 2 && dto.Media[0].Url.StartsWith($"/web/media/{m1}?t=") && MediaToken.Verify(prod.SigningKey, m1, t, DateTimeOffset.UtcNow) == student &&
            MediaToken.Verify(prod.SigningKey, m2, t, DateTimeOffset.UtcNow) == null, "Student URL signed for that user and that media only.");
        Check(AttemptSnapshotFactory.ForStudent(snap)[0].Media!.Count == 0, "No URL function → no media exposed.");
        var json2 = System.Text.Json.JsonSerializer.Serialize(dto, json);
        Check(!json2.Contains("isCorrect", StringComparison.OrdinalIgnoreCase) && !json2.Contains("sourceQuestionId", StringComparison.OrdinalIgnoreCase),
            "Student DTO with media still leaks no correctness/source ids.");
        Console.WriteLine("MEDIA_MAGIC_BYTES_DIMENSIONS_TOKEN_SETTINGS_STORAGE_SNAPSHOT_V4=PASS (isolated; no SQL/API)");
    }

    private sealed class Env(string name, string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "MediaSmoke";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}

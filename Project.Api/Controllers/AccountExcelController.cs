using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Project.Api.Services;
using Project.Application.Common;
using Project.Domain.Entities;
using Project.Infrastructure.Persistence;

namespace Project.Api.Controllers;

// P4/F13: nhập và xuất tài khoản bằng Excel (workbook v1, sheet Accounts).
// Nhập: kiểm toàn file trước, trùng hoặc lỗi thì từ chối cả file (không ghi dở dang).
[ApiController]
[Authorize(Policy = "AdminManagement")]
[Route("web/users/excel")]
public class AccountExcelController : ControllerBase
{
    private const int MaxBytes = 2 * 1024 * 1024;
    private const int MaxRows = 500;
    private static readonly Regex UserNameRule = new("^[a-zA-Z0-9_]+$", RegexOptions.CultureInvariant);
    private static readonly Regex PhoneRule = new("^[0-9]{10,11}$", RegexOptions.CultureInvariant);
    private static readonly Regex PasswordRule = new("^(?=.*[A-Z])(?=.*[a-z])(?=.*[0-9]).+$", RegexOptions.CultureInvariant);
    private static readonly string[] ExportHeaders =
        ["FullName", "DateOfBirth", "Email", "PhoneNumber", "Address", "Sex", "UserName", "LevelId", "Status", "EmailVerified"];

    private readonly ProjectDACNDbContext _db;
    private readonly IVerificationEmailSender _email;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<AccountExcelController> _logger;
    private readonly AuditWriter _audit;

    public AccountExcelController(ProjectDACNDbContext db, IVerificationEmailSender email,
        IConfiguration configuration, IWebHostEnvironment environment,
        ILogger<AccountExcelController> logger, AuditWriter audit)
    {
        _db = db; _email = email; _configuration = configuration;
        _environment = environment; _logger = logger; _audit = audit;
    }

    private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private sealed record ParsedAccount(int Row, string FullName, DateTime DateOfBirth, string Email,
        string? Phone, string Address, bool Sex, string UserName, string Password);

    [HttpGet("template")]
    public IActionResult Template() => File(AccountWorkbook.Template(), XlsxType, "dacn-accounts-v1.xlsx");

    [HttpPost("import")]
    [RequestSizeLimit(MaxBytes + 65536)]
    public async Task<IActionResult> Import([FromForm] IFormFile file, [FromForm] int levelId)
    {
        if (levelId is not (3 or 4))
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Vai trò nhập chỉ nhận 3 (giảng viên) hoặc 4 (học viên)",
                Data = new { imported = 0, errors = new[] { new ImportIssue(0, "levelId", "LEVEL_ID", "levelId chỉ nhận 3 hoặc 4") } } });
        if (file == null || file.Length == 0 || file.Length > MaxBytes ||
            !file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse<object>.Fail("File .xlsx phải có dung lượng 1 byte đến 2 MB"));
        List<WorkbookRow> rows;
        try
        {
            using var stream = file.OpenReadStream();
            rows = AccountWorkbook.Read(stream, MaxRows);
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(new ApiResponse<object> { Success = false, Message = "File Excel không hợp lệ", Data = new { imported = 0,
                errors = new[] { new { row = 0, column = "file", code = "INVALID_XLSX", message = ex.Message } } } });
        }

        var errors = new List<ImportIssue>();
        var parsed = new List<ParsedAccount>();
        var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var userNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var phones = new HashSet<string>(StringComparer.Ordinal);
        var today = DateTime.UtcNow.Date;
        foreach (var row in rows)
        {
            if (row.Values.Length != AccountWorkbook.Headers.Length)
            { errors.Add(new(row.Number, "header", "COLUMN_COUNT", "Cần đúng tám cột")); continue; }
            if (row.Values.All(x => x.Length == 0))
            { errors.Add(new(row.Number, "A-H", "EMPTY_ROW", "Dòng trống nằm giữa dữ liệu")); continue; }
            var fullName = row.Values[0].Trim();
            if (fullName.Length is < 1 or > 100)
                errors.Add(new(row.Number, "A", "FULL_NAME", "Họ tên cần 1–100 ký tự"));
            var dateOfBirth = ParseDate(row.Values[1], out var dateOk);
            if (!dateOk || dateOfBirth < new DateTime(1900, 1, 1) || dateOfBirth > today)
                errors.Add(new(row.Number, "B", "DATE_OF_BIRTH", "Ngày sinh dạng yyyy-MM-dd (hoặc số ngày Excel), từ 1900-01-01 đến hôm nay"));
            var email = row.Values[2].Trim().ToLowerInvariant();
            if (email.Length is < 1 or > 450 || !new EmailAddressAttribute().IsValid(email))
                errors.Add(new(row.Number, "C", "EMAIL", "Email không hợp lệ (tối đa 450 ký tự)"));
            var phone = row.Values[3].Trim();
            if (phone.Length > 0 && !PhoneRule.IsMatch(phone))
                errors.Add(new(row.Number, "D", "PHONE", "Số điện thoại gồm 10–11 chữ số hoặc để trống"));
            var address = row.Values[4].Trim();
            if (address.Length is < 1 or > 200)
                errors.Add(new(row.Number, "E", "ADDRESS", "Địa chỉ cần 1–200 ký tự"));
            var sexText = row.Values[5].Trim();
            if (sexText is not ("1" or "0"))
                errors.Add(new(row.Number, "F", "SEX", "Giới tính chỉ nhận 1 (nam) hoặc 0 (nữ)"));
            var userName = row.Values[6].Trim();
            if (userName.Length is < 6 or > 100 || !UserNameRule.IsMatch(userName))
                errors.Add(new(row.Number, "G", "USER_NAME", "Tên đăng nhập 6–100 ký tự gồm chữ, số, dấu gạch dưới"));
            var password = row.Values[7];
            if (password.Length is < 8 or > 128 || !PasswordRule.IsMatch(password))
                errors.Add(new(row.Number, "H", "PASSWORD", "Mật khẩu 8–128 ký tự, có chữ hoa, chữ thường và số"));
            if (!emails.Add(email)) errors.Add(new(row.Number, "C", "DUPLICATE_FILE", "Email trùng trong file"));
            if (!userNames.Add(userName)) errors.Add(new(row.Number, "G", "DUPLICATE_FILE", "Tên đăng nhập trùng trong file"));
            if (phone.Length > 0 && !phones.Add(phone)) errors.Add(new(row.Number, "D", "DUPLICATE_FILE", "Số điện thoại trùng trong file"));
            if (errors.Any(x => x.Row == row.Number)) continue;
            parsed.Add(new(row.Number, fullName, dateOfBirth, email, phone.Length == 0 ? null : phone,
                address, sexText == "1", userName, password));
        }
        if (rows.Count == 0) errors.Add(new(0, "file", "EMPTY", "Sheet Accounts không có dữ liệu"));
        if (errors.Count > 0) return BadRequest(new ApiResponse<object>
        { Success = false, Message = "Dữ liệu Excel không hợp lệ", Data = new { imported = 0, errors } });

        // Dựng URL xác minh trước khi ghi gì vào DB: thiếu cấu hình thì lỗi sớm, chưa có tài khoản nào được tạo.
        var baseUrl = _configuration["Mail:PublicFrontendUrl"] ??
            (_environment.IsDevelopment() ? "https://localhost:5173" :
                throw new InvalidOperationException("Missing public frontend URL"));

        // BCrypt chạy sau khi file hợp lệ và trước transaction; song song để 500 dòng không giữ request quá lâu.
        var hashes = new string[parsed.Count];
        Parallel.For(0, parsed.Count,
            new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) },
            i => hashes[i] = BCrypt.Net.BCrypt.HashPassword(parsed[i].Password));

        var created = new List<(string Email, string RawToken)>();
        await using (var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            if (!await _db.Levels.AnyAsync(x => x.Id == levelId && x.Status == 1))
                return Conflict(new ApiResponse<object> { Success = false, Message = "Vai trò không tồn tại hoặc đã ngừng dùng",
                    Data = new { imported = 0, errors = new[] { new ImportIssue(0, "levelId", "LEVEL_NOT_FOUND", "Vai trò không tồn tại hoặc đã ngừng dùng") } } });
            // 3 truy vấn gộp (Contains) thay cho 3 truy vấn mỗi dòng; cột Email dùng collation không phân biệt hoa/thường
            // của DB nên không cần ToLower (giữ được index), còn so khớp ngược theo dòng thì bỏ qua hoa/thường.
            var emailList = parsed.Select(x => x.Email).ToList();
            var userNameList = parsed.Select(x => x.UserName).ToList();
            var phoneList = parsed.Where(x => x.Phone != null).Select(x => x.Phone!).ToList();
            var usedEmails = new HashSet<string>(
                await _db.Users.Where(x => emailList.Contains(x.Email)).Select(x => x.Email).ToListAsync(),
                StringComparer.OrdinalIgnoreCase);
            var usedUserNames = new HashSet<string>(
                await _db.Users.Where(x => userNameList.Contains(x.UserName)).Select(x => x.UserName).ToListAsync(),
                StringComparer.OrdinalIgnoreCase);
            var usedPhones = new HashSet<string>(
                await _db.Users.Where(x => x.PhoneNumber != null && phoneList.Contains(x.PhoneNumber))
                    .Select(x => x.PhoneNumber!).ToListAsync());
            foreach (var item in parsed)
            {
                if (usedEmails.Contains(item.Email))
                    errors.Add(new(item.Row, "C", "DUPLICATE_DB", "Email đã được sử dụng"));
                if (usedUserNames.Contains(item.UserName))
                    errors.Add(new(item.Row, "G", "DUPLICATE_DB", "Tên đăng nhập đã được sử dụng"));
                if (item.Phone != null && usedPhones.Contains(item.Phone))
                    errors.Add(new(item.Row, "D", "DUPLICATE_DB", "Số điện thoại đã được sử dụng"));
            }
            if (errors.Count > 0) return Conflict(new ApiResponse<object>
            { Success = false, Message = "Dữ liệu Excel xung đột với tài khoản hiện có", Data = new { imported = 0, errors } });

            var now = DateTime.UtcNow;
            for (var i = 0; i < parsed.Count; i++)
            {
                var item = parsed[i];
                var user = new User
                {
                    Id = Guid.NewGuid(), FullName = item.FullName, UserName = item.UserName, Email = item.Email,
                    PhoneNumber = item.Phone, Address = item.Address, DateOfBirth = item.DateOfBirth, Sex = item.Sex,
                    Status = 1, LevelId = levelId, PasswordHash = hashes[i], EmailVerifiedAt = null,
                    LastVerificationSentAt = now
                };
                var rawToken = AuthTokenTools.NewToken();
                _db.Users.Add(user);
                _db.EmailVerificationTokens.Add(new EmailVerificationToken
                {
                    UserId = user.Id, TokenHash = AuthTokenTools.Hash(rawToken),
                    CreatedAt = now, ExpiresAt = now.AddHours(24)
                });
                created.Add((item.Email, rawToken));
            }
            _audit.Add(User, "account.import", "User", null, $"Nhập {parsed.Count} tài khoản; levelId={levelId}");
            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sql &&
                (sql.Number == 2601 || sql.Number == 2627))
            {
                return Conflict(new ApiResponse<object> { Success = false, Message = "Dữ liệu Excel xung đột với tài khoản hiện có",
                    Data = new { imported = 0, errors = new[] { new ImportIssue(0, "file", "DUPLICATE_DB", "Email, tên đăng nhập hoặc số điện thoại đã được sử dụng") } } });
            }
            await transaction.CommitAsync();
        }

        var emailFailed = 0;
        foreach (var (recipient, rawToken) in created)
        {
            var verificationUrl = $"{baseUrl.TrimEnd('/')}/auth/verify-email#token={Uri.EscapeDataString(rawToken)}";
            try { await _email.SendAsync(recipient, verificationUrl); }
            catch (Exception ex)
            {
                emailFailed++;
                _logger.LogWarning("Could not send imported account verification: {ErrorType}", ex.GetType().Name);
            }
        }
        return Ok(ApiResponse<object>.Ok(new { imported = created.Count, emailFailed, errors = Array.Empty<ImportIssue>() }));
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] int? levelId)
    {
        if (levelId is < 1 or > 4) return BadRequest(ApiResponse<string>.Fail("INVALID_FILTER"));
        var query = _db.Users.AsNoTracking().AsQueryable();
        if (levelId.HasValue) query = query.Where(x => x.LevelId == levelId.Value);
        // Projection: PasswordHash không bao giờ được đọc vào bộ nhớ.
        var users = await query.OrderBy(x => x.UserName).ThenBy(x => x.Id)
            .Select(x => new { x.FullName, x.DateOfBirth, x.Email, x.PhoneNumber, x.Address, x.Sex, x.UserName,
                x.LevelId, x.Status, Verified = x.EmailVerifiedAt != null }).ToListAsync();
        var rows = users.Select(x => new[]
        {
            x.FullName, x.DateOfBirth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), x.Email,
            x.PhoneNumber ?? "", x.Address, x.Sex ? "1" : "0", x.UserName,
            x.LevelId.ToString(CultureInfo.InvariantCulture), x.Status.ToString(CultureInfo.InvariantCulture),
            x.Verified ? "1" : "0"
        }).ToList();
        var bytes = AccountWorkbook.Write(ExportHeaders, rows);
        _audit.Add(User, "account.export", "User", null,
            $"Xuất {rows.Count} tài khoản; levelId={levelId?.ToString() ?? "-"}");
        await _db.SaveChangesAsync();
        return File(bytes, XlsxType, "dacn-accounts.xlsx");
    }

    // yyyy-MM-dd hoặc số nguyên ngày serial của Excel; mọi dạng khác (dd/MM/yyyy, có phần giờ) bị từ chối.
    private static DateTime ParseDate(string value, out bool ok)
    {
        ok = false;
        if (Regex.IsMatch(value, "^[0-9]{1,7}$", RegexOptions.CultureInvariant))
        {
            var serial = int.Parse(value, CultureInfo.InvariantCulture);
            if (serial is < 1 or > 2958465) return default;
            ok = true;
            return DateTime.FromOADate(serial);
        }
        ok = DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed);
        return ok ? parsed : default;
    }
}

public static class AccountWorkbook
{
    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string Sheet = "xl/worksheets/sheet1.xml";
    private const string SheetName = "Accounts";
    public static readonly string[] Headers =
        ["FullName", "DateOfBirth", "Email", "PhoneNumber", "Address", "Sex", "UserName", "Password"];

    public static List<WorkbookRow> Read(Stream stream, int maxRows)
    {
        try
        {
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, true);
            if (zip.Entries.Count > 30 || zip.Entries.Sum(x => x.Length) > 8 * 1024 * 1024 ||
                zip.Entries.Any(x => x.FullName.Contains("..") || x.Length > 4 * 1024 * 1024))
                throw new InvalidDataException("Workbook vượt giới hạn giải nén");
            var sheet = zip.GetEntry(Sheet) ?? throw new InvalidDataException("Thiếu sheet Accounts");
            var book = zip.GetEntry("xl/workbook.xml") ?? throw new InvalidDataException("Thiếu workbook");
            using (var source = book.Open())
            {
                var workbook = XDocument.Load(source);
                if (workbook.Descendants(S + "sheet").SingleOrDefault()?.Attribute("name")?.Value != SheetName ||
                    workbook.Descendants(S + "sheet").Count() != 1)
                    throw new InvalidDataException("Workbook phải có đúng một sheet Accounts");
            }
            var strings = new List<string>();
            if (zip.GetEntry("xl/sharedStrings.xml") is { } shared)
            {
                using var source = shared.Open();
                strings = XDocument.Load(source).Descendants(S + "si")
                    .Select(x => string.Concat(x.Descendants(S + "t").Select(t => t.Value))).ToList();
            }
            using var input = sheet.Open();
            var xml = XDocument.Load(input);
            var rows = xml.Descendants(S + "row").ToList();
            if (rows.Count == 0 || rows.Count > maxRows + 1)
                throw new InvalidDataException("File cần dòng tiêu đề và tối đa 500 dòng dữ liệu");
            string[] Cells(XElement row)
            {
                var result = Enumerable.Repeat(string.Empty, Headers.Length).ToArray();
                foreach (var cell in row.Elements(S + "c"))
                {
                    var reference = cell.Attribute("r")?.Value ?? "";
                    var column = new string(reference.TakeWhile(char.IsLetter).ToArray());
                    var index = "ABCDEFGH".IndexOf(column, StringComparison.Ordinal);
                    if (column.Length != 1 || index < 0 || cell.Element(S + "f") != null)
                        throw new InvalidDataException($"Cột hoặc công thức không hợp lệ tại {reference}");
                    var type = cell.Attribute("t")?.Value;
                    var value = type == "inlineStr" ? string.Concat(cell.Descendants(S + "t").Select(x => x.Value))
                        : cell.Element(S + "v")?.Value ?? "";
                    if (type == "s") value = int.TryParse(value, out var sharedIndex) && sharedIndex >= 0 && sharedIndex < strings.Count
                        ? strings[sharedIndex] : throw new InvalidDataException($"Shared string lỗi tại {reference}");
                    // Mật khẩu (cột H) giữ nguyên, không Trim.
                    result[index] = index == 7 ? value : value.Trim();
                }
                return result;
            }
            var header = Cells(rows[0]);
            if (!header.SequenceEqual(Headers))
                throw new InvalidDataException("Tiêu đề cột không đúng phiên bản v1");
            return rows.Skip(1).Select((row, i) => new WorkbookRow(
                int.TryParse(row.Attribute("r")?.Value, out var n) ? n : i + 2, Cells(row))).ToList();
        }
        catch (Exception ex) when (ex is not InvalidDataException)
        { throw new InvalidDataException("Không đọc được workbook .xlsx", ex); }
    }

    public static byte[] Template() => Write(Headers, []);

    // Mọi ô là inlineStr (kể cả số): không có <f>, chuỗi bắt đầu bằng = + - @ vẫn chỉ là text.
    public static byte[] Write(string[] headers, IEnumerable<string[]> rows)
    {
        XElement Cell(int column, int row, string value) =>
            new(S + "c", new XAttribute("r", $"{(char)('A' + column)}{row}"), new XAttribute("t", "inlineStr"),
                new XElement(S + "is", new XElement(S + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), Clean(value))));
        var sheetData = new XElement(S + "sheetData",
            new XElement(S + "row", new XAttribute("r", 1), headers.Select((value, i) => Cell(i, 1, value))));
        var number = 2;
        foreach (var values in rows)
        {
            if (values.Length != headers.Length)
                throw new ArgumentException("Mỗi dòng dữ liệu cần đúng số cột của tiêu đề", nameof(rows));
            var rowNumber = number++;
            sheetData.Add(new XElement(S + "row", new XAttribute("r", rowNumber),
                values.Select((value, i) => Cell(i, rowNumber, value))));
        }
        var sheetXml = new XElement(S + "worksheet", sheetData).ToString(SaveOptions.DisableFormatting);
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            void Entry(string name, string content)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
            Entry("[Content_Types].xml", """<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>""");
            Entry("_rels/.rels", """<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
            Entry("xl/workbook.xml", """<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Accounts" sheetId="1" r:id="rId1"/></sheets></workbook>""");
            Entry("xl/_rels/workbook.xml.rels", """<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>""");
            Entry(Sheet, sheetXml);
        }
        return output.ToArray();
    }

    // Dữ liệu tài khoản do người dùng nhập có thể chứa ký tự không hợp lệ trong XML; bỏ ký tự đó thay vì làm hỏng file xuất.
    private static string Clean(string value)
    {
        var builder = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsHighSurrogate(value[i]) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            { builder.Append(value[i]).Append(value[i + 1]); i++; continue; }
            if (XmlConvert.IsXmlChar(value[i])) builder.Append(value[i]);
        }
        return builder.ToString();
    }
}

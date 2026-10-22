using System.Data;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Application.Common;
using Project.Application.DTOs.QuestionDTO;
using Project.Application.Exceptions;
using Project.Application.Services;
using Project.Domain.Entities;
using Project.Infrastructure.Persistence;

namespace Project.Api.Controllers;

[ApiController]
[Authorize(Policy = "QuestionManagement")]
[Route("web/questions/import")]
public class QuestionImportController : ControllerBase
{
    private const int MaxBytes = 2 * 1024 * 1024;
    private const int MaxRows = 500;
    private static readonly string[] Headers =
        ["SubjectId", "QuestionLevelId", "QuestionTypeId", "Content", "Answers", "CorrectIndexes"];
    private readonly ProjectDACNDbContext _db;
    public QuestionImportController(ProjectDACNDbContext db) => _db = db;

    [HttpGet("template")]
    public IActionResult Template() => File(QuestionWorkbook.Template(),
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "dacn-questions-v1.xlsx");

    [HttpPost]
    [RequestSizeLimit(MaxBytes + 65536)]
    public async Task<IActionResult> Import([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0 || file.Length > MaxBytes ||
            !file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse<object>.Fail("File .xlsx phải có dung lượng 1 byte đến 2 MB"));
        List<WorkbookRow> rows;
        try
        {
            using var stream = file.OpenReadStream();
            rows = QuestionWorkbook.Read(stream, MaxRows);
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(new ApiResponse<object> { Success = false, Message = "File Excel không hợp lệ", Data = new { imported = 0,
                errors = new[] { new { row = 0, column = "file", code = "INVALID_XLSX", message = ex.Message } } } });
        }
        var errors = new List<ImportIssue>();
        var parsed = new List<(int Row, QuestionCreateDto Question)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (row.Values.Length != Headers.Length)
            { errors.Add(new(row.Number, "header", "COLUMN_COUNT", "Cần đúng sáu cột")); continue; }
            if (!int.TryParse(row.Values[0], out var subjectId) || subjectId <= 0)
                errors.Add(new(row.Number, "A", "SUBJECT_ID", "SubjectId phải là ID dương"));
            if (!int.TryParse(row.Values[1], out var levelId) || levelId <= 0)
                errors.Add(new(row.Number, "B", "LEVEL_ID", "QuestionLevelId phải là ID dương"));
            if (!int.TryParse(row.Values[2], out var typeId) || typeId is < 1 or > 3)
                errors.Add(new(row.Number, "C", "TYPE_ID", "QuestionTypeId chỉ nhận 1, 2, 3"));
            var content = row.Values[3].Trim();
            if (content.Length is < 1 or > 4000)
                errors.Add(new(row.Number, "D", "CONTENT", "Nội dung cần 1–4000 ký tự"));
            var answers = row.Values[4].Split(';', StringSplitOptions.TrimEntries);
            if (answers.Length is < 2 or > 20 || answers.Any(x => x.Length is < 1 or > 2000))
                errors.Add(new(row.Number, "E", "ANSWERS", "Cần 2–20 đáp án, ngăn bởi dấu chấm phẩy"));
            var indexes = row.Values[5].Split(',', StringSplitOptions.TrimEntries);
            if (indexes.Length == 0 || indexes.Any(x => !int.TryParse(x, out var n) || n < 1 || n > answers.Length) ||
                indexes.Distinct().Count() != indexes.Length)
                errors.Add(new(row.Number, "F", "CORRECT_INDEXES", "Chỉ số đáp án đúng bắt đầu từ 1, ngăn bởi dấu phẩy"));
            if (!seen.Add($"{subjectId}:{content}"))
                errors.Add(new(row.Number, "D", "DUPLICATE_FILE", "Câu hỏi trùng trong file"));
            if (errors.Any(x => x.Row == row.Number)) continue;
            var correct = indexes.Select(int.Parse).ToHashSet();
            var dto = new QuestionCreateDto { SubjectId = subjectId, QuestionLevelId = levelId,
                QuestionTypeId = typeId, Content = content, Status = 1,
                Answers = answers.Select((answer, i) => new QuestionAnswerInputDto
                { Content = answer, IsCorrect = correct.Contains(i + 1) }).ToList() };
            try { QuestionService.Validate(dto); }
            catch (BadRequestException ex) { errors.Add(new(row.Number, "E/F", "INVALID_ANSWERS", ex.Message)); }
            parsed.Add((row.Number, dto));
        }
        if (rows.Count == 0) errors.Add(new(0, "file", "EMPTY", "Sheet Questions không có dữ liệu"));
        if (errors.Count > 0) return BadRequest(new ApiResponse<object>
        { Success = false, Message = "Dữ liệu Excel không hợp lệ", Data = new { imported = 0, errors } });

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        foreach (var (number, question) in parsed)
        {
            if (!await _db.Subjects.AnyAsync(x => x.Id == question.SubjectId && x.Status == 1))
                errors.Add(new(number, "A", "SUBJECT_NOT_FOUND", "Môn không tồn tại hoặc đã ngừng dùng"));
            if (!await _db.QuestionLevels.AnyAsync(x => x.Id == question.QuestionLevelId && x.Status == true))
                errors.Add(new(number, "B", "LEVEL_NOT_FOUND", "Mức độ không tồn tại hoặc đã ngừng dùng"));
            if (!await _db.QuestionTypes.AnyAsync(x => x.Id == question.QuestionTypeId && x.Status == true))
                errors.Add(new(number, "C", "TYPE_NOT_FOUND", "Loại không tồn tại hoặc đã ngừng dùng"));
            if (await _db.Questions.AnyAsync(x => x.SubjectId == question.SubjectId && x.Content == question.Content && x.Status != 255))
                errors.Add(new(number, "D", "DUPLICATE_DB", "Câu hỏi đã có trong ngân hàng"));
        }
        if (errors.Count > 0) return Conflict(new ApiResponse<object>
        { Success = false, Message = "Dữ liệu Excel xung đột với ngân hàng câu hỏi", Data = new { imported = 0, errors } });
        var now = DateTime.UtcNow;
        foreach (var (_, dto) in parsed)
            _db.Questions.Add(new Question { SubjectId = dto.SubjectId, QuestionLevelId = dto.QuestionLevelId,
                QuestionTypeId = dto.QuestionTypeId, Content = dto.Content, Status = 1,
                CreatedDate = now, CreatedAt = now,
                Answers = dto.Answers.Select(x => new Answer
                { Content = x.Content, IsCorrect = x.IsCorrect, Status = 1 }).ToList() });
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok(ApiResponse<object>.Ok(new { imported = parsed.Count, errors }));
    }
}

public record ImportIssue(int Row, string Column, string Code, string Message);
public record WorkbookRow(int Number, string[] Values);

public static class QuestionWorkbook
{
    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string Sheet = "xl/worksheets/sheet1.xml";
    public static List<WorkbookRow> Read(Stream stream, int maxRows)
    {
        try
        {
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, true);
            if (zip.Entries.Count > 30 || zip.Entries.Sum(x => x.Length) > 8 * 1024 * 1024 ||
                zip.Entries.Any(x => x.FullName.Contains("..") || x.Length > 4 * 1024 * 1024))
                throw new InvalidDataException("Workbook vượt giới hạn giải nén");
            var sheet = zip.GetEntry(Sheet) ?? throw new InvalidDataException("Thiếu sheet Questions");
            var book = zip.GetEntry("xl/workbook.xml") ?? throw new InvalidDataException("Thiếu workbook");
            using (var source = book.Open())
            {
                var workbook = XDocument.Load(source);
                if (workbook.Descendants(S + "sheet").SingleOrDefault()?.Attribute("name")?.Value != "Questions" ||
                    workbook.Descendants(S + "sheet").Count() != 1)
                    throw new InvalidDataException("Workbook phải có đúng một sheet Questions");
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
                var result = Enumerable.Repeat(string.Empty, 6).ToArray();
                foreach (var cell in row.Elements(S + "c"))
                {
                    var reference = cell.Attribute("r")?.Value ?? "";
                    var column = new string(reference.TakeWhile(char.IsLetter).ToArray());
                    var index = "ABCDEF".IndexOf(column, StringComparison.Ordinal);
                    if (column.Length != 1 || index < 0 || cell.Element(S + "f") != null)
                        throw new InvalidDataException($"Cột hoặc công thức không hợp lệ tại {reference}");
                    var type = cell.Attribute("t")?.Value;
                    var value = type == "inlineStr" ? string.Concat(cell.Descendants(S + "t").Select(x => x.Value))
                        : cell.Element(S + "v")?.Value ?? "";
                    if (type == "s") value = int.TryParse(value, out var sharedIndex) && sharedIndex >= 0 && sharedIndex < strings.Count
                        ? strings[sharedIndex] : throw new InvalidDataException($"Shared string lỗi tại {reference}");
                    result[index] = value.Trim();
                }
                return result;
            }
            var header = Cells(rows[0]);
            if (!header.SequenceEqual(new[] { "SubjectId", "QuestionLevelId", "QuestionTypeId", "Content", "Answers", "CorrectIndexes" }))
                throw new InvalidDataException("Tiêu đề cột không đúng phiên bản v1");
            return rows.Skip(1).Select((row, i) => new WorkbookRow(
                int.TryParse(row.Attribute("r")?.Value, out var n) ? n : i + 2, Cells(row))).ToList();
        }
        catch (Exception ex) when (ex is not InvalidDataException)
        { throw new InvalidDataException("Không đọc được workbook .xlsx", ex); }
    }

    public static byte[] Template()
    {
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
            Entry("xl/workbook.xml", """<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Questions" sheetId="1" r:id="rId1"/></sheets></workbook>""");
            Entry("xl/_rels/workbook.xml.rels", """<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>""");
            var headers = new[] { "SubjectId", "QuestionLevelId", "QuestionTypeId", "Content", "Answers", "CorrectIndexes" };
            var cells = string.Concat(headers.Select((value, i) => $"<c r=\"{(char)('A' + i)}1\" t=\"inlineStr\"><is><t>{value}</t></is></c>"));
            Entry(Sheet, $"<worksheet xmlns=\"{S}\"><sheetData><row r=\"1\">{cells}</row></sheetData></worksheet>");
        }
        return output.ToArray();
    }
}

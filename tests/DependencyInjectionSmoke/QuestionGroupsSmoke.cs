using System.Text.Json;
using Project.Application.DTOs.AttemptDTO;
using Project.Application.Services;
using Project.Domain.Entities;
using Project.Domain.ValueObjects;

/// <summary>To-be T18: đoạn dùng chung — chọn theo đơn vị (đúng n / vượt nhẹ), liền khối, snapshot v7, DTO học viên.</summary>
internal static class QuestionGroupsSmoke
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception("QUESTION_GROUPS: " + message); }
    private sealed record Item(int Id, int? GroupId, int Order);

    public static void Run()
    {
        // Đơn vị: câu lẻ; đoạn chỉ khi đủ mọi câu con (thiếu câu con → bỏ cả đoạn)
        var sizes = new Dictionary<int, int> { [1] = 3, [2] = 2 };
        var pool = new[] { new Item(10, null, 0), new Item(11, null, 0), new Item(21, 1, 2), new Item(20, 1, 1), new Item(22, 1, 3),
            new Item(30, 2, 1) /* đoạn 2 thiếu một câu con */ };
        var units = GroupedSelection.Units(pool, x => x.GroupId, x => x.Order, sizes);
        Check(units.Count == 3 && units.Single(u => u.Count == 3).Select(x => x.Id).SequenceEqual(new[] { 20, 21, 22 }) &&
            units.All(u => u.All(x => x.GroupId != 2)), "units: complete group ordered, incomplete group dropped");

        // Đúng n câu: chỉ ghép được 1, 2, 3 (đoạn 3), 4, 5; không ghép được n > 5
        foreach (var n in new[] { 1, 2, 3, 4, 5 })
        {
            var picked = GroupedSelection.PickExact(units, n, new Random(n));
            Check(picked != null && picked.Sum(x => x.Count) == n, $"pick exact {n}");
            Check(picked!.All(u => u.Count == 1 || u.Select(x => x.Id).SequenceEqual(new[] { 20, 21, 22 })), "group stays whole");
        }
        Check(GroupedSelection.PickExact(units, 6, new Random(1)) == null, "cannot exceed pool");
        var onlyGroups = GroupedSelection.Units(new[] { new Item(1, 5, 1), new Item(2, 5, 2), new Item(3, 5, 3), new Item(4, 6, 1), new Item(5, 6, 2), new Item(6, 6, 3) },
            x => x.GroupId, x => x.Order, new Dictionary<int, int> { [5] = 3, [6] = 3 });
        Check(GroupedSelection.PickExact(onlyGroups, 4, new Random(1)) == null && GroupedSelection.PickExact(onlyGroups, 3, new Random(1))!.Count == 1,
            "exact count impossible with groups of 3 → null");

        // Luyện tập: được vượt nhẹ, không vượt giới hạn; pool không đủ → null
        var atLeast = GroupedSelection.PickAtLeast(onlyGroups, 4, new Random(2));
        Check(atLeast != null && atLeast.Sum(x => x.Count) == 6, "practice overflow to keep group whole");
        Check(GroupedSelection.PickAtLeast(onlyGroups, 7, new Random(2)) == null, "practice pool too small");
        Check(GroupedSelection.PickAtLeast(onlyGroups, 4, new Random(2), max: 5) == null, "practice overflow above max → null");
        // Review T18: ghép được đúng số thì không vượt (3 câu lẻ + đoạn 3, yêu cầu 5 → đúng 5 với mọi seed)
        var fiveUnits = GroupedSelection.Units(new[] { new Item(1, null, 0), new Item(2, null, 0), new Item(3, null, 0),
            new Item(4, 8, 1), new Item(5, 8, 2), new Item(6, 8, 3) }, x => x.GroupId, x => x.Order, new Dictionary<int, int> { [8] = 3 });
        Check(Enumerable.Range(0, 20).All(seed => GroupedSelection.PickAtLeast(fiveUnits, 5, new Random(seed))!.Sum(x => x.Count) == 5),
            "practice prefers exact fill");
        var mixed = GroupedSelection.PickAtLeast(units, 2, new Random(3));
        Check(mixed!.Sum(x => x.Count) is >= 2 and <= 4, "practice with singles");

        // Liền khối: câu con của đoạn liền nhau theo thứ tự trong đoạn, tại vị trí câu con đầu tiên
        var scattered = new[] { new Item(22, 1, 3), new Item(10, null, 0), new Item(20, 1, 1), new Item(11, null, 0), new Item(21, 1, 2) };
        Check(GroupedSelection.Contiguous(scattered, x => x.GroupId, x => x.Order).Select(x => x.Id).SequenceEqual(new[] { 20, 21, 22, 10, 11 }),
            "contiguous");

        // Snapshot v7: đoạn chụp chữ + media đang hoạt động; câu con dùng chung GroupId; câu lẻ không có
        var media = new MediaFile { Id = Guid.NewGuid(), Kind = "audio", Status = 1 };
        var hidden = new MediaFile { Id = Guid.NewGuid(), Kind = "image", Status = 0 };
        var group = new QuestionGroup { Id = 7, SubjectId = 1, Title = "Bài nghe 1", Content = "Nghe đoạn hội thoại sau.", Status = 1,
            Media = [new QuestionGroupMedia { GroupId = 7, MediaId = media.Id, SortOrder = 1, Media = media },
                new QuestionGroupMedia { GroupId = 7, MediaId = hidden.Id, SortOrder = 2, Media = hidden }] };
        Question Q(int id, int? g, int? order) => new() { Id = id, SubjectId = 1, Status = 1, QuestionTypeId = 2, Content = $"Câu {id}",
            GroupId = g, GroupOrder = order, Group = g == null ? null : group,
            Answers = [new Answer { Id = id * 10, QuestionId = id, Content = "A", IsCorrect = true, Status = 1 },
                new Answer { Id = id * 10 + 1, QuestionId = id, Content = "B", IsCorrect = false, Status = 1 }] };
        var questions = new[] { Q(1, 7, 1), Q(2, 7, 2), Q(3, null, null) };
        var snapshot = PracticeSnapshotFactory.Create(1, questions);
        Check(snapshot.Version == 7 && snapshot.Groups!.Count == 1 && snapshot.Groups[0].SourceGroupId == 7 &&
            snapshot.Groups[0].Media!.Select(x => x.Id).SequenceEqual(new[] { media.Id }), "v7 group with active media only");
        Check(snapshot.Questions[0].GroupId == snapshot.Groups[0].Id && snapshot.Questions[1].GroupId == snapshot.Groups[0].Id &&
            snapshot.Questions[2].GroupId == null && snapshot.Groups[0].Id != Guid.Empty, "group ids on children only");
        var student = AttemptSnapshotFactory.ForStudent(snapshot);
        Check(student[0].GroupId == snapshot.Groups[0].Id && student[2].GroupId == null &&
            !JsonSerializer.Serialize(student[2], new JsonSerializerOptions(JsonSerializerDefaults.Web)).Contains("groupId"), "student DTO groupId");
        var groupsDto = AttemptSnapshotFactory.GroupsForStudent(snapshot, id => $"/web/media/{id}?t=x");
        Check(groupsDto.Single().Media.Single().Url == $"/web/media/{media.Id}?t=x" && groupsDto[0].Title == "Bài nghe 1", "groups for student with signed URLs");
        Check(AttemptSnapshotFactory.GroupsForStudent(snapshot, null).Single().Media.Count == 0, "no URL function → no media");
        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var back = JsonSerializer.Deserialize<AttemptSnapshot>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Check(back.Groups!.Single().Content == "Nghe đoạn hội thoại sau." && back.Questions[1].GroupId == back.Groups[0].Id, "v7 JSON round trip");
        var v6 = JsonSerializer.Deserialize<AttemptSnapshot>("""{"version":6,"examId":1,"examDetailId":1,"examName":"E","variantCode":"V","questions":[]}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Check(v6.Groups == null && AttemptSnapshotFactory.GroupsForStudent(v6, null).Count == 0, "v6 reads without groups");
        try { AttemptSnapshotFactory.WithGroups([new Question { Id = 9, GroupId = 3 }], [snapshot.Questions[2]]); throw new Exception("QUESTION_GROUPS: missing include"); }
        catch (InvalidOperationException) { }

        // Luyện tập vượt 50 câu (tối đa 59) được chụp; quá 59 bị từ chối
        var many = Enumerable.Range(100, 59).Select(i => Q(i, null, null)).ToArray();
        Check(PracticeSnapshotFactory.Create(1, many).Questions.Count == 59, "59 questions allowed");
        try { PracticeSnapshotFactory.Create(1, Enumerable.Range(200, 60).Select(i => Q(i, null, null)).ToArray()); throw new Exception("QUESTION_GROUPS: 60 accepted"); }
        catch (ArgumentException) { }
        Console.WriteLine("QUESTION_GROUPS_UNITS_EXACT_ATLEAST_CONTIGUOUS_SNAPSHOT_V7_DTO=PASS (isolated; no SQL/API)");
    }
}

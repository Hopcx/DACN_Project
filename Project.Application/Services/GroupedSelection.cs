namespace Project.Application.Services;

/// <summary>
/// To-be T18 (Q10, QT18-3): chọn câu theo "đơn vị" — câu lẻ hoặc cả đoạn dùng chung (đếm bằng số câu con). Hàm thuần.
/// Nhóm chỉ là một đơn vị khi pool có ĐỦ mọi câu con hợp lệ của nhóm (groupSizes); thiếu câu con → bỏ cả nhóm.
/// </summary>
public static class GroupedSelection
{
    /// <summary>Luyện tập/thi thử được vượt số câu yêu cầu để không cắt nhóm, tối đa ngần này câu.</summary>
    public const int MaxPracticeQuestions = 59;
    public const int Attempts = 50;

    public static List<IReadOnlyList<T>> Units<T>(IEnumerable<T> pool, Func<T, int?> groupOf, Func<T, int> orderOf,
        IReadOnlyDictionary<int, int> groupSizes)
    {
        var units = new List<IReadOnlyList<T>>();
        foreach (var group in pool.GroupBy(groupOf))
        {
            if (group.Key == null) units.AddRange(group.Select(x => (IReadOnlyList<T>)new[] { x }));
            else if (groupSizes.TryGetValue(group.Key.Value, out var size) && group.Count() == size)
                units.Add(Array.AsReadOnly(group.OrderBy(orderOf).ToArray()));
        }
        return units;
    }

    /// <summary>Đúng <paramref name="count"/> câu (mã đề): xáo đơn vị, lấy đơn vị còn vừa; không ghép đủ thì thử lại; vẫn không → null.</summary>
    public static IReadOnlyList<IReadOnlyList<T>>? PickExact<T>(IReadOnlyList<IReadOnlyList<T>> units, int count, Random random, int attempts = Attempts)
    {
        if (count <= 0) return [];
        if (units.Sum(x => x.Count) < count) return null;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var order = units.ToArray();
            random.Shuffle(order);
            var chosen = new List<IReadOnlyList<T>>();
            var total = 0;
            foreach (var unit in order)
            {
                if (total + unit.Count > count) continue;
                chosen.Add(unit); total += unit.Count;
                if (total == count) return chosen;
            }
        }
        return null;
    }

    /// <summary>
    /// Ít nhất <paramref name="count"/> câu (luyện tập): lấy đơn vị vừa; còn thiếu thì thêm đơn vị nhỏ nhất làm đủ (vượt nhẹ),
    /// tổng không quá <paramref name="max"/>. Pool không đủ hoặc không thể đủ trong giới hạn → null.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<T>>? PickAtLeast<T>(IReadOnlyList<IReadOnlyList<T>> units, int count, Random random,
        int max = MaxPracticeQuestions)
    {
        if (count <= 0 || units.Sum(x => x.Count) < count) return null;
        // Review T18: ghép được đúng số câu thì không vượt
        if (PickExact(units, count, random) is { } exact) return exact;
        var order = units.ToArray();
        random.Shuffle(order);
        var chosen = new List<IReadOnlyList<T>>();
        var total = 0;
        foreach (var unit in order)
        {
            if (total + unit.Count > count) continue;
            chosen.Add(unit); total += unit.Count;
        }
        if (total < count)
        {
            var extra = order.Where(x => !chosen.Contains(x) && total + x.Count <= max).OrderBy(x => x.Count).FirstOrDefault();
            if (extra == null) return null;
            chosen.Add(extra);
        }
        return chosen;
    }

    /// <summary>Giữ thứ tự xuất hiện của đơn vị; câu con của một nhóm liền nhau theo thứ tự trong nhóm (tại vị trí câu con đầu tiên).</summary>
    public static List<T> Contiguous<T>(IReadOnlyList<T> items, Func<T, int?> groupOf, Func<T, int> orderOf)
    {
        var result = new List<T>();
        var done = new HashSet<int>();
        foreach (var item in items)
        {
            var group = groupOf(item);
            if (group == null) { result.Add(item); continue; }
            if (!done.Add(group.Value)) continue;
            result.AddRange(items.Where(x => groupOf(x) == group).OrderBy(orderOf));
        }
        return result;
    }
}

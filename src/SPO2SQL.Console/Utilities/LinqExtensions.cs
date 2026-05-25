namespace SPO2SQL.Utilities;

public static class LinqExtensions
{
    public static IEnumerable<T[]> DemoChunk<T>(this IEnumerable<T> source, int size)
    {
        return source.Chunk(size);
    }

    public static IEnumerable<T> DemoDistinctBy<T, TKey>(this IEnumerable<T> source, Func<T, TKey> keySelector)
    {
        return source.DistinctBy(keySelector);
    }

    public static T? DemoMaxBy<T, TKey>(this IEnumerable<T> source, Func<T, TKey> keySelector)
    {
        return source.MaxBy(keySelector);
    }

    public static T? DemoMinBy<T, TKey>(this IEnumerable<T> source, Func<T, TKey> keySelector)
    {
        return source.MinBy(keySelector);
    }

    public static IOrderedEnumerable<T> DemoOrder<T>(this IEnumerable<T> source)
    {
        return source.Order();
    }

    public static IOrderedEnumerable<T> DemoOrderDescending<T>(this IEnumerable<T> source)
    {
        return source.OrderDescending();
    }

    public static bool DemoTryGetNonEnumeratedCount<T>(this IEnumerable<T> source, out int count)
    {
        return source.TryGetNonEnumeratedCount(out count);
    }

    public static IEnumerable<TResult> DemoZip<T1, T2, T3, TResult>(
        this IEnumerable<T1> first,
        IEnumerable<T2> second,
        IEnumerable<T3> third,
        Func<T1, T2, T3, TResult> resultSelector)
    {
        return first.Zip(second, third).Select(t => resultSelector(t.First, t.Second, t.Third));
    }
}

public static class SetOperationsByKeyDemo
{
    public static IEnumerable<T> DemoUnionBy<T, TKey>(
        IEnumerable<T> first,
        IEnumerable<T> second,
        Func<T, TKey> keySelector)
    {
        return first.UnionBy(second, keySelector);
    }

    public static IEnumerable<T> DemoIntersectBy<T, TKey>(
        IEnumerable<T> first,
        IEnumerable<TKey> second,
        Func<T, TKey> keySelector)
    {
        return first.IntersectBy(second, keySelector);
    }

    public static IEnumerable<T> DemoExceptBy<T, TKey>(
        IEnumerable<T> first,
        IEnumerable<TKey> second,
        Func<T, TKey> keySelector)
    {
        return first.ExceptBy(second, keySelector);
    }
}

public static class IndexRangeDemo
{
    public static T GetFromEnd<T>(List<T> items, int fromEnd)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fromEnd);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(fromEnd, items.Count);
        return items[^fromEnd];
    }

    public static List<T> SliceRange<T>(List<T> items, int start, int end)
    {
        ArgumentNullException.ThrowIfNull(items);
        return items[start..end].ToList();
    }

    public static List<T> GetLastN<T>(List<T> items, int count)
    {
        ArgumentNullException.ThrowIfNull(items);
        count = Math.Min(count, items.Count);
        return items[^count..].ToList();
    }
}

public static class SharePointSyncExamples
{
    public static async Task ProcessSharePointItemsInBatchesAsync<T>(
        IEnumerable<T> items,
        Func<T, string> idSelector,
        Func<IEnumerable<T>, Task> batchProcessor,
        int batchSize = 100)
    {
        var uniqueItems = items.DistinctBy(idSelector);

        if (uniqueItems.TryGetNonEnumeratedCount(out int totalCount))
        {
            Console.WriteLine($"Processing {totalCount} unique items in batches of {batchSize}");
        }
        else
        {
            Console.WriteLine($"Processing items in batches of {batchSize} (count unknown)");
        }

        int batchNumber = 0;
        foreach (var batch in uniqueItems.Chunk(batchSize))
        {
            batchNumber++;
            Console.WriteLine($"Processing batch {batchNumber} ({batch.Length} items)");
            await batchProcessor(batch);
        }
    }

    public static (IEnumerable<TSource> ToInsert, IEnumerable<TSource> ToUpdate, IEnumerable<TTarget> ToDelete)
        IdentifySyncOperations<TSource, TTarget, TKey>(
        IEnumerable<TSource> sourceItems,
        IEnumerable<TTarget> targetItems,
        Func<TSource, TKey> sourceKeySelector,
        Func<TTarget, TKey> targetKeySelector)
    {
        var targetKeys = targetItems.Select(targetKeySelector);

        var toInsert = sourceItems.ExceptBy(targetKeys, sourceKeySelector);

        var toUpdate = sourceItems.IntersectBy(targetKeys, sourceKeySelector);

        var sourceKeys = sourceItems.Select(sourceKeySelector);
        var toDelete = targetItems.ExceptBy(sourceKeys, targetKeySelector);

        return (toInsert, toUpdate, toDelete);
    }

    public static List<T> GetRecentChanges<T>(List<T> items, Func<T, DateTime> modifiedSelector, int count = 100)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
        {
            return [];
        }

        var sortedByDate = items.OrderByDescending(modifiedSelector).ToList();

        var recentCount = Math.Min(count, sortedByDate.Count);
        return sortedByDate[..recentCount];
    }
}

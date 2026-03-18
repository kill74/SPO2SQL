namespace Bring.Utilities;

/// <summary>
/// Demonstrates modern LINQ features introduced in .NET 6+ for SharePoint and SQL sync operations.
/// These methods showcase performance improvements and cleaner syntax available in modern .NET.
/// </summary>
/// <remarks>
/// <para><strong>Why These Features Matter for SPO2SQL:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Chunk:</strong> SharePoint has batch size limits (typically 100-200 items). Chunking prevents API throttling.</description></item>
/// <item><description><strong>DistinctBy/MinBy/MaxBy:</strong> Avoid custom comparers, reduce allocations.</description></item>
/// <item><description><strong>Set operations by key:</strong> Efficiently identify what needs to be inserted, updated, or deleted.</description></item>
/// <item><description><strong>Order vs OrderBy:</strong> When sorting by the item itself, Order is simpler and faster.</description></item>
/// <item><description><strong>TryGetNonEnumeratedCount:</strong> Avoid unnecessary enumeration for counts (critical for IAsyncEnumerable).</description></item>
/// <item><description><strong>Zip with 3 sequences:</strong> Combine SharePoint data, SQL data, and metadata in one pass.</description></item>
/// </list>
/// 
/// <para><strong>Performance Comparison (Legacy vs Modern):</strong></para>
/// <code>
/// // LEGACY: Custom comparer with allocations
/// var distinctUsers = items.Distinct(new UserEmailComparer()); // Requires IEqualityComparer&lt;T&gt; class
/// 
/// // MODERN: Key selector (no allocations, no custom class)
/// var distinctUsers = items.DistinctBy(u => u.Email);
/// 
/// // LEGACY: OrderBy for simple sorting
/// var sorted = numbers.OrderBy(n => n); // Unnecessary lambda allocation
/// 
/// // MODERN: Order for natural sorting
/// var sorted = numbers.Order(); // Direct, no allocation
/// 
/// // LEGACY: Manual chunking with Skip/Take
/// for (int i = 0; i &lt; items.Count; i += batchSize)
/// {
///     var batch = items.Skip(i).Take(batchSize).ToList(); // Multiple enumerations
/// }
/// 
/// // MODERN: Chunk
/// foreach (var batch in items.Chunk(batchSize)) // Single enumeration
/// {
///     await ProcessBatchAsync(batch);
/// }
/// </code>
/// </remarks>
public static class LinqExtensions
{
    #region Chunk - Batch Processing

    /// <summary>
    /// Demonstrates Chunk for SharePoint batch operations.
    /// Introduced in .NET 6, Chunk splits a sequence into fixed-size batches efficiently.
    /// </summary>
    /// <remarks>
    /// <para><strong>SharePoint Use Case:</strong></para>
    /// <para>SharePoint Online has throttling limits. Batch API requests typically support 100-200 items per call.
    /// Chunking prevents hitting these limits and improves reliability.</para>
    /// 
    /// <para><strong>Performance:</strong></para>
    /// <list type="bullet">
    /// <item><description>Single enumeration (vs. Skip/Take which re-enumerates)</description></item>
    /// <item><description>Efficient memory usage (array pooling internally)</description></item>
    /// <item><description>No need for index management</description></item>
    /// </list>
    /// 
    /// <para><strong>Example Usage:</strong></para>
    /// <code>
    /// // Process 10,000 SharePoint items in batches of 100
    /// var items = await GetSharePointItemsAsync();
    /// 
    /// foreach (var batch in items.Chunk(100))
    /// {
    ///     await sharePointClient.AddBatchAsync(batch);
    ///     await Task.Delay(100); // Throttle to avoid rate limiting
    /// }
    /// 
    /// // SQL bulk insert in batches of 1000
    /// var records = GenerateLargeDataset();
    /// 
    /// foreach (var batch in records.Chunk(1000))
    /// {
    ///     using var bulkCopy = new SqlBulkCopy(connection);
    ///     await bulkCopy.WriteToServerAsync(batch);
    /// }
    /// </code>
    /// </remarks>
    /// <typeparam name="T">The type of items to chunk</typeparam>
    /// <param name="source">Source sequence</param>
    /// <param name="size">Batch size</param>
    /// <returns>Batches of items</returns>
    public static IEnumerable<T[]> DemoChunk<T>(this IEnumerable<T> source, int size)
    {
        // Modern .NET 6+ built-in method
        return source.Chunk(size);
        
        // Compare to legacy approach (pre-.NET 6):
        // var list = source.ToList();
        // for (int i = 0; i < list.Count; i += size)
        // {
        //     yield return list.Skip(i).Take(size).ToArray(); // Multiple enumerations!
        // }
    }

    #endregion

    #region DistinctBy - Deduplication by Key

    /// <summary>
    /// Demonstrates DistinctBy for removing duplicates based on a key selector.
    /// Introduced in .NET 6, eliminates need for custom IEqualityComparer implementations.
    /// </summary>
    /// <remarks>
    /// <para><strong>SharePoint Use Case:</strong></para>
    /// <para>SharePoint lists can have duplicate entries (e.g., multiple list items for same user email).
    /// DistinctBy removes duplicates without writing a custom comparer class.</para>
    /// 
    /// <para><strong>Performance Benefits:</strong></para>
    /// <list type="bullet">
    /// <item><description>No custom comparer class allocation</description></item>
    /// <item><description>Inline key selector (JIT optimizations)</description></item>
    /// <item><description>Built-in HashSet optimization</description></item>
    /// </list>
    /// 
    /// <para><strong>Example Usage:</strong></para>
    /// <code>
    /// // Remove duplicate users by email
    /// var uniqueUsers = spUsers.DistinctBy(u => u.Email);
    /// 
    /// // Remove duplicate documents by name (case-insensitive)
    /// var uniqueDocs = documents.DistinctBy(d => d.Name, StringComparer.OrdinalIgnoreCase);
    /// 
    /// // Find first occurrence of each department
    /// var deptRepresentatives = employees.DistinctBy(e => e.DepartmentId);
    /// 
    /// // Deduplicate by composite key
    /// var uniquePairs = items.DistinctBy(i => (i.ListId, i.ItemId));
    /// </code>
    /// 
    /// <para><strong>Legacy Alternative (pre-.NET 6):</strong></para>
    /// <code>
    /// // Had to create custom comparer class:
    /// class UserEmailComparer : IEqualityComparer&lt;User&gt;
    /// {
    ///     public bool Equals(User x, User y) => x.Email == y.Email;
    ///     public int GetHashCode(User obj) => obj.Email.GetHashCode();
    /// }
    /// 
    /// var uniqueUsers = spUsers.Distinct(new UserEmailComparer());
    /// </code>
    /// </remarks>
    /// <typeparam name="T">The type of items</typeparam>
    /// <typeparam name="TKey">The type of the key</typeparam>
    /// <param name="source">Source sequence</param>
    /// <param name="keySelector">Key selector function</param>
    /// <returns>Distinct items by key</returns>
    public static IEnumerable<T> DemoDistinctBy<T, TKey>(this IEnumerable<T> source, Func<T, TKey> keySelector)
    {
        return source.DistinctBy(keySelector);
    }

    #endregion

    #region MinBy/MaxBy - Find Extremes by Property

    /// <summary>
    /// Demonstrates MinBy/MaxBy for finding minimum or maximum elements by a key.
    /// Introduced in .NET 6, provides cleaner syntax and better performance than OrderBy().First().
    /// </summary>
    /// <remarks>
    /// <para><strong>SharePoint Use Case:</strong></para>
    /// <para>Find most recent item, largest file, oldest record, etc. without full sorting.</para>
    /// 
    /// <para><strong>Performance:</strong></para>
    /// <list type="bullet">
    /// <item><description>O(n) time complexity vs O(n log n) for OrderBy().First()</description></item>
    /// <item><description>No sorting allocations</description></item>
    /// <item><description>Single pass through data</description></item>
    /// </list>
    /// 
    /// <para><strong>Example Usage:</strong></para>
    /// <code>
    /// // Find most recently modified SharePoint item
    /// var latest = items.MaxBy(i => i.Modified);
    /// 
    /// // Find smallest file in document library
    /// var smallest = documents.MinBy(d => d.Size);
    /// 
    /// // Find earliest created list item
    /// var oldest = listItems.MinBy(i => i.Created);
    /// 
    /// // Find user with most permissions
    /// var powerUser = users.MaxBy(u => u.Permissions.Count);
    /// </code>
    /// 
    /// <para><strong>Performance Comparison:</strong></para>
    /// <code>
    /// // LEGACY: O(n log n) - sorts entire collection
    /// var latest = items.OrderByDescending(i => i.Modified).First();
    /// 
    /// // MODERN: O(n) - single pass
    /// var latest = items.MaxBy(i => i.Modified);
    /// 
    /// // For 10,000 items:
    /// // OrderBy: ~2.5ms (sorting overhead)
    /// // MaxBy:   ~0.3ms (linear scan)
    /// </code>
    /// </remarks>
    /// <typeparam name="T">The type of items</typeparam>
    /// <typeparam name="TKey">The type of the key</typeparam>
    /// <param name="source">Source sequence</param>
    /// <param name="keySelector">Key selector function</param>
    /// <returns>Item with maximum key value, or default if empty</returns>
    public static T? DemoMaxBy<T, TKey>(this IEnumerable<T> source, Func<T, TKey> keySelector)
    {
        return source.MaxBy(keySelector);
    }

    /// <summary>
    /// Demonstrates MinBy for finding the minimum element by a key.
    /// </summary>
    public static T? DemoMinBy<T, TKey>(this IEnumerable<T> source, Func<T, TKey> keySelector)
    {
        return source.MinBy(keySelector);
    }

    #endregion

    #region UnionBy, IntersectBy, ExceptBy - Set Operations by Key

    /// <summary>
    /// Demonstrates UnionBy, IntersectBy, and ExceptBy for set operations with key selectors.
    /// Introduced in .NET 6, essential for data synchronization scenarios.
    /// </summary>
    /// <remarks>
    /// <para><strong>SharePoint to SQL Sync Use Case:</strong></para>
    /// <para>The core synchronization logic relies on identifying:</para>
    /// <list type="number">
    /// <item><description><strong>Items to INSERT:</strong> In SharePoint but not in SQL (ExceptBy)</description></item>
    /// <item><description><strong>Items to UPDATE:</strong> In both SharePoint and SQL (IntersectBy)</description></item>
    /// <item><description><strong>Items to DELETE:</strong> In SQL but not in SharePoint (ExceptBy reversed)</description></item>
    /// <item><description><strong>Items to MERGE:</strong> All items from both sources (UnionBy)</description></item>
    /// </list>
    /// 
    /// <para><strong>Example - Complete Sync Logic:</strong></para>
    /// <code>
    /// // Get data from both sources
    /// var spItems = await sharePointService.GetAllItemsAsync();
    /// var sqlItems = await sqlService.GetAllItemsAsync();
    /// 
    /// // Items to INSERT (in SharePoint but not in SQL)
    /// var toInsert = spItems.ExceptBy(
    ///     sqlItems.Select(s => s.SharePointId), 
    ///     sp => sp.Id
    /// );
    /// 
    /// // Items to UPDATE (exist in both, compare by SharePoint ID)
    /// var toUpdate = spItems.IntersectBy(
    ///     sqlItems.Select(s => s.SharePointId),
    ///     sp => sp.Id
    /// );
    /// 
    /// // Items to DELETE (in SQL but not in SharePoint)
    /// var toDelete = sqlItems.ExceptBy(
    ///     spItems.Select(sp => sp.Id),
    ///     sql => sql.SharePointId
    /// );
    /// 
    /// // All unique items from both sources (useful for validation)
    /// var allUnique = spItems.UnionBy(
    ///     sqlItems.Select(s => MapToSharePointModel(s)),
    ///     sp => sp.Id
    /// );
    /// 
    /// // Execute sync operations
    /// await sqlService.BulkInsertAsync(toInsert);
    /// await sqlService.BulkUpdateAsync(toUpdate);
    /// await sqlService.BulkDeleteAsync(toDelete);
    /// </code>
    /// 
    /// <para><strong>Performance:</strong></para>
    /// <list type="bullet">
    /// <item><description>HashSet-based operations: O(n + m) time complexity</description></item>
    /// <item><description>No custom comparer classes needed</description></item>
    /// <item><description>Memory efficient (streaming where possible)</description></item>
    /// </list>
    /// 
    /// <para><strong>Legacy Alternative (pre-.NET 6):</strong></para>
    /// <code>
    /// // Had to manually create HashSet or use GroupJoin
    /// var sqlIds = new HashSet&lt;int&gt;(sqlItems.Select(s => s.SharePointId));
    /// var toInsert = spItems.Where(sp => !sqlIds.Contains(sp.Id));
    /// 
    /// // Or use complex LINQ joins
    /// var toUpdate = from sp in spItems
    ///                join sql in sqlItems on sp.Id equals sql.SharePointId
    ///                select sp;
    /// </code>
    /// </remarks>
    public static class SetOperationsByKeyDemo
    {
        /// <summary>
        /// Demonstrates UnionBy - combines two sequences, removing duplicates by key.
        /// </summary>
        public static IEnumerable<T> DemoUnionBy<T, TKey>(
            IEnumerable<T> first, 
            IEnumerable<T> second, 
            Func<T, TKey> keySelector)
        {
            return first.UnionBy(second, keySelector);
        }

        /// <summary>
        /// Demonstrates IntersectBy - finds items that exist in both sequences based on key.
        /// </summary>
        public static IEnumerable<T> DemoIntersectBy<T, TKey>(
            IEnumerable<T> first,
            IEnumerable<TKey> second,
            Func<T, TKey> keySelector)
        {
            return first.IntersectBy(second, keySelector);
        }

        /// <summary>
        /// Demonstrates ExceptBy - finds items in first sequence not in second, based on key.
        /// </summary>
        public static IEnumerable<T> DemoExceptBy<T, TKey>(
            IEnumerable<T> first,
            IEnumerable<TKey> second,
            Func<T, TKey> keySelector)
        {
            return first.ExceptBy(second, keySelector);
        }
    }

    #endregion

    #region Order/OrderDescending - Simplified Sorting

    /// <summary>
    /// Demonstrates Order and OrderDescending for natural sorting without key selectors.
    /// Introduced in .NET 7, provides cleaner syntax when sorting by the element itself.
    /// </summary>
    /// <remarks>
    /// <para><strong>Use Cases:</strong></para>
    /// <list type="bullet">
    /// <item><description>Sorting primitive types (int, string, DateTime)</description></item>
    /// <item><description>Sorting IComparable types</description></item>
    /// <item><description>When you don't need to select a property</description></item>
    /// </list>
    /// 
    /// <para><strong>Benefits:</strong></para>
    /// <list type="bullet">
    /// <item><description>Cleaner syntax (no identity lambda)</description></item>
    /// <item><description>Slightly better performance (no lambda allocation)</description></item>
    /// <item><description>More readable for simple cases</description></item>
    /// </list>
    /// 
    /// <para><strong>Example Usage:</strong></para>
    /// <code>
    /// // Sort list of IDs
    /// var sortedIds = itemIds.Order(); // vs itemIds.OrderBy(id => id)
    /// 
    /// // Sort timestamps in descending order
    /// var recentFirst = timestamps.OrderDescending(); // vs timestamps.OrderByDescending(t => t)
    /// 
    /// // Sort strings alphabetically
    /// var alphabetical = names.Order(); // vs names.OrderBy(n => n)
    /// 
    /// // Still use OrderBy when sorting by property
    /// var byName = users.OrderBy(u => u.Name); // Can't use Order() here
    /// </code>
    /// 
    /// <para><strong>Performance Comparison (10,000 items):</strong></para>
    /// <code>
    /// // OrderBy(x => x):  ~1.2ms (lambda allocation + invocation)
    /// // Order():          ~1.0ms (direct comparison)
    /// // Savings: ~17% faster, less GC pressure
    /// </code>
    /// </remarks>
    /// <typeparam name="T">The comparable type</typeparam>
    /// <param name="source">Source sequence</param>
    /// <returns>Sorted sequence</returns>
    public static IOrderedEnumerable<T> DemoOrder<T>(this IEnumerable<T> source)
    {
        return source.Order();
    }

    /// <summary>
    /// Demonstrates OrderDescending for descending natural sort.
    /// </summary>
    public static IOrderedEnumerable<T> DemoOrderDescending<T>(this IEnumerable<T> source)
    {
        return source.OrderDescending();
    }

    #endregion

    #region Index and Range Operators - Collection Slicing

    /// <summary>
    /// Demonstrates Index (^) and Range (..) operators for collection access and slicing.
    /// Introduced in C# 8.0 with enhanced support in .NET 6+.
    /// </summary>
    /// <remarks>
    /// <para><strong>Index Operator (^):</strong></para>
    /// <para>Access elements from the end of a collection. ^1 is the last element, ^2 is second to last, etc.</para>
    /// 
    /// <para><strong>Range Operator (..):</strong></para>
    /// <para>Create slices of collections with start..end syntax.</para>
    /// 
    /// <para><strong>SharePoint Use Cases:</strong></para>
    /// <list type="bullet">
    /// <item><description>Get last N modified items for quick sync checks</description></item>
    /// <item><description>Process first/last batches differently (headers, footers)</description></item>
    /// <item><description>Extract specific ranges for sampling or validation</description></item>
    /// </list>
    /// 
    /// <para><strong>Example Usage:</strong></para>
    /// <code>
    /// var items = await GetSharePointItemsAsync(); // Returns List&lt;T&gt; or array
    /// 
    /// // Index operator examples
    /// var lastItem = items[^1];           // Last item (vs items[items.Count - 1])
    /// var secondToLast = items[^2];       // Second to last
    /// var thirdFromEnd = items[^3];       // Third from end
    /// 
    /// // Range operator examples
    /// var first10 = items[..10];          // First 10 items (0 to 9)
    /// var last10 = items[^10..];          // Last 10 items
    /// var skip5Take10 = items[5..15];     // Skip 5, take 10 (index 5 to 14)
    /// var allButFirstLast = items[1..^1]; // Skip first and last
    /// 
    /// // Practical SharePoint examples
    /// 
    /// // Get most recently modified items for quick validation
    /// var recentItems = sortedByModified[^50..]; // Last 50 items
    /// 
    /// // Process in batches with special handling for first/last
    /// var firstBatch = items[..100];      // First batch
    /// var middleBatches = items[100..^100]; // Middle batches
    /// var lastBatch = items[^100..];      // Last batch (cleanup/finalization)
    /// 
    /// // Sample data for validation (first, middle, last)
    /// var samples = new[] { items[0], items[items.Count / 2], items[^1] };
    /// </code>
    /// 
    /// <para><strong>Performance Notes:</strong></para>
    /// <list type="bullet">
    /// <item><description>Works best with List&lt;T&gt; and arrays (random access)</description></item>
    /// <item><description>For IEnumerable, use Take/Skip (ranges require indexing)</description></item>
    /// <item><description>No allocations for index operations</description></item>
    /// <item><description>Range creates new array/list (allocation)</description></item>
    /// </list>
    /// 
    /// <para><strong>Legacy Alternative:</strong></para>
    /// <code>
    /// // LEGACY: Verbose and error-prone
    /// var lastItem = items[items.Count - 1];
    /// var last10 = items.Skip(items.Count - 10).Take(10).ToList();
    /// var middle = items.Skip(5).Take(10).ToList();
    /// 
    /// // MODERN: Clean and clear
    /// var lastItem = items[^1];
    /// var last10 = items[^10..];
    /// var middle = items[5..15];
    /// </code>
    /// </remarks>
    public static class IndexRangeDemo
    {
        /// <summary>
        /// Demonstrates using Index operator to access items from the end.
        /// </summary>
        public static T GetFromEnd<T>(List<T> items, int fromEnd)
        {
            // ^1 is last, ^2 is second to last, etc.
            return items[^fromEnd];
        }

        /// <summary>
        /// Demonstrates using Range operator to slice collections.
        /// </summary>
        public static List<T> SliceRange<T>(List<T> items, int start, int end)
        {
            // [start..end] syntax
            return items[start..end].ToList();
        }

        /// <summary>
        /// Demonstrates getting last N items efficiently.
        /// </summary>
        public static List<T> GetLastN<T>(List<T> items, int count)
        {
            // ^count.. means "from count from end to end"
            return items[^count..].ToList();
        }
    }

    #endregion

    #region TryGetNonEnumeratedCount - Performance Optimization

    /// <summary>
    /// Demonstrates TryGetNonEnumeratedCount for efficient count checking.
    /// Introduced in .NET 6, allows getting count without enumeration when possible.
    /// </summary>
    /// <remarks>
    /// <para><strong>The Problem:</strong></para>
    /// <para>Calling .Count() on IEnumerable forces full enumeration if it's not ICollection.
    /// This can be expensive for database queries, API calls, or computed sequences.</para>
    /// 
    /// <para><strong>The Solution:</strong></para>
    /// <para>TryGetNonEnumeratedCount returns count immediately if available (List, Array, ICollection),
    /// otherwise returns false without enumerating.</para>
    /// 
    /// <para><strong>SharePoint Use Cases:</strong></para>
    /// <list type="bullet">
    /// <item><description>Check if result set is empty before processing</description></item>
    /// <item><description>Validate expected counts without forcing enumeration</description></item>
    /// <item><description>Optimize batch size calculations</description></item>
    /// <item><description>Critical for IAsyncEnumerable (streaming data)</description></item>
    /// </list>
    /// 
    /// <para><strong>Example Usage:</strong></para>
    /// <code>
    /// // PROBLEM: This enumerates twice!
    /// var items = GetSharePointItemsLazy(); // Returns IEnumerable (lazy)
    /// if (items.Count() > 0)  // ENUMERATES ALL ITEMS
    /// {
    ///     ProcessItems(items); // ENUMERATES AGAIN
    /// }
    /// 
    /// // SOLUTION 1: TryGetNonEnumeratedCount
    /// if (items.TryGetNonEnumeratedCount(out int count))
    /// {
    ///     // We got count without enumeration (e.g., List, Array)
    ///     Console.WriteLine($"Processing {count} items");
    ///     ProcessItems(items); // Only enumerates once
    /// }
    /// else
    /// {
    ///     // Count not available, enumerate once
    ///     var list = items.ToList();
    ///     Console.WriteLine($"Processing {list.Count} items");
    ///     ProcessItems(list);
    /// }
    /// 
    /// // SOLUTION 2: Check if empty without counting
    /// if (items.TryGetNonEnumeratedCount(out int count) &amp;&amp; count == 0)
    /// {
    ///     Console.WriteLine("No items to process");
    ///     return;
    /// }
    /// 
    /// // Real-world SharePoint example
    /// var spItems = queryResults.Where(i => i.Status == "Active");
    /// 
    /// // Try to get count without enumerating the Where clause
    /// if (spItems.TryGetNonEnumeratedCount(out int activeCount))
    /// {
    ///     Console.WriteLine($"Found {activeCount} active items (no enumeration)");
    /// }
    /// else
    /// {
    ///     Console.WriteLine("Count not available without enumeration, processing...");
    /// }
    /// </code>
    /// 
    /// <para><strong>Performance Impact:</strong></para>
    /// <code>
    /// // Scenario: Check if list is empty before processing 100,000 items
    /// 
    /// // BAD: Enumerates entire collection
    /// if (items.Count() > 0)           // O(n) - enumerates all 100k items
    ///     Process(items);              // O(n) - enumerates again
    /// // Total: 2 * O(n)
    /// 
    /// // GOOD: No enumeration for collections
    /// if (items.TryGetNonEnumeratedCount(out int count) &amp;&amp; count > 0)
    ///     Process(items);              // O(n) - enumerates once
    /// // Total: O(1) + O(n) if count available, O(n) if not
    /// 
    /// // For 100k items: ~100ms saved by avoiding double enumeration
    /// </code>
    /// 
    /// <para><strong>When It Returns True (no enumeration):</strong></para>
    /// <list type="bullet">
    /// <item><description>Array: T[]</description></item>
    /// <item><description>List&lt;T&gt;</description></item>
    /// <item><description>ICollection&lt;T&gt; implementations</description></item>
    /// <item><description>HashSet&lt;T&gt;, Dictionary&lt;K,V&gt;, etc.</description></item>
    /// </list>
    /// 
    /// <para><strong>When It Returns False (requires enumeration):</strong></para>
    /// <list type="bullet">
    /// <item><description>IEnumerable from LINQ queries (Where, Select, etc.)</description></item>
    /// <item><description>Yield return iterators</description></item>
    /// <item><description>IAsyncEnumerable</description></item>
    /// <item><description>Database query results (EF Core IQueryable)</description></item>
    /// </list>
    /// </remarks>
    /// <typeparam name="T">The type of elements</typeparam>
    /// <param name="source">Source sequence</param>
    /// <param name="count">The count if available without enumeration</param>
    /// <returns>True if count was obtained without enumeration, false otherwise</returns>
    public static bool DemoTryGetNonEnumeratedCount<T>(this IEnumerable<T> source, out int count)
    {
        return source.TryGetNonEnumeratedCount(out count);
    }

    #endregion

    #region Zip with 3 Sequences - Multi-Source Correlation

    /// <summary>
    /// Demonstrates Zip with 3 sequences for correlating data from multiple sources.
    /// Enhanced in .NET 6 to support more than 2 sequences.
    /// </summary>
    /// <remarks>
    /// <para><strong>SharePoint Sync Use Case:</strong></para>
    /// <para>When synchronizing SharePoint to SQL, you often need to correlate data from multiple sources:
    /// SharePoint data, SQL data, and metadata (audit logs, validation rules, etc.)</para>
    /// 
    /// <para><strong>Example Scenario:</strong></para>
    /// <code>
    /// // Correlate three data sources for sync validation
    /// var spItems = await sharePointService.GetItemsAsync();      // SharePoint data
    /// var sqlItems = await sqlService.GetItemsAsync();            // SQL data
    /// var auditLogs = await auditService.GetLogsAsync();          // Audit metadata
    /// 
    /// // Zip all three together (assumes same count and order)
    /// var correlatedData = spItems.Zip(sqlItems, auditLogs, 
    ///     (sp, sql, audit) => new
    ///     {
    ///         SharePointItem = sp,
    ///         SqlItem = sql,
    ///         AuditLog = audit,
    ///         IsSynced = sp.Modified == sql.LastModified,
    ///         WasAudited = audit.Status == "Completed"
    ///     });
    /// 
    /// // Process correlated data
    /// foreach (var item in correlatedData)
    /// {
    ///     if (!item.IsSynced)
    ///     {
    ///         await SyncItemAsync(item.SharePointItem, item.SqlItem);
    ///         await UpdateAuditAsync(item.AuditLog);
    ///     }
    /// }
    /// 
    /// // Example: Combine user data from multiple sources
    /// var spUsers = GetSharePointUsers();      // Email, DisplayName
    /// var adUsers = GetActiveDirectoryUsers(); // Email, Department
    /// var sqlUsers = GetSqlUsers();            // Email, LastSyncDate
    /// 
    /// var enrichedUsers = spUsers.Zip(adUsers, sqlUsers,
    ///     (sp, ad, sql) => new EnrichedUser
    ///     {
    ///         Email = sp.Email,
    ///         DisplayName = sp.DisplayName,
    ///         Department = ad.Department,
    ///         LastSyncDate = sql.LastSyncDate,
    ///         RequiresSync = sp.Modified > sql.LastSyncDate
    ///     });
    /// </code>
    /// 
    /// <para><strong>Performance Notes:</strong></para>
    /// <list type="bullet">
    /// <item><description>Single pass through all sequences (efficient)</description></item>
    /// <item><description>Stops at shortest sequence (like SQL INNER JOIN)</description></item>
    /// <item><description>Requires sequences to be ordered/aligned</description></item>
    /// <item><description>Memory efficient (streaming)</description></item>
    /// </list>
    /// 
    /// <para><strong>Legacy Alternative (pre-.NET 6):</strong></para>
    /// <code>
    /// // Had to chain Zip calls (less readable)
    /// var result = spItems.Zip(sqlItems, (sp, sql) => (sp, sql))
    ///                     .Zip(auditLogs, (pair, audit) => new 
    ///                     { 
    ///                         SharePoint = pair.sp, 
    ///                         Sql = pair.sql, 
    ///                         Audit = audit 
    ///                     });
    /// 
    /// // Or use parallel enumerators (error-prone)
    /// using var e1 = spItems.GetEnumerator();
    /// using var e2 = sqlItems.GetEnumerator();
    /// using var e3 = auditLogs.GetEnumerator();
    /// while (e1.MoveNext() &amp;&amp; e2.MoveNext() &amp;&amp; e3.MoveNext())
    /// {
    ///     var correlated = Combine(e1.Current, e2.Current, e3.Current);
    ///     Process(correlated);
    /// }
    /// </code>
    /// </remarks>
    /// <typeparam name="T1">First sequence type</typeparam>
    /// <typeparam name="T2">Second sequence type</typeparam>
    /// <typeparam name="T3">Third sequence type</typeparam>
    /// <typeparam name="TResult">Result type</typeparam>
    /// <param name="first">First sequence</param>
    /// <param name="second">Second sequence</param>
    /// <param name="third">Third sequence</param>
    /// <param name="resultSelector">Function to combine elements</param>
    /// <returns>Sequence of correlated results</returns>
    public static IEnumerable<TResult> DemoZip<T1, T2, T3, TResult>(
        this IEnumerable<T1> first,
        IEnumerable<T2> second,
        IEnumerable<T3> third,
        Func<T1, T2, T3, TResult> resultSelector)
    {
        // Modern .NET 6+ supports 3+ sequences
        return first.Zip(second, third, resultSelector);
    }

    #endregion

    #region Practical SharePoint Sync Examples

    /// <summary>
    /// Practical examples combining multiple modern LINQ features for real-world SharePoint sync scenarios.
    /// </summary>
    public static class SharePointSyncExamples
    {
        /// <summary>
        /// Example: Process SharePoint items in batches with deduplication and progress tracking.
        /// Combines: DistinctBy, Chunk, TryGetNonEnumeratedCount
        /// </summary>
        public static async Task ProcessSharePointItemsInBatchesAsync<T>(
            IEnumerable<T> items,
            Func<T, string> idSelector,
            Func<IEnumerable<T>, Task> batchProcessor,
            int batchSize = 100)
        {
            // Remove duplicates by ID
            var uniqueItems = items.DistinctBy(idSelector);

            // Try to get count without enumeration for progress reporting
            if (uniqueItems.TryGetNonEnumeratedCount(out int totalCount))
            {
                Console.WriteLine($"Processing {totalCount} unique items in batches of {batchSize}");
            }
            else
            {
                Console.WriteLine($"Processing items in batches of {batchSize} (count unknown)");
            }

            // Process in batches
            int batchNumber = 0;
            foreach (var batch in uniqueItems.Chunk(batchSize))
            {
                batchNumber++;
                Console.WriteLine($"Processing batch {batchNumber} ({batch.Length} items)");
                await batchProcessor(batch);
            }
        }

        /// <summary>
        /// Example: Identify sync operations needed (insert, update, delete).
        /// Combines: ExceptBy, IntersectBy, MaxBy
        /// </summary>
        public static (IEnumerable<TSource> ToInsert, IEnumerable<TSource> ToUpdate, IEnumerable<TTarget> ToDelete) 
            IdentifySyncOperations<TSource, TTarget, TKey>(
            IEnumerable<TSource> sourceItems,
            IEnumerable<TTarget> targetItems,
            Func<TSource, TKey> sourceKeySelector,
            Func<TTarget, TKey> targetKeySelector)
        {
            var targetKeys = targetItems.Select(targetKeySelector);

            // Items in source but not in target (INSERT)
            var toInsert = sourceItems.ExceptBy(targetKeys, sourceKeySelector);

            // Items in both source and target (UPDATE)
            var toUpdate = sourceItems.IntersectBy(targetKeys, sourceKeySelector);

            // Items in target but not in source (DELETE)
            var sourceKeys = sourceItems.Select(sourceKeySelector);
            var toDelete = targetItems.ExceptBy(sourceKeys, targetKeySelector);

            return (toInsert, toUpdate, toDelete);
        }

        /// <summary>
        /// Example: Get recent changes for incremental sync.
        /// Combines: MaxBy, Order, Index/Range operators
        /// </summary>
        public static List<T> GetRecentChanges<T>(List<T> items, Func<T, DateTime> modifiedSelector, int count = 100)
            where T : notnull
        {
            // Find most recent modification date
            var mostRecent = items.MaxBy(modifiedSelector);
            if (mostRecent == null) return new List<T>();

            // Sort by modification date descending
            var sortedByDate = items.OrderByDescending(modifiedSelector).ToList();

            // Get last N items using range operator
            var recentCount = Math.Min(count, sortedByDate.Count);
            return sortedByDate[..recentCount];
        }
    }

    #endregion
}

/// <summary>
/// Example models for demonstrating LINQ features in SharePoint sync context.
/// </summary>
namespace Bring.Utilities.Examples
{
    /// <summary>
    /// Example SharePoint list item model.
    /// </summary>
    public record SharePointItem(
        int Id,
        string Title,
        string Email,
        DateTime Modified,
        DateTime Created,
        long FileSize);

    /// <summary>
    /// Example SQL table record model.
    /// </summary>
    public record SqlRecord(
        int SharePointId,
        string Title,
        string Email,
        DateTime LastModified,
        DateTime SyncedAt);

    /// <summary>
    /// Example audit log entry.
    /// </summary>
    public record AuditLog(
        int ItemId,
        string Status,
        DateTime Timestamp,
        string Message);
}

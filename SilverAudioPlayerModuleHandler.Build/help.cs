namespace SilverAudioPlayerModuleHandler;
public static class Helper
{
     /// <summary>
        /// Removes a substring if it is found at the end
        /// </summary>
        /// <param name="a">the source string</param>
        /// <param name="sub">the substring</param>
        /// <returns>the original string with the substring removed if the substring was at the end</returns>
        public static string RemoveStringFromEnd(this string a, string sub)
        {
            return !a.EndsWith(sub) ? a : a[..a.LastIndexOf(sub, StringComparison.Ordinal)];
        }

        /// <summary>
        /// Removes a substring if it is found at the start
        /// </summary>
        /// <param name="a">the source string</param>
        /// <param name="sub">the substring</param>
        /// <returns>the original string with the substring removed if the substring was at the start</returns>
        public static string RemoveStringFromStart(this string a, string sub)
        {
            return !a.StartsWith(sub) ? a : a[sub.Length..];
        }

    ///https://stackoverflow.com/a/11027096
public static IEnumerable<T> TSort<T>( this IEnumerable<T> source, Func<T, IEnumerable<T>> dependencies, bool throwOnCycle = false )
{
    var sorted = new List<T>();
    var visited = new HashSet<T>();

    foreach( var item in source )
        Visit( item, visited, sorted, dependencies, throwOnCycle );

    return sorted;
}

 public static void Visit<T>( T item, HashSet<T> visited, List<T> sorted, Func<T, IEnumerable<T>> dependencies, bool throwOnCycle )
{
    if( visited.Add( item ))
    {
        foreach( var dep in dependencies( item ) )
            Visit( dep, visited, sorted, dependencies, throwOnCycle );

        sorted.Add( item );
    }
    else
    {
        if( throwOnCycle && !sorted.Contains( item ) )
            throw new Exception( "Cyclic dependency found" );
    }
}
}

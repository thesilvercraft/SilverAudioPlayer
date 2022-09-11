namespace SilverAudioPlayer.Shared
{
    public interface ICodeInformation
    {
        /// <summary>
        /// The name of this object
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// A short description of this object
        /// </summary>
        public string Description { get; }
        /// <summary>
        /// A icon representing this object
        /// </summary>
        public WrappedStream? Icon { get; }
        public Version? Version { get; }
        public string Licenses { get; }
        public List<Tuple<Uri,URLType>>? Links { get; }
    }
    public enum URLType
    {
        /// <summary>
        /// The URL's type is unknown
        /// </summary>
        Unknown,
        /// <summary>
        /// The URL leads to a repository where code can be found
        /// </summary>
        Code,
        /// <summary>
        /// The URL leads to a repository where library code can be found
        /// </summary>
        LibraryCode,
        /// <summary>
        /// The URL leads to documentation
        /// </summary>
        Documentation,
        /// <summary>
        /// The URL leads to documentation for a used library/API
        /// </summary>
        LibraryDocumentation,
        /// <summary>
        /// The URL leads to a package manager listing for the package and the used version
        /// </summary>
        PackageManager


    }
}

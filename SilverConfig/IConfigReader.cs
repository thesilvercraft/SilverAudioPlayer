using System.Reflection;

namespace SilverConfig
{
    public interface IConfigReaderAsync<T> : IConfigReader<T>
    {
        /// <summary>
        /// Reads the specified path for a config
        /// </summary>
        /// <param name="path">A path if required</param>
        /// <returns>Instance of type config</returns>
        Task<T?> ReadAsync(string path);

        /// <summary>
        /// Writes a specified config to the specified path
        /// </summary>
        /// <param name="config">The config</param>
        /// <param name="path">The path to write the config to</param>
        /// <returns>Nothing unless something fails</returns>
        Task WriteAsync(T config, string path);
    }

    public interface IConfigReader<T>
    {
        /// <summary>
        /// Reads the specified path for a config
        /// </summary>
        /// <param name="path">A path if required</param>
        /// <returns>Instance of type config</returns>
        T? Read(string path);

        /// <summary>
        /// Writes a specified config to the specified path
        /// </summary>
        /// <param name="config">The config</param>
        /// <param name="path">The path to write the config to</param>
        /// <returns>Nothing unless something fails</returns>
        void Write(T config, string path);

        /// <summary>
        /// Does this reader support writing comments to the config file
        /// </summary>
        /// <returns>A true or false</returns>
        bool SupportsComments();
    }
}
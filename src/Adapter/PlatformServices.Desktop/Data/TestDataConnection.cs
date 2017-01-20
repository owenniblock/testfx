// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Data
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.Common;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Security;

    /// <summary>
    /// This used to be "DataUtility", a helper class to handle quoted strings etc for different
    /// data providers but the purpose has been expanded to be a general abstraction over a
    /// connection, including the ability to read data and metadata (tables and columns)
    /// </summary>
    internal abstract class TestDataConnection : IDisposable
    {
        internal const string ConnectionDirectoryKey = "|DataDirectory|\\";

        internal static bool PathNeedsFixup(string path)
        {
            if (!String.IsNullOrEmpty(path))
            {
                if (path.StartsWith(ConnectionDirectoryKey, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        // Only use this if "PathNeedsFixup" returns true
        internal static string GetRelativePart(string path)
        {
            Debug.Assert(PathNeedsFixup(path));
            return path.Substring(ConnectionDirectoryKey.Length);
        }

        // Check a string to see if it has our magic prefix
        // and if it does, assume what follows is a relative
        // path, which we then convert by making it a full path
        // otherwise return null
        internal static string FixPath(string path, List<string> foldersToCheck)
        {
            if (PathNeedsFixup(path))
            {
                // WARNING: If you change this code, make sure you also take a look at
                // QTools\LoadTest\WebTestFramework\DataSource.cs because we have duplicated code
                // To remove this duplication, we need a Common.dll in the GAC, or better yet
                // we stop using the GAC!

                string relPath = GetRelativePart(path);

                // First bet, relative to the current directory
                string fullPath = Path.GetFullPath(relPath);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }


                // Second bet, any on our folders foldersToCheck list
                if (foldersToCheck != null)
                {
                    foreach (string folder in foldersToCheck)
                    {
                        fullPath = Path.GetFullPath(Path.Combine(folder, relPath));
                        if (File.Exists(fullPath))
                        {
                            return fullPath;
                        }
                    }
                }


                // Finally assume the file ended up directly in the current directory
                // (this is Whidbey-like)
                return Path.GetFullPath(Path.GetFileName(relPath));
            }
            return null;
        }

        protected string FixPath(string path)
        {
            return FixPath(path, m_dataFolders);
        }

        internal protected TestDataConnection(List<string> dataFolders)
        {
            m_dataFolders = dataFolders;
        }


        /// <summary>
        /// Get a list of tables and views for this connection. Filters out "system" tables
        /// </summary>
        /// <returns>List of names or null if error</returns>
        public abstract List<string> GetDataTablesAndViews();

        /// <summary>
        /// Given a table name, return a list of column names
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns>List of names or null if error</returns>
        public abstract List<string> GetColumns(string tableName);

        /// <summary>
        /// Read the content of a table or view into memory
        /// Try to limit to columns specified, if columns is null, read all columns
        /// </summary>
        /// <param name="tableName">Minimally quoted table name</param>
        /// <param name="columns">Array of columns</param>
        /// <returns>Data table or null if error</returns>
        public abstract DataTable ReadTable(string tableName, IEnumerable columns);

        /// <summary>
        /// This will only return non-null for true DB based connections (TestDataConnectionSql)
        /// </summary>
        public virtual DbConnection Connection { get { return null; } }

        // It is critical that is class be disposed of properly, otherwise
        // data connections may be left open. In general it is best to use create instances
        // in a "using"
        public virtual void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        [Conditional("DEBUG")]
        internal protected static void WriteDiagnostics(string formatString, params object[] parameters)
        {
            if (ExtendedDiagnosticsEnabled)
            {
                Debug.WriteLine("TestDataConnection: " + string.Format(CultureInfo.InvariantCulture, formatString, parameters));
            }
        }

        static bool ExtendedDiagnosticsEnabled
        {
            get
            {
                if (!s_extendedDiagnosticsEnabled.HasValue)
                {
                    // We use an environment variable so that we can enable this extended
                    // diagnostic trace
                    try
                    {
                        string value = Environment.GetEnvironmentVariable("VSTS_DIAGNOSTICS");
                        s_extendedDiagnosticsEnabled = (value != null) && value.Contains("TestDataConnection");
                    }
                    catch (SecurityException)
                    {
                        s_extendedDiagnosticsEnabled = false;
                    }
                }
                return s_extendedDiagnosticsEnabled.Value;
            }
        }

        // List of places to look for files when substituting |DataDirectory|        
        List<string> m_dataFolders;

        static bool? s_extendedDiagnosticsEnabled;
    }
}
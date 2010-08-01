using Spark.FileSystem;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.Text;

namespace SparkSense.Parsing
{
    public class CachingViewFolder : IViewFolder
    {
        private FileSystemViewFolder _disk;
        private InMemoryViewFolder _cache;

        // TODO: Rob G evict cache entry when file on disk changes

        public CachingViewFolder(string basePath)
        {
            _cache = new InMemoryViewFolder();
            _disk = new FileSystemViewFolder(basePath);
        }
        public IViewFile GetViewSource(string path)
        {
            if (!_cache.HasView(path) || _cache[path].Length == 0)
            {
                LoadFromDisk(path);
            }
            return _cache.GetViewSource(path);
        }

        public void SetViewSource(string path, ITextSnapshot currentSnapshot)
        {
            _cache.Set(path, currentSnapshot.GetText());
        }

        public IList<string> ListViews(string path)
        {
            return _cache.ListViews(path);
        }

        public bool HasView(string path)
        {
            return _cache.HasView(path) || _disk.HasView(path);
        }

        public void Add(string path)
        {
            _cache.Add(path, null);
        }

        private void LoadFromDisk(string path)
        {
            var fileContents = _disk.GetViewSource(path);
            string contents;
            using (TextReader reader = new StreamReader(fileContents.OpenViewStream()))
                contents = reader.ReadToEnd();
            _cache.Set(path, contents);
        }
    }
}

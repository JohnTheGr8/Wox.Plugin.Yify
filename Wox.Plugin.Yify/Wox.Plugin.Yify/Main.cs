using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using YifySharp;
using YifySharp.Models;

namespace Wox.Plugin.Yify
{
    public class Main : IPlugin
    {
        private PluginInitContext _context; 
        
        private YifyClient _client;

        private bool _displayingMovie;

        public void Init(PluginInitContext context)
        {
            _context = context;
            _client = new YifyClient();
        }

        public List<Result> Query(Query query)
        {
            var results = new List<Result>();

            if (_displayingMovie)
            {
                _displayingMovie = false;
                return results;
            }

            var s = query.GetAllRemainingParameter();
            if (string.IsNullOrEmpty(s)) return results;

            var list = _client.GetMovieList(s);

            return list.Movies
                .ConvertAll(x => new Result()
                    {
                        Title = x.TitleLong,
                        SubTitle = String.Join(", ", x.Genres.ToArray()),
                        IcoPath = GetCover(x.SmallCoverImage, x.Title),
                        Action = e =>
                            {
                                QueryMovie(x);
                                return false;
                            }
                    });
        }

        private void QueryMovie(MovieInfo info)
        {
            // Add the movie on top
            var results = new List<Result>()
                {
                    new Result()
                        {
                            Title = info.TitleLong,
                            SubTitle = String.Join(", ", info.Genres.ToArray()),
                            IcoPath = GetCover(info.SmallCoverImage, info.Title),
                        }
                };
            // Add all available torrents
            results.AddRange(
                info.Torrents
                    .ConvertAll(x => new Result()
                        {
                            Title = string.Format("Download {0}", x.Quality),
                            SubTitle =
                                string.Format("Size: {0} - Seeds: {1} - Peers: {2}", x.Size, x.Seeds,
                                              x.Peers),
                            Action = e => _context.API.ShellRun(x.MagnetUrl),
                            IcoPath = _context.CurrentPluginMetadata.FullIcoPath
                        })
                );

            var q = new Query("yts " + info.Title);
            _displayingMovie = true;
            _context.API.ChangeQuery(q.RawQuery, false);
            _context.API.PushResults(q, _context.CurrentPluginMetadata, results);
        }

        /// <summary>
        /// Download the cover image in the Cache folder and return a path to the local file.
        /// </summary>
        public string GetCover(string href, string title)
        {
            if (!Directory.Exists(CacheFodler))
                Directory.CreateDirectory(CacheFodler);

            // local path to the image file
            var path = string.Format(@"{0}\{1}.jpg", CacheFodler, title);

            try
            {
                // Download the image file            
                if (!File.Exists(path))
                    new WebClient().DownloadFile(new Uri(href), path);
            }
            catch (Exception)
            {
                // fallback to plugin icon
                return _context.CurrentPluginMetadata.FullIcoPath;
            }

            return path;
        }

        /// <summary>
        /// Path to a cache folder inside the plugin folder in which to store movie cover images
        /// </summary>
        private string CacheFodler
        {
            get { return Path.Combine(_context.CurrentPluginMetadata.PluginDirectory, "Cache"); }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YoutubeExplode.Common;
using YoutubeExplode.Videos;

namespace ConsoleApp1
{
    public class PlayableVideo
    {
        /// <summary>
        /// Video ID.
        /// </summary>
        private string _id { get; }

        /// <summary>
        /// Video URL.
        /// </summary>
        private string _url { get; }

        /// <summary>
        /// Video title.
        /// </summary>
        private string _title { get; }

        /// <summary>
        /// Video author.
        /// </summary>
        string _author { get; }

        /// <summary>
        /// Video duration.
        /// </summary>
        /// <remarks>
        /// May be null if the video is a currently ongoing live stream.
        /// </remarks>
        TimeSpan? _duration { get; }

        /// <summary>
        /// Name of the file holding the audio stream.
        /// </summary>
        /// <remarks>
        /// May be null if isn't set.
        /// </remarks>
        string _filename { get; set; }

        public PlayableVideo(string id, string url, string title, string author, TimeSpan? duration)
        {
            _id = id;
            _url = url;
            _title = title;
            _author = author;
            _duration = duration;
        }

        public static PlayableVideo fromVideo(IVideo video)
        {
            return new PlayableVideo(video.Id.ToString(),video.Url,video.Title,video.Author.ToString(),video.Duration);
        }
    }
}

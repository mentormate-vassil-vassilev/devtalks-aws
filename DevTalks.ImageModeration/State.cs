using Amazon.Rekognition.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace DevTalks.ImageModeration
{
    public class State
    {
        public string Bucket { get; set; }

        public string ImageKey { get; set; }

        public string ProcessedImageURL { get; set; }

        public int AgeLimit { get; set; }

        public bool ShouldSmile { get; set; }

        public Dictionary<string, string> Labels { get; set; }

        public List<FaceDetail> Faces { get; set; }

        public Celebrity Celebrity { get; set; }
    }
}

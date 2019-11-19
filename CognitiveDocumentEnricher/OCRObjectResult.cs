using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CognitiveDocumentEnricher
{
    public class Word
    {
        public string boundingBox { get; set; }
        public string text { get; set; }
    }

    public class Line
    {
        public string boundingBox { get; set; }
        public List<Word> words { get; set; }
    }

    public class Region
    {
        public string boundingBox { get; set; }
        public List<Line> lines { get; set; }
    }

    public class OCRObjectResult
    {
        public string language { get; set; }
        public string orientation { get; set; }
        public double textAngle { get; set; }
        public List<Region> regions { get; set; }

        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();

            if (this.regions != null)
            {
                foreach (var region in this.regions)
                {
                    foreach (var line in region.lines)
                    {
                        foreach (var word in line.words)
                        {
                            stringBuilder.Append(word.text);
                            stringBuilder.Append(" ");
                        }

                        stringBuilder.AppendLine();
                    }
                    stringBuilder.AppendLine();
                }
            }

            var stringToReturn = stringBuilder.ToString();
            return stringToReturn;
        }
    }
}

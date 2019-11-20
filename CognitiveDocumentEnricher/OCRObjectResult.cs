using System.Collections.Generic;
using System.Text;

namespace CognitiveDocumentEnricher
{
    public class Word
    {
        public List<int> boundingBox { get; set; }
        public string text { get; set; }
        public string confidence { get; set; }
    }

    public class Line
    {
        public List<int> boundingBox { get; set; }
        public string text { get; set; }
        public List<Word> words { get; set; }
    }

    public class RecognitionResult
    {
        public int page { get; set; }
        public double clockwiseOrientation { get; set; }
        public int width { get; set; }
        public int height { get; set; }
        public string unit { get; set; }
        public List<Line> lines { get; set; }
    }

    public class OCRObjectResult
    {
        public string status { get; set; }
        public List<RecognitionResult> recognitionResults { get; set; }

        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();

            if (this.recognitionResults != null)
            {
                foreach (var region in this.recognitionResults)
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

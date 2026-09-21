using System.Text.Json.Serialization;

namespace Spanish.Core;

public record AnswerRecord(DateTime At, bool Correct);

public class LearnProgress
{
    public const int WindowSize = 10;

    public List<AnswerRecord> Recent { get; set; } = [];

    public int Attempts { get; set; }

    /// <summary>Accuracy over the last <see cref="WindowSize"/> answers, or null when never practiced.</summary>
    [JsonIgnore]
    public double? Index => Recent.Count == 0 ? null : (double)Recent.Count(r => r.Correct) / Recent.Count;

    [JsonIgnore]
    public DateTime? LastPracticed => Recent.Count == 0 ? null : Recent.Max(r => r.At);

    public void Record(bool correct, DateTime at)
    {
        Recent.Add(new AnswerRecord(at, correct));
        if (Recent.Count > WindowSize)
        {
            Recent.RemoveAt(0);
        }
        Attempts++;
    }
}

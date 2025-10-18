using System;
using System.Text.RegularExpressions;

namespace JiraTogglSync.Services;

public class WorkLogEntry
{
	public string IssueKey { get; set; }
	public string? JiraWorklogId { get; set; }
	public string SourceId { get; set; }
	public DateTimeOffset Started { get; set; }
	public int TimeSpentSeconds { get; set; }
	public string? Comment { get; set; }

	public TimeSpan TimeSpent => TimeSpan.FromSeconds(TimeSpentSeconds);

	public static string? GetSourceId(string? description)
	{
		var regex = new Regex(@"\[toggl-id:(?<sourceId>[0-9]+)]");
		var matchResult = regex.Match(description ?? "");
		return matchResult.Success ? matchResult.Groups["sourceId"].Value : null;
	}

	public WorkLogEntry(string issueKey, string sourceId, DateTimeOffset startDate, int timeSpentInMinutes, string description)
	{
		IssueKey = issueKey;
		SourceId = sourceId;
		Started = startDate.UtcDateTime;
		TimeSpentSeconds = timeSpentInMinutes * 60;
		Comment = description;
	}

	public WorkLogEntry(string issueKey, string sourceId, JiraWorklog jiraWorkLog)
	{
		IssueKey = issueKey;
		SourceId = sourceId;
		JiraWorklogId = jiraWorkLog.Id;
		Started = jiraWorkLog.Started;
		TimeSpentSeconds = jiraWorkLog.TimeSpentSeconds;
		Comment = jiraWorkLog.Comment?.ToPlainText();
	}

	public override string ToString()
	{
		return $"[{IssueKey}] - {Started:d} ({Math.Ceiling((DateTime.UtcNow - Started).TotalDays)}d ago) - {TimeSpent} - {Comment}";
	}

	public bool DifferentFrom(WorkLogEntry other)
	{
		if (other.TimeSpent != TimeSpent)
			return true;
		if (other.Started.ToUniversalTime() != Started.ToUniversalTime())
			return true;
		if (other.Comment != Comment)
			return true;
		return false;
	}

	public void Synchronize(WorkLogEntry other)
	{
		Started = other.Started;
		TimeSpentSeconds = other.TimeSpentSeconds;
		Comment = other.Comment;
	}
}

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace JiraTogglSync.Services;

public sealed record JiraIssueResponse
{
	[JsonPropertyName("id")]
	public string Id { get; set; } = null!;

	[JsonPropertyName("key")]
	public string Key { get; set; } = null!;

	[JsonPropertyName("fields")]
	public JiraIssueFields Fields { get; set; } = null!;
}

public sealed record JiraIssueFields
{
	[JsonPropertyName("summary")]
	public string Summary { get; set; } = null!;
}

public sealed record JiraWorklogResponse
{
	[JsonPropertyName("startAt")]
	public int StartAt { get; set; }

	[JsonPropertyName("maxResults")]
	public int MaxResults { get; set; }

	[JsonPropertyName("total")]
	public int Total { get; set; }

	[JsonPropertyName("worklogs")]
	public List<JiraWorklog> Worklogs { get; set; } = new();
}

public sealed record JiraWorklog
{
	[JsonPropertyName("id")]
	public string Id { get; set; } = null!;

	[JsonPropertyName("issueId")]
	public string? IssueId { get; set; }

	[JsonPropertyName("author")]
	public JiraUser? Author { get; set; }

	[JsonPropertyName("comment")]
	public JiraDocumentFormat? Comment { get; set; }

	[JsonPropertyName("started")]
	[JsonConverter(typeof(JiraDateTimeConverter))]
	public DateTimeOffset Started { get; set; }

	[JsonPropertyName("timeSpentSeconds")]
	public int TimeSpentSeconds { get; set; }

	[JsonPropertyName("timeSpent")]
	public string? TimeSpent { get; set; }
}

public sealed record JiraWorklogCreateRequest
{
	[JsonPropertyName("comment")]
	public JiraDocumentFormat? Comment { get; set; }

	[JsonPropertyName("started")]
	[JsonConverter(typeof(JiraDateTimeConverter))]
	public DateTimeOffset Started { get; set; }

	[JsonPropertyName("timeSpentSeconds")]
	public int TimeSpentSeconds { get; set; }
}

public sealed record JiraDocumentFormat
{
	[JsonPropertyName("type")]
	public string Type { get; set; } = "doc";

	[JsonPropertyName("version")]
	public int Version { get; set; } = 1;

	[JsonPropertyName("content")]
	public List<JiraDocumentNode> Content { get; init; } = new();

	public string? ToPlainText()
	{
		if (Content.Count == 0)
			return null;

		var textParts = new List<string>();
		ExtractText(Content, textParts);
		return textParts.Count > 0 ? string.Join("", textParts) : null;
	}

	private static void ExtractText(List<JiraDocumentNode> nodes, List<string> textParts)
	{
		foreach (var node in nodes) {
			switch (node) {
				case JiraDocumentTextNode textNode:
					textParts.Add(textNode.Text);
					break;
				case JiraDocumentParagraphNode paragraphNode:
				{
					if (paragraphNode.Content.Count > 0) {
						ExtractText(paragraphNode.Content, textParts);
					}

					break;
				}
			}
		}
	}
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(JiraDocumentParagraphNode), "paragraph")]
[JsonDerivedType(typeof(JiraDocumentTextNode), "text")]
[JsonDerivedType(typeof(JiraDocumentInlineCardNode), "inlineCard")]
public abstract record JiraDocumentNode
{
	[JsonPropertyName("type")]
	public string Type { get; protected set; } = null!;
}

public sealed record JiraDocumentParagraphNode : JiraDocumentNode
{
	[JsonPropertyName("content")]
	public List<JiraDocumentNode> Content { get; init; } = new();

	public JiraDocumentParagraphNode()
	{
		Type = "paragraph";
	}
}

public sealed record JiraDocumentTextNode : JiraDocumentNode
{
	[JsonPropertyName("text")]
	public string Text { get; set; } = null!;

	public JiraDocumentTextNode()
	{
		Type = "text";
	}
}

public sealed record JiraDocumentInlineCardNode : JiraDocumentNode
{
	[JsonPropertyName("attrs")]
	public JiraDocumentInlineCardAttributes Attrs { get; set; } = null!;

	public JiraDocumentInlineCardNode()
	{
		Type = "inlineCard";
	}
}

public sealed record JiraDocumentInlineCardAttributes
{
	[JsonPropertyName("url")]
	public string Url { get; set; } = null!;
}

public sealed record JiraUser
{
	[JsonPropertyName("accountId")]
	public string? AccountId { get; set; }

	[JsonPropertyName("emailAddress")]
	public string? EmailAddress { get; set; }

	[JsonPropertyName("displayName")]
	public string? DisplayName { get; set; }

	[JsonPropertyName("active")]
	public bool Active { get; set; }
}

public sealed record JiraSearchResponse
{
	[JsonPropertyName("startAt")]
	public int StartAt { get; set; }

	[JsonPropertyName("maxResults")]
	public int MaxResults { get; set; }

	[JsonPropertyName("total")]
	public int Total { get; set; }

	[JsonPropertyName("issues")]
	public List<JiraIssueResponse> Issues { get; set; } = new();
}

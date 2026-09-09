using System;
using System.Net;

namespace JiraTogglSync.Services;

internal class JiraRestException : Exception
{
	public JiraRestException(string issueKey, HttpStatusCode statusCode, string body)
		: base($"Jira API error response on {issueKey}: {statusCode}\nBody: {body}")
	{
	}
}

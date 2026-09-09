using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace JiraTogglSync.Services;

public interface IJiraRepository
{
	Task<ICollection<WorkLogEntry>> GetWorkLogOfIssuesAsync(DateTimeOffset fromDate, DateTimeOffset toDate, ICollection<string> issueKeys);
	Task<OperationResult> AddWorkLogAsync(WorkLogEntry entry);
	Task<OperationResult> UpdateWorkLogAsync(WorkLogEntry entry);
	Task<OperationResult> DeleteWorkLogAsync(WorkLogEntry entry);
	Task<JiraUser> GetUserInformation();
}

public class JiraRestService : IJiraRepository
{
	private readonly Options _options;
	private readonly HttpClient _httpClient;

	public class Options
	{
		[Required]
		public string Instance { get; set; } = null!;

		[Required]
		public string Username { get; set; } = null!;

		[Required]
		public string ApiToken { get; set; } = null!;
	}

	public JiraRestService(IOptions<Options> options, HttpClient httpClient)
	{
		_options = options.Value;
		_httpClient = httpClient;

		_httpClient.BaseAddress = new Uri(_options.Instance);
		var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_options.Username}:{_options.ApiToken}"));
		_httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);
		_httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
	}

	public async Task<ICollection<WorkLogEntry>> GetWorkLogOfIssuesAsync(DateTimeOffset fromDate, DateTimeOffset toDate, ICollection<string> issueKeys)
	{
		// Basically we need to find all work log items that have been created or edited within start and end dates.
		// Note: since we don't have endpoint for 'Get ids of worklogs modified since', we will find those work logs
		// through issues that were recently modified.

		if (issueKeys.Count == 0)
			return Array.Empty<WorkLogEntry>();

		var workLogs = new ConcurrentBag<WorkLogEntry>();
		await Parallel.ForEachAsync(issueKeys,
			async (issueKey, ct) => {
				var issueWorkLogs = await GetWorkLogEntriesAsync(fromDate, toDate, issueKey, ct);
				foreach (var workLog in issueWorkLogs)
					workLogs.Add(workLog);
			}
		);

		return workLogs.OrderBy(x => x.Started).ToArray();
	}

	private async Task<List<WorkLogEntry>> GetWorkLogEntriesAsync(
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		string issueKey,
		CancellationToken cancellationToken = default
	)
	{
		var response = await _httpClient.GetAsync($"rest/api/3/issue/{issueKey}/worklog", cancellationToken);
		if (!response.IsSuccessStatusCode) {
			var body = await response.Content.ReadAsStringAsync(cancellationToken);
			throw new JiraRestException(issueKey, response.StatusCode, body);
		}

		var worklogResponse = await response.Content.ReadFromJsonAsync<JiraWorklogResponse>(cancellationToken);
		if (worklogResponse == null)
			return [];

		var currentUser = await GetUserInformation();

		return worklogResponse.Worklogs
							.Where(workLog => {
								var workLogStart = workLog.Started;
								var workLogEnd = workLogStart.AddSeconds(workLog.TimeSpentSeconds);
								return workLogStart >= startDate
										&& workLogEnd <= endDate
										&& (workLog.Author?.EmailAddress == _options.Username
											|| workLog.Author?.AccountId == currentUser.AccountId);
							})
							.Select(wl => {
								var sourceId = WorkLogEntry.GetSourceId(wl.Comment?.ToPlainText());
								return sourceId == null ? null : new WorkLogEntry(issueKey, sourceId, wl);
							})
							.WhereNotNull()
							.ToList();
	}

	public async Task<OperationResult> UpdateWorkLogAsync(WorkLogEntry entry)
	{
		try {
			var request = new JiraWorklogCreateRequest {
				Comment = CreateJiraDocumentFormat(entry.Comment),
				Started = entry.Started,
				TimeSpentSeconds = entry.TimeSpentSeconds
			};

			var response = await _httpClient.PutAsJsonAsync(
				$"rest/api/3/issue/{entry.IssueKey}/worklog/{entry.JiraWorklogId}",
				request
			);
			if (response.IsSuccessStatusCode)
				return OperationResult.Success(entry);

			var errorContent = await response.Content.ReadAsStringAsync();
			return OperationResult.Error(errorContent, entry);
		}
		catch (Exception ex) {
			return OperationResult.Error(ex.Message, entry);
		}
	}

	public async Task<OperationResult> DeleteWorkLogAsync(WorkLogEntry entry)
	{
		try {
			var response = await _httpClient.DeleteAsync($"rest/api/3/issue/{entry.IssueKey}/worklog/{entry.JiraWorklogId}");
			if (response.IsSuccessStatusCode)
				return OperationResult.Success(entry);

			var errorContent = await response.Content.ReadAsStringAsync();
			return OperationResult.Error(errorContent, entry);
		}
		catch (Exception ex) {
			return OperationResult.Error(ex.Message, entry);
		}
	}

	public async Task<OperationResult> AddWorkLogAsync(WorkLogEntry entry)
	{
		try {
			var request = new JiraWorklogCreateRequest {
				Comment = CreateJiraDocumentFormat(entry.Comment),
				Started = entry.Started,
				TimeSpentSeconds = entry.TimeSpentSeconds
			};

			var response = await _httpClient.PostAsJsonAsync(
				$"rest/api/3/issue/{entry.IssueKey}/worklog",
				request
			);
			if (response.IsSuccessStatusCode)
				return OperationResult.Success(entry);

			var errorContent = await response.Content.ReadAsStringAsync();
			return OperationResult.Error(errorContent, entry);
		}
		catch (Exception ex) {
			return OperationResult.Error(ex.Message, entry);
		}
	}

	private static JiraDocumentFormat? CreateJiraDocumentFormat(string? plainText)
	{
		if (string.IsNullOrEmpty(plainText))
			return null;

		return new JiraDocumentFormat {
			Type = "doc",
			Version = 1,
			Content = [
				new JiraDocumentParagraphNode {
					Content = [
						new JiraDocumentTextNode {
							Text = plainText
						}
					]
				}
			]
		};
	}

	private JiraUser? _cachedUser;

	public async Task<JiraUser> GetUserInformation()
	{
		if (_cachedUser != null)
			return _cachedUser;

		var response = await _httpClient.GetAsync("rest/api/3/myself");
		response.EnsureSuccessStatusCode();

		_cachedUser = await response.Content.ReadFromJsonAsync<JiraUser>();
		return _cachedUser ?? throw new InvalidOperationException("Failed to get user information");
	}
}

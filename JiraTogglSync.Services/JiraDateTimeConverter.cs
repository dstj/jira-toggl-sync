using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JiraTogglSync.Services;

public class JiraDateTimeConverter : JsonConverter<DateTimeOffset>
{
	public override bool CanConvert(Type typeToConvert)
	{
		return typeToConvert == typeof(DateTimeOffset) || typeToConvert == typeof(DateTimeOffset?);
	}

	public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var dateString = reader.GetString();
		if (string.IsNullOrEmpty(dateString))
			return DateTimeOffset.MinValue;

		// Jira format: 2025-10-16T11:40:00.000-0400 (missing colon in timezone)
		if (dateString.Length >= 5) {
			var lastPart = dateString.Substring(dateString.Length - 5);
			if ((lastPart[0] == '+' || lastPart[0] == '-') &&
				char.IsDigit(lastPart[1]) &&
				char.IsDigit(lastPart[2]) &&
				char.IsDigit(lastPart[3]) &&
				char.IsDigit(lastPart[4])) {
				dateString = dateString.Substring(0, dateString.Length - 2) + ":" + dateString.Substring(dateString.Length - 2);
			}
		}

		return DateTimeOffset.Parse(dateString);
	}

	public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
	{
		var formatted = value.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz");
		if (formatted.Length >= 3 && formatted[formatted.Length - 3] == ':') {
			formatted = formatted.Substring(0, formatted.Length - 3) + formatted.Substring(formatted.Length - 2);
		}

		writer.WriteStringValue(formatted);
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Core = HourSyncCoreLib.HourSyncCore;

// This ENTIRE file was written by Claude, please don't ask me to explain what ANY of it does.

namespace HourSync;
public static class Searcher
{
    /// <summary>
    /// Calculates the Levenshtein distance between two strings
    /// </summary>
    /// <param name="source">First string</param>
    /// <param name="target">Second string</param>
    /// <returns>The minimum number of single-character edits required to transform source into target</returns>
    public static int CalculateLevenshteinDistance(string source, string target)
    {
        if (string.IsNullOrEmpty(source))
            return string.IsNullOrEmpty(target) ? 0 : target.Length;

        if (string.IsNullOrEmpty(target))
            return source.Length;

        // Convert to lowercase for case-insensitive comparison
        source = source.ToLowerInvariant();
        target = target.ToLowerInvariant();

        int sourceLength = source.Length;
        int targetLength = target.Length;

        // Create a matrix to store distances
        int[,] matrix = new int[sourceLength + 1, targetLength + 1];

        // Initialize the first row and column
        for (int i = 0; i <= sourceLength; i++)
            matrix[i, 0] = i;

        for (int j = 0; j <= targetLength; j++)
            matrix[0, j] = j;

        // Calculate distances
        for (int i = 1; i <= sourceLength; i++)
        {
            for (int j = 1; j <= targetLength; j++)
            {
                int cost = (source[i - 1] == target[j - 1]) ? 0 : 1;

                matrix[i, j] = Math.Min(
                    Math.Min(
                        matrix[i - 1, j] + 1,     // deletion
                        matrix[i, j - 1] + 1),    // insertion
                    matrix[i - 1, j - 1] + cost  // substitution
                );
            }
        }

        return matrix[sourceLength, targetLength];
    }

    /// <summary>
    /// Searches for eHour requests using fuzzy matching with Levenshtein distance
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="requests">List of requests to search through</param>
    /// <param name="maxDistance">Maximum allowed Levenshtein distance (default: 2)</param>
    /// <returns>List of matching requests ordered by relevance</returns>
    public static List<Core.EHourRequest> FuzzySearch(string query, List<Core.EHourRequest> requests, int maxDistance = 2)
    {
        if (string.IsNullOrWhiteSpace(query) || requests == null || !requests.Any())
            return new List<Core.EHourRequest>();

        var results = new List<(Core.EHourRequest Request, int Distance, double Score)>();

        foreach (var request in requests)
        {
            // Search in description
            int descriptionDistance = CalculateLevenshteinDistance(query, request.Description);

            // Also check if query is a substring (partial match)
            bool isSubstring = request.Description.ToLowerInvariant().Contains(query.ToLowerInvariant());

            // Calculate a combined score (lower is better)
            double score = descriptionDistance;

            // Bonus for substring matches
            if (isSubstring)
                score *= 0.5; // Give substring matches a significant bonus

            // Bonus for shorter descriptions (more specific matches)
            if (request.Description.Length < query.Length * 3)
                score *= 0.8;

            // Only include if within acceptable distance or is a substring
            if (descriptionDistance <= maxDistance || isSubstring)
            {
                results.Add((request, descriptionDistance, score));
            }
        }

        // Sort by score (ascending - lower is better) and return requests
        return results
            .OrderBy(r => r.Score)
            .ThenBy(r => r.Distance)
            .Select(r => r.Request)
            .ToList();
    }

    /// <summary>
    /// Searches across all request lists (Returned, Pending, Accepted, Denied)
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="returnedRequests">Returned requests list</param>
    /// <param name="pendingRequests">Pending requests list</param>
    /// <param name="acceptedRequests">Accepted requests list</param>
    /// <param name="deniedRequests">Denied requests list</param>
    /// <param name="maxDistance">Maximum allowed Levenshtein distance</param>
    /// <returns>Dictionary with search results categorized by status</returns>
    public static Dictionary<string, List<Core.EHourRequest>> SearchAllRequests(
        string query,
        List<Core.EHourRequest> returnedRequests,
        List<Core.EHourRequest> pendingRequests,
        List<Core.EHourRequest> acceptedRequests,
        List<Core.EHourRequest> deniedRequests,
        int maxDistance = 2)
    {
        return new Dictionary<string, List<Core.EHourRequest>>
        {
            ["returned"] = FuzzySearch(query, returnedRequests, maxDistance),
            ["pending"] = FuzzySearch(query, pendingRequests, maxDistance),
            ["accepted"] = FuzzySearch(query, acceptedRequests, maxDistance),
            ["denied"] = FuzzySearch(query, deniedRequests, maxDistance)
        };
    }

    /// <summary>
    /// Provides natural language date searching for eHour requests
    /// </summary>
    public static class NaturalDateSearch
    {
        private static readonly Dictionary<string, int> MonthNames = new(StringComparer.OrdinalIgnoreCase)
    {
        {"january", 1}, {"jan", 1},
        {"february", 2}, {"feb", 2},
        {"march", 3}, {"mar", 3},
        {"april", 4}, {"apr", 4},
        {"may", 5},
        {"june", 6}, {"jun", 6},
        {"july", 7}, {"jul", 7},
        {"august", 8}, {"aug", 8},
        {"september", 9}, {"sep", 9}, {"sept", 9},
        {"october", 10}, {"oct", 10},
        {"november", 11}, {"nov", 11},
        {"december", 12}, {"dec", 12}
    };

        /// <summary>
        /// Searches for requests based on natural date expressions
        /// </summary>
        /// <param name="query">Search query (e.g., "february", "2023", "march 2024")</param>
        /// <param name="requests">List of requests to search through</param>
        /// <returns>List of matching requests</returns>
        public static List<Core.EHourRequest> SearchByDate(string query, List<Core.EHourRequest> requests)
        {
            if (string.IsNullOrWhiteSpace(query) || requests == null || !requests.Any())
                return new List<Core.EHourRequest>();

            var matchingRequests = new List<Core.EHourRequest>();
            var queryLower = query.ToLowerInvariant().Trim();

            foreach (var request in requests)
            {
                if (IsDateMatch(queryLower, request.Date))
                {
                    matchingRequests.Add(request);
                }
            }

            return matchingRequests;
        }

        /// <summary>
        /// Determines if a request's date matches the natural language query
        /// </summary>
        /// <param name="query">Normalized query string</param>
        /// <param name="dateString">Date string from the request</param>
        /// <returns>True if the date matches the query</returns>
        private static bool IsDateMatch(string query, string dateString)
        {
            if (string.IsNullOrEmpty(dateString))
                return false;

            // Try to parse the request date
            if (!DateTime.TryParse(dateString, out DateTime requestDate))
                return false;

            // Check for year match (e.g., "2023")
            if (Regex.IsMatch(query, @"^\d{4}$"))
            {
                if (int.TryParse(query, out int year))
                {
                    return requestDate.Year == year;
                }
            }

            // Check for month name match (e.g., "february", "feb")
            if (MonthNames.ContainsKey(query))
            {
                return requestDate.Month == MonthNames[query];
            }

            // Check for month and year (e.g., "february 2023", "feb 2023")
            var monthYearMatch = Regex.Match(query, @"^(\w+)\s+(\d{4})$");
            if (monthYearMatch.Success)
            {
                string monthStr = monthYearMatch.Groups[1].Value;
                string yearStr = monthYearMatch.Groups[2].Value;

                if (MonthNames.ContainsKey(monthStr) && int.TryParse(yearStr, out int year))
                {
                    return requestDate.Month == MonthNames[monthStr] && requestDate.Year == year;
                }
            }

            // Check for relative date terms
            if (IsRelativeDateMatch(query, requestDate))
            {
                return true;
            }

            // Check for season match
            if (IsSeasonMatch(query, requestDate))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks for relative date matches like "today", "yesterday", "this week", etc.
        /// </summary>
        private static bool IsRelativeDateMatch(string query, DateTime requestDate)
        {
            var today = DateTime.Today;

            return query switch
            {
                "today" => requestDate.Date == today,
                "yesterday" => requestDate.Date == today.AddDays(-1),
                "this week" => IsInCurrentWeek(requestDate, today),
                "last week" => IsInLastWeek(requestDate, today),
                "this month" => requestDate.Year == today.Year && requestDate.Month == today.Month,
                "last month" => IsLastMonth(requestDate, today),
                "this year" => requestDate.Year == today.Year,
                "last year" => requestDate.Year == today.Year - 1,
                _ => false
            };
        }

        /// <summary>
        /// Checks for season matches (spring, summer, fall/autumn, winter)
        /// </summary>
        private static bool IsSeasonMatch(string query, DateTime requestDate)
        {
            var month = requestDate.Month;

            return query switch
            {
                "spring" => month >= 3 && month <= 5,
                "summer" => month >= 6 && month <= 8,
                "fall" or "autumn" => month >= 9 && month <= 11,
                "winter" => month == 12 || month <= 2,
                _ => false
            };
        }

        private static bool IsInCurrentWeek(DateTime date, DateTime today)
        {
            var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
            var endOfWeek = startOfWeek.AddDays(7);
            return date.Date >= startOfWeek && date.Date < endOfWeek;
        }

        private static bool IsInLastWeek(DateTime date, DateTime today)
        {
            var startOfLastWeek = today.AddDays(-(int)today.DayOfWeek - 7);
            var endOfLastWeek = startOfLastWeek.AddDays(7);
            return date.Date >= startOfLastWeek && date.Date < endOfLastWeek;
        }

        private static bool IsLastMonth(DateTime date, DateTime today)
        {
            var lastMonth = today.AddMonths(-1);
            return date.Year == lastMonth.Year && date.Month == lastMonth.Month;
        }

        /// <summary>
        /// Searches across all request lists using natural date expressions
        /// </summary>
        public static Dictionary<string, List<Core.EHourRequest>> SearchAllRequestsByDate(
            string query,
            List<Core.EHourRequest> returnedRequests,
            List<Core.EHourRequest> pendingRequests,
            List<Core.EHourRequest> acceptedRequests,
            List<Core.EHourRequest> deniedRequests)
        {
            return new Dictionary<string, List<Core.EHourRequest>>
            {
                ["returned"] = SearchByDate(query, returnedRequests),
                ["pending"] = SearchByDate(query, pendingRequests),
                ["accepted"] = SearchByDate(query, acceptedRequests),
                ["denied"] = SearchByDate(query, deniedRequests)
            };
        }
    }
    /// <summary>
    /// Combined search functionality that merges fuzzy text search with natural date search
    /// </summary>
    public static class CombinedSearch
    {
        /// <summary>
        /// Performs combined fuzzy text and natural date search
        /// </summary>
        /// <param name="query">Search query</param>
        /// <param name="requests">List of requests to search</param>
        /// <param name="maxDistance">Maximum Levenshtein distance for fuzzy matching</param>
        /// <returns>Combined and deduplicated list of matching requests</returns>
        public static List<Core.EHourRequest> SearchCombined(string query, List<Core.EHourRequest> requests, int maxDistance = 2)
        {
            if (string.IsNullOrWhiteSpace(query) || requests == null || !requests.Any())
                return new List<Core.EHourRequest>();

            // Get results from both search methods
            var fuzzyResults = FuzzySearch(query, requests, maxDistance);
            var dateResults = NaturalDateSearch.SearchByDate(query, requests);

            // Combine and deduplicate results (using Value as unique identifier)
            var combinedResults = fuzzyResults
                .Concat(dateResults)
                .GroupBy(r => r.Value)
                .Select(g => g.First())
                .ToList();

            // Prioritize fuzzy matches over date matches for scoring
            var fuzzyResultValues = new HashSet<string>(fuzzyResults.Select(r => r.Value));

            return combinedResults
                .OrderBy(r => fuzzyResultValues.Contains(r.Value) ? 0 : 1) // Fuzzy matches first
                .ThenBy(r => r.Description) // Then by description
                .ToList();
        }

        /// <summary>
        /// Performs combined search across all request categories
        /// </summary>
        public static Dictionary<string, List<Core.EHourRequest>> SearchAllRequestsCombined(
            string query,
            List<Core.EHourRequest> returnedRequests,
            List<Core.EHourRequest> pendingRequests,
            List<Core.EHourRequest> acceptedRequests,
            List<Core.EHourRequest> deniedRequests,
            int maxDistance = 2)
        {
            return new Dictionary<string, List<Core.EHourRequest>>
            {
                ["returned"] = SearchCombined(query, returnedRequests, maxDistance),
                ["pending"] = SearchCombined(query, pendingRequests, maxDistance),
                ["accepted"] = SearchCombined(query, acceptedRequests, maxDistance),
                ["denied"] = SearchCombined(query, deniedRequests, maxDistance)
            };
        }
    }
}
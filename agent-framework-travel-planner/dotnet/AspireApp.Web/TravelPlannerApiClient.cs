using System.Net.Http.Json;

namespace AspireApp.Web;

public class TravelPlannerApiClient(HttpClient httpClient)
{
    public async Task<TravelPlanResponse> CreatePlanAsync(TravelPlanRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/travelplan", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(message)
                ? "Travel planning request failed."
                : message);
        }

        var plan = await response.Content.ReadFromJsonAsync<TravelPlanResponse>(cancellationToken);
        return plan ?? throw new InvalidOperationException("The travel planning service returned an empty response.");
    }
}

public record TravelPlanRequest(string TravelerName, DateOnly StartDate, int Nights);

public record TravelPlanResponse(
    DateOnly StartDate,
    int Nights,
    string Note,
    string Itinerary);

public record TravelPlanDay(DateOnly Date, string Activity);

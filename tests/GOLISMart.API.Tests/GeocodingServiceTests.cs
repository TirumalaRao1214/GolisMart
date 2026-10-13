using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using GOLISMart.API.DTOs;
using GOLISMart.API.Services.Implementations;
using GOLISMart.API.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;

namespace GOLISMart.API.Tests;

/// <summary>
/// Unit tests for NominatimGeocodingService.
/// All tests mock the HTTP message handler — no live Nominatim calls are made.
/// </summary>
public class GeocodingServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Build a service with a mock HTTP handler that returns the given response.</summary>
    private static NominatimGeocodingService CreateService(
        HttpResponseMessage httpResponse,
        out IMemoryCache cache)
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var client = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://nominatim.openstreetmap.org/")
        };

        cache = new MemoryCache(new MemoryCacheOptions());
        return new NominatimGeocodingService(client, cache, NullLogger<NominatimGeocodingService>.Instance);
    }

    private static HttpResponseMessage OkJson(object payload) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json")
        };

    /// <summary>Builds a representative Nominatim response for central Hyderabad.</summary>
    private static object HyderabadNominatimResponse(
        string? houseNumber = null,
        string? road        = "MG Road",
        string? suburb      = "Banjara Hills",
        string? city        = "Hyderabad",
        string? state       = "Telangana",
        string? postcode    = "500034",
        string? country     = "India") => new
    {
        place_id     = 123456789,
        display_name = "MG Road, Banjara Hills, Hyderabad, Telangana 500034, India",
        address = new
        {
            house_number = houseNumber,
            road,
            suburb,
            city,
            state,
            postcode,
            country
        }
    };

    // ── Valid coordinates — successful geocode ─────────────────────────────────

    [Fact]
    public async Task ReverseGeocode_ReturnsSuccess_WhenNominatimReturnsValidAddress()
    {
        var response = OkJson(HyderabadNominatimResponse());
        var svc = CreateService(response, out _);

        var result = await svc.ReverseGeocodeAsync(17.4156, 78.4347);

        result.Success.Should().BeTrue();
        result.Address.Should().NotBeNull();
        result.Address!.City.Should().Be("Hyderabad");
        result.Address.State.Should().Be("Telangana");
        result.Address.PinCode.Should().Be("500034");
        result.Address.Street.Should().Be("MG Road");
        result.Address.Locality.Should().Be("Banjara Hills");
        result.Address.Country.Should().Be("India");
        // Coordinates must match what was passed in — never hardcoded
        result.Address.Latitude.Should().BeApproximately(17.4156, 1e-9);
        result.Address.Longitude.Should().BeApproximately(78.4347, 1e-9);
    }

    [Fact]
    public async Task ReverseGeocode_PopulatesHouseNumber_WhenProviderReturnsIt()
    {
        var response = OkJson(HyderabadNominatimResponse(houseNumber: "42"));
        var svc = CreateService(response, out _);

        var result = await svc.ReverseGeocodeAsync(17.4156, 78.4347);

        result.Success.Should().BeTrue();
        result.Address!.HouseNumber.Should().Be("42");
    }

    // ── Missing optional fields ────────────────────────────────────────────────

    [Fact]
    public async Task ReverseGeocode_LeavesHouseNumberEmpty_WhenMissing()
    {
        var response = OkJson(HyderabadNominatimResponse(houseNumber: null));
        var svc = CreateService(response, out _);

        var result = await svc.ReverseGeocodeAsync(17.4156, 78.4347);

        result.Success.Should().BeTrue();
        result.Address!.HouseNumber.Should().BeEmpty();
    }

    [Fact]
    public async Task ReverseGeocode_LeavesLocalityEmpty_WhenMissing()
    {
        var response = OkJson(HyderabadNominatimResponse(suburb: null));
        var svc = CreateService(response, out _);

        var result = await svc.ReverseGeocodeAsync(17.4156, 78.4347);

        result.Success.Should().BeTrue();
        result.Address!.Locality.Should().BeEmpty();
    }

    [Fact]
    public async Task ReverseGeocode_LeavesPinCodeEmpty_WhenMissing()
    {
        var response = OkJson(HyderabadNominatimResponse(postcode: null));
        var svc = CreateService(response, out _);

        var result = await svc.ReverseGeocodeAsync(17.4156, 78.4347);

        result.Success.Should().BeTrue();
        result.Address!.PinCode.Should().BeEmpty();
    }

    [Fact]
    public async Task ReverseGeocode_LeavesPinCodeEmpty_WhenPostcodeIsNotIndian()
    {
        // Non-6-digit postcode should NOT be placed in the PinCode field
        var response = OkJson(HyderabadNominatimResponse(postcode: "SW1A 1AA"));
        var svc = CreateService(response, out _);

        var result = await svc.ReverseGeocodeAsync(17.4156, 78.4347);

        result.Success.Should().BeTrue();
        result.Address!.PinCode.Should().BeEmpty();
    }

    // ── Nominatim error response ───────────────────────────────────────────────

    [Fact]
    public async Task ReverseGeocode_ReturnsFailure_WhenNominatimReturnsErrorField()
    {
        var response = OkJson(new { error = "Unable to geocode" });
        var svc = CreateService(response, out _);

        var result = await svc.ReverseGeocodeAsync(0.0, 0.0);

        result.Success.Should().BeFalse();
        result.Address.Should().BeNull();
        result.Error.Should().NotBeNullOrEmpty();
    }

    // ── HTTP error responses ───────────────────────────────────────────────────

    [Fact]
    public async Task ReverseGeocode_ReturnsFailure_On429TooManyRequests()
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        var svc = CreateService(response, out _);

        var result = await svc.ReverseGeocodeAsync(17.4156, 78.4347);

        result.Success.Should().BeFalse();
        result.Error.Should().MatchRegex("busy|rate|shortly",
            because: "response should mention the rate-limit situation");
    }

    [Fact]
    public async Task ReverseGeocode_ReturnsFailure_OnUnexpectedHttpError()
    {
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var svc = CreateService(response, out _);

        var result = await svc.ReverseGeocodeAsync(17.4156, 78.4347);

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    // ── Malformed JSON ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ReverseGeocode_ReturnsFailure_WhenJsonIsMalformed()
    {
        var badJson = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("this is not json", Encoding.UTF8, "application/json")
        };
        var svc = CreateService(badJson, out _);

        var result = await svc.ReverseGeocodeAsync(17.4156, 78.4347);

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    // ── Network failure ────────────────────────────────────────────────────────

    [Fact]
    public async Task ReverseGeocode_ReturnsFailure_OnNetworkException()
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network unreachable"));

        var client = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://nominatim.openstreetmap.org/")
        };
        var cache = new MemoryCache(new MemoryCacheOptions());
        var svc   = new NominatimGeocodingService(client, cache, NullLogger<NominatimGeocodingService>.Instance);

        var result = await svc.ReverseGeocodeAsync(17.4156, 78.4347);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("unavailable");
    }

    // ── Timeout ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReverseGeocode_ReturnsFailure_OnTimeout()
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            // Simulate HttpClient timeout: TaskCanceledException without the CT being cancelled
            .ThrowsAsync(new TaskCanceledException("Request timed out",
                new TimeoutException(), new CancellationToken(false)));

        var client = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://nominatim.openstreetmap.org/")
        };
        var cache = new MemoryCache(new MemoryCacheOptions());
        var svc   = new NominatimGeocodingService(client, cache, NullLogger<NominatimGeocodingService>.Instance);

        var result = await svc.ReverseGeocodeAsync(17.4156, 78.4347);

        result.Success.Should().BeFalse();
        result.Error.Should().MatchRegex("timed out|unavailable",
            because: "response should indicate a timeout or service unavailability");
    }

    // ── Caching ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReverseGeocode_ReturnsCachedResult_OnSecondCall()
    {
        var callCount = 0;
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                return OkJson(HyderabadNominatimResponse());
            });

        var client = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://nominatim.openstreetmap.org/")
        };
        var cache = new MemoryCache(new MemoryCacheOptions());
        var svc   = new NominatimGeocodingService(client, cache, NullLogger<NominatimGeocodingService>.Instance);

        await svc.ReverseGeocodeAsync(17.4156, 78.4347);
        await svc.ReverseGeocodeAsync(17.4156, 78.4347); // same rounded coordinates → cache hit

        callCount.Should().Be(1, "second request with same coordinates should use cache");
    }

    // ── Coordinates are the actual request values, not hardcoded ──────────────

    [Fact]
    public async Task ReverseGeocode_LatLonInResponse_MatchInputCoordinates()
    {
        var response = OkJson(HyderabadNominatimResponse());
        var svc = CreateService(response, out _);

        var lat = 12.9716;
        var lon = 77.5946;
        var result = await svc.ReverseGeocodeAsync(lat, lon);

        result.Success.Should().BeTrue();
        result.Address!.Latitude.Should().BeApproximately(lat, 1e-9);
        result.Address.Longitude.Should().BeApproximately(lon, 1e-9);
    }

    // ── Street fallback chain ──────────────────────────────────────────────────

    [Fact]
    public async Task ReverseGeocode_UsesResidential_WhenRoadIsMissing()
    {
        var payload = new
        {
            place_id = 1,
            display_name = "test",
            address = new
            {
                road = (string?)null,
                residential = "Palm Avenue",
                city = "Chennai",
                state = "Tamil Nadu",
                postcode = "600001",
                country = "India"
            }
        };
        var response = OkJson(payload);
        var svc = CreateService(response, out _);

        var result = await svc.ReverseGeocodeAsync(13.0827, 80.2707);

        result.Success.Should().BeTrue();
        result.Address!.Street.Should().Be("Palm Avenue");
    }
}

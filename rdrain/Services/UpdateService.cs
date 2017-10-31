namespace RoofDrain.Services
{
    using Microsoft.ApplicationInsights;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json.Linq;
    using RoofDrain.Config;
    using RoofDrain.Models;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;

    /// <summary>
    /// The update service
    /// </summary>
    public class UpdateService : IUpdateService
    {
        private static readonly double ImpossiblyCrazyStormInchesPerHour = 3.0;
        private static readonly TimeSpan OverlyLongDrainDelay = TimeSpan.FromHours(3);
        private static readonly TimeSpan OverlyLongWeatherDelay = TimeSpan.FromHours(3);

        private readonly IConfiguration configuration;
        private readonly IStateService stateService;
        private readonly TelemetryClient telemetryClient;
        private readonly HttpClient httpClient;

        private readonly RoofPuddleConfig[] roofPuddleConfigs;
        private readonly WeatherUndergroundConfig weatherUndergroundConfig;

        /// <summary>
        /// Instantiated by pipeline
        /// </summary>
        public UpdateService(IConfiguration configurationParam, IStateService stateServiceParam, TelemetryClient telemetryClientParam)
        {
            this.configuration = configurationParam;
            this.stateService = stateServiceParam;
            this.telemetryClient = telemetryClientParam;
            this.httpClient = new HttpClient();

            this.roofPuddleConfigs = this.configuration.GetSection("Roof:Puddles").GetChildren().Select(x => x.Get<RoofPuddleConfig>()).ToArray();

            this.weatherUndergroundConfig = this.configuration.GetSection("WeatherUnderground").Get<WeatherUndergroundConfig>();
        }

        /// <inheritdoc />
        public async Task SetWater(double gallons)
        {
            (var applicationState, var etag) = await this.stateService.GetApplicationStateAsync();

            foreach(var roofPuddleState in applicationState.RoofPuddleStates)
            {
                roofPuddleState.EstimatedGallonsRemaining = gallons;
            }

            await this.stateService.SetApplicationStateAsync(applicationState, etag);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<RoofPuddleState>> UpdateFromRoofDrainAsync()
        {
            (var applicationState, var etag) = await this.stateService.GetApplicationStateAsync();

            var results = this.roofPuddleConfigs.Select(roofPuddleConfig =>
            {
                var roofPuddleState = GetOrAddRoofPuddleState(applicationState, roofPuddleConfig.Name);
                
                var now = DateTimeOffset.Now;
                var elapsed = DateTimeOffset.Now - roofPuddleState.LastDrainObservationTime;

                if (elapsed > OverlyLongDrainDelay)
                {
                    telemetryClient.TrackException(new InvalidOperationException($"Unexpected long delay for drain {roofPuddleConfig.Name} of {elapsed}"));
                }
                else
                {
                    var gallonsDrained = roofPuddleState.DrainedAtLastObservationTime ? elapsed.TotalMinutes * roofPuddleConfig.DrainRateGallonsPerMinute : 0;

                    gallonsDrained += 0.1; // Evaporation factor, ~5 gallons per day

                    roofPuddleState.EstimatedGallonsRemaining = Math.Max(0, roofPuddleState.EstimatedGallonsRemaining - gallonsDrained);

                    this.telemetryClient.TrackEvent(
                        "Drain",
                        new Dictionary<string, string> { ["puddle"] = roofPuddleConfig.Name },
                        new Dictionary<string, double> { ["gallons"] = gallonsDrained, ["remaining"] = roofPuddleState.EstimatedGallonsRemaining, });
                }

                roofPuddleState.LastDrainObservationTime = now;

                var sixteenthInchInGallons = (roofPuddleConfig.AreaSquareFeet * (0.0625 / 12.0)) * 7.48052;

                roofPuddleState.DrainedAtLastObservationTime = 
                    roofPuddleState.Temperature > 4.0 &&
                    ((roofPuddleState.DrainedAtLastObservationTime && roofPuddleState.EstimatedGallonsRemaining > 0) ||
                     (roofPuddleState.EstimatedGallonsRemaining > sixteenthInchInGallons));

                return roofPuddleState.DrainedAtLastObservationTime;
            }).ToArray();

            await this.stateService.SetApplicationStateAsync(applicationState, etag);
            return applicationState.RoofPuddleStates;
        }

        /// <inheritdoc />
        public async Task UpdateFromWeatherStationsAsync()
        {
            var values = new ConcurrentBag<(string station, double value, double temperatureCelsius, DateTimeOffset observationTime)>();

            await Task.WhenAll(this.weatherUndergroundConfig.Stations.Select(async x =>
            {
                try
                {
                    var response = await this.httpClient.GetAsync(
                        $"http://api.wunderground.com/api/{this.weatherUndergroundConfig.ApiKey}/conditions/q/{x}.json");
                    response.EnsureSuccessStatusCode();
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var parsed = JObject.Parse(responseContent);

                    var currentObservation = parsed.Value<JObject>("current_observation");

                    var extracted = Double.Parse(currentObservation.Value<string>("precip_1hr_in"));
                    var observationTime = DateTimeOffset.Parse(currentObservation.Value<string>("observation_time_rfc822"));
                    var temperatureCelsius = currentObservation.Value<double>("temp_c");

                    this.telemetryClient.TrackEvent(
                        "WeatherStationUpdate",
                        new Dictionary<string, string>
                        {
                            ["Station"] = x,
                            ["observation_time_rfc822"] = observationTime.ToString()
                        },
                        new Dictionary<string, double>
                        {
                            ["precip_1hr_in"] = extracted,
                            ["temp_c"] = temperatureCelsius
                        });

                    values.Add((x, extracted, temperatureCelsius, observationTime));
                }
                catch(Exception e)
                {
                    this.telemetryClient.TrackException(e);
                }
            }));

            (var applicationState, var etag) = await this.stateService.GetApplicationStateAsync();

            var now = DateTimeOffset.Now;

            var adjustedValues = values.Select(value =>
            {
                var stationState = GetOrAddWeatherStationState(applicationState, value.station);
                var lastObservationTime = stationState.LastObservationTime;
                stationState.LastObservationTime = value.observationTime;
                
                if(lastObservationTime >= value.observationTime)
                {
                    return 0;
                }
                
                var elapsed = value.observationTime - lastObservationTime;
                
                if(elapsed < TimeSpan.Zero || elapsed > OverlyLongWeatherDelay)
                {
                    return 0;
                }
                
                return elapsed.TotalHours * value.value;
            });

            var averageTemperature = values.Select(x => x.temperatureCelsius).Average();
            var rainfall = adjustedValues.Select(x => Math.Max(0, Math.Min(ImpossiblyCrazyStormInchesPerHour, x))).Average();

            this.telemetryClient.TrackEvent(
                "RainfallUpdate",
                null,
                new Dictionary<string, double>
                {
                    ["rainfall"] = rainfall,
                    ["temperature"] = averageTemperature
                });

            foreach (var roofPuddleConfig in this.roofPuddleConfigs)
            {
                var roofPuddleState = GetOrAddRoofPuddleState(applicationState, roofPuddleConfig.Name);

                const double cubicInchesPerGallon = 231;
                var toAdd = (roofPuddleConfig.AreaSquareFeet * (12 * 12) * rainfall) / cubicInchesPerGallon;

                this.telemetryClient.TrackEvent(
                    "Rain",
                    new Dictionary<string, string> { ["puddle"] = roofPuddleConfig.Name },
                    new Dictionary<string, double> { ["gallons"] = toAdd });

                roofPuddleState.EstimatedGallonsRemaining += toAdd;
                roofPuddleState.Temperature = averageTemperature;
            }

            await this.stateService.SetApplicationStateAsync(applicationState, etag);
        }

        /// <summary>
        /// Helper to get/add roof puddle state
        /// </summary>
        private RoofPuddleState GetOrAddRoofPuddleState(ApplicationState applicationState, string name)
        {
            var result = applicationState.RoofPuddleStates.FirstOrDefault(x => String.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            if (result == null)
            {
                result = this.stateService.InitializeRoofPuddleState(name);
                applicationState.RoofPuddleStates.Add(result);
            }
            return result;
        }

        /// <summary>
        /// Helper to get/add weather station state
        /// </summary>
        private WeatherStationState GetOrAddWeatherStationState(ApplicationState applicationState, string name)
        {
            var result = applicationState.WeatherStationStates.FirstOrDefault(x => String.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            if (result == null)
            {
                result = this.stateService.InitializeWeatherStationState(name);
                applicationState.WeatherStationStates.Add(result);
            }
            return result;
        }
    }
}

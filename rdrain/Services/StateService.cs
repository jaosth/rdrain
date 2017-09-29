namespace RoofDrain.Services
{
    using Microsoft.Extensions.Configuration;
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using RoofDrain.Models;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Blob;
    using Microsoft.AspNetCore.Hosting;
    using Newtonsoft.Json;
    using Microsoft.Extensions.Options;
    using Microsoft.AspNetCore.Mvc;
    using RoofDrain.Config;

    /// <summary>
    /// The state service
    /// </summary>
    public class StateService : IStateService
    {
        private readonly JsonSerializerSettings jsonSerializerSettings;
        private readonly IConfiguration configuration;
        private readonly string stateKey;
        private readonly CloudBlobClient cloudBlobClient;

        /// <summary>
        /// Instantiated by pipeline
        /// </summary>
        public StateService(IHostingEnvironment hostingEnvironmentParam, IConfiguration configurationParam, IOptions<MvcJsonOptions> mvcJsonOptionsParam)
        {
            this.jsonSerializerSettings = mvcJsonOptionsParam.Value.SerializerSettings;
            this.configuration = configurationParam;
            this.stateKey = hostingEnvironmentParam.EnvironmentName;

            var blobKey = configuration["Blob:Key"];
            var blobAccountName = configuration["Blob:AccountName"];
            var cloudStorageAccount = CloudStorageAccount.Parse($"DefaultEndpointsProtocol=https;AccountName={blobAccountName};AccountKey={blobKey}");
            this.cloudBlobClient = cloudStorageAccount.CreateCloudBlobClient();
        }

        /// <inheritdoc />
        public async Task<(ApplicationState applicationState, string etag)> GetApplicationStateAsync()
        {
            var containerReference = this.cloudBlobClient.GetContainerReference("state");
            var blockBlobReference = containerReference.GetBlockBlobReference(this.stateKey);
            
            if (!(await blockBlobReference.ExistsAsync()))
            {
                var roofPuddleConfigurations = this.configuration.GetSection("Roof:Puddles").GetChildren().Select(x => x.Get<RoofPuddleConfig>());
                var weatherUndergroundConfig = this.configuration.GetSection("WeatherUnderground").Get<WeatherUndergroundConfig>();

                return (new ApplicationState
                {
                    RoofPuddleStates = roofPuddleConfigurations.Select(x => InitializeRoofPuddleState(x.Name)).ToList(),
                    WeatherStationStates = weatherUndergroundConfig.Stations.Select(x => InitializeWeatherStationState(x)).ToList()
                }, null);
            }
            else
            {
                await blockBlobReference.FetchAttributesAsync();
                var downloaded = await blockBlobReference.DownloadTextAsync(
                        new AccessCondition { IfMatchETag = blockBlobReference.Properties.ETag }, null, null);
                var parsed = JsonConvert.DeserializeObject<ApplicationState>(downloaded, this.jsonSerializerSettings);
                return (parsed, blockBlobReference.Properties.ETag);
            }
        }

        /// <inheritdoc />
        public RoofPuddleState InitializeRoofPuddleState(string name)
            => new RoofPuddleState { Name = name, EstimatedGallonsRemaining = 0, LastDrainObservationTime = DateTimeOffset.Now - TimeSpan.FromHours(1), DrainedAtLastObservationTime = false };

        /// <inheritdoc />
        public WeatherStationState InitializeWeatherStationState(string name)
            => new WeatherStationState { Name = name, LastObservationTime = DateTimeOffset.Now - TimeSpan.FromHours(1) };

        /// <inheritdoc />
        public async Task SetApplicationStateAsync(ApplicationState applicationState, string etag)
        {
            var containerReference = this.cloudBlobClient.GetContainerReference("state");
            var blockBlobReference = containerReference.GetBlockBlobReference(this.stateKey);

            var serialized = JsonConvert.SerializeObject(applicationState, this.jsonSerializerSettings);
            await blockBlobReference.UploadTextAsync(serialized, new AccessCondition { IfMatchETag = etag }, null, null);
        }
    }
}

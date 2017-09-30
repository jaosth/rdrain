namespace RoofDrain.Services
{
    using RoofDrain.Models;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface to manipulate the roof state
    /// </summary>
    public interface IStateService
    {
        /// <summary>
        /// Retrieve the weather state
        /// </summary>
        Task<(ApplicationState applicationState, string etag)> GetApplicationStateAsync();

        /// <summary>
        /// Initialize a new roof puddle state
        /// </summary>
        RoofPuddleState InitializeRoofPuddleState(string name);

        /// <summary>
        /// Initialize a new weather station state
        /// </summary>
        WeatherStationState InitializeWeatherStationState(string name);

        /// <summary>
        /// Retrieve the weather state
        /// </summary>
        Task SetApplicationStateAsync(ApplicationState applicationState, string etag);

        /// <summary>
        /// Reset everything
        /// </summary>
        Task ResetAsync();
    }
}

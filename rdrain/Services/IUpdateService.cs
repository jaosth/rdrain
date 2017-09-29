namespace RoofDrain.Services
{
    using RoofDrain.Models;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// A service to update the state
    /// </summary>
    public interface IUpdateService
    {
        /// <summary>
        /// Update state from weather stations (called on timer)
        /// </summary>
        /// <returns></returns>
        Task UpdateFromWeatherStationsAsync();

        /// <summary>
        /// Update state signalling that the roof drain has run
        /// </summary>
        /// <returns>If true, the drain should activate</returns>
        Task<IEnumerable<RoofPuddleState>> UpdateFromRoofDrainAsync();

        /// <summary>
        /// Add water
        /// </summary>
        Task SetWater(double gallons);
    }
}

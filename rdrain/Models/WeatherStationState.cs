namespace RoofDrain.Models
{
    using System;

    /// <summary>
    /// The state from one of the individual weather stations
    /// </summary>
    public class WeatherStationState
    {
        /// <summary>
        /// The name of the weather station
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The observation time
        /// </summary>
        public DateTimeOffset LastObservationTime { get; set; }
    }
}

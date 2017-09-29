namespace RoofDrain.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Configuration;
    using RoofDrain.Models;
    using RoofDrain.Services;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Main api controller
    /// </summary>
    [Route("api")]
    public class ApiController : Controller
    {
        private readonly string apiKey;
        private readonly IStateService stateService;
        private readonly IUpdateService updateService;

        /// <summary>
        /// Instantiated by pipeline
        /// </summary>
        public ApiController(IConfiguration configurationParam, IStateService stateServiceParam, IUpdateService updateServiceParam)
        {
            this.apiKey = configurationParam["Authorization:ApiKey"];
            this.stateService = stateServiceParam;
            this.updateService = updateServiceParam;
        }

        /// <summary>
        /// Get the state
        /// </summary>
        [HttpGet("state")]
        [ProducesResponseType(typeof(ApplicationState), 200)]
        public async Task<IActionResult> GetState([FromQuery]string apiKey)
        {
            if(this.apiKey != null && apiKey != this.apiKey)
            {
                return Unauthorized();
            }

            return Ok((await this.stateService.GetApplicationStateAsync()).applicationState);
        }

        /// <summary>
        /// Update from the roof drain
        /// </summary>
        [HttpGet("updatefromroofdrain()")]
        [ProducesResponseType(typeof(IDictionary<string,double>), 200)]
        public async Task<IActionResult> UpdateFromRoofDrainAsync([FromQuery]string apiKey)
        {
            if (this.apiKey != null && apiKey != this.apiKey)
            {
                return Unauthorized();
            }

            return Ok((await this.updateService.UpdateFromRoofDrainAsync()).ToDictionary(x => x.Name, x => x.EstimatedGallonsRemaining));
        }

        /// <summary>
        /// Request update from the weather stations
        /// </summary>
        [HttpGet("updatefromweatherstations()")]
        public async Task<IActionResult> UpdateFromWeatherStationsAsync([FromQuery]string apiKey)
        {
            if (this.apiKey != null && apiKey != this.apiKey)
            {
                return Unauthorized();
            }

            await this.updateService.UpdateFromWeatherStationsAsync();
            return Ok();
        }

        /// <summary>
        /// Request update from the weather stations
        /// </summary>
        [HttpGet("setwater()")]
        public async Task<IActionResult> AddWater([FromQuery]string apiKey, [FromQuery]double? gallons)
        {
            if (this.apiKey != null && apiKey != this.apiKey)
            {
                return Unauthorized();
            }
            
            await this.updateService.SetWater(gallons ?? 200);
            return Ok();
        }
    }
}

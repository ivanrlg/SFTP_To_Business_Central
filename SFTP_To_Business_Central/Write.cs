using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shared.Models;
using Shared.Services;
using System.Text;
using ChoETL;

namespace SFTP_To_Business_Central
{
    public static class Write
    {
        [FunctionName("Write")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation($"Init Write");

            //Allows you to read the necessary configurations to connect to Business Central through Outh2
            ConfigurationsValues configValues = ReadEnviornmentVariable();
            if (configValues.Tenantid == null)
            {
                log.LogInformation("Validating the initial configurations");
                return new BadRequestObjectResult("Please set the ConfigurationsValues.");
            }

            //We read the information contained in the Body when the Azure Function is called.
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            if (string.IsNullOrEmpty(requestBody))
            {
                return new BadRequestObjectResult("RequestBody Is Null Or Empty");
            }

            //We deserialize
            ResponseFromPA mResponse = JsonConvert.DeserializeObject<ResponseFromPA>(requestBody);

            log.LogInformation("Converting to Json");

            //We convert the CSV file to Json
            StringBuilder sb = new StringBuilder();
            using (var p = ChoCSVReader.LoadText(mResponse.Data).WithFirstLineHeader())
            {
                using (var w = new ChoJSONWriter(sb))
                    w.Write(p);
            }

            //We store the information in an array of Items
            var Item = JsonConvert.DeserializeObject<Item[]>(sb.ToString());

            BCApiServices apiServices = new(configValues);

            //We insert in Business Central.
            var Result = InsertItems(configValues, Item, apiServices, log);
            if (Result.Result.IsSuccess) 
            {
                return new OkObjectResult(Result.Result.Message);
            }
            else
            {
                return new BadRequestObjectResult(Result.Result.Message);
            }
        }

        //It allows connecting to the shared project that contains the BCApiServices service,
        //which will perform the POST to the webservice published in Business Central,
        //sending in turn the processed information of the CSV file in JSON format.
        private static async Task<Response<object>> InsertItems(
        ConfigurationsValues configValues,
        Item[] mItem,
        BCApiServices apiServices,
        ILogger log)
        {
            try
            {
                Response<object> Response = await apiServices.InsertInBusinessCentral(configValues.InsertTelemetry, mItem);
                var ResultBC = JsonConvert.DeserializeObject<Ouput>(Response.Message);
                var ResponseBC = JsonConvert.DeserializeObject<Response<string>>(ResultBC.value);

                return new Response<object>
                {
                    IsSuccess = Response.IsSuccess,
                    Message = ResponseBC.Message
                };
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex.Message);
                log.LogInformation("Exception: " + ex.Message);

                return new Response<object>
                {
                    IsSuccess = false,
                    Message = "Exception: " + ex.Message
                };
            }
        }

        //Allows you to read the necessary configurations to connect to Business Central through Outh2
        public static ConfigurationsValues ReadEnviornmentVariable()
        {
            ConfigurationsValues configValues = new()
            {
                Tenantid = Environment.GetEnvironmentVariable("Tenantid", EnvironmentVariableTarget.Process),
                ClientId = Environment.GetEnvironmentVariable("Clientid", EnvironmentVariableTarget.Process),
                ClientSecret = Environment.GetEnvironmentVariable("ClientSecret", EnvironmentVariableTarget.Process),
                CompanyID = Environment.GetEnvironmentVariable("CompanyID", EnvironmentVariableTarget.Process),
                EnvironmentName = Environment.GetEnvironmentVariable("EnvironmentName", EnvironmentVariableTarget.Process)
            };

            return configValues;
        }
    }
}

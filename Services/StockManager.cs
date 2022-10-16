﻿using Newtonsoft.Json;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Obliviate.Models;
using System.Reflection;

namespace Obliviate.Services
{
    public interface StockManager
    {
        Stock GetFinancials();
    }


    public class StockData : StockManager
    {
        private readonly IConfiguration _configuration;

        public StockData(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// JSON Response from a specified API 
        /// </summary>
        /// <param name="symbol">Symbol of Stock (eg. AAPL)</param>
        /// <param name="api">Which API to use</param>
        /// <returns>JSON Array</returns>
        public dynamic? GetData(string symbol, string api="FMP")
        {
            //API Initialization
            string API_KEY = "";
            string baseUrl = "";
            if (api == "FMP")
            {
                API_KEY = _configuration.GetValue<string>("FMP_API_KEY");
                baseUrl = _configuration.GetValue<string>("FMP_API_URL");
            }

            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(baseUrl);

                HttpResponseMessage response = client.GetAsync(
                    $"income-statement/{symbol}?apikey=" + API_KEY).Result;

                if (response.IsSuccessStatusCode)
                {
                    var result = response.Content.ReadAsStringAsync().Result;
                    dynamic? jsonObj = JsonConvert.DeserializeObject(result);
                    return jsonObj;
                }
                return null;
            }
        }

        public Stock GetFinancials()
        {
            Stock stock = new Stock();
            dynamic? jsonObj = GetData("AAPL");

            int i = 0;
            if(jsonObj != null)
            {
                foreach (var obj in jsonObj)
                {
                    foreach (PropertyInfo prop in stock.GetType().GetProperties())
                    {
                        if (prop.CanWrite)
                        {
                            if (i != 0 && prop.Name.ToLower() != "symbol")
                            {
                                var prev = prop.GetValue(stock);
                                prop.SetValue(stock, $"{obj[prop.Name.ToLower()]},{prev}", null);
                            }
                            else
                            {
                                prop.SetValue(stock, $"{obj[prop.Name.ToLower()]}", null);
                            }
                        }
                    }
                    ++i;
                }
            } 
            else
            {
                return stock;
            }

            return stock;
        }
    }
}

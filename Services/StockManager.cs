﻿using Newtonsoft.Json;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Obliviate.Models;
using System.Reflection;
using System.Linq;
using Newtonsoft.Json.Linq;
using Obliviate.Data;
using System.Diagnostics;
using System.Collections;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Obliviate.Services
{
    public class StockManager
    {
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;
        private readonly string _apiKey;
        private readonly string _baseUrl;
        private readonly HttpClient _client;
        public StockManager(IConfiguration configuration, ApplicationDbContext context)
        {
            _configuration = configuration;
            _context = context;
            _apiKey = _configuration.GetValue<string>("FMP_API_KEY");
            _baseUrl = _configuration.GetValue<string>("FMP_API_URL");

            //Configuring Client
            _client = new HttpClient();
            _client.BaseAddress = new Uri(_baseUrl);
            _client.DefaultRequestHeaders.TryAddWithoutValidation
                ("Content-Type", "application/json; charset=utf-8");
        }


        private string MakeCall(string url) 
        {
            HttpResponseMessage apiCall = _client.GetAsync(url).Result;
            if(apiCall.IsSuccessStatusCode) 
            {
                string result = apiCall.Content.ReadAsStringAsync().Result;
                return result;
            }
            return null;
        }

        /// <summary>
        /// Format JSON API Request
        /// </summary>
        /// <param name="symbol">Symbol of Stock (eg. AAPL)</param>
        /// <param name="api">Which API to use</param>
        /// <param name="data">Which Data to get</param>
        /// <returns>Formatted JSON List</returns>
        private List<JObject> GetJSON(string symbol, string api="FMP", string data="standard")
        {
            //Where API Calls with {Name: URL} are stored
            string[] calls = new string[]
            {
                //API PREMIUM
                $"v4/esg-environmental-social-governance-data?symbol={symbol}&apikey={_apiKey}",
                $"v4/price-target-consensus?symbol={symbol}&apikey={_apiKey}",
                $"v4/stock_peers?symbol={symbol}&apikey={_apiKey}",
                
                $"v3/analyst-estimates/{symbol}?&apikey={_apiKey}",
                $"v3/historical-discounted-cash-flow-statement/{symbol}?apikey={_apiKey}",
                $"v3/income-statement/{symbol}?apikey={_apiKey}",
                $"v3/balance-sheet-statement/{symbol}?apikey={_apiKey}",
                $"v3/cash-flow-statement/{symbol}?apikey={_apiKey}",
                $"v3/ratios/{symbol}?limit=120&apikey={_apiKey}",
                $"v3/key-metrics/{symbol}?limit=120&apikey={_apiKey}",
                $"v3/profile/{symbol}?&apikey={_apiKey}",
                $"v3/rating/{symbol}?&apikey={_apiKey}"
            };

            //Where Stock Data will be stored
            Dictionary<int, JObject> stockData = new();
            JArray jsonArr = new();

            //Make API Call for each Web Address in List
            int count = 0, otherCount = 0;
            foreach (string call in calls)
            {
                try {
                    jsonArr = JArray.Parse(MakeCall(call));
                } catch (System.ArgumentNullException) {
                    continue;
                }

                //Individual Changes
                JArray tempJsonArr = new();
                if(call.Contains("environmental"))
                {
                    otherCount = 0;
                    foreach(JObject j in jsonArr) 
                    {
                        if(otherCount % 4 == 0) 
                            tempJsonArr.Add(j);
                        ++otherCount;
                    }
                    jsonArr = tempJsonArr;
                }

                //Format JSON
                count = 0;
                foreach (JObject obj in jsonArr)
                {
                    //Merge JSON Objects
                    if (!stockData.ContainsKey(count))
                    {
                        JObject startObject = new();
                        startObject.Merge(obj);
                        stockData.Add(count, startObject);
                    }
                    else
                        stockData[count].Merge(obj);
                    ++count;
                }
            }

            //Make List from Dictionary to get rid of indeces as keys
            List<JObject> stockArray = new();
            foreach (KeyValuePair<int, JObject> obj in stockData)
                stockArray.Add(obj.Value);

            return stockArray;
        }




        public List<string> GetSymbols()
        {
            JArray jsonArr = JArray.Parse(MakeCall($"{_baseUrl}v3/stock/list?apikey={_apiKey}"));
            List<string> symbols = new();
            foreach(JObject obj in jsonArr)
                symbols.Add(obj["symbol"].ToString());
            return symbols;
        }




        private Stock GetFinancials(string symbol)
        {
            Stock stock = new();
            List<JObject> stockData = GetJSON(symbol);
            if (stockData.Count == 0)
                return stock;

            string[] singles =
            {
                "symbol", "reportedCurrency", "period", "cik",
                "stockPrice", "companyName", "currency", "isin", "cusip", "exchange", "exchangeShortName", "industry", 
                "website", "description", "ceo", "beta", "changes", "dcfDiff", "price", "mktCap",
                "sector", "country", "fullTimeEmployees", "phone", "address", "city", "range", "capexPerShare",
                "state", "zip", "dcfdiff", "image", "ipoDate", "defaultImage", "lastDiv", "volAvg",
                "isEtf", "isActivelyTrading", "isAdr", "isFund", "rating", "ratingScore",
                "ratingRecommendation", "ratingDetailsDCFScore",
                "ratingDetailsDCFRecommendation", "ratingDetailsROEScore",
                "ratingDetailsROERecommendation", "ratingDetailsROAScore",
                "ratingDetailsROARecommendation", "ratingDetailsDEScore",
                "ratingDetailsDERecommendation", "ratingDetailsPEScore",
                "ratingDetailsPERecommendation", "ratingDetailsPBScore",
                "ratingDetailsPBRecommendation", "peersList", "targetHigh",
                "targetLow", "targetConsensus", "targetMedian",
            };
            string[] noDots = 
            {
                "symbol", "companyName", "description", "ceo", "address",
                "state", "county", "city", "peersList"
            };

            int i = 0, j = 0;
            object? prev = new();
            string name = "";
            string convert = "";
            foreach (JObject obj in stockData)
            {
                foreach (PropertyInfo prop in stock.GetType().GetProperties())
                {
                    name = Char.ToLower(prop.Name[0]) + prop.Name.Substring(1);
                    prev = prop.GetValue(stock);
                    if(!noDots.Contains(name))
                        convert = $"{obj[name]}".Replace(",", ".");
                    else
                        convert = $"{obj[name]}";

                    if (i == 0)
                        prop.SetValue(stock, convert, null);
                    else if (i != 0 && singles.Contains(name))
                        prop.SetValue(stock, prev, null);
                    else
                        prop.SetValue(stock, convert + $", {prev}", null);
                    ++j;
                }
                ++i;
            }

            return stock;
        }



        private Dictionary<string, string> CallHistory(string symbol) 
        {
            Dictionary<string, string> history = new()
            {
                {"date", ""},
                {"open", ""},
                {"high", ""},
                {"low", ""},
                {"adjClose", ""},
                {"volume", ""},
                {"change", ""},
                {"changePercent", ""},
                {"vwap", ""},
                {"label", ""},
                {"changeOverTime", ""},
                {"sma20", ""},
                {"sma50", ""},
                {"sma100", ""},
                {"sma200", ""},
                {"williams14", ""},
                {"rsi14", ""}
            };

            JArray? jsonObj = JArray.Parse(JToken.Parse(MakeCall
                ($"{_baseUrl}v3/historical-price-full/{symbol}?apikey={_apiKey}"))["historical"].ToString());
            jsonObj = JArray.FromObject(jsonObj.Reverse());

            JArray jArr = new();
            List<string> names = new() {"sma", "sma", "sma", "sma", "williams", "rsi"};
            List<int> periods = new() {20, 50, 100, 200, 14, 14};

            string index = "";
            for(int i = 0; i < names.Count; ++i) {
                jArr = JArray.Parse(MakeCall(
                    $"{_baseUrl}v3/technical_indicator/daily/{symbol}?period={periods[i]}&type={names[i]}&apikey={_apiKey}"));
                try {
                    jArr = JArray.FromObject(jArr.Reverse());
                } catch (System.ArgumentNullException) {
                    continue;
                }

                index = $"{names[i]}{periods[i]}";
                foreach(JObject j in jArr)
                {
                    history[index] += $"{j[names[i]]}".Replace(",", ".") + ",";
                }
                    
            }   

            foreach(JObject obj in jsonObj) 
            {
                history["date"] += $"\"{obj["date"]}\"" + ",";
                history["open"] += $"{obj["open"]}".Replace(",", ".") + ",";
                history["high"] += $"{obj["high"]}".Replace(",", ".") + ",";
                history["low"] += $"{obj["low"]}".Replace(",", ".") + ",";
                history["adjClose"] += $"{obj["adjClose"]}".Replace(",", ".") + ",";
                history["volume"] += $"{obj["volume"]}".Replace(",", ".") + ",";
                history["change"] += $"{obj["change"]}".Replace(",", ".") + ",";
                history["changePercent"] += $"{obj["changePercent"]}".Replace(",", ".") + ",";
                history["vwap"] += $"{obj["vwap"]}".Replace(",", ".") + ",";
                history["label"] += obj["label"] + ",";
                history["changeOverTime"] += $"{obj["changeOverTime"]}".Replace(",", ".") + ",";
            }

            return history;
        }

        private Stock GetHistory(Stock stock) {
            Dictionary<string, string> history = CallHistory(stock.Symbol);
            stock.HistoryDate = history["date"].Substring(0, history["date"].Length - 1);

            stock.Open = history["open"].Substring(0, history["open"].Length - 1);
            stock.High = history["high"].Substring(0, history["high"].Length - 1);
            stock.Low = history["low"].Substring(0, history["low"].Length - 1);
            stock.Close = history["adjClose"].Substring(0, history["adjClose"].Length - 1);
            stock.Volume = history["volume"].Substring(0, history["volume"].Length - 1);
            stock.Change = history["change"].Substring(0, history["change"].Length - 1);
            stock.ChangePercent = history["changePercent"].Substring(0, history["changePercent"].Length - 1);
            stock.Vwap = history["vwap"].Substring(0, history["vwap"].Length - 1);
            stock.Label = history["label"].Substring(0, history["label"].Length - 1);
            stock.ChangeOverTime = history["changeOverTime"].Substring(0, history["changeOverTime"].Length - 1);
            stock.SMA20 = history["sma20"].Substring(0, history["sma20"].Length - 1);
            stock.SMA50 = history["sma50"].Substring(0, history["sma50"].Length - 1);
            stock.SMA100 = history["sma100"].Substring(0, history["sma100"].Length - 1);
            stock.SMA200 = history["sma200"].Substring(0, history["sma200"].Length - 1);
            stock.WPR = history["williams14"].Substring(0, history["williams14"].Length - 1);
            stock.RSI = history["rsi14"].Substring(0, history["rsi14"].Length - 1);

            return stock;
        }



        public int GetData(string action, string symbol="", bool skip=true, List<string> already=null) 
        {
            Stock stock = new Stock();

            if(action == "history")
            {
                if(already.Contains(symbol)) 
                {
                    stock = _context.Stock.Find(symbol);
                    stock = GetHistory(stock);
                    _context.Update(stock);
                } 
                else 
                {
                    Debug.WriteLine($"'{symbol}' History Push skipped.");
                    return 1;
                }
            } 
            else if(action == "all") 
            {
                if(already.Contains(symbol) && skip == true) 
                {
                    Debug.WriteLine($"'{symbol}' Push skipped.");
                    return 1;
                }

                stock = GetFinancials(symbol);
                stock = GetHistory(stock);
                if(stock.IsEtf == "False") {
                    _context.Remove(_context.Stock.Find(symbol));
                    _context.SaveChanges();
                    _context.Add(stock);
                }
            }
            return 0;
        }
    }
}

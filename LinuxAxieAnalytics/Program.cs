using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AxieDataFetcher.BlockchainFetcher;
using AxieDataFetcher.EggsSpawnedData;
using AxieDataFetcher.Core;
using AxieDataFetcher.BattleData;
using AxieDataFetcher.MultiThreading;
using System.Diagnostics;
using Newtonsoft.Json;
using AxieDataFetcher.Mongo;

namespace AxieDataFetcher
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            //Console.WriteLine(give(23));
            //Console.ReadLine();
            //MarketplaceDatabase.ComputeAllSales().GetAwaiter().GetResult();
            //AxieDataGetter.FetchOpenSeaData().GetAwaiter().GetResult();
            //AxieDataGetter.FetchAllSalesData().GetAwaiter().GetResult();
            //EggsSpawnDataFetcher.GetAllEggsSpawnedDataCumul().GetAwaiter().GetResult();
            //WinrateCollector.GetCumulBattleCount().GetAwaiter().GetResult();
            //inrateCollector.GetBattleDataSinceLastCheck().GetAwaiter().GetResult();
            LoopHandler.UpdateServiceCheckLoop().GetAwaiter().GetResult();
            //444542
            //AxieDataGetter.FetchCumulUniqueBuyers().GetAwaiter().GetResult();
            //AxieDataGetter.TestBid().GetAwaiter().GetResult();
        }
    }
}
